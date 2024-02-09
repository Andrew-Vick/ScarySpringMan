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
            if(!isTimerSet)
            {
                stopwatch.Start();
                isTimerSet = true;
                ScarySpringManBase.mls.LogInfo("Timer start");
            }

            flag = false;
            amountOfPlayers = 0;

            for (int i = 0; i < 4; i++) // an 'i' value is assinged to each player in a match so host is 0 and so on
                // Issue I might have using 'i' to detect multiple people is that if anyone but the host is looking the 'i' value will be greater than zero
                // need to capture when 2 or more of the 'i' values are satisfing the if statement below
            {
                if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) && StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) && Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) > 0.3f)
                {
                    flag = true;
                    amountOfPlayers++;
                }

            }
            if (flag && stopwatch.ElapsedMilliseconds > 1000)
            {
                int num = rnd.Next(1,50);    // add random num stuff here currently set to 2 for testing
                if (num == 25)
                {
                    SetAnimationGoServerRpc(__instance);  
                    stopwatch.Restart();
                }
            }
        }
        // read it and weep with me

        /** Use Coroutine for smooth movement of enemies. Since it takes longer than one frame for the SpringMan to move I needed something that acted as a loop 
         * but wasn't a direct loop as that doesn't allow for Unity to update and render the rest of the game(freezes your game until done). Coroutine allows for 
         * the movement to be handle as a sort of loop but still gives control back to Unity to allow for it to render and do other tasks. **/
        public static void StartMovingCoroutine(SpringManAI __instance)
        {
            __instance.StartCoroutine(MoveTowardsPlayer(__instance));
        }

        private static IEnumerator MoveTowardsPlayer(SpringManAI __instance)
        {
            // BEWARE DISGUSTING MATH AHEAD 
            float currentSpeed = 0f; 
            float targetSpeed = 10f;
            float acceleration = 2.5f;
            Vector3 startPosition = __instance.transform.position;  // Grab SpringMan's current postion
            Vector3 playerPosition = GameNetworkManager.Instance.localPlayerController.transform.position; // Grab target players current location
            float totalDistance = Vector3.Distance(playerPosition, startPosition);  // Calculate the distance between the 2
            float distanceMoved = 0f;

            while (distanceMoved < totalDistance * 0.25f)   // Check to see if distanced moved is 25% of the total
            {
                /**Use Mathf.Lerp to slowly bring SpringMan up to speed. This removes the Jerkyness
                 * of simply setting agent.speed and creatureAnimator to a value **/
                currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, acceleration * Time.deltaTime);    
                __instance.agent.speed = currentSpeed;  // Set the current speed to SpringMan's NavMeshAgent variable
                __instance.creatureAnimator.SetFloat("walkSpeed", currentSpeed);    // Set the current speed to the creature animator to allow for animations to be played  

                //Vector3 direction = (playerPosition - __instance.transform.position).normalized;

                float step = currentSpeed * Time.deltaTime; // does what it says

                Vector3 newPosition = Vector3.MoveTowards(__instance.transform.position, playerPosition, step); // Move SpringMan from his current postion to the player 
                __instance.transform.position = newPosition;    // Grab his updated postion

                distanceMoved += Vector3.Distance(__instance.transform.position, startPosition);    // Calculate how far SpringMan is from starting position
                startPosition = __instance.transform.position;  // New start Position for next iteration

                yield return null;
            }

            __instance.agent.speed = 0;
            __instance.creatureAnimator.SetFloat("walkSpeed", 0);
        }
        [ServerRpc(RequireOwnership = false)]
        public static void SetAnimationGoServerRpc(SpringManAI __instance)
        {
            SetAnimationGoClientRpc(__instance);
        }

        [ClientRpc]
        public static void SetAnimationGoClientRpc(SpringManAI __instance)
        {
            StartMovingCoroutine(__instance);
        }


    }
}

