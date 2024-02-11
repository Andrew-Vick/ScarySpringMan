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


namespace ScarySpringMan.Patches
{
    [HarmonyPatch(typeof(SpringManAI))]
    internal class SpringManAIPatch : EnemyAI
    {

        private static System.Random rnd = new System.Random();
        private static Stopwatch stopwatch = new Stopwatch();
        private static bool movedRecently = false;

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        public static void DoAIIntervalPatch(SpringManAI __instance)
        {
            // Check if a NetworkTransform exist on the Coil Head if not add one
            // This is used by Unity to help sync up positioning
            if (!__instance.gameObject.GetComponent<NetworkTransform>())
                __instance.gameObject.AddComponent<NetworkTransform>();
        }

        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void patchUpdate(SpringManAI __instance)
        {

            if (ShouldStartMoving(__instance))
            {
                __instance.StartCoroutine(MoveTowardsPlayer(__instance));
            }
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
                // Update gets called roughly 60 times a second. To ensure theres a 100% chance this mod runs in a minute we need 3600 values to choose from.
                // 1/3600 * 60fps = 0.0167 per sec; 0.0167 * 60s = 1(100%)
                // alternatively 60fps * 60sec in min = 3600
                int num = rnd.Next(1, 1800); 
                if (num == 123)
                {
                    return true;
                }
            }
            return false;
        }
        // read it and weep with me
        // Zeeker's forgot a move method in his code so had to make my own yay!

        /** Use Coroutine for smooth movement of enemies. Since it takes longer than one frame for the SpringMan to move I needed something that acted as a loop 
         * but allowed for Unity to take control again to update and render the rest of the game. **/
        private static IEnumerator MoveTowardsPlayer(SpringManAI __instance)
        {

            float currentSpeed = 0f;
            float targetSpeed = 10f;
            float acceleration = 2.5f;

            Vector3 startPosition = __instance.transform.position;
            Vector3 targetPosition = GameNetworkManager.Instance.localPlayerController.transform.position;

            float totalDistance = Vector3.Distance(targetPosition, startPosition);
            float distanceMoved = 0f;

            bool flag2 = false;

            // __instance.base to grab enemy ai stuff
            // check to make sure players are 
            for (int i = 0; i < StartOfRound.Instance.allPlayerScripts.Length; i++)
            {
                if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) &&
                    StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) &&
                    Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) < 0.3f)
                {
                    flag2 = true;

                }
            }
            if (flag2)
            {
                yield break;
            }
            while (!__instance.agent.pathPending && distanceMoved < totalDistance * 0.25f)
            {
                __instance.destination = RoundManager.Instance.GetNavMeshPosition(targetPosition, RoundManager.Instance.navHit, 2.7f);
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

                float step = currentSpeed * Time.deltaTime;
                Vector3 newPosition = Vector3.MoveTowards(__instance.transform.position, targetPosition, step);
                __instance.transform.position = newPosition;

                __instance.agent.speed = currentSpeed;

                distanceMoved += step; // Update the distance moved
                Vector3 targetDirection = targetPosition - __instance.transform.position;
                if (targetDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                    __instance.transform.rotation = Quaternion.Slerp(__instance.transform.rotation, targetRotation, Time.deltaTime * __instance.agent.angularSpeed);
                }
                UpdateGoAnimationServerRpc(__instance, currentSpeed);
                yield return null;
            }
            UpdateStopAnimationServerRpc(__instance, currentSpeed);
            __instance.agent.speed = 0;
            // If you are using this code as inspiration or you're me looking back do not set __instance.agent.IsStopped = true here as even if you hand control back to the game it will use that variable and break the games code

        }
        [ServerRpc(RequireOwnership = false)]
        public static void UpdateGoAnimationServerRpc(SpringManAI __instance, float speed)
        {
            __instance.creatureAnimator.SetFloat("walkSpeed", speed);
        }

        [ServerRpc(RequireOwnership = false)]
        public static void UpdateStopAnimationServerRpc(SpringManAI __instance, float speed)
        {
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