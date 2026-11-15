using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Tooling only (tools/run_selftest.py sets CSAP_SELFTEST). Plays a scripted session in the
    /// real game against a real local Archipelago server and writes a JSON report:
    /// connect, new game, discovery, research, receiving, save/load, checkpoint, ending.
    /// Saves go to the separate "(Archipelago Test)" folder.
    /// </summary>
    internal static class SelfTest
    {
        private static readonly List<JObject> steps = new List<JObject>();
        private static bool failed;

        private static readonly MethodInfo AddCardSimple = AccessTools.Method(typeof(GameManager), "AddCard", new[]
        {
            typeof(CardData), typeof(InGameCardBase), typeof(bool), typeof(TransferedDurabilities), typeof(bool),
            typeof(SpawningLiquid), typeof(Vector2Int), typeof(bool),
        });

        private static void Check(string name, bool ok, object detail = null)
        {
            var step = new JObject { ["step"] = name, ["ok"] = ok };
            if (detail != null) step["detail"] = JToken.FromObject(detail);
            steps.Add(step);
            if (!ok) failed = true;
            Plugin.Log.LogInfo("[SelfTest] " + (ok ? "OK   " : "FAIL ") + name + (detail != null ? " " + JsonConvert.SerializeObject(detail) : ""));
        }

        private static IEnumerator WaitFor(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end) yield return null;
        }

        private static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        public static IEnumerator Run(string configPath)
        {
            JObject config = JObject.Parse(File.ReadAllText(configPath));
            string report = (string)config["report"];
            IEnumerator body = Body(config);
            while (true)
            {
                object current;
                try
                {
                    if (!body.MoveNext()) break;
                    current = body.Current;
                }
                catch (Exception error)
                {
                    Check("exception", false, error.ToString());
                    break;
                }
                yield return current;
            }
            var result = new JObject { ["passed"] = !failed, ["steps"] = new JArray(steps) };
            File.WriteAllText(report, result.ToString(Formatting.Indented));
            Plugin.Log.LogInfo("[SelfTest] finished, passed=" + !failed);
            yield return Wait(1f);
            Application.Quit();
        }

        private static GameManager GM
        {
            get { return MBSingleton<GameManager>.Instance; }
        }

        private static object Describe(CardData card)
        {
            GameManager gm = GM;
            return new
            {
                card = card.name,
                state = gm.BlueprintModelStates[card].ToString(),
                known = gm.BlueprintModelCards.Contains(card),
                researchable = gm.PurchasableBlueprintCards.Contains(card),
                researched = BlueprintRando.IsResearched(card),
                received = BlueprintRando.IsReceived(card),
            };
        }

        private static IEnumerator Body(JObject config)
        {
            Check("registry loaded", Registry.Error == null, new { version = Registry.Version, locations = Registry.Locations.Count });
            yield return WaitFor(() => GameLoad.Instance != null && UnityEngine.Object.FindObjectOfType<MainMenu>() != null, 120f);
            yield return Wait(2f);
            Check("main menu reached", UnityEngine.Object.FindObjectOfType<MainMenu>() != null);

            // 1. Connect.
            ApConnection.Connect((string)config["host"], (string)config["slot"], "", null);
            yield return WaitFor(() => ApConnection.State != ApConnection.Status.Connecting, 30f);
            Check("connected", ApConnection.IsConnected, ApConnection.StatusText);
            if (!ApConnection.IsConnected) yield break;
            yield return WaitFor(() => ApConnection.Scouted.Count >= Registry.Locations.Count, 20f);
            Check("all locations scouted", ApConnection.Scouted.Count == Registry.Locations.Count, ApConnection.Scouted.Count);

            // 2. New game (default gamemode, first character).
            MainMenu menu = UnityEngine.Object.FindObjectOfType<MainMenu>();
            Gamemode[] modes = Traverse.Create(menu).Field("AvailableGamemodes").GetValue<Gamemode[]>();
            GameManager.CurrentGamemode = modes[0];
            GameManager.CurrentPlayerCharacter = modes[0].PlayableCharacters.FirstOrDefault(c => c.name == "Tourist") ?? modes[0].PlayableCharacters[0];
            GameManager.CurrentModifierPackages = new List<GameModifierPackage>();
            GameLoad.Instance.StartNewGame(0, false, false);
            Check("save bound to slot", ApGame.Active && ApGame.State.SeedName == ApConnection.SeedName);
            yield return WaitFor(() => ApGame.InGame, 120f);
            yield return Wait(3f);
            GameManager gm = GM;
            Check("game running", ApGame.InGame, new
            {
                character = GameManager.CurrentPlayerCharacter.name,
                offered = modes[0].PlayableCharacters.Select(c => c.name).ToArray(),
            });
            Check("safe mode forced on", gm.IsSafeMode);
            Check("research uses time", gm.BlueprintPurchasing && gm.PurchasingWithTime);

            // 3. Pick a blueprint whose research holds another blueprint for this slot.
            var shuffled = BlueprintRando.Shuffled;
            CardData target = null, reward = null;
            foreach (var pair in shuffled)
            {
                if (gm.BlueprintModelCards.Contains(pair.Key)) continue;
                var info = ApConnection.Scouted[pair.Value.Id];
                if (info.Player.Slot != ApConnection.Slot) continue;
                Registry.Entry item;
                if (!Registry.Items.TryGetValue(info.ItemId, out item) || item.CardId == null || item.CardId == pair.Key.UniqueID) continue;
                CardData card = UniqueIDScriptable.GetFromID<CardData>(item.CardId);
                if (card == null || gm.BlueprintModelCards.Contains(card) || card.BlueprintUnlockSunsCost <= 0f || pair.Key.BlueprintUnlockSunsCost <= 0f) continue;
                target = pair.Key;
                reward = card;
                break;
            }
            Check("found a test research", target != null, target != null ? target.name + " holds " + reward.name : null);
            if (target == null) yield break;
            long targetLocation = shuffled[target].Id;
            long rewardLocation = shuffled[reward].Id;

            // 4. Discover it the vanilla way (the unlock action adds the card).
            yield return gm.StartCoroutine((IEnumerator)AddCardSimple.Invoke(gm, new object[]
                { target, null, true, null, true, SpawningLiquid.Empty, new Vector2Int(gm.CurrentTickInfo.z, 0), false }));
            BlueprintRando.Reconcile(true);
            Check("discovered blueprint is researchable, not buildable",
                  gm.PurchasableBlueprintCards.Contains(target) && gm.BlueprintModelStates[target] == BlueprintModelState.Purchasable,
                  Describe(target));

            // 5. Finish its research.
            BlueprintModelsScreen screen = MBSingleton<GraphicsManager>.Instance.BlueprintModelsPopup;
            screen.LoadResearchedBlueprint(target);
            screen.FinishBlueprintResearch();
            Check("research sends the check", ApGame.State.IsChecked(targetLocation));
            Check("research does not teach the blueprint",
                  gm.BlueprintModelStates[target] == BlueprintModelState.Hidden && !gm.PurchasableBlueprintCards.Contains(target),
                  Describe(target));
            yield return WaitFor(() => ApConnection.Session.Locations.AllLocationsChecked.Contains(targetLocation), 10f);
            Check("server confirms the check", ApConnection.Session.Locations.AllLocationsChecked.Contains(targetLocation));

            // 6. The item from that research arrives and unlocks the other recipe only.
            yield return WaitFor(() => BlueprintRando.IsReceived(reward) && gm.BlueprintModelCards.Contains(reward), 15f);
            yield return Wait(1f);
            BlueprintRando.Reconcile(true);
            Check("received recipe is buildable", gm.BlueprintModelStates[reward] == BlueprintModelState.Available, Describe(reward));
            Check("received recipe keeps its research check", !ApGame.State.IsChecked(rewardLocation), Describe(reward));

            // 7. Its vanilla discovery still makes the research available.
            bool discoveredByConditions = gm.PurchasableBlueprintCards.Contains(reward);
            if (!discoveredByConditions)
            {
                yield return gm.StartCoroutine((IEnumerator)AddCardSimple.Invoke(gm, new object[]
                    { reward, null, true, null, true, SpawningLiquid.Empty, new Vector2Int(gm.CurrentTickInfo.z, 0), false }));
            }
            BlueprintRando.Reconcile(true);
            Check("discovery of a received recipe opens its research", gm.PurchasableBlueprintCards.Contains(reward),
                  new { discoveredByConditions, now = Describe(reward) });
            BlueprintRando.ResearchViewOpen = true;
            BlueprintRando.Reconcile(true);
            bool shownInResearch = gm.BlueprintModelStates[reward] == BlueprintModelState.Purchasable;
            BlueprintRando.ResearchViewOpen = false;
            BlueprintRando.Reconcile(true);
            Check("research tab shows it, build tabs still build it",
                  shownInResearch && gm.BlueprintModelStates[reward] == BlueprintModelState.Available);

            // 8. Save with a checkpoint, then research it (state moves on after the checkpoint).
            GameLoad.Instance.AutoSaveGame(true);
            Check("checkpoint records applied items", ApGame.State.AppliedCheckpoint == ApGame.AppliedNow,
                  new { ApGame.State.AppliedCheckpoint, ApGame.AppliedNow });
            screen.LoadResearchedBlueprint(reward);
            screen.FinishBlueprintResearch();
            Check("researching a received recipe sends its check and keeps the recipe",
                  ApGame.State.IsChecked(rewardLocation) && gm.BlueprintModelStates[reward] == BlueprintModelState.Available,
                  Describe(reward));
            GameLoad.Instance.AutoSaveGame(false);
            yield return Wait(2f);

            // 9. Reload the save: everything comes back the same.
            GameLoad.Instance.LoadGame(0);
            yield return WaitFor(() => !ApGame.InGame, 30f);
            yield return WaitFor(() => ApGame.InGame, 120f);
            yield return Wait(3f);
            gm = GM;
            BlueprintRando.Reconcile(true);
            Check("after reload: researched-not-received stays hidden", gm.BlueprintModelStates[target] == BlueprintModelState.Hidden, Describe(target));
            Check("after reload: received recipe still buildable", gm.BlueprintModelStates[reward] == BlueprintModelState.Available &&
                  !gm.PurchasableBlueprintCards.Contains(reward), Describe(reward));
            int serverCount = ApConnection.Session.Items.AllItemsReceived.Count;
            Check("after reload: ledger matches the server, nothing doubled", ApGame.State.Received.Count == serverCount &&
                  ApGame.AppliedNow == serverCount, new { ledger = ApGame.State.Received.Count, server = serverCount, ApGame.AppliedNow });

            // 10. Load the checkpoint (taken before the second research).
            GameLoad.Instance.LoadCheckpoint();
            yield return WaitFor(() => !ApGame.InGame, 30f);
            yield return WaitFor(() => ApGame.InGame, 120f);
            yield return Wait(3f);
            gm = GM;
            BlueprintRando.Reconcile(true);
            Check("after checkpoint: checks are not rolled back", ApGame.State.IsChecked(rewardLocation) && ApGame.State.IsChecked(targetLocation));
            Check("after checkpoint: research already done is not offered again",
                  !gm.PurchasableBlueprintCards.Contains(reward) && gm.BlueprintModelStates[reward] == BlueprintModelState.Available,
                  Describe(reward));
            Check("after checkpoint: applied counter restored", ApGame.AppliedNow == ApGame.State.Received.Count,
                  new { ApGame.AppliedNow, received = ApGame.State.Received.Count });

            // 11. A real ending's screen completes the goal and offers the checkpoint.
            MBSingleton<EndgameMenu>.Instance.Setup("Self test ending", 0, 0, GameLoad.Instance.Games[GameLoad.Instance.CurrentGameDataIndex].MainData, false, false, false, false, false);
            yield return Wait(1f);
            GameObject button = Traverse.Create(MBSingleton<EndgameMenu>.Instance).Field("LoadCheckpointButton").GetValue<GameObject>();
            Check("ending completes the goal", ApGame.State.GoalDone);
            Check("ending offers load checkpoint", button != null && button.activeSelf);
            yield return Wait(2f);
        }
    }
}
