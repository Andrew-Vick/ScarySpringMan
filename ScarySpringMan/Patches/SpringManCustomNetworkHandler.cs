using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace ScarySpringMan.Patches
{
    [HarmonyPatch(typeof(RoundManager))]
    internal class SpringManCustomNetworkHandler : NetworkBehaviour
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(RoundManager), "SpawnEnemyGameObject")]
        public static void AddCustomNetworkHandler(RoundManager __result)
        {
            List<EnemyAI> spawnedEnemies = __result.SpawnedEnemies;
            var enemy = spawnedEnemies;
            ScarySpringManBase.mls.LogInfo(enemy);

        }
    }
}
