using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using GameNetcodeStuff;
using UnityEngine.AI;
using Unity.Netcode.Components;
using System.Collections;
using System.Net.Security;
using UnityEngine.Scripting.APIUpdating;
using System.Diagnostics.Eventing.Reader;
using BepInEx;


namespace ScarySpringMan.Patches
{
    [HarmonyPatch(typeof(SpringManAI))]
    internal class SpringManAIPatch : EnemyAI
    {

        private static System.Random rnd = new System.Random();
        private static Stopwatch stopwatch = new Stopwatch();
        private static bool RunningCoroutine = false;
        private static bool lockGameCode = false;

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void patchUpdate(SpringManAI __instance)
        {
            if (__instance.IsOwner)
            {

                if (ShouldStartMoving(__instance) && RunningCoroutine == false)
                {
                    // Start the coroutine and set flag to lock games code from runnign and messing with NavMeshAgent
                    RunningCoroutine = true;
                    lockGameCode = true;
                    __instance.StartCoroutine(MoveTowardsPlayer(__instance));

                    ScarySpringManBase.mls.LogInfo($"1. coroutine is set to run: {RunningCoroutine}");
                }
                else if (ShouldStartMoving(__instance) && RunningCoroutine == true)
                {
                    lockGameCode = true;
                    RunningCoroutine = true;
                    // a coroutine is running but still don't want the games update running as that overrides the mods AI logic
                    ScarySpringManBase.mls.LogInfo($"2. (set to still run) coroutine is set to run: {RunningCoroutine}");
                }
                else
                {
                    // coroutine has finsihed so release lock and reset flag
                    lockGameCode = false;
                    RunningCoroutine = false;
                }
            }
        }

        // Two methods below are used to block the games code from running when the mod's coroutine is going
        [HarmonyPatch("DoAIInterval")]
        [HarmonyPrefix]
        public static bool PrefixDoAIInterval()
        {
            if (lockGameCode)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static bool prefixUpdate()
        {
            if (lockGameCode)
            {
                return false;
            }
            return true;
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static void placeNetworkTransfom(SpringManAI __instance)
        {
            if (!__instance.gameObject.GetComponent<NetworkTransform>())
                __instance.gameObject.AddComponent<NetworkTransform>();
        }


        static bool ShouldStartMoving(SpringManAI __instance)
        {
            bool flag = false;

            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++) // Check eveyplayer in the server to see if they're looking at THIS Coil Head
            {
                // Determine if a player is looking at the Coil Head
                if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) &&
                    StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) &&
                    Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) > 0.3f)
                {
                    flag = true;
                }
            }

            if (flag)
            {
                // 100% chance to run every 30 seconds
                // 60fps * 30sec in min = 1800
                int num = rnd.Next(1, 1800);
                if (num == 123)
                {
                    return true;
                }
            }
            return false;
        }

        // read it and weep with me
        private static IEnumerator MoveTowardsPlayer(SpringManAI __instance)
        {

            /**
             * This trap is the route cause of animations not updating, client falls into the trap and breaks out of coroutine, server does not
             * 
             * server = targeted player
             * client = bystander
             */

            if (!__instance.agent.enabled || !__instance.agent.isOnNavMesh)
            {
                if (!__instance.agent.Warp(__instance.transform.position))
                {
                    // Reset flags in case of error
                    lockGameCode = false;
                    RunningCoroutine = false;
                    ScarySpringManBase.mls.LogInfo("Cannot place on NavMesh");
                    yield break;
                }
            }

            //if (!__instance.agent.enabled || !__instance.agent.isOnNavMesh)
            //{
            //    Vector3 nearestPoint;
            //    if (NavMesh.SamplePosition(__instance.transform.position, out NavMeshHit hit2, 10.0f, NavMesh.AllAreas))
            //    {
            //        nearestPoint = hit2.position;
            //        __instance.agent.Warp(nearestPoint);
            //    }
            //    else
            //    {
            //        // Reset flags in case of error
            //        lockGameCode = false;
            //        RunningCoroutine = false;
            //        ScarySpringManBase.mls.LogInfo("Cannot place on NavMesh");
            //        yield break;
            //    }
            //}

            bool playerLooking = false;
            float currentSpeed = 0f;
            float targetSpeed = 14.5f;
            float acceleration = 2.5f;
            __instance.agent.angularSpeed = 270f;
            __instance.agent.stoppingDistance = 0.5f; 
            __instance.agent.updatePosition = true;
            __instance.agent.updateRotation = true;
            
            Vector3 PlayerPosition = GameNetworkManager.Instance.localPlayerController.transform.position;
            Vector3 directionToPlayer = PlayerPosition - __instance.transform.position;
            Vector3 targetPosition = __instance.transform.position + (directionToPlayer * 0.5f);
            float distanceToTarget = Vector3.Distance(__instance.transform.position, targetPosition);
            float maxDistance = 10f;

            NavMeshHit hit;

            if (NavMesh.SamplePosition(targetPosition, out hit, maxDistance, NavMesh.AllAreas))
            {
                targetPosition = hit.position;
            }
            else
            {
                ScarySpringManBase.mls.LogInfo("Failed to find a point on the NavMesh near the target position.");
            }
            __instance.agent.SetDestination(targetPosition);
            yield return new WaitWhile(() => __instance.agent.pathPending);
            __instance.agent.isStopped = false;

            // Main movement loop
            while (distanceToTarget > __instance.agent.stoppingDistance)
            {
                for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++) // Check eveyplayer in the server to see if they're looking at THIS Coil Head
                {
                    // Determine if a player is looking at the Coil Head
                    if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) &&
                        StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) &&
                        Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) > 0.3f)
                    {
                        playerLooking = true;
                        break;
                    }
                }
                if (!playerLooking)
                {
                    lockGameCode = false;
                    RunningCoroutine = false;
                    yield break;
                }
                if (Vector3.Distance(__instance.transform.position, targetPosition) <= __instance.agent.stoppingDistance + 0.5f)    // Check to see if enemy has overshot stopping point
                {
                    break;
                }

                distanceToTarget = Vector3.Distance(__instance.transform.position, targetPosition);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
                __instance.agent.speed = currentSpeed;
                
                UpdateGoServerRpc(__instance, currentSpeed);

                ScarySpringManBase.mls.LogInfo($"Remaining Distance: {__instance.agent.remainingDistance}, Speed: {__instance.agent.speed}");
                ScarySpringManBase.mls.LogInfo($"Agent Position: {__instance.transform.position}, Destination: {targetPosition}");
                ScarySpringManBase.mls.LogInfo($"Path Status: {__instance.agent.pathStatus}, Path Complete: {!__instance.agent.pathPending}");

                yield return null;
            }

            __instance.agent.speed = 0;
            __instance.agent.isStopped = true;

            UpdateStopServerRpc(__instance, 0);
            __instance.agent.ResetPath();
            ScarySpringManBase.mls.LogInfo("Movement coroutine completed.");
            lockGameCode = false;
            RunningCoroutine = false;
        }

        [ServerRpc(RequireOwnership = false)]
        public static void UpdateGoServerRpc(SpringManAI __instance, float speed)
        {
            __instance.creatureAnimator.SetFloat("walkSpeed", speed);
            //UpdateGoAnimationClientRpc(__instance, speed);
        }

        [ServerRpc(RequireOwnership = false)]
        public static void UpdateStopServerRpc(SpringManAI __instance, float speed)
        {
            __instance.creatureAnimator.SetFloat("walkSpeed", speed);
            //UpdateStopAnimationClientRpc(__instance, speed);
        }

        [ClientRpc]
        public static void UpdateGoAnimationClientRpc(SpringManAI __instance, float speed)
        {

            ScarySpringManBase.mls.LogInfo("client got this");
            __instance.creatureAnimator.SetFloat("walkSpeed", speed);
        }

        [ClientRpc]
        public static void UpdateStopAnimationClientRpc(SpringManAI __instance, float speed)
        {
            ScarySpringManBase.mls.LogInfo("client got this");
            __instance.creatureAnimator.SetFloat("walkSpeed", speed);
            RoundManager.PlayRandomClip(__instance.creatureVoice, __instance.springNoises, randomize: false);
            int animationNum = rnd.Next(1, 3);
            if (animationNum == 2)
            {
                __instance.creatureAnimator.SetTrigger("springBoing");
            }
            else
            {
                __instance.creatureAnimator.SetTrigger("springBoingPosition2");
            }
        }

    }

}