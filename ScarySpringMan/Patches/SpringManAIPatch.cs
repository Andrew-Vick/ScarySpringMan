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
using System.Collections;


namespace ScarySpringMan.Patches
{
    [HarmonyPatch(typeof(SpringManAI))]
    internal class SpringManAIPatch : EnemyAI
    {

        private static System.Random rnd = new System.Random();
        private static Stopwatch stopwatch = new Stopwatch();
        private static bool isTimerSet = false;
        private static bool flag = false;
        private static int amountOfPlayers = 0;

        private static bool isHost = NetworkManager.Singleton.IsHost;


        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void patchUpdate(SpringManAI __instance)
        {
            if (!isTimerSet)
            {
                stopwatch.Start();
                isTimerSet = true;
                ScarySpringManBase.mls.LogInfo("Timer start");
            }

            flag = false;
            amountOfPlayers = 0;

            for (int i = 0; i < 4; i++) // i relates to clientId everyone gets one when they join starting at 0 for host
            {
                if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) && StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) && Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) > 0.3f)
                {
                    flag = true;
                    amountOfPlayers++;
                }

            }
            if (flag && stopwatch.ElapsedMilliseconds > 1000)
            {
                int num = rnd.Next(1, 50);    // add random num stuff here currently set to 2 for testing
                if (num == 25)
                {
                    StartMovingCoroutineServerRpc(__instance);  // Call the serverRpc to begine movement
                    stopwatch.Restart();
                }
            }
        }
        // read it and weep with me

        /** Use Coroutine for smooth movement of enemies. Since it takes longer than one frame for the SpringMan to move I needed something that acted as a loop 
         * but wasn't a direct loop as that doesn't allow for Unity to update and render the rest of the game(freezes your game until done). Coroutine allows for 
         * the movement to be handle as a sort of loop but still gives control back to Unity to allow for it to render and do other tasks. **/

        public static void StartMovingCoroutineServerRpc(SpringManAI __instance)
        {
            __instance.StartCoroutine(MoveTowardsPlayerServerRpc(__instance));
        }
        // Need to tone down speed and distance moved as its a bit buggy in the current state with my Rpc handling
        [ServerRpc(RequireOwnership = false)]
        private static IEnumerator MoveTowardsPlayerServerRpc(SpringManAI __instance)
        {
            if (!NetworkManager.Singleton.IsServer) // Ensure we're on the server
            {
                yield break; // Exit if not on the server
            }

            float currentSpeed = 0f;
            float targetSpeed = 5f;
            float acceleration = 1.5f;

            Vector3 startPosition = __instance.transform.position;
            Vector3 targetPosition = GameNetworkManager.Instance.localPlayerController.transform.position;

            __instance.agent.SetDestination(targetPosition);

            float totalDistance = Vector3.Distance(targetPosition, startPosition);
            float distanceMoved = 0f;

            while (!__instance.agent.pathPending && distanceMoved < totalDistance * 0.10f)
            {
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);

                float step = currentSpeed * Time.deltaTime;
                Vector3 newPosition = Vector3.MoveTowards(__instance.transform.position, targetPosition, step);
                __instance.transform.position = newPosition; // NetworkTransform synchronizes this change

                __instance.agent.speed = currentSpeed;
                __instance.creatureAnimator.SetFloat("walkSpeed", currentSpeed);
                UpdateAnimationClientRpc(__instance, currentSpeed);

                distanceMoved += step; // Update the distance moved
                Vector3 targetDirection = targetPosition - __instance.transform.position;
                if (targetDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
                    __instance.transform.rotation = Quaternion.Slerp(__instance.transform.rotation, targetRotation, Time.deltaTime * __instance.agent.angularSpeed);
                }
                yield return null;
            }
            __instance.agent.isStopped = true;
            __instance.agent.speed = 0;
            UpdateAnimationClientRpc(__instance, 0f);

        }

        // Believe this may be redundant as the coroutine should update the postion with NetworkTransorm via __instance.transform.position
        [ClientRpc]
        public static void UpdateAnimationClientRpc(SpringManAI __instance, float speed)
        {
            // This method should directly update the animation speed on both the host and clients.
            // Ensure `__instance` references are managed correctly if needed. This might be a simplified view.
            __instance.creatureAnimator.SetFloat("walkSpeed", speed);
        }
    }

}