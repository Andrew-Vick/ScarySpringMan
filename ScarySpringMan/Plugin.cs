using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ScarySpringMan.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ScarySpringMan
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class ScarySpringManBase : BaseUnityPlugin
    {

        private const string modGUID = "Goobius.ScaryCoilHead";
        private const string modName = "Scary Coil Head";
        private const string modVersion = "0.1.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static ScarySpringManBase Instance;

        internal static ManualLogSource mls;

        void Awake()
        {
            NetcodePatcher();
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            mls.LogInfo("Scary Spring Man is ALIVE :)");

            harmony.PatchAll(typeof(ScarySpringManBase));
            harmony.PatchAll(typeof(SpringManAIPatch));

        }

        private static void NetcodePatcher()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

    }
}
