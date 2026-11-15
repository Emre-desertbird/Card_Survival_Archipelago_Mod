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

        /// <summary>Logbook rewards whose Determination is already in the running game.</summary>
        public static int RewardsNow;

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
            ReadSeedOptions();
            State.Write();
            AppliedNow = 0;
            RewardsNow = 0;
            Finds.Reset();
            Traps.OnLoad(State);
            HutUpgrades.OnLoad(State);
            syncedThisSession = false;
            Messages.Add("New Archipelago game for " + State.SlotName + " (seed " + State.SeedName + ").");
        }

        /// <summary>An existing save in slot <paramref name="index"/> is loaded.</summary>
        public static void OnLoadGame(int index)
        {
            State = SlotState.Load(index);
            AppliedNow = State != null ? State.AppliedMain : 0;
            RewardsNow = State != null ? State.RewardsMain : 0;
            Finds.OnLoad(State != null ? State.BufferMain : null);
            Traps.OnLoad(State);
            HutUpgrades.OnLoad(State);
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
            State.BufferMain = new List<int>(State.BufferCheckpoint);
            State.RewardsMain = State.RewardsCheckpoint;
            State.TrapsMain = new List<int>(State.TrapsCheckpoint);
            State.TrapDayMain = State.TrapDayCheckpoint;
            State.KitsMain = State.KitsCheckpoint;
            State.StorageMain = new List<string>(State.StorageCheckpoint);
            State.Write();
        }

        public static void OnSaved(bool checkpoint)
        {
            if (State == null) return;
            State.AppliedMain = AppliedNow;
            State.BufferMain = new List<int>(Finds.Buffer);
            State.RewardsMain = RewardsNow;
            State.TrapsMain = new List<int>(Traps.Queue);
            State.TrapDayMain = Traps.LastDay;
            State.KitsMain = HutUpgrades.KitsUsed;
            State.StorageMain = new List<string>(HutUpgrades.Extended);
            if (checkpoint)
            {
                State.RewardsCheckpoint = RewardsNow;
                State.TrapsCheckpoint = new List<int>(Traps.Queue);
                State.TrapDayCheckpoint = Traps.LastDay;
                State.KitsCheckpoint = HutUpgrades.KitsUsed;
                State.StorageCheckpoint = new List<string>(HutUpgrades.Extended);
                State.AppliedCheckpoint = AppliedNow;
                State.BufferCheckpoint = new List<int>(Finds.Buffer);
            }
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

        /// <summary>Copies the seed's options from the slot data into the save's Archipelago file.</summary>
        private static void ReadSeedOptions()
        {
            Dictionary<string, object> data = ApConnection.SlotData;
            if (data == null) return;
            object value;
            if (data.TryGetValue("location_kinds", out value) && value is Newtonsoft.Json.Linq.JArray kinds)
            {
                State.LocationKinds = kinds.Select(kind => (string)kind).ToList();
            }
            if (data.TryGetValue("finds_amount", out value)) State.FindsAmount = System.Convert.ToInt32(value);
            if (data.TryGetValue("character_lock", out value)) State.CharacterLock = System.Convert.ToBoolean(value);
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
                    ReadSeedOptions();
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
                ApplyRewards(gm);
                BlueprintRando.Reconcile(false);
                Checks.Update(gm);
                Checks.ScanCards(gm);
                NewPerks.Update(gm);
                Traps.Update(gm);
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
                while (State.ReceivedFrom.Count < State.Received.Count - 1) State.ReceivedFrom.Add("?");
                State.ReceivedFrom.Add(info.Player.Slot == ApConnection.Slot ? "you" : info.Player.Name);
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
                int index = AppliedNow;
                AppliedNow++;
                ItemEffects.ApplyOnce(index, State.Received[index]);
            }
        }

        /// <summary>Gives the Determination of logbook tasks that is not in the running game yet.</summary>
        private static void ApplyRewards(GameManager gm)
        {
            if (RewardsNow >= State.Rewards.Count) return;
            GameStat determination = UniqueIDScriptable.GetFromID<GameStat>(Registry.DeterminationStatId);
            while (RewardsNow < State.Rewards.Count)
            {
                Registry.Entry task;
                if (determination != null && Registry.Locations.TryGetValue(State.Rewards[RewardsNow], out task) && task.Determination > 0f)
                {
                    var modifier = new StatModifier { Stat = determination, ValueModifier = new Vector2(task.Determination, task.Determination) };
                    gm.ApplyStatModifier(modifier, StatModification.Permanent, "Archipelago logbook", null);
                }
                RewardsNow++;
            }
        }

        /// <summary>A check was completed in the game.</summary>
        public static void CompleteLocation(long location)
        {
            if (State == null || !State.AddChecked(location)) return;
            Registry.Entry entry;
            bool known = Registry.Locations.TryGetValue(location, out entry);
            string name = known ? entry.Name : location.ToString();
            string holds = ApConnection.Describe(location);
            if (known && entry.Kind == "logbook")
            {
                State.Rewards.Add(location);
                State.Write();
                Messages.Add("Logbook: " + name.Replace("Logbook - ", "") + " done (+" + entry.Determination + " Determination).");
            }
            Messages.Add(holds != null ? name + ": found " + holds + "." : name + " completed.");
            ApConnection.SendChecks(new[] { location });
            Checks.EvaluateWatched();   // logbook tasks that count checks
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
