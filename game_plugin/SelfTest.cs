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
        private static readonly MethodInfo AddCardFull = AccessTools.GetDeclaredMethods(typeof(GameManager))
            .First(method => method.Name == "AddCard" && method.GetParameters().Length == 21);

        /// <summary>Puts a card into the player's hand (the item row that counts as carried).</summary>
        private static IEnumerator SpawnInHand(GameManager gm, CardData card)
        {
            yield return gm.StartCoroutine((IEnumerator)AddCardFull.Invoke(gm, new object[]
            {
                card, new SlotInfo(SlotsTypes.Item, -2), gm.CurrentEnvironment, null, true, null, null, null, null, null,
                Vector3.zero, true, SpawningLiquid.DefaultLiquid, false, false, new Vector2Int(gm.CurrentTickInfo.z, 0), null, 0, -1, false, "",
            }));
        }

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

        /// <summary>
        /// Waits until a loaded game is running. While loading, the game shows events that crossed a
        /// threshold (e.g. "Cooking level 1" after a skill item) and waits for a click, as for a player;
        /// the test clicks their first button.
        /// </summary>
        private static IEnumerator WaitForLoaded()
        {
            float end = Time.realtimeSinceStartup + 120f;
            while (!ApGame.InGame && Time.realtimeSinceStartup < end)
            {
                GraphicsManager graphics = MBSingleton<GraphicsManager>.Instance;
                InGameCardBase inspected = graphics != null ? graphics.InspectedCard : null;
                if (inspected != null && inspected.CardModel != null && inspected.CardModel.CardType == CardTypes.Event &&
                    graphics.CurrentInspectionPopup != null)
                {
                    Plugin.Log.LogInfo("[SelfTest] confirming event " + inspected.CardModel.name);
                    graphics.CurrentInspectionPopup.OnButtonClicked(0, false);
                    yield return Wait(1f);
                }
                yield return null;
            }
        }

        /// <summary>Clicks away event popups, like a player would.</summary>
        private static IEnumerator ConfirmEvents()
        {
            for (int i = 0; i < 5; i++)
            {
                GraphicsManager graphics = MBSingleton<GraphicsManager>.Instance;
                InGameCardBase inspected = graphics != null ? graphics.InspectedCard : null;
                if (inspected == null || inspected.CardModel == null || inspected.CardModel.CardType != CardTypes.Event ||
                    graphics.CurrentInspectionPopup == null) yield break;
                graphics.CurrentInspectionPopup.OnButtonClicked(0, false);
                yield return Wait(1f);
            }
        }

        /// <summary>Reports an action on a card the way the game does when the player performs it.</summary>
        private static void ReportAction(string cardId, string actionName)
        {
            GameManager.OnActionPerformed(new ActionReport
            {
                FromCard = UniqueIDScriptable.GetFromID<CardData>(cardId),
                Action = new CardAction { ActionName = new LocalizedString { DefaultText = actionName } },
            });
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

        private static int CountCards(GameManager gm, CardData card)
        {
            return gm.AllCards.Count(c => c != null && c.CardModel == card && !c.Destroyed);
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
            Check("character lock: the game starts as the Archipelago character",
                  GameManager.CurrentPlayerCharacter != null && GameManager.CurrentPlayerCharacter.UniqueID == Registry.CharacterId,
                  GameManager.CurrentPlayerCharacter != null ? GameManager.CurrentPlayerCharacter.name : null);
            Check("safe mode forced on", gm.IsSafeMode);
            Check("research uses time", gm.BlueprintPurchasing && gm.PurchasingWithTime);
            // A character perk can start the game in an area outside the logic (e.g. the crashed plane);
            // such an area has no check. Every area that is a check must be sent on arrival.
            Registry.Entry startArea;
            bool startIsCheck = Registry.VisitByCard.TryGetValue(gm.CurrentEnvironment.UniqueID, out startArea);
            Check("starting area sends its visit check if it is one", !startIsCheck || ApGame.State.IsChecked(startArea.Id),
                  new { area = gm.CurrentEnvironment.name, startIsCheck });

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

            // 7b. A survival basic: research teaches it as in vanilla and also sends its check.
            CardData shelter = UniqueIDScriptable.GetFromID<CardData>(
                Registry.ResearchByCard.Values.First(entry => entry.Key == "Bp_Shelter").CardId);
            if (!gm.BlueprintModelCards.Contains(shelter))
            {
                yield return gm.StartCoroutine((IEnumerator)AddCardSimple.Invoke(gm, new object[]
                    { shelter, null, true, null, true, SpawningLiquid.Empty, new Vector2Int(gm.CurrentTickInfo.z, 0), false }));
            }
            screen.LoadResearchedBlueprint(shelter);
            screen.FinishBlueprintResearch();
            BlueprintRando.Reconcile(true);
            Check("survival basic: research teaches it and sends its check",
                  ApGame.State.IsChecked(Registry.ResearchByCard[shelter.UniqueID].Id) &&
                  gm.BlueprintModelStates[shelter] == BlueprintModelState.Available, gm.BlueprintModelStates[shelter].ToString());

            // 8. Save with a checkpoint, then research it (state moves on after the checkpoint).
            GameLoad.Instance.AutoSaveGame(true);
            Check("checkpoint records applied items", ApGame.State.AppliedCheckpoint == ApGame.AppliedNow,
                  new { ApGame.State.AppliedCheckpoint, ApGame.AppliedNow });
            screen.LoadResearchedBlueprint(reward);
            screen.FinishBlueprintResearch();
            Check("researching a received recipe sends its check and keeps the recipe",
                  ApGame.State.IsChecked(rewardLocation) && gm.BlueprintModelStates[reward] == BlueprintModelState.Available,
                  Describe(reward));
            // 8b. A build check fires only on the final construction step.
            var build = Registry.BuildByCard.Values.First(entry => !ApGame.State.IsChecked(entry.Id));
            CardData buildCard = UniqueIDScriptable.GetFromID<CardData>(build.CardId);
            GameManager.OnActionPerformed(new ActionReport { FromCard = buildCard, Action = new CardAction
                { ProducedCards = new CardsDropCollection[0] } });
            bool earlyStep = ApGame.State.IsChecked(build.Id);
            GameManager.OnActionPerformed(new ActionReport { FromCard = buildCard, Action = new CardAction
                { ProducedCards = new[] { new CardsDropCollection { CollectionName = buildCard.name + "_blueprint" } } } });
            Check("build check only on the final step", !earlyStep && ApGame.State.IsChecked(build.Id), build.Name);

            // 8c. Receive a skill, a perk and an equipment blueprint.
            Registry.Entry skill = null, perk = null, equipment = null;
            long skillAt = 0, perkAt = 0, equipmentAt = 0;
            foreach (var pair in ApConnection.Scouted)
            {
                Registry.Entry item;
                if (ApGame.State.IsChecked(pair.Key) || pair.Value.Player.Slot != ApConnection.Slot ||
                    !Registry.Items.TryGetValue(pair.Value.ItemId, out item)) continue;
                if (item.Kind == "skill" && skill == null) { skill = item; skillAt = pair.Key; }
                else if (item.Kind == "perk" && perk == null) { perk = item; perkAt = pair.Key; }
                else if (item.Equipment.Count > 0 && equipment == null) { equipment = item; equipmentAt = pair.Key; }
            }
            Check("seed has a skill, a perk and an equipment item for this slot", skill != null && perk != null && equipment != null,
                  new { skill = skill?.Name, perk = perk?.Name, equipment = equipment?.Name });
            if (skill == null || perk == null || equipment == null) yield break;
            GameStat skillStat = UniqueIDScriptable.GetFromID<GameStat>(skill.StatId);
            CardData equipmentCard = UniqueIDScriptable.GetFromID<CardData>(equipment.Equipment[0]);
            float skillBefore = gm.StatsDict[skillStat].CurrentBaseValue;
            int equipmentBefore = CountCards(gm, equipmentCard);
            int perksBefore = ItemEffects.ActivePerks;
            foreach (long location in new[] { skillAt, perkAt, equipmentAt }) ApGame.CompleteLocation(location);
            yield return WaitFor(() => ApGame.State.HasReceived(skill.Id) && ApGame.State.HasReceived(perk.Id) &&
                                       ApGame.State.HasReceived(equipment.Id), 15f);
            yield return Wait(3f);
            float skillAfter = gm.StatsDict[skillStat].CurrentBaseValue;
            int equipmentAfter = CountCards(gm, equipmentCard);
            Check("skill item raises the skill", Mathf.Approximately(skillAfter, Mathf.Min(skillBefore + skill.Amount, skillStat.MinMaxValue.y)),
                  new { skill.Name, skillBefore, skillAfter });
            Check("perk item becomes active", ItemEffects.ActivePerks == perksBefore + 1, perk.Name);
            Check("equipment blueprint gives one finished copy", equipmentAfter == equipmentBefore + 1,
                  new { equipment.Name, equipmentBefore, equipmentAfter });
            Check("receiving equipment does not complete its build check",
                  !Registry.BuildByCard.ContainsKey(equipment.CardId) || !ApGame.State.IsChecked(Registry.BuildByCard[equipment.CardId].Id));
            // 8d. "First time" checks (Phase A): the seed's options arrive, a watched card completes its check.
            Check("seed options stored with the save", new[] { "fish", "hunt", "cook", "farm" }.All(ApGame.State.LocationKinds.Contains),
                  ApGame.State.LocationKinds);
            Registry.Entry hunt = Registry.Watched.FirstOrDefault(entry => entry.Kind == "hunt" && !ApGame.State.IsChecked(entry.Id));
            CardData prey = UniqueIDScriptable.GetFromID<CardData>(hunt.Watch[0].Cards.First());
            GameManager.GiveCard(prey, true);
            yield return WaitFor(() => ApGame.State.IsChecked(hunt.Id), 5f);
            Check("a watched card completes its first-time check", ApGame.State.IsChecked(hunt.Id), hunt.Name + " via " + prey.name);
            Registry.Entry three = Registry.Watched.First(entry => entry.Name == "Plant - Three Different Crops");
            Registry.Entry first = Registry.Watched.First(entry => entry.Name == "Plant - First Crop");
            List<string> crops = three.Watch[0].Cards.Where(id => !ApGame.State.HasSeen(id)).Take(3).ToList();
            foreach (string id in crops.Take(2)) GameManager.GiveCard(UniqueIDScriptable.GetFromID<CardData>(id), true);
            yield return Wait(1.5f);
            bool afterTwo = ApGame.State.IsChecked(three.Id);
            GameManager.GiveCard(UniqueIDScriptable.GetFromID<CardData>(crops[2]), true);
            yield return WaitFor(() => ApGame.State.IsChecked(three.Id), 5f);
            Check("counting check: three different crops needs the third", !afterTwo && ApGame.State.IsChecked(three.Id) &&
                  ApGame.State.IsChecked(first.Id), new { afterTwo, three = ApGame.State.IsChecked(three.Id) });

            // 8d2. The logbook (Phase B): tasks complete from cards, actions and sent checks, and give
            // their Determination once; the normal journal's reward for the same thing is skipped.
            GameStat determination = UniqueIDScriptable.GetFromID<GameStat>(Registry.DeterminationStatId);
            float determinationStart = gm.StatsDict[determination].CurrentBaseValue;
            int rewardsStart = ApGame.State.Rewards.Count;
            Registry.Entry explore = Registry.Watched.First(entry => entry.Name == "Logbook - Explore the Beach");
            GameManager.GiveCard(UniqueIDScriptable.GetFromID<CardData>(explore.Watch[0].Cards.First()), true);
            yield return WaitFor(() => ApGame.State.IsChecked(explore.Id), 5f);
            yield return ConfirmEvents();
            Check("logbook: a card task completes", ApGame.State.IsChecked(explore.Id), explore.Name);
            Registry.Entry refreshment = Registry.Watched.First(entry => entry.Name == "Logbook - First Refreshment");
            var actions = refreshment.Watch.Select(part => part.Actions.First().Split('|')).ToList();
            ReportAction(actions[0][0], actions[0][1]);
            bool afterOne = ApGame.State.IsChecked(refreshment.Id);
            ReportAction(actions[1][0], actions[1][1]);
            Check("logbook: an action task needs every action", !afterOne && ApGame.State.IsChecked(refreshment.Id),
                  actions.Select(action => action[1]).ToArray());
            Registry.Entry tally = Registry.Watched.First(entry => entry.Name == "Logbook - Hunter's Tally");
            foreach (Registry.Entry animal in Registry.Watched.Where(entry => entry.Kind == "hunt" && !ApGame.State.IsChecked(entry.Id)).Take(4))
            {
                GameManager.GiveCard(UniqueIDScriptable.GetFromID<CardData>(animal.Watch[0].Cards.First()), true);
            }
            yield return WaitFor(() => ApGame.State.IsChecked(tally.Id), 8f);
            Check("logbook: a task counting sent checks", ApGame.State.IsChecked(tally.Id),
                  new { hunts = Checks.Progress(tally.Watch[0]) });
            yield return Wait(1f);
            float determinationGiven = ApGame.State.Rewards.Skip(rewardsStart).Sum(id => Registry.Locations[id].Determination);
            float determinationNow = gm.StatsDict[determination].CurrentBaseValue;
            Check("logbook: Determination given once, the normal journal's reward skipped",
                  Mathf.Abs(determinationNow - (determinationStart + determinationGiven)) < 2f,
                  new { determinationStart, determinationGiven, determinationNow });

            // 8d2b. The logbook in the game's own journal book (the "press a key" screen is dismissed like a player would).
            AsyncLoading loading = UnityEngine.Object.FindObjectOfType<AsyncLoading>();
            GameObject pressKey = loading != null ? Traverse.Create(loading).Field("InitLoadingVisuals").GetValue<GameObject>() : null;
            if (pressKey != null) pressKey.SetActive(false);
            gm.OpenJournal();
            yield return Wait(2f);
            ContentDisplayer journal = Traverse.Create(gm).Field("Journal").GetValue<ContentDisplayer>();
            var logbookPages = LogbookJournal.LastPages;
            string beachLine = logbookPages.SelectMany(page => page.Sections).SelectMany(section => section.Entries)
                .Select(entry => entry.Title.DefaultText).FirstOrDefault(title => title.Contains("Explore the Beach"));
            string screenshot = Path.Combine(Path.GetDirectoryName((string)config["report"]), "journal.png");
            ScreenCapture.CaptureScreenshot(screenshot);
            yield return Wait(1f);
            Check("logbook in the journal book: one page per chapter, done tasks ticked",
                  journal != null && logbookPages.Count == 6 && journal.AllPages.Contains(logbookPages[0]) &&
                  beachLine != null && beachLine.StartsWith("[x]"),
                  new { pages = logbookPages.Select(page => page.PageTitle.DefaultText).ToArray(), beachLine, screenshot });
            gm.CloseJournal();
            yield return Wait(2f);

            // 8d3. New perks (Phase C): receive all of them through real checks, then measure each effect.
            CardAction[] allActions = GameLoad.Instance.DataBase.AllData.OfType<CardData>()
                .Where(card => card.DismantleActions != null).SelectMany(card => card.DismantleActions).ToArray();
            CardAction travelTwo = allActions.FirstOrDefault(action => action.IsTravellingAction && action.UnmodifiedDaytimeCost == 2);
            CardAction travelOne = allActions.FirstOrDefault(action => action.IsTravellingAction && action.UnmodifiedDaytimeCost == 1);
            var buildAction = new CardAction(LocalizedString.Build, default(LocalizedString), 3, null, null, null);
            var toolAction = new CardOnCardAction();
            toolAction.GivenCardChanges = new CardStateChange { ModType = CardModifications.DurabilityChanges, UsageChange = new Vector2(-2f, -2f) };
            InGameCardBase food = gm.AllCards.FirstOrDefault(card => card != null && !card.Destroyed && card.CardModel != null &&
                                                                      card.CardModel.CardType == CardTypes.Item && card.CurrentSpoilageRate < 0f);
            int researchTicks = GameManager.DaysToTicks(1f);
            var perkLocations = ApConnection.Scouted.Where(pair => !ApGame.State.IsChecked(pair.Key) && pair.Value.Player.Slot == ApConnection.Slot &&
                    Registry.Items.ContainsKey(pair.Value.ItemId) && Registry.Items[pair.Value.ItemId].Kind == "newperk").ToList();
            int perksAlready = ApGame.State.Received.Count(id => Registry.Items.ContainsKey(id) && Registry.Items[id].Kind == "newperk");
            Check("seed has every new perk level for this slot", perkLocations.Count + perksAlready == 10,
                  new { open = perkLocations.Count, received = perksAlready });
            foreach (var pair in perkLocations) ApGame.CompleteLocation(pair.Key);
            yield return WaitFor(() => NewPerks.Level("pack_mule") == 3 && NewPerks.Level("pantry_keeper") == 1 &&
                                       NewPerks.Level("inquisitive_mind") == 2 && NewPerks.Level("careful_hands") == 2, 15f);
            yield return Wait(1f);
            CardData stone = GameLoad.Instance.DataBase.AllData.OfType<CardData>().First(card => card.name == "Stone");
            for (int i = 0; i < 3; i++) yield return SpawnInHand(gm, stone);
            gm.CalculateCarryWeight();
            yield return Wait(1f);
            GraphicsManager graphics = MBSingleton<GraphicsManager>.Instance;
            float carriedSum = graphics.GetHandCards(false).Concat(graphics.GetEquippedCards(false)).Sum(card => card.CurrentWeight);
            float carriedStat = gm.InGamePlayerWeight.CurrentValue(gm.NotInBase);
            Check("Pack Mule 3: carried weight counts 70%", carriedSum > 0f && Mathf.Abs(carriedStat - carriedSum * 0.7f) < 0.5f,
                  new { carriedSum, carriedStat });
            travelTwo.CollectActionModifiers(null, null);
            travelOne.CollectActionModifiers(null, null);
            Check("Pathfinder: 30 minutes of travel become 15, 15 stay 15",
                  travelTwo.TotalDaytimeCost == 1 && travelOne.TotalDaytimeCost == 1,
                  new { two = travelTwo.TotalDaytimeCost, one = travelOne.TotalDaytimeCost });
            buildAction.CollectActionModifiers(null, null);
            Check("Nimble Hands: a 45-minute building step takes 30", buildAction.TotalDaytimeCost == 2, buildAction.TotalDaytimeCost);
            toolAction.CollectActionModifiers(null, null);
            Check("Careful Hands 2: tools wear 30% slower", Mathf.Abs(toolAction.GivenDurabilityChanges.Usage.FloatValue - 0.6f) < 0.01f,
                  toolAction.GivenDurabilityChanges.Usage.FloatValue);
            Check("Inquisitive Mind 2: research takes 30% fewer ticks",
                  GameManager.DaysToTicks(1f) == Mathf.CeilToInt(researchTicks * 0.7f), new { before = researchTicks, now = GameManager.DaysToTicks(1f) });
            float spoilWith = food != null ? food.CurrentSpoilageRate : 0f;
            NewPerks.Suspended = true;
            float spoilWithout = food != null ? food.CurrentSpoilageRate : 0f;
            NewPerks.Suspended = false;
            Check("Pantry Keeper: food spoils 15% slower", food != null && Mathf.Abs(spoilWith - spoilWithout * 0.85f) < 0.0001f,
                  new { card = food != null ? food.CardModel.name : null, spoilWith, spoilWithout });

            // 8e. Finds: they land on the ground; beyond 10 per in-game hour they wait in the buffer.
            var findLocations = ApConnection.Scouted.Where(pair => !ApGame.State.IsChecked(pair.Key) &&
                    pair.Value.Player.Slot == ApConnection.Slot && Registry.Items.ContainsKey(pair.Value.ItemId) &&
                    Registry.Items[pair.Value.ItemId].Kind == "find")
                .OrderByDescending(pair => Registry.Items[pair.Value.ItemId].Contents.Any(part => part.LiquidId != null))
                .Take(Finds.PerHour + 4).ToList();
            Check("seed has enough finds for this slot", findLocations.Count == Finds.PerHour + 4, findLocations.Count);
            Registry.Entry liquidFind = Registry.Items[findLocations[0].Value.ItemId];
            int receivedBefore = ApGame.State.Received.Count;
            foreach (var pair in findLocations) ApGame.CompleteLocation(pair.Key);
            yield return WaitFor(() => ApGame.State.Received.Count >= receivedBefore + findLocations.Count &&
                                       ApGame.AppliedNow == ApGame.State.Received.Count, 15f);
            yield return Wait(2f);
            Check("finds beyond the hourly limit wait in the buffer", Finds.Buffer.Count >= 4 && Finds.Buffer.Distinct().Count() == Finds.Buffer.Count,
                  new { buffered = Finds.Buffer.Count, received = findLocations.Count });
            CardData shell = UniqueIDScriptable.GetFromID<CardData>(liquidFind.Contents[0].CardId);
            CardData drink = liquidFind.Contents[0].LiquidId != null ? UniqueIDScriptable.GetFromID<CardData>(liquidFind.Contents[0].LiquidId) : null;
            var shells = gm.AllCards.Where(c => c != null && !c.Destroyed && c.CardModel == shell).ToList();
            Check("a liquid find is a filled coconut shell on the ground", drink != null && shells.Any(c =>
                      c.ContainedLiquid != null && c.ContainedLiquid.CardModel == drink && c.CurrentSlotInfo.SlotType == SlotsTypes.Base),
                  new { find = liquidFind.Name, shells = shells.Select(c => new { slot = c.CurrentSlotInfo.SlotType.ToString(),
                      liquid = c.ContainedLiquid != null ? c.ContainedLiquid.CardModel.name : null }).ToArray() });
            int buffered = Finds.Buffer.Count;
            int firstIndex = Finds.Buffer[0];
            Finds.Release(firstIndex);
            Check("taking one find from the buffer", Finds.Buffer.Count == buffered - 1 && !Finds.Buffer.Contains(firstIndex));
            int bufferBeforeSave = Finds.Buffer.Count;
            float determinationSaved = gm.StatsDict[determination].CurrentBaseValue;
            // Later checks may have delivered more copies (a second skill level, another perk);
            // after reload and checkpoint everything must be exactly as it was when saved.
            float skillSaved = gm.StatsDict[skillStat].CurrentBaseValue;
            int perksSaved = ItemEffects.ActivePerks;
            int equipmentSaved = CountCards(gm, equipmentCard);
            GameLoad.Instance.AutoSaveGame(false);
            yield return Wait(2f);

            // 9. Reload the save: everything comes back the same.
            GameLoad.Instance.LoadGame(0);
            yield return WaitFor(() => !ApGame.InGame, 30f);
            yield return WaitForLoaded();
            yield return Wait(3f);
            gm = GM;
            Check("after reload: the game finished loading", ApGame.InGame);
            BlueprintRando.Reconcile(true);
            Check("after reload: researched-not-received stays hidden", gm.BlueprintModelStates[target] == BlueprintModelState.Hidden, Describe(target));
            Check("after reload: received recipe still buildable", gm.BlueprintModelStates[reward] == BlueprintModelState.Available &&
                  !gm.PurchasableBlueprintCards.Contains(reward), Describe(reward));
            int serverCount = ApConnection.Session.Items.AllItemsReceived.Count;
            Check("after reload: skill, perk and equipment exactly once",
                  Mathf.Approximately(gm.StatsDict[skillStat].CurrentBaseValue, skillSaved) && ItemEffects.ActivePerks == perksSaved &&
                  CountCards(gm, equipmentCard) == equipmentSaved,
                  new { skill = gm.StatsDict[skillStat].CurrentBaseValue, perks = ItemEffects.ActivePerks, equipment = CountCards(gm, equipmentCard) });
            Check("after reload: ledger matches the server, nothing doubled", ApGame.State.Received.Count == serverCount &&
                  ApGame.AppliedNow == serverCount, new { ledger = ApGame.State.Received.Count, server = serverCount, ApGame.AppliedNow });

            Check("after reload: the finds buffer is as saved", Finds.Buffer.Count == bufferBeforeSave,
                  new { now = Finds.Buffer.Count, saved = bufferBeforeSave });
            Check("after reload: new perk levels are kept", NewPerks.Level("pack_mule") == 3 && NewPerks.Level("pathfinder") == 1 &&
                  NewPerks.Level("careful_hands") == 2, new { packMule = NewPerks.Level("pack_mule") });
            Check("after reload: logbook Determination exactly once",
                  Mathf.Abs(gm.StatsDict[determination].CurrentBaseValue - determinationSaved) < 2f,
                  new { now = gm.StatsDict[determination].CurrentBaseValue, determinationSaved });

            // 10. Load the checkpoint (taken before the second research).
            int checkpointApplied = ApGame.State.AppliedCheckpoint;
            int findsSinceCheckpoint = ApGame.State.Received.Skip(checkpointApplied)
                .Count(id => Registry.Items.ContainsKey(id) && Registry.Items[id].Kind == "find");
            GameLoad.Instance.LoadCheckpoint();
            yield return WaitFor(() => !ApGame.InGame, 30f);
            yield return WaitForLoaded();
            yield return Wait(3f);
            gm = GM;
            Check("after checkpoint: the game finished loading", ApGame.InGame);
            BlueprintRando.Reconcile(true);
            Check("after checkpoint: checks are not rolled back", ApGame.State.IsChecked(rewardLocation) && ApGame.State.IsChecked(targetLocation));
            Check("after checkpoint: research already done is not offered again",
                  !gm.PurchasableBlueprintCards.Contains(reward) && gm.BlueprintModelStates[reward] == BlueprintModelState.Available,
                  Describe(reward));
            yield return Wait(2f);
            Check("after checkpoint: items received since then come back exactly once",
                  Mathf.Approximately(gm.StatsDict[skillStat].CurrentBaseValue, skillSaved) && ItemEffects.ActivePerks == perksSaved &&
                  CountCards(gm, equipmentCard) == equipmentSaved,
                  new { skill = gm.StatsDict[skillStat].CurrentBaseValue, perks = ItemEffects.ActivePerks, equipment = CountCards(gm, equipmentCard) });
            Check("after checkpoint: finds since then land again, the rest waits (no duplicates)",
                  Finds.Buffer.Count == Mathf.Max(0, findsSinceCheckpoint - Finds.PerHour) && Finds.Buffer.Distinct().Count() == Finds.Buffer.Count,
                  new { buffer = Finds.Buffer.Count, findsSinceCheckpoint });
            Check("after checkpoint: first-time checks stay done", ApGame.State.IsChecked(hunt.Id) && ApGame.State.IsChecked(three.Id));
            Check("after checkpoint: logbook rewards since then come back exactly once",
                  Mathf.Abs(gm.StatsDict[determination].CurrentBaseValue - determinationSaved) < 2f && ApGame.RewardsNow == ApGame.State.Rewards.Count,
                  new { now = gm.StatsDict[determination].CurrentBaseValue, determinationSaved, ApGame.RewardsNow });
            Check("after checkpoint: applied counter restored", ApGame.AppliedNow == ApGame.State.Received.Count,
                  new { ApGame.AppliedNow, received = ApGame.State.Received.Count });

            // 10b. Phase D: extras, traps, storage extension and hut upgrade kit.
            gm = GM;
            Registry.Entry partridges = Registry.Watched.First(entry => entry.Name == "Extra - Keep Partridges");
            Registry.Entry week = Registry.Watched.First(entry => entry.Name == "Extra - Survive 7 Days");
            GameManager.GiveCard(UniqueIDScriptable.GetFromID<CardData>(partridges.Watch[0].Cards.First()), true);
            yield return WaitFor(() => ApGame.State.IsChecked(partridges.Id), 5f);
            Check("extra: keeping partridges is a check; day 7 is not reached on day " + gm.CurrentDay,
                  ApGame.State.IsChecked(partridges.Id) && (gm.CurrentDay >= 7 || !ApGame.State.IsChecked(week.Id)));

            int trapsBefore = Traps.WentOff.Count;
            var trapLocations = ApConnection.Scouted.Where(pair => !ApGame.State.IsChecked(pair.Key) && pair.Value.Player.Slot == ApConnection.Slot &&
                    Registry.Items.ContainsKey(pair.Value.ItemId) && Registry.Items[pair.Value.ItemId].Kind == "trap").Take(2).ToList();
            var upgradeLocations = ApConnection.Scouted.Where(pair => !ApGame.State.IsChecked(pair.Key) && pair.Value.Player.Slot == ApConnection.Slot &&
                    Registry.Items.ContainsKey(pair.Value.ItemId) && Registry.Items[pair.Value.ItemId].Kind == "upgrade").ToList();
            foreach (var pair in trapLocations.Concat(upgradeLocations)) ApGame.CompleteLocation(pair.Key);
            yield return WaitFor(() => ApGame.AppliedNow == ApGame.State.Received.Count && HutUpgrades.KitsLeft >= 1 && HutUpgrades.ExtensionsLeft >= 1, 15f);
            yield return Wait(2f);
            Check("traps: at most one per day, the rest waits", trapLocations.Count == 2 && Traps.LastDay == gm.CurrentDay &&
                  Traps.WentOff.Count - trapsBefore <= 1 && Traps.WentOff.Count >= 1 && Traps.Queue.Count >= 1,
                  new { wentOff = Traps.WentOff.ToArray(), waiting = Traps.Queue.Count, Traps.LastDay, gm.CurrentDay });

            string[] shedTier = Registry.HutTiers[0], mudTier = Registry.HutTiers[1];
            GameManager.GiveCard(UniqueIDScriptable.GetFromID<CardData>(shedTier[1]), true);
            yield return Wait(2f);
            InGameCardBase entrance = gm.AllCards.LastOrDefault(card => card != null && !card.Destroyed && card.CardModel != null &&
                                                                        card.CardModel.UniqueID == shedTier[1]);
            CardAction enter = entrance?.CardModel.DismantleActions.FirstOrDefault(action => action.IsTravellingAction);
            if (enter != null) GameManager.PerformAction(enter, entrance, _FastMode: false);
            yield return WaitFor(() => gm.CurrentEnvironment != null && gm.CurrentEnvironment.UniqueID == shedTier[0] && ApGame.InGame, 60f);
            yield return Wait(2f);
            Check("entering a shed", gm.CurrentEnvironment.UniqueID == shedTier[0], gm.CurrentEnvironment.name);
            float spaceBefore = gm.MaxEnvWeight;
            HutUpgrades.Extend();
            Check("storage extension: +25% space here", Mathf.Abs(gm.MaxEnvWeight - spaceBefore * 1.25f) < 1f,
                  new { spaceBefore, now = gm.MaxEnvWeight });
            GameManager.GiveCard(stone, true);
            yield return Wait(2f);
            int stonesInside = gm.AllCards.Count(card => card != null && !card.Destroyed && card.CardModel == stone);
            int kitsBefore = HutUpgrades.KitsLeft;
            HutUpgrades.Upgrade();
            yield return WaitFor(() => !ApGame.InGame, 30f);
            yield return WaitForLoaded();
            yield return Wait(3f);
            gm = GM;
            CardData mudArea = UniqueIDScriptable.GetFromID<CardData>(mudTier[0]);
            Check("upgrade kit: the shed became a mud hut, contents kept",
                  ApGame.InGame && gm.CurrentEnvironment.UniqueID == mudTier[0] && stonesInside > 0 &&
                  gm.AllCards.Count(card => card != null && !card.Destroyed && card.CardModel == stone) == stonesInside &&
                  HutUpgrades.KitsLeft == kitsBefore - 1,
                  new { area = gm.CurrentEnvironment.name, stonesInside, kitsLeft = HutUpgrades.KitsLeft });
            Check("upgrade kit: the storage extension moved along",
                  Mathf.Abs(gm.MaxEnvWeight - mudArea.GetWeightCapacity(0f) * 1.25f) < 1f, gm.MaxEnvWeight);
            InGameCardBase exit = gm.AllCards.FirstOrDefault(card => card != null && !card.Destroyed && card.CardModel != null &&
                                                                     card.CardModel.DismantleActions != null &&
                                                                     card.CardModel.DismantleActions.Any(action => action.IsTravellingAction));
            if (exit != null) GameManager.PerformAction(exit.CardModel.DismantleActions.First(action => action.IsTravellingAction), exit, _FastMode: false);
            yield return WaitFor(() => gm.CurrentEnvironment != null && gm.CurrentEnvironment.UniqueID != mudTier[0] && ApGame.InGame, 60f);
            yield return Wait(2f);
            Check("outside, the entrance is a mud hut now",
                  gm.AllCards.Any(card => card != null && !card.Destroyed && card.CardModel != null && card.CardModel.UniqueID == mudTier[1]) &&
                  !gm.AllCards.Any(card => card != null && !card.Destroyed && card.CardModel != null && card.CardModel.UniqueID == shedTier[1]),
                  gm.CurrentEnvironment.name);

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
