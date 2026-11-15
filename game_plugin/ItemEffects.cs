using System.Collections.Generic;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// What received items do. Two kinds of effects, chosen by where the game keeps the result:
    ///
    /// Once (ApplyOnce, counted by ApGame.AppliedNow and stored with the save): finds (Finds.cs), the
    /// finished copy of an equipment blueprint, skill levels. Their result is part of the save, so
    /// after loading an older checkpoint exactly the items received since then are applied again.
    ///
    /// Every session (ApplyPermanent, derived from the ledger): blueprint recipes (see
    /// BlueprintRando.Reconcile) and perks. The game re-applies perk bonuses on every load and does
    /// not save them, so the mod does the same. Nothing here can ever be doubled.
    /// </summary>
    internal static class ItemEffects
    {
        private static GameManager perksAppliedTo;
        private static readonly HashSet<long> perksApplied = new HashSet<long>();

        /// <param name="index">The item's position in the received-item ledger.</param>
        public static void ApplyOnce(int index, long itemId)
        {
            Registry.Entry entry;
            if (!Registry.Items.TryGetValue(itemId, out entry))
            {
                Plugin.Log.LogWarning("Unknown item id " + itemId);
                return;
            }
            if (entry.Kind == "find")
            {
                Finds.Receive(index, entry);
            }
            foreach (string equipment in entry.Equipment)
            {
                Give(equipment, 1);
            }
            if (entry.Kind == "skill")
            {
                GameStat stat = UniqueIDScriptable.GetFromID<GameStat>(entry.StatId);
                GameManager gm = MBSingleton<GameManager>.Instance;
                if (stat != null && gm != null)
                {
                    var modifier = new StatModifier { Stat = stat, ValueModifier = new Vector2(entry.Amount, entry.Amount) };
                    gm.ApplyStatModifier(modifier, StatModification.Permanent, "Archipelago", null);
                }
            }
        }

        /// <summary>Applies received perks once per loaded game (the game does not save perk bonuses).</summary>
        public static void ApplyPermanent(GameManager gm, IList<long> received)
        {
            if (perksAppliedTo != gm)
            {
                perksAppliedTo = gm;
                perksApplied.Clear();
            }
            foreach (long itemId in received)
            {
                Registry.Entry entry;
                if (perksApplied.Contains(itemId) || !Registry.Items.TryGetValue(itemId, out entry) || entry.Kind != "perk") continue;
                perksApplied.Add(itemId);
                CharacterPerk perk = UniqueIDScriptable.GetFromID<CharacterPerk>(entry.PerkId);
                if (perk == null || perk.PassiveStatModifiers == null) continue;
                foreach (StatModifier modifier in perk.PassiveStatModifiers)
                {
                    if (modifier.Stat == null) continue;
                    gm.ApplyStatModifier(modifier, StatModification.GlobalModifier, StatModifierReport.SourceFromPerk(perk), perk);
                }
            }
        }

        /// <summary>Perks already active in this game session (for tests and the status line).</summary>
        public static int ActivePerks
        {
            get { return perksApplied.Count; }
        }

        private static void Give(string cardId, int count)
        {
            CardData card = UniqueIDScriptable.GetFromID<CardData>(cardId);
            if (card == null) return;
            for (int i = 0; i < count; i++)
            {
                GameManager.GiveCard(card, true);
            }
        }
    }
}
