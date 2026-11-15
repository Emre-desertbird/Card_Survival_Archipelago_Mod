using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
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
        public const string Version = "0.4.0";

        internal static ManualLogSource Log;
        internal static ConfigFile Settings;
        internal static ConfigEntry<string> ConfigHost;
        internal static ConfigEntry<string> ConfigSlot;
        internal static ConfigEntry<string> ConfigPassword;

        private void Awake()
        {
            Log = Logger;
            Settings = Config;
            ConfigHost = Config.Bind("Connection", "Server", "archipelago.gg:38281", "Archipelago server address");
            ConfigSlot = Config.Bind("Connection", "Slot", "", "Your slot (player) name");
            ConfigPassword = Config.Bind("Connection", "Password", "", "Room password, if any");

            if (!Registry.Load(Path.GetDirectoryName(Info.Location)))
            {
                Log.LogError(Registry.Error);
            }
            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Checks.Install();
            SaveIsolation.EnsureFolder();
            gameObject.AddComponent<Overlay>();

            // Tooling only: tools/dump_game_data.py sets this, the game writes its
            // data as JSON and quits. Nothing is dumped during normal play.
            string dumpDir = Environment.GetEnvironmentVariable("CSAP_DUMP_DIR");
            if (!string.IsNullOrEmpty(dumpDir))
            {
                StartCoroutine(DataDumper.Run(dumpDir));
            }
            // Tooling only: tools/run_selftest.py plays a scripted session against a local server.
            string selfTest = Environment.GetEnvironmentVariable("CSAP_SELFTEST");
            if (!string.IsNullOrEmpty(selfTest))
            {
                StartCoroutine(SelfTest.Run(selfTest));
            }
            Log.LogInfo(Name + " " + Version + " loaded (registry " + Registry.Version + ").");
        }

        private void Update()
        {
            ApConnection.Pump();
            ApGame.Update();
        }

        private void OnApplicationQuit()
        {
            ApConnection.Disconnect();
        }
    }
}
