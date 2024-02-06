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
                    ScarySpringManBase.mls.LogInfo($"amount of players looking is {amountOfPlayers}");
                }

            }
            if (flag && stopwatch.ElapsedMilliseconds > 15000)
            {
                int num = rnd.Next(1,50);    // add random num stuff here currently set to 2 for testing
                if (num == 25)
                {
                    ScarySpringManBase.mls.LogInfo("telling spring man to move");

                    // Need to have both creatureAnimator and agent.speed set to have proper movement with animations/sounds
                    __instance.creatureAnimator.SetFloat("walkSpeed", 14f); //still moved with only this line but it was a single jittery step
                    __instance.agent.speed = 14f; // this line by itself acts similary to the above however there's no animation/sound it simply glides a bit towards you then stops

                    // Getting Unity error when this is called that only owner can call this however the spring man will move for everyone on the server
                    var helper = new SpringManServerHelper(__instance);
                    helper.TriggerServerRpc();
                    
                    stopwatch.Restart();
                    ScarySpringManBase.mls.LogInfo("Spring Man should have moved");
                    ScarySpringManBase.mls.LogInfo("Timer reset");
                }
            }

        }

        private class SpringManServerHelper
        {
            private SpringManAI springManAI;

            public SpringManServerHelper(SpringManAI springManAI)
            {
                this.springManAI = springManAI;
            }

            [ServerRpc(RequireOwnership = false)]
            public void TriggerServerRpc()
            {
                springManAI.SetAnimationGoServerRpc();
            }
        }
    }
}
