using System.IO;
using HarmonyLib;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Randomizer saves must never mix with the player's normal saves (which Steam
    /// also syncs to the cloud). Every save, options and portrait path in the game
    /// is built from Application.persistentDataPath, so that one value is moved to
    /// "...\WinterSpring Games\Card Survival - Tropical Island (Archipelago)".
    /// </summary>
    [HarmonyPatch(typeof(Application), "persistentDataPath", MethodType.Getter)]
    internal static class SaveIsolation
    {
        public const string Suffix = " (Archipelago)";

        public static bool IsIsolated
        {
            get { return Application.persistentDataPath.TrimEnd('/', '\\').EndsWith(Suffix); }
        }

        private static void Postfix(ref string __result)
        {
            if (!string.IsNullOrEmpty(__result) && !__result.TrimEnd('/', '\\').EndsWith(Suffix))
            {
                __result = __result.TrimEnd('/', '\\') + Suffix;
            }
        }

        public static void EnsureFolder()
        {
            if (IsIsolated)
            {
                Directory.CreateDirectory(Application.persistentDataPath);
            }
        }
    }

    /// <summary>
    /// Safety net: if the redirect above ever fails (for example after a game
    /// update), stop before a single save or options file is read or written.
    /// </summary>
    [HarmonyPatch(typeof(GameLoad), "Awake")]
    internal static class SaveIsolationGuard
    {
        private static bool Prefix()
        {
            if (SaveIsolation.IsIsolated)
            {
                return true;
            }
            Plugin.Log.LogFatal("Save folder is not isolated (" + Application.persistentDataPath +
                                "). Closing so the normal saves stay untouched.");
            Application.Quit();
            return false;
        }
    }
}
