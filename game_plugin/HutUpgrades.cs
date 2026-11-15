using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Hut upgrades (YAML hut_upgrades), used from the F8 panel while standing in a hut.
    ///
    /// Storage Extension: +25% space for the hut or cellar you stand in. The game reads an area's
    /// space from its area card every time (InGameCardBase.MaxWeightCapacity), so the bonus is
    /// added there for every extended area (kept by its environment key).
    ///
    /// Hut Upgrade Kit: turns the Shed you stand in into a Mud Hut, or a Mud Hut into a Stone Hut,
    /// keeping everything inside. The game has no such change, so the mod uses the save: it saves,
    /// rewrites the hut in the main save (area, entrance, explorable and exit card become the next
    /// tier; the save is the game's own JSON), and loads it again. The game then builds everything
    /// from the save as on any load.
    ///
    /// Used kits and extended areas are game progress: they follow the main save and the
    /// checkpoint copy, so loading an older checkpoint gives a kit back together with the old hut.
    /// </summary>
    internal static class HutUpgrades
    {
        public const float StorageStep = 0.25f;

        public static int KitsUsed;
        public static readonly List<string> Extended = new List<string>();

        public static void OnLoad(SlotState state)
        {
            KitsUsed = state != null ? state.KitsMain : 0;
            Extended.Clear();
            if (state != null) Extended.AddRange(state.StorageMain);
        }

        private static int Received(string key)
        {
            return ApGame.State.Received.Count(id => Registry.Items.ContainsKey(id) && Registry.Items[id].Key == key);
        }

        public static int KitsLeft
        {
            get { return ApGame.Active ? Received("upgrade_kit") - KitsUsed : 0; }
        }

        public static int ExtensionsLeft
        {
            get { return ApGame.Active ? Received("storage_extension") - Extended.Count : 0; }
        }

        /// <summary>Index of the hut tier the player stands in, or -1.</summary>
        public static int CurrentTier
        {
            get
            {
                GameManager gm = MBSingleton<GameManager>.Instance;
                if (gm == null || gm.CurrentEnvironment == null) return -1;
                return Registry.HutTiers.FindIndex(tier => tier[0] == gm.CurrentEnvironment.UniqueID);
            }
        }

        public static bool CanUpgrade
        {
            get { return ApGame.InGame && KitsLeft > 0 && CurrentTier >= 0 && CurrentTier < Registry.HutTiers.Count - 1; }
        }

        public static bool CanExtend
        {
            get
            {
                GameManager gm = MBSingleton<GameManager>.Instance;
                return ApGame.InGame && ExtensionsLeft > 0 && gm.CurrentEnvironment != null &&
                       Registry.StorageAreas.Contains(gm.CurrentEnvironment.UniqueID);
            }
        }

        public static string CurrentKey(GameManager gm)
        {
            return gm.CurrentEnvironment.EnvironmentDictionaryKey(gm.PrevEnvironment, gm.CurrentTravelIndex);
        }

        public static void Extend()
        {
            if (!CanExtend) return;
            GameManager gm = MBSingleton<GameManager>.Instance;
            Extended.Add(CurrentKey(gm));
            Messages.Add("Storage extended: this area now holds " + Mathf.RoundToInt(gm.MaxEnvWeight) + ".");
        }

        public static void Upgrade()
        {
            if (!CanUpgrade) return;
            GameManager gm = MBSingleton<GameManager>.Instance;
            string[] from = Registry.HutTiers[CurrentTier];
            string[] to = Registry.HutTiers[CurrentTier + 1];
            int index = GameLoad.Instance.CurrentGameDataIndex;
            KitsUsed++;
            GameLoad.Instance.AutoSaveGame(false);
            GameSaveData data = GameLoad.Instance.Games[index].MainData;
            int sameTier = data.EnvironmentsData.Count(env => env.EnvironmentID != null && env.EnvironmentID.Contains(from[0]));
            int nextTier = data.EnvironmentsData.Count(env => env.EnvironmentID != null && env.EnvironmentID.Contains(to[0]));
            if (sameTier != 1 || nextTier != 0)
            {
                KitsUsed--;
                GameLoad.Instance.AutoSaveGame(false);
                Messages.Add("The kit only works when you have exactly one hut of this kind and none of the next.");
                return;
            }
            string json = JsonUtility.ToJson(data);
            for (int i = 0; i < from.Length; i++)
            {
                UniqueIDScriptable before = UniqueIDScriptable.GetFromID(from[i]);
                UniqueIDScriptable after = UniqueIDScriptable.GetFromID(to[i]);
                if (before == null || after == null) continue;
                json = json.Replace(UniqueIDScriptable.SaveID(before), UniqueIDScriptable.SaveID(after)).Replace(from[i], to[i]);
            }
            GameSaveData upgraded = JsonUtility.FromJson<GameSaveData>(json);
            CardData oldArea = UniqueIDScriptable.GetFromID<CardData>(from[0]);
            CardData newArea = UniqueIDScriptable.GetFromID<CardData>(to[0]);
            foreach (EnvironmentSaveData env in upgraded.EnvironmentsData.Where(env => env.EnvironmentID != null && env.EnvironmentID.Contains(to[0])))
            {
                env.CurrentMaxWeight += newArea.GetWeightCapacity(0f) - oldArea.GetWeightCapacity(0f);
            }
            for (int i = 0; i < Extended.Count; i++) Extended[i] = Extended[i].Replace(from[0], to[0]);
            ApGame.State.StorageMain = new List<string>(Extended);
            ApGame.State.Write();
            GameLoad.Instance.Games[index].MainData = upgraded;
            GameLoad.Instance.SaveGameDataToFile(index);
            Messages.Add("Hut upgraded to " + (string)newArea.CardName + ". Loading ...");
            GameLoad.Instance.LoadGame(index);
        }
    }

    /// <summary>Storage extensions: the area card of an extended hut or cellar holds 25% more per extension.</summary>
    [HarmonyPatch(typeof(InGameCardBase), nameof(InGameCardBase.MaxWeightCapacity), MethodType.Getter)]
    internal static class StoragePatch
    {
        private static void Postfix(InGameCardBase __instance, ref float __result)
        {
            if (HutUpgrades.Extended.Count == 0 || !ApGame.Active || __result <= 0f) return;
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm == null || __instance != gm.CurrentEnvironmentCard || gm.CurrentEnvironment == null) return;
            string key = HutUpgrades.CurrentKey(gm);
            int count = HutUpgrades.Extended.Count(extended => extended == key);
            if (count > 0) __result += count * HutUpgrades.StorageStep * __instance.CardModel.GetWeightCapacity(0f);
        }
    }
}
