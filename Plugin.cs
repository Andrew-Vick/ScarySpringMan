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
        private const string modVersion = "0.5.1";

        private readonly Harmony harmony = new Harmony(modGUID);

        internal static ManualLogSource mls = BepInEx.Logging.Logger.CreateLogSource(modGUID);

        void Awake()
        {

            mls.LogInfo("Scary Spring Man is ALIVE :)");

            harmony.PatchAll(typeof(ScarySpringManBase));
            harmony.PatchAll(typeof(SpringManAIPatch));

        }

    }
}
