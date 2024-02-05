using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace ScarySpringMan.Patches
{
    [HarmonyPatch(typeof(SpringManAI))]
    internal class SpringManAIPatch : EnemyAI
    {

        private static System.Random rnd = new System.Random();
        private static Stopwatch stopwatch = new Stopwatch();
        private static bool isTimerSet = false;
        private static bool flag = false;


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

            flag = true;

            for (int i = 0; i < 4; i++)
            {
                if (__instance.PlayerIsTargetable(StartOfRound.Instance.allPlayerScripts[i]) && StartOfRound.Instance.allPlayerScripts[i].HasLineOfSightToPosition(__instance.transform.position + Vector3.up * 1.6f, 68f) && Vector3.Distance(StartOfRound.Instance.allPlayerScripts[i].gameplayCamera.transform.position, __instance.eye.position) > 0.3f)
                {
                    flag = false;
                    ScarySpringManBase.mls.LogInfo($"Flag should be false. Elapsed: {stopwatch.ElapsedMilliseconds}");
                    break;
                }

            }
            if (!(flag) && stopwatch.ElapsedMilliseconds > 15000)
            {
                int num = 2;    //add random num stuff here currently set to 2 for testing
                if (num == 2)
                {
                    ScarySpringManBase.mls.LogInfo("telling spring man to move");
                    __instance.SetAnimationGoServerRpc();
                    
                    stopwatch.Restart();
                    ScarySpringManBase.mls.LogInfo("Spring Man should have moved");
                    ScarySpringManBase.mls.LogInfo("Timer reset");
                }
            }

        }
    }
}
