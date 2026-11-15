using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Models;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Ties the running game to its Archipelago slot: which save belongs to which seed,
    /// receiving items into the ledger, applying them, and sending checks (queued while offline).
    /// </summary>
    internal static class ApGame
    {
        /// <summary>Archipelago data of the loaded save, or null for a normal (non-randomized) game.</summary>
        public static SlotState State { get; private set; }

        /// <summary>Received items whose one-time effect is already in the running game.</summary>
        public static int AppliedNow;

        private static bool syncedThisSession;

        public static bool Active
        {
            get { return State != null; }
        }

        /// <summary>The game is running and finished loading (safe to change cards).</summary>
        public static bool InGame
        {
            get
            {
                GameManager gm = MBSingleton<GameManager>.Instance;
                return gm != null && !gm.IsInitializing && GameLoad.Instance != null;
            }
        }

        // ---- Save binding ---------------------------------------------------------------

        /// <summary>A new game starts in save slot <paramref name="index"/>.</summary>
        public static void OnNewGame(int index)
        {
            SlotState.Delete(index);
            State = null;
            if (!ApConnection.IsConnected || index < 0)
            {
                return;   // a normal game
            }
            State = new SlotState
            {
                SaveIndex = index,
                SeedName = ApConnection.SeedName,
                SlotName = ApConnection.SlotName,
                Host = Plugin.ConfigHost.Value,
                Password = Plugin.ConfigPassword.Value,
                Team = ApConnection.Team,
                Slot = ApConnection.Slot,
                RegistryVersion = Registry.Version,
            };
            State.Write();
            AppliedNow = 0;
            syncedThisSession = false;
            Messages.Add("New Archipelago game for " + State.SlotName + " (seed " + State.SeedName + ").");
        }

        /// <summary>An existing save in slot <paramref name="index"/> is loaded.</summary>
        public static void OnLoadGame(int index)
        {
            State = SlotState.Load(index);
            AppliedNow = State != null ? State.AppliedMain : 0;
            syncedThisSession = false;
            if (State != null && ApConnection.IsConnected && !MatchesConnection())
            {
                ApConnection.Disconnect();
                Messages.Add("This save belongs to " + State.SlotName + " on seed " + State.SeedName +
                             ". Disconnected from the other seed.");
            }
        }

        public static void OnLoadCheckpoint()
        {
            if (State == null) return;
            State.AppliedMain = State.AppliedCheckpoint;   // mirrors MainData = CheckpointData.Copy()
            State.Write();
        }

        public static void OnSaved(bool checkpoint)
        {
            if (State == null) return;
            State.AppliedMain = AppliedNow;
            if (checkpoint) State.AppliedCheckpoint = AppliedNow;
            State.Write();
        }

        public static void OnDeleted(int index)
        {
            SlotState.Delete(index);
        }

        public static void OnBackToMenu()
        {
            State = null;
        }

        public static bool MatchesConnection()
        {
            return State != null && State.SeedName == ApConnection.SeedName && State.Slot == ApConnection.Slot &&
                   State.Team == ApConnection.Team;
        }

        // ---- Every frame ----------------------------------------------------------------

        public static void Update()
        {
            if (State == null) return;
            if (ApConnection.IsConnected)
            {
                if (!MatchesConnection())
                {
                    ApConnection.Disconnect();
                    Messages.Add("This save belongs to " + State.SlotName + " on seed " + State.SeedName + ".");
                    return;
                }
                if (!syncedThisSession)
                {
                    syncedThisSession = true;
                    // Everything found while offline, and the goal, is sent again. The server ignores repeats.
                    ApConnection.SendChecks(State.Checked);
                    if (State.GoalDone) ApConnection.SendGoal();
                }
                PullReceivedItems();
            }
            if (InGame)
            {
                GameManager gm = MBSingleton<GameManager>.Instance;
                ItemEffects.ApplyPermanent(gm, State.Received);
                ApplyNewItems();
                BlueprintRando.Reconcile(false);
                Checks.Update(gm);
            }
        }

        /// <summary>Copies newly received items from the server into the ledger.</summary>
        private static void PullReceivedItems()
        {
            IReadOnlyList<ItemInfo> all;
            try
            {
                all = ApConnection.Session.Items.AllItemsReceived;
            }
            catch (System.Exception)
            {
                return;
            }
            int count = all.Count;
            if (count <= State.Received.Count) return;
            for (int index = State.Received.Count; index < count; index++)
            {
                ItemInfo info = all[index];
                State.AddReceived(info.ItemId);
                string from = info.Player.Slot == ApConnection.Slot ? "yourself" : info.Player.Name;
                Messages.Add("Received " + info.ItemDisplayName + " from " + from + ".");
            }
            State.Write();
        }

        /// <summary>Gives the one-time part of every item that is not in the running game yet.</summary>
        private static void ApplyNewItems()
        {
            while (AppliedNow < State.Received.Count)
            {
                long id = State.Received[AppliedNow];
                AppliedNow++;
                ItemEffects.ApplyOnce(id);
            }
        }

        /// <summary>A check was completed in the game.</summary>
        public static void CompleteLocation(long location)
        {
            if (State == null || !State.AddChecked(location)) return;
            Registry.Entry entry;
            string name = Registry.Locations.TryGetValue(location, out entry) ? entry.Name : location.ToString();
            string holds = ApConnection.Describe(location);
            Messages.Add(holds != null ? name + ": found " + holds + "." : name + " completed.");
            ApConnection.SendChecks(new[] { location });
        }

        public static void CompleteGoal()
        {
            if (State == null) return;
            if (!State.GoalDone)
            {
                State.GoalDone = true;
                State.Write();
                Messages.Add("Goal complete! Load the checkpoint to keep collecting checks.");
            }
            ApConnection.SendGoal();
        }

        public static int CheckedCount
        {
            get { return State == null ? 0 : State.Checked.Count; }
        }

    }
}
