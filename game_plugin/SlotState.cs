using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Archipelago data for one game save slot, kept in its own file next to the saves
    /// ("Archipelago/slot_N.json"). It binds the save to one seed and slot.
    ///
    /// Two kinds of data live here:
    /// - Permanent, never rolled back: the received-item ledger, the checks found, the goal.
    /// - The number of received items whose one-time effect (a finished tool, supplies) is
    ///   already inside the game save. The game keeps a main save and a checkpoint copy, so
    ///   this number is kept twice and moves exactly like the game's own data: SaveGame
    ///   writes "main" (and "checkpoint" when it also writes a checkpoint), LoadCheckpoint
    ///   copies "checkpoint" back over "main". See docs/architecture.md.
    /// </summary>
    internal sealed class SlotState
    {
        public int Format = 1;
        public string SeedName;
        public string SlotName;
        public string Host;
        public string Password;
        public int Team = -1;
        public int Slot = -1;
        public int RegistryVersion;

        /// <summary>Every item received, in server order (index = server item index).</summary>
        public List<long> Received = new List<long>();
        /// <summary>Received items whose one-time effect is in the main save on disk.</summary>
        public int AppliedMain;
        /// <summary>The same for the checkpoint copy.</summary>
        public int AppliedCheckpoint;
        /// <summary>Locations completed in this save (sent or still waiting to be sent).</summary>
        public List<long> Checked = new List<long>();
        public bool GoalDone;

        [JsonIgnore] public int SaveIndex = -1;
        [JsonIgnore] private HashSet<long> checkedSet;
        [JsonIgnore] private HashSet<long> receivedSet;

        public bool IsChecked(long location)
        {
            if (checkedSet == null) checkedSet = new HashSet<long>(Checked);
            return checkedSet.Contains(location);
        }

        public bool HasReceived(long item)
        {
            if (receivedSet == null) receivedSet = new HashSet<long>(Received);
            return receivedSet.Contains(item);
        }

        /// <summary>Records a check. Returns false if it was already recorded.</summary>
        public bool AddChecked(long location)
        {
            if (IsChecked(location)) return false;
            Checked.Add(location);
            checkedSet.Add(location);
            Write();
            return true;
        }

        public void AddReceived(long item)
        {
            if (receivedSet == null) receivedSet = new HashSet<long>(Received);
            Received.Add(item);
            receivedSet.Add(item);
        }

        public static string Folder
        {
            get { return Path.Combine(Application.persistentDataPath, "Archipelago"); }
        }

        public static string PathFor(int saveIndex)
        {
            return Path.Combine(Folder, "slot_" + saveIndex + ".json");
        }

        public static SlotState Load(int saveIndex)
        {
            string path = PathFor(saveIndex);
            if (!File.Exists(path)) return null;
            try
            {
                SlotState state = JsonConvert.DeserializeObject<SlotState>(File.ReadAllText(path));
                state.SaveIndex = saveIndex;
                return state;
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("Could not read " + path + ": " + error.Message);
                return null;
            }
        }

        public static void Delete(int saveIndex)
        {
            string path = PathFor(saveIndex);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>Writes the file atomically (temp file, then replace).</summary>
        public void Write()
        {
            if (SaveIndex < 0 || !SaveIsolation.IsIsolated) return;
            Directory.CreateDirectory(Folder);
            string path = PathFor(SaveIndex);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonConvert.SerializeObject(this, Formatting.Indented));
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }
    }
}
