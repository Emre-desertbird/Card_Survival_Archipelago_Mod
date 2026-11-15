using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CardSurvivalArchipelago
{
    /// <summary>BepInEx entry point. Runs inside a separate modded copy of the game.</summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "archipelago.cardsurvival.tropicalisland";
        public const string Name = "Card Survival Archipelago";
        public const string Version = "0.0.1";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            SaveIsolation.EnsureFolder();

            // Tooling only: tools/dump_game_data.py sets this, the game writes its
            // data as JSON and quits. Nothing is dumped during normal play.
            string dumpDir = Environment.GetEnvironmentVariable("CSAP_DUMP_DIR");
            if (!string.IsNullOrEmpty(dumpDir))
            {
                StartCoroutine(DataDumper.Run(dumpDir));
            }
            Log.LogInfo(Name + " " + Version + " loaded.");
        }
    }
}
