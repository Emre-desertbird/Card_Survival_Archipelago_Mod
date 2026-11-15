using HarmonyLib;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Binds saves to their Archipelago slot and keeps the "applied items" counter in step with
    /// the game's main save and checkpoint copy. Randomized games always use Safe Mode, the
    /// game's own checkpoint system: a death offers "Load checkpoint" instead of ending the run.
    /// </summary>
    [HarmonyPatch(typeof(GameLoad), nameof(GameLoad.StartNewGame))]
    internal static class NewGamePatch
    {
        private static void Prefix(int _Index, ref bool _SafeMode)
        {
            CharacterLock.Apply();
            ApGame.OnNewGame(_Index);
            BlueprintRando.Reset();
            Checks.Reset();
            NewPerks.Reset();
            if (ApGame.Active) _SafeMode = true;
        }
    }

    [HarmonyPatch(typeof(GameLoad), nameof(GameLoad.LoadGame))]
    internal static class LoadGamePatch
    {
        private static void Prefix(int _Index)
        {
            ApGame.OnLoadGame(_Index);
            BlueprintRando.Reset();
            Checks.Reset();
            NewPerks.Reset();
        }
    }

    [HarmonyPatch(typeof(GameLoad), nameof(GameLoad.LoadCheckpoint))]
    internal static class LoadCheckpointPatch
    {
        private static void Prefix()
        {
            ApGame.OnLoadCheckpoint();
        }
    }

    [HarmonyPatch(typeof(GameLoad), nameof(GameLoad.SaveGame))]
    internal static class SaveGamePatch
    {
        private static void Postfix(bool __result, bool _Checkpoint)
        {
            if (__result) ApGame.OnSaved(_Checkpoint);
        }
    }

    [HarmonyPatch(typeof(GameLoad), nameof(GameLoad.DeleteGameData))]
    internal static class DeleteGamePatch
    {
        private static void Postfix(int _Index)
        {
            ApGame.OnDeleted(_Index);
        }
    }

    /// <summary>Any real ending completes the goal. After it, "Load checkpoint" lets the player go on.</summary>
    [HarmonyPatch(typeof(EndgameMenu), nameof(EndgameMenu.Setup))]
    internal static class EndingPatch
    {
        private static void Prefix(bool _GameOver, bool _KeepPlaying)
        {
            // _KeepPlaying is only used by the cheat menu's journal view, never by a real ending.
            if (!_GameOver && !_KeepPlaying) ApGame.CompleteGoal();
        }

        private static void Postfix(EndgameMenu __instance, bool _GameOver, bool _KeepPlaying)
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (!ApGame.Active || _GameOver || _KeepPlaying || gm == null || !gm.IsSafeMode) return;
            UnityEngine.GameObject button = Traverse.Create(__instance).Field("LoadCheckpointButton").GetValue<UnityEngine.GameObject>();
            if (button != null) button.SetActive(true);
        }
    }
}

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// The Archipelago character: with the seed's "character_lock" (on by default), every new
    /// Archipelago game starts as Normal Guy, the game's neutral character. He is not in the
    /// character list (he is the base of custom characters), so the start is redirected to him.
    /// </summary>
    internal static class CharacterLock
    {
        public static bool Active
        {
            get
            {
                object value;
                return ApConnection.IsConnected && ApConnection.SlotData != null &&
                       ApConnection.SlotData.TryGetValue("character_lock", out value) && System.Convert.ToBoolean(value);
            }
        }

        public static string CharacterName
        {
            get
            {
                PlayerCharacter character = UniqueIDScriptable.GetFromID<PlayerCharacter>(Registry.CharacterId);
                return character != null ? (string)character.CharacterName : "Normal Guy";
            }
        }

        public static void Apply()
        {
            if (!Active) return;
            PlayerCharacter character = UniqueIDScriptable.GetFromID<PlayerCharacter>(Registry.CharacterId);
            if (character == null)
            {
                Plugin.Log.LogError("The Archipelago character is missing from this game build.");
                return;
            }
            if (GameManager.CurrentPlayerCharacter != character)
            {
                string chosen = GameManager.CurrentPlayerCharacter != null ? (string)GameManager.CurrentPlayerCharacter.CharacterName : "?";
                GameManager.CurrentPlayerCharacter = character;
                Messages.Add("Archipelago games start as " + (string)character.CharacterName + " (chosen: " + chosen + ").");
            }
            GameManager.CurrentModifierPackages = new System.Collections.Generic.List<GameModifierPackage>();
        }
    }

    /// <summary>
    /// With the logbook on, the logbook gives the Determination. The normal journal's tasks still
    /// show as done, but their rewards (Determination, less stress) are skipped so nothing doubles.
    /// </summary>
    [HarmonyPatch(typeof(Objective), "OnComplete")]
    internal static class JournalRewardPatch
    {
        private static bool Prefix(Objective __instance, bool _Init)
        {
            return _Init || !__instance.JournalObjective || !ApGame.Active || !ApGame.State.LocationKinds.Contains("logbook");
        }
    }
}
