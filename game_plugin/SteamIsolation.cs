using HarmonyLib;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// The modded copy never talks to Steam. Vanilla SteamManager.Awake would
    /// relaunch the Steam installation (a copy has no Steam launch), and with a
    /// steam_appid.txt workaround the game refuses to save. Skipping Awake leaves
    /// Steam "not initialized", which the game already handles: it runs normally
    /// and simply grants no Steam achievements for randomizer runs.
    /// </summary>
    [HarmonyPatch(typeof(SteamManager), "Awake")]
    internal static class SteamIsolation
    {
        private static bool Prefix(SteamManager __instance, ref SteamManager ___s_instance)
        {
            if (___s_instance != null && ___s_instance != __instance)
            {
                Object.Destroy(__instance.gameObject);
                return false;
            }
            ___s_instance = __instance;
            Object.DontDestroyOnLoad(__instance.gameObject);
            return false;
        }
    }
}
