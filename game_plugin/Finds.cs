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
    /// Finds (the filler items): "a mysterious being left something behind". A find lands on the
    /// ground (the base row) of the area the player is in right now.
    ///
    /// At most <see cref="PerHour"/> finds land per in-game hour. Everything beyond waits in the
    /// buffer, which the F8 panel lists with contents and sender; the player releases it there
    /// (again at most <see cref="PerHour"/> at a time). This protects against floods, e.g. when
    /// another player releases all items at once.
    ///
    /// A find is a one-time effect (ApGame.AppliedNow counts it when it is received). Whether it
    /// sits in the buffer or on the ground is part of the game's progress, so the buffer follows
    /// the main save and the checkpoint copy exactly like the applied counter does.
    /// </summary>
    internal static class Finds
    {
        public const int PerHour = 10;

        /// <summary>Received-item indices waiting in the buffer, in the running game.</summary>
        public static readonly List<int> Buffer = new List<int>();

        private static int hour = -1;
        private static int landedThisHour;

        private static readonly MethodInfo AddCardSimple = AccessTools.Method(typeof(GameManager), "AddCard", new[]
        {
            typeof(CardData), typeof(InGameCardBase), typeof(bool), typeof(TransferedDurabilities), typeof(bool),
            typeof(SpawningLiquid), typeof(Vector2Int), typeof(bool),
        });
        private static readonly MethodInfo FindLatest = AccessTools.Method(typeof(GameManager), "FindLatestCreatedCard");

        /// <summary>A find was received (index into the ledger). It lands now or goes to the buffer.</summary>
        public static void Receive(int index, Registry.Entry entry)
        {
            if (TakeBudget())
            {
                Land(entry);
            }
            else if (!Buffer.Contains(index))
            {
                Buffer.Add(index);
                Messages.Add(entry.Name + " went to the buffer (F8): at most " + PerHour + " finds per hour land on the ground.");
            }
        }

        /// <summary>Releases up to <see cref="PerHour"/> buffered finds (or just one) to the current area.</summary>
        public static int Release(int onlyIndex = -1)
        {
            int released = 0;
            foreach (int index in Buffer.ToList())
            {
                if (onlyIndex >= 0 && index != onlyIndex) continue;
                if (released >= PerHour) break;
                Registry.Entry entry;
                if (index < ApGame.State.Received.Count && Registry.Items.TryGetValue(ApGame.State.Received[index], out entry))
                {
                    Land(entry);
                }
                Buffer.Remove(index);
                released++;
            }
            return released;
        }

        /// <summary>One more find may land in this in-game hour.</summary>
        private static bool TakeBudget()
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            int now = gm != null ? gm.CurrentTickInfo.z / 4 : 0;   // 4 ticks of 15 minutes
            if (now != hour)
            {
                hour = now;
                landedThisHour = 0;
            }
            if (landedThisHour >= PerHour) return false;
            landedThisHour++;
            return true;
        }

        /// <summary>Puts a find on the ground of the current area.</summary>
        private static void Land(Registry.Entry entry)
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm == null) return;
            foreach (Registry.FindPart part in entry.Contents)
            {
                CardData card = UniqueIDScriptable.GetFromID<CardData>(part.CardId);
                if (card == null) continue;
                if (part.LiquidId != null)
                {
                    LandWithLiquid(gm, card, UniqueIDScriptable.GetFromID<CardData>(part.LiquidId));
                    continue;
                }
                for (int i = 0; i < Scaled(part.Count); i++)
                {
                    GameManager.GiveCard(card, true);   // items land on the base row (the ground)
                }
            }
            Messages.Add("A mysterious being left something behind: " + Describe(entry) + ".");
        }

        /// <summary>A container filled with a liquid, the way the game's own GiveCard does it.</summary>
        private static void LandWithLiquid(GameManager gm, CardData container, CardData liquid)
        {
            if (liquid == null || !container.CanContainThisLiquid(liquid))
            {
                GameManager.GiveCard(container, true);
                return;
            }
            var tick = new Vector2Int(gm.CurrentTickInfo.z, 0);
            gm.StartCoroutine((IEnumerator)AddCardSimple.Invoke(gm, new object[]
                { container, null, true, null, true, new SpawningLiquid(liquid), tick, false }));
            var filled = (InGameCardBase)FindLatest.Invoke(gm, new object[] { container });
            var durabilities = new TransferedDurabilities();
            durabilities.Spoilage.FloatValue = liquid.SpoilageTime;
            durabilities.Usage.FloatValue = liquid.UsageDurability;
            durabilities.Fuel.FloatValue = liquid.FuelCapacity;
            durabilities.ConsumableCharges.FloatValue = liquid.Progress;
            durabilities.Liquid = container.MaxLiquidCapacity;
            durabilities.Special1.FloatValue = liquid.SpecialDurability1;
            durabilities.Special2.FloatValue = liquid.SpecialDurability2;
            durabilities.Special3.FloatValue = liquid.SpecialDurability3;
            durabilities.Special4.FloatValue = liquid.SpecialDurability4;
            gm.StartCoroutine((IEnumerator)AddCardSimple.Invoke(gm, new object[]
                { liquid, filled, true, durabilities, true, SpawningLiquid.DefaultLiquid, tick, false }));
        }

        /// <summary>The YAML option "finds_amount": more = 50% larger amounts (rounded up).</summary>
        private static int Scaled(int count)
        {
            return ApGame.State != null && ApGame.State.FindsAmount >= 2 ? (int)Math.Ceiling(count * 1.5) : count;
        }

        /// <summary>"3 Sticks", "Ginger Tea in a Coconut Shell".</summary>
        public static string Describe(Registry.Entry entry)
        {
            var parts = new List<string>();
            foreach (Registry.FindPart part in entry.Contents)
            {
                CardData card = UniqueIDScriptable.GetFromID<CardData>(part.CardId);
                string name = card != null ? (string)card.CardName : part.Name;
                if (part.LiquidId != null)
                {
                    CardData liquid = UniqueIDScriptable.GetFromID<CardData>(part.LiquidId);
                    parts.Add((liquid != null ? (string)liquid.CardName : part.LiquidName) + " in a " + name);
                }
                else
                {
                    parts.Add(Scaled(part.Count) + " " + name);
                }
            }
            return string.Join(", ", parts.ToArray());
        }

        // ---- Follows the save like the applied counter -----------------------------------

        public static void OnLoad(List<int> fromSave)
        {
            Buffer.Clear();
            if (fromSave != null) Buffer.AddRange(fromSave);
            hour = -1;
        }

        public static void Reset()
        {
            Buffer.Clear();
            hour = -1;
            landedThisHour = 0;
        }
    }
}
