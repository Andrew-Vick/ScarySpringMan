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


// Starting to think the animation issue arises from a desync in game lock flags between host and client since only the host can control the movement code.
// One weired behaviour is Client Rpc log will not appear on the client side but if I place in a native game method that contains logs those will apear.
// May be issue where the client jumps back to games code while host is still running modded code so client can't access animation calls in client Rpc


namespace ScarySpringMan.Patches
{
    [HarmonyPatch(typeof(SpringManAI))]
    internal class SpringManAIPatch : EnemyAI
    {

        private static System.Random rnd = new System.Random();
        private static Stopwatch stopwatch = new Stopwatch();



        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void patchUpdate(SpringManAI __instance)
        {
            if (__instance.IsOwner)
            {
                if (ShouldStartMoving(__instance))
                {
                    __instance.StartCoroutine(MoveTowardsPlayer(__instance));
                    
                }
            }
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
            if (!__instance.agent.enabled || !__instance.agent.isOnNavMesh)
            {
                if (!__instance.agent.Warp(__instance.transform.position))
                {
                    // Reset flags in case of error
                    ScarySpringManBase.mls.LogInfo("Cannot place on NavMesh");
                    yield break;
                }
            }

            bool playerLooking = false;
            float currentSpeed = 0f;
            float targetSpeed = 14.5f;
            float acceleration = 2.5f;
            __instance.agent.angularSpeed = 270f;
            __instance.agent.stoppingDistance = 0.5f;
            __instance.agent.updatePosition = true;
            __instance.agent.updateRotation = true;
            bool SpringMoving = false;

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
                if (!playerLooking||!__instance.IsOwner)
                {
                    yield break;
                }
                if (Vector3.Distance(__instance.transform.position, targetPosition) <= __instance.agent.stoppingDistance + 0.5f)    // Check to see if enemy has overshot stopping point
                {
                    break;
                }


                distanceToTarget = Vector3.Distance(__instance.transform.position, targetPosition);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);
                __instance.agent.speed = currentSpeed;
                //UpdateGoServerRpc(__instance);
                __instance.SetAnimationGoServerRpc();
                
                yield return null;
            }

            __instance.agent.speed = 0;
            __instance.agent.isStopped = true;

            //UpdateStopServerRpc(__instance);
            __instance.SetAnimationStopServerRpc();
            __instance.agent.ResetPath();
            ScarySpringManBase.mls.LogInfo("Movement coroutine completed.");
        }

    }

}