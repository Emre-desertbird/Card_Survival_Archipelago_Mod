using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// New perks the base game does not have (YAML option new_perks). Their level is the number of
    /// copies in the received-item ledger, so like the game's perks they are derived anew every
    /// session and can never be lost or doubled by a checkpoint. Small steps on purpose
    /// (docs/plan_erweiterung.md, 3):
    ///
    /// - Progressive Pack Mule: carried weight counts 10% less per level (3 levels).
    /// - Nimble Hands: building a blueprint takes 33% less time, never under one tick (15 min).
    /// - Pathfinder: travelling takes 33% less time, never under one tick.
    /// - Progressive Careful Hands: tools wear 15% slower per level (2 levels).
    /// - Progressive Inquisitive Mind: research is 15% faster per level (2 levels).
    /// - Pantry Keeper: food and drinks spoil 15% slower.
    ///
    /// Durations are whole ticks in the game, so a percentage is rounded and never goes below one
    /// tick: a 15-minute trip stays 15 minutes, a 30-minute trip becomes 15.
    /// </summary>
    internal static class NewPerks
    {
        public const float TimeFactor = 0.67f;

        /// <summary>Tooling only: lets the self-test read a value without the perks.</summary>
        public static bool Suspended;

        private static readonly Dictionary<string, int> levels = new Dictionary<string, int>();
        private static SlotState countedFor;
        private static int counted = -1;

        /// <summary>Copies of a new perk received (cached; the ledger only grows).</summary>
        public static int Level(string key)
        {
            if (Suspended || !ApGame.Active) return 0;
            SlotState state = ApGame.State;
            if (state != countedFor || state.Received.Count != counted)
            {
                countedFor = state;
                counted = state.Received.Count;
                levels.Clear();
                foreach (long id in state.Received)
                {
                    Registry.Entry entry;
                    if (!Registry.Items.TryGetValue(id, out entry) || entry.Kind != "newperk") continue;
                    int level;
                    levels.TryGetValue(entry.Key, out level);
                    levels[entry.Key] = level + 1;
                }
            }
            int found;
            return levels.TryGetValue(key, out found) ? found : 0;
        }

        public static float CarryFactor
        {
            get { return 1f - 0.1f * Level("pack_mule"); }
        }

        /// <summary>A shorter duration in whole ticks: rounded, never under one tick (zero stays zero).</summary>
        public static int Shorten(int ticks)
        {
            return ticks <= 0 ? ticks : Mathf.Max(1, Mathf.RoundToInt(ticks * TimeFactor));
        }

        private static int carryLevel = -1;

        /// <summary>Every frame in a randomized game: recalculates the carried weight when Pack Mule changes.</summary>
        public static void Update(GameManager gm)
        {
            int level = Level("pack_mule");
            if (level == carryLevel) return;
            carryLevel = level;
            gm.CalculateCarryWeight();
        }

        public static void Reset()
        {
            carryLevel = -1;
        }
    }

    /// <summary>Pack Mule: the game sums the weight of carried and equipped cards into a stat; the perk scales that sum.</summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.CalculateCarryWeight))]
    internal static class CarryWeightPatch
    {
        private static bool Prefix(GameManager __instance)
        {
            float factor = NewPerks.CarryFactor;
            if (factor >= 1f) return true;
            if (__instance.InGamePlayerWeight == null) return false;
            Traverse running = Traverse.Create(__instance).Field("WillCalculateCarryWeight");
            if (running.GetValue<bool>()) return false;
            __instance.StartCoroutine(Calculate(__instance, factor, running));
            return false;
        }

        /// <summary>The game's own CalculateCarryWeightRoutine, with the carried sum scaled.</summary>
        private static IEnumerator Calculate(GameManager gm, float factor, Traverse running)
        {
            running.SetValue(true);
            yield return new WaitForEndOfFrame();
            try
            {
                GraphicsManager graphics = MBSingleton<GraphicsManager>.Instance;
                List<InGameCardBase> cards = graphics.GetHandCards(false);
                cards.AddRange(graphics.GetEquippedCards(false));
                float current = gm.InGamePlayerWeight.CurrentValue(gm.NotInBase);
                float carried = cards.Sum(card => card.CurrentWeight) * factor;
                if (!Mathf.Approximately(current, carried))
                {
                    gm.ApplyStatModifier(new StatModifier { Stat = gm.PlayerWeightStat, ValueModifier = Vector2.one * (carried - current) },
                                         StatModification.Permanent, "Carried Weight", null);
                }
            }
            finally
            {
                running.SetValue(false);
            }
        }
    }

    /// <summary>Pathfinder, Nimble Hands and Careful Hands act on an action's time and durability changes.</summary>
    [HarmonyPatch(typeof(CardAction), nameof(CardAction.CollectActionModifiers))]
    internal static class ActionPerksPatch
    {
        private static readonly string BuildKey = LocalizedString.Build.LocalizationKey;

        private static void Postfix(CardAction __instance)
        {
            if (!ApGame.Active) return;
            if (__instance.IsTravellingAction && NewPerks.Level("pathfinder") > 0)
            {
                __instance.TotalDaytimeCost = NewPerks.Shorten(__instance.TotalDaytimeCost);
            }
            else if (__instance.ActionName.LocalizationKey == BuildKey && NewPerks.Level("nimble_hands") > 0)
            {
                __instance.TotalDaytimeCost = NewPerks.Shorten(__instance.TotalDaytimeCost);
            }
            int careful = NewPerks.Level("careful_hands");
            if (careful <= 0) return;
            float share = 0.15f * careful;
            // Wear is a negative usage change on the card the action is performed with or on.
            Soften(__instance.ReceivingCardChanges, __instance.ReceivingDurabilityChanges, share);
            var withTool = __instance as CardOnCardAction;
            if (withTool != null) Soften(withTool.GivenCardChanges, withTool.GivenDurabilityChanges, share);
        }

        private static void Soften(CardStateChange change, TransferedDurabilities extra, float share)
        {
            if (!change.ModifyDurability && change.ModType != CardModifications.DurabilityChanges) return;
            float wear = (change.UsageChange.x + change.UsageChange.y) / 2f;
            if (wear < 0f) extra.Usage.FloatValue += -wear * share;
        }
    }

    /// <summary>Inquisitive Mind: research days become fewer ticks (DaysToTicks is used for research only).</summary>
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.DaysToTicks))]
    internal static class ResearchPerkPatch
    {
        private static void Postfix(ref int __result)
        {
            int level = NewPerks.Level("inquisitive_mind");
            if (level > 0 && __result > 1) __result = Mathf.Max(1, Mathf.CeilToInt(__result * (1f - 0.15f * level)));
        }
    }

    /// <summary>Pantry Keeper: a falling spoilage value falls 15% slower.</summary>
    [HarmonyPatch(typeof(InGameCardBase), nameof(InGameCardBase.CurrentSpoilageRate), MethodType.Getter)]
    internal static class SpoilagePerkPatch
    {
        private static void Postfix(ref float __result)
        {
            if (__result < 0f && NewPerks.Level("pantry_keeper") > 0) __result *= 0.85f;
        }
    }
}
