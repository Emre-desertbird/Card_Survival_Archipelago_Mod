using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Splits "research finished" from "recipe unlocked" for every shuffled blueprint.
    ///
    /// The game has one state per blueprint (BlueprintModelStates: Available = buildable,
    /// Purchasable = researchable, Hidden, Locked) plus a saved list of researchable
    /// blueprints (PurchasableBlueprintCards). This mod gives them separate meanings:
    ///
    ///   research done   = the location is in the slot's check list (never rolled back)
    ///   recipe unlocked = the blueprint item was received (ledger)
    ///   discovered      = in BlueprintModelCards and, until researched, in PurchasableBlueprintCards
    ///
    /// <see cref="Reconcile"/> derives the game state from these three facts, so loading,
    /// checkpoints and reconnects always end in the same, correct state.
    /// </summary>
    internal static class BlueprintRando
    {
        /// <summary>The research tab of the blueprint screen is showing.</summary>
        public static bool ResearchViewOpen;

        /// <summary>Blueprints currently being added by this mod (not a vanilla discovery).</summary>
        private static readonly HashSet<CardData> granting = new HashSet<CardData>();
        private static Dictionary<CardData, Registry.Entry> shuffled;
        private static float nextReconcile;

        private static readonly MethodInfo AddCardSimple = AccessTools.Method(typeof(GameManager), "AddCard", new[]
        {
            typeof(CardData), typeof(InGameCardBase), typeof(bool), typeof(TransferedDurabilities), typeof(bool),
            typeof(SpawningLiquid), typeof(Vector2Int), typeof(bool),
        });
        private static readonly MethodInfo UpdateResearchIcon = AccessTools.Method(typeof(BlueprintModelsScreen), "UpdateResearchIcon");

        /// <summary>Every shuffled blueprint card with its research location.</summary>
        public static Dictionary<CardData, Registry.Entry> Shuffled
        {
            get
            {
                if (shuffled == null)
                {
                    shuffled = new Dictionary<CardData, Registry.Entry>();
                    foreach (var pair in Registry.ResearchByCard)
                    {
                        CardData card = UniqueIDScriptable.GetFromID<CardData>(pair.Key);
                        if (card != null) shuffled[card] = pair.Value;
                        else Plugin.Log.LogWarning("Blueprint " + pair.Value.Key + " is not in this game build.");
                    }
                }
                return shuffled;
            }
        }

        public static bool IsShuffled(CardData card)
        {
            return ApGame.Active && card != null && card.CardType == CardTypes.Blueprint && Shuffled.ContainsKey(card);
        }

        public static bool IsResearched(CardData card)
        {
            return ApGame.State.IsChecked(Shuffled[card].Id);
        }

        public static bool IsReceived(CardData card)
        {
            Registry.Entry item;
            return Registry.BlueprintItemByCard.TryGetValue(card.UniqueID, out item) && ApGame.State.HasReceived(item.Id);
        }

        /// <summary>The research of a shuffled blueprint was completed in some vanilla way.</summary>
        public static void MarkResearched(CardData card)
        {
            ApGame.CompleteLocation(Shuffled[card].Id);
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm != null)
            {
                gm.PurchasableBlueprintCards.Remove(card);
            }
            Reconcile(true);
        }

        /// <summary>Brings every shuffled blueprint into the state its three facts call for.</summary>
        public static void Reconcile(bool now)
        {
            if (!ApGame.Active || !ApGame.InGame) return;
            if (!now && Time.unscaledTime < nextReconcile) return;
            nextReconcile = Time.unscaledTime + 0.25f;
            GameManager gm = MBSingleton<GameManager>.Instance;
            GraphicsManager graphics = MBSingleton<GraphicsManager>.Instance;
            BlueprintModelsScreen screen = graphics != null ? graphics.BlueprintModelsPopup : null;
            foreach (var pair in Shuffled)
            {
                CardData card = pair.Key;
                bool researched = ApGame.State.IsChecked(pair.Value.Id);
                bool received = IsReceived(card);
                if (!gm.BlueprintModelCards.Contains(card))
                {
                    // Not discovered. A received recipe is added; it does not make the research available.
                    if (received && !granting.Contains(card)) gm.StartCoroutine(Grant(gm, card));
                    continue;
                }
                bool listed = gm.PurchasableBlueprintCards.Contains(card);
                if (researched && listed)
                {
                    gm.PurchasableBlueprintCards.Remove(card);
                    listed = false;
                }
                if (!researched && !listed && !received)
                {
                    // Discovered and not researched yet (e.g. after loading, the game marks it Available).
                    gm.PurchasableBlueprintCards.Add(card);
                    listed = true;
                }
                if (researched && screen != null && screen.CurrentResearch == card)
                {
                    StopResearch(screen);
                }
                BlueprintModelState wanted = received
                    ? (listed && ResearchViewOpen ? BlueprintModelState.Purchasable : BlueprintModelState.Available)
                    : (listed ? BlueprintModelState.Purchasable : BlueprintModelState.Hidden);
                if (gm.BlueprintModelStates[card] != wanted)
                {
                    gm.BlueprintModelStates[card] = wanted;
                }
            }
        }

        /// <summary>Clears the running research like the game does when one finishes.</summary>
        public static void StopResearch(BlueprintModelsScreen screen)
        {
            var traverse = Traverse.Create(screen);
            traverse.Property("CurrentResearch").SetValue(null);
            // The screen only fills its GameManager reference once it was opened.
            if (traverse.Field("GM").GetValue<GameManager>() == null)
            {
                traverse.Field("GM").SetValue(MBSingleton<GameManager>.Instance);
            }
            UpdateResearchIcon.Invoke(screen, null);
        }

        /// <summary>Adds a received, not yet discovered blueprint so it can be built.</summary>
        private static IEnumerator Grant(GameManager gm, CardData card)
        {
            granting.Add(card);
            try
            {
                var routine = (IEnumerator)AddCardSimple.Invoke(gm, new object[]
                {
                    card, null, true, null, true, SpawningLiquid.Empty, new Vector2Int(gm.CurrentTickInfo.z, 0), false,
                });
                yield return gm.StartCoroutine(routine);
            }
            finally
            {
                granting.Remove(card);
            }
            // Receiving the recipe is not a discovery: the research appears only through its vanilla conditions.
            gm.PurchasableBlueprintCards.Remove(card);
            Reconcile(true);
        }

        public static bool IsGranting(CardData card)
        {
            return granting.Contains(card);
        }

        public static void Reset()
        {
            shuffled = null;
            granting.Clear();
            ResearchViewOpen = false;
        }
    }

    // ---- Harmony patches ----------------------------------------------------------------

    /// <summary>Finishing a research sends its check instead of teaching the blueprint.</summary>
    [HarmonyPatch(typeof(BlueprintModelsScreen), nameof(BlueprintModelsScreen.FinishBlueprintResearch))]
    internal static class FinishResearchPatch
    {
        private static bool Prefix(BlueprintModelsScreen __instance)
        {
            CardData card = __instance.CurrentResearch;
            if (!BlueprintRando.IsShuffled(card)) return true;
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm != null && gm.FinishedBlueprintResearch == card && !BlueprintRando.IsReceived(card))
            {
                gm.FinishedBlueprintResearch = null;   // no "blueprint learned" popup
            }
            BlueprintRando.StopResearch(__instance);
            BlueprintRando.MarkResearched(card);
            return false;
        }
    }

    /// <summary>
    /// Actions that fully unlock a blueprint (BlueprintsFullUnlock) are an alternative vanilla way
    /// to learn it. For a shuffled blueprint they complete its research check instead.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "MakeBlueprintAvailableRoutine")]
    internal static class FullUnlockPatch
    {
        private static bool Prefix(CardData _Blueprint, ref IEnumerator __result)
        {
            if (!BlueprintRando.IsShuffled(_Blueprint) || BlueprintRando.IsGranting(_Blueprint)) return true;
            BlueprintRando.MarkResearched(_Blueprint);
            __result = Enumerable.Empty<object>().GetEnumerator();
            return false;
        }
    }

    /// <summary>
    /// Watches blueprint discoveries. A blueprint with no research time is learned the moment it is
    /// found, so finding it completes the check. A received blueprint that is found later becomes
    /// researchable now (the game itself would skip it because the card already exists).
    /// </summary>
    [HarmonyPatch]
    internal static class DiscoveryPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.GetDeclaredMethods(typeof(GameManager))
                .First(method => method.Name == "AddCard" && method.GetParameters().Length == 21);
        }

        private static void Prefix(GameManager __instance, CardData _Data, SlotInfo _Slot,
                                   TransferedDurabilities _TransferedDurabilites)
        {
            if (!BlueprintRando.IsShuffled(_Data) || __instance.IsInitializing || _TransferedDurabilites != null) return;
            if (_Slot != null && _Slot.SlotType != SlotsTypes.Blueprint) return;   // a blueprint being built
            if (BlueprintRando.IsGranting(_Data) || !__instance.BlueprintPurchasing) return;
            if (BlueprintRando.IsResearched(_Data)) return;
            if (_Data.BlueprintUnlockSunsCost <= 0f)
            {
                BlueprintRando.MarkResearched(_Data);
            }
            else if (__instance.BlueprintModelCards.Contains(_Data) && !__instance.PurchasableBlueprintCards.Contains(_Data))
            {
                __instance.PurchasableBlueprintCards.Add(_Data);
                Messages.Add("New research available: " + (string)_Data.CardName + ".");
                BlueprintRando.Reconcile(true);
            }
        }
    }

    /// <summary>
    /// The game treats a blueprint as discovered once its card exists. A received recipe creates
    /// the card early, so for the unlock conditions it only counts as discovered once it is in
    /// the research list or researched. This keeps every research check reachable.
    /// </summary>
    [HarmonyPatch(typeof(CardUnlockConditions), nameof(CardUnlockConditions.IsUnlocked))]
    internal static class UnlockConditionsPatch
    {
        private static void Postfix(CardUnlockConditions __instance, ref bool __result)
        {
            CardData card = __instance.UnlockedCard;
            if (!__result || !BlueprintRando.IsShuffled(card)) return;
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm == null || gm.CardsAboutToBeUnlocked.Contains(card)) return;
            if (BlueprintRando.IsReceived(card) && !BlueprintRando.IsResearched(card) &&
                !gm.PurchasableBlueprintCards.Contains(card))
            {
                __result = false;
            }
        }
    }

    /// <summary>The research tab shows researchable blueprints even when their recipe is already received.</summary>
    [HarmonyPatch(typeof(BlueprintModelsScreen), "ShowSubTab")]
    internal static class ResearchTabPatch
    {
        private static void Prefix(int _Index)
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            BlueprintRando.ResearchViewOpen = _Index == -1 && gm != null && gm.BlueprintPurchasing;
            BlueprintRando.Reconcile(true);
        }
    }

    [HarmonyPatch(typeof(BlueprintModelsScreen), nameof(BlueprintModelsScreen.Hide))]
    internal static class ResearchScreenHidePatch
    {
        private static void Prefix()
        {
            BlueprintRando.ResearchViewOpen = false;
            BlueprintRando.Reconcile(true);
        }
    }

    /// <summary>No "blueprint learned" popup for a shuffled blueprint whose recipe was not received.</summary>
    [HarmonyPatch(typeof(BlueprintResearchedPopup), nameof(BlueprintResearchedPopup.Show))]
    internal static class ResearchedPopupPatch
    {
        private static bool Prefix(CardData _Card)
        {
            return !BlueprintRando.IsShuffled(_Card) || BlueprintRando.IsReceived(_Card);
        }
    }
}
