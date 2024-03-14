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

        public static bool multiplePeps = false;

        public static bool kill = false;

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

        [HarmonyPatch("OnCollideWithPlayer")]
        [HarmonyPostfix]
        static void collideWithPlayer(SpringManAI __instance, Collider other)
        {
            ScarySpringManBase.mls.LogInfo("onCollide is getting called");
            if (kill)
            {
                ScarySpringManBase.mls.LogInfo("set to kill player");
                PlayerControllerB playerControllerB = __instance.MeetsStandardPlayerCollisionConditions(other);
                if (playerControllerB != null)
                {
                    playerControllerB.DamagePlayer(100, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling, 2);
                    playerControllerB.JumpToFearLevel(1f);
                    kill = false;
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

            int counter = 0;

            multiplePeps = false;

            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++) // Check evey player in the server to see if they're looking at THIS Coil Head
            {
                // Determine if a player is looking at the Coil Head
                if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) &&
                    StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) &&
                    Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) > 0.3f)
                {
                    flag = true;
                    counter++;
                }
            }

            if (flag)
            {
                // 100% chance to run every 30 seconds
                // 60fps * 30sec in min = 1800
                int num = rnd.Next(1, 1800);
                int attack = 0;

                if (num == 420)
                {
                    if(counter == 2)
                    {
                        attack = rnd.Next(1, 200);
                        if (attack == 69)
                        {
                            multiplePeps = true;
                            ScarySpringManBase.mls.LogInfo($"{multiplePeps}");
                        }
                    }else if(counter == 3)
                    {
                        attack = rnd.Next(1, 75);
                        if(attack == 69)
                        {
                            multiplePeps = true;
                        }
                    }else if (counter >= 4){
                        attack = rnd.Next(1, 25);
                        if(attack == 10)
                        {
                            multiplePeps = true;
                        }
                    }
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
                    ScarySpringManBase.mls.LogInfo("Cannot place on NavMesh");
                    yield break;
                }
            }

            float distanceScalar = 0.5f;
            bool playerLooking = false;
            float currentSpeed = 0f;
            float targetSpeed = 14.5f;
            float acceleration = 2.5f;

            __instance.agent.angularSpeed = 270f;
            __instance.agent.stoppingDistance = 0.5f;
            __instance.agent.updatePosition = true;
            __instance.agent.updateRotation = true;

            if (multiplePeps)
            {
                distanceScalar = 1.0f;
            }

            Vector3 PlayerPosition = GameNetworkManager.Instance.localPlayerController.transform.position;
            Vector3 directionToPlayer = PlayerPosition - __instance.transform.position;

            Vector3 targetPosition = __instance.transform.position + (directionToPlayer * distanceScalar);
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

                if (multiplePeps)
                {
                    kill = true;
                }

                __instance.SetAnimationGoServerRpc();
                
                yield return null;
            }

            __instance.agent.speed = 0;
            

            __instance.SetAnimationStopServerRpc();
            __instance.agent.ResetPath();
        }
    }
}