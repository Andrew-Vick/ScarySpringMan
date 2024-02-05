using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using ScarySpringMan.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScarySpringMan
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class ScarySpringManBase : BaseUnityPlugin
    {
        private const string modGUID = "Goobius.ScarySpringMan";
        private const string modName = "Scary Spring Man";
        private const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        private static ScarySpringManBase Instance;

        internal ManualLogSource mls;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

            mls.LogInfo("Scary Spring Man is ALIVE :)");

            harmony.PatchAll(typeof(ScarySpringManBase));
            harmony.PatchAll(typeof(SpringManAIPatch));

        }

    }
}
