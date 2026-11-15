using System.Collections.Generic;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Mild traps (YAML trap_percentage, trap_limit): the worst is a light wound. At most one trap
    /// goes off per in-game day and none at sea or during a fight or an event; the others wait
    /// in a queue. Like the finds buffer, the queue and the last trap day are game progress and
    /// follow the main save and the checkpoint copy.
    /// </summary>
    internal static class Traps
    {
        /// <summary>Received-item indices of traps that have not gone off yet.</summary>
        public static readonly List<int> Queue = new List<int>();
        public static int LastDay;
        /// <summary>Traps that went off in this session (for the self-test).</summary>
        public static readonly List<string> WentOff = new List<string>();

        public static void Receive(int index)
        {
            if (!Queue.Contains(index)) Queue.Add(index);
        }

        public static void OnLoad(SlotState state)
        {
            Queue.Clear();
            if (state != null) Queue.AddRange(state.TrapsMain);
            LastDay = state != null ? state.TrapDayMain : 0;
        }

        public static void Update(GameManager gm)
        {
            if (Queue.Count == 0 || gm.CurrentDay <= LastDay || gm.CurrentEnvironment == null) return;
            if (Registry.TrapFreeAreas.Contains(gm.CurrentEnvironment.UniqueID)) return;
            GraphicsManager graphics = MBSingleton<GraphicsManager>.Instance;
            if (gm.CurrentEventCard != null || (graphics != null && graphics.EncounterPopupWindow != null &&
                                                graphics.EncounterPopupWindow.OngoingEncounter)) return;
            int index = Queue[0];
            Queue.RemoveAt(0);
            LastDay = gm.CurrentDay;
            Registry.Entry trap;
            if (index >= ApGame.State.Received.Count || !Registry.Items.TryGetValue(ApGame.State.Received[index], out trap)) return;
            if (trap.StatId != null)
            {
                GameStat stat = UniqueIDScriptable.GetFromID<GameStat>(trap.StatId);
                if (stat != null)
                {
                    gm.ApplyStatModifier(new StatModifier { Stat = stat, ValueModifier = new Vector2(trap.Amount, trap.Amount) },
                                         StatModification.Permanent, "Archipelago trap", null);
                }
            }
            else
            {
                CardData card = UniqueIDScriptable.GetFromID<CardData>(trap.CardId);
                if (card != null) GameManager.GiveCard(card, true);   // a wound, or a weather replacing the current one
            }
            WentOff.Add(trap.Name);
            Messages.Add(trap.Name + " (from " + ApGame.State.SenderOf(index) + ")." +
                         (Queue.Count > 0 ? " " + Queue.Count + " more wait for the next days." : ""));
        }
    }
}
