using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CardSurvivalArchipelago
{
    /// <summary>A small Archipelago badge (six coloured dots in a ring), drawn by the mod itself.</summary>
    internal static class ApIcon
    {
        private static Sprite sprite;

        private static readonly Color[] Colors =
        {
            new Color32(0xC9, 0x76, 0x82, 0xFF), new Color32(0xD9, 0xA0, 0x7D, 0xFF), new Color32(0xEE, 0xE3, 0x91, 0xFF),
            new Color32(0x75, 0xC2, 0x75, 0xFF), new Color32(0x76, 0x7E, 0xBD, 0xFF), new Color32(0xCA, 0x94, 0xC2, 0xFF),
        };

        public static Sprite Sprite
        {
            get
            {
                if (sprite != null) return sprite;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var pixels = new Color[size * size];
                Vector2 centre = new Vector2(size / 2f, size / 2f);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                        float fromCentre = Vector2.Distance(p, centre);
                        // dark round background with a light rim
                        Color c = fromCentre <= 30f ? new Color(0.12f, 0.12f, 0.14f, 1f) : Color.clear;
                        if (fromCentre > 28f && fromCentre <= 30f) c = new Color(0.95f, 0.95f, 0.95f, 1f);
                        for (int i = 0; i < 6; i++)
                        {
                            float angle = Mathf.PI / 2f + i * Mathf.PI / 3f;
                            Vector2 dot = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 16f;
                            if (Vector2.Distance(p, dot) <= 7.5f) c = Colors[i];
                        }
                        pixels[y * size + x] = c;
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return sprite;
            }
        }
    }

    /// <summary>
    /// Blueprint cards in the blueprint screen tell what Archipelago does with them:
    /// - a research card shows the item its research sends and for whom, instead of the original
    ///   blueprint's name (the original stays in the description);
    /// - a buildable blueprint whose "Build - ..." check is still open carries the Archipelago badge,
    ///   and its description says what building it the first time sends.
    /// </summary>
    internal static class BlueprintCards
    {
        private const string BadgeName = "ArchipelagoBuildBadge";

        private static bool InBlueprintScreen(InGameCardBase card)
        {
            return card != null && card.CardModel != null && card.CardModel.CardType == CardTypes.Blueprint &&
                   card.CurrentSlotInfo != null && card.CurrentSlotInfo.SlotType == SlotsTypes.Blueprint;
        }

        private static BlueprintModelState? StateOf(CardData card)
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            BlueprintModelState state;
            if (gm == null || gm.BlueprintModelStates == null || !gm.BlueprintModelStates.TryGetValue(card, out state)) return null;
            return state;
        }

        /// <summary>The open research location of a research card in the research tab, or null.</summary>
        public static Registry.Entry OpenResearch(InGameCardBase card)
        {
            if (!ApGame.Active || !InBlueprintScreen(card)) return null;
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm == null || !gm.BlueprintPurchasing || StateOf(card.CardModel) != BlueprintModelState.Purchasable) return null;
            Registry.Entry location;
            if (!Registry.ResearchByCard.TryGetValue(card.CardModel.UniqueID, out location) || ApGame.State.IsChecked(location.Id)) return null;
            return location;
        }

        /// <summary>The open build location of a buildable blueprint card, or null.</summary>
        public static Registry.Entry OpenBuild(InGameCardBase card)
        {
            if (!ApGame.Active || !InBlueprintScreen(card)) return null;
            if (StateOf(card.CardModel) != BlueprintModelState.Available) return null;
            Registry.Entry location;
            if (!Registry.BuildByCard.TryGetValue(card.CardModel.UniqueID, out location) || ApGame.State.IsChecked(location.Id)) return null;
            return location;
        }

        /// <summary>"Item" and "for whom" of a scouted location, or null when not scouted yet.</summary>
        public static bool Holds(long location, out string item, out string owner, out string kind)
        {
            item = owner = kind = null;
            Archipelago.MultiClient.Net.Models.ScoutedItemInfo info;
            if (!ApConnection.Scouted.TryGetValue(location, out info)) return false;
            item = info.ItemDisplayName;
            owner = info.Player.Slot == ApConnection.Slot ? "you" : info.Player.Name;
            var flags = info.Flags;
            kind = (flags & Archipelago.MultiClient.Net.Enums.ItemFlags.Advancement) != 0 ? "progression"
                 : (flags & Archipelago.MultiClient.Net.Enums.ItemFlags.NeverExclude) != 0 ? "useful"
                 : (flags & Archipelago.MultiClient.Net.Enums.ItemFlags.Trap) != 0 ? "trap" : "";
            return true;
        }

        public static string ResearchTitle(Registry.Entry location, string original)
        {
            string item, owner, kind;
            if (!Holds(location.Id, out item, out owner, out kind)) return original;
            return item + "\n<size=70%>for " + owner + "</size>";
        }

        public static string ResearchDescription(Registry.Entry location, string original)
        {
            string item, owner, kind;
            string head = Holds(location.Id, out item, out owner, out kind)
                ? "<b>Archipelago:</b> this research sends <b>" + item + "</b> to " + owner + (kind.Length > 0 ? " (" + kind + ")" : "") + "."
                : "<b>Archipelago:</b> this research sends a check (connect to see what it holds).";
            return head + "\n<size=85%>Original research: " + original + "</size>";
        }

        public static string BuildDescription(Registry.Entry location, string original)
        {
            string item, owner, kind;
            string head = Holds(location.Id, out item, out owner, out kind)
                ? "<b>Archipelago:</b> building this the first time sends <b>" + item + "</b> to " + owner + (kind.Length > 0 ? " (" + kind + ")" : "") + "."
                : "<b>Archipelago:</b> building this the first time sends a check.";
            return head + "\n\n" + original;
        }

        /// <summary>Shows or hides the badge of one blueprint card.</summary>
        public static void UpdateBadge(CardGraphics graphics, bool show)
        {
            Transform existing = graphics.transform.Find(BadgeName);
            if (!show)
            {
                if (existing != null && existing.gameObject.activeSelf) existing.gameObject.SetActive(false);
                return;
            }
            if (existing == null)
            {
                var badge = new GameObject(BadgeName, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)badge.transform;
                rect.SetParent(graphics.transform, false);
                // bottom right: the top right corner holds the game's own blueprint icon
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(40f, 40f);
                rect.anchoredPosition = new Vector2(-8f, 8f);
                Image image = badge.GetComponent<Image>();
                image.sprite = ApIcon.Sprite;
                image.raycastTarget = false;
                existing = rect;
            }
            if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
            existing.SetAsLastSibling();
        }
    }

    [HarmonyPatch(typeof(InGameCardBase), nameof(InGameCardBase.CardName))]
    internal static class BlueprintCardNamePatch
    {
        private static void Postfix(InGameCardBase __instance, ref string __result)
        {
            Registry.Entry research = BlueprintCards.OpenResearch(__instance);
            if (research != null) __result = BlueprintCards.ResearchTitle(research, __result);
        }
    }

    [HarmonyPatch(typeof(InGameCardBase), nameof(InGameCardBase.CardDescription))]
    internal static class BlueprintCardDescriptionPatch
    {
        private static void Postfix(InGameCardBase __instance, ref string __result)
        {
            Registry.Entry research = BlueprintCards.OpenResearch(__instance);
            if (research != null)
            {
                __result = BlueprintCards.ResearchDescription(research, (string)__instance.CardModel.CardName);
                return;
            }
            Registry.Entry build = BlueprintCards.OpenBuild(__instance);
            if (build != null) __result = BlueprintCards.BuildDescription(build, __result);
        }
    }

    /// <summary>Keeps title and badge of blueprint cards current while the screen is open (tabs change states).</summary>
    [HarmonyPatch(typeof(CardGraphics), "Update")]
    internal static class BlueprintCardGraphicsPatch
    {
        private static void Postfix(CardGraphics __instance)
        {
            InGameCardBase card = __instance.CardLogic;
            if (card == null || card.CardModel == null || card.CardModel.CardType != CardTypes.Blueprint ||
                card.CurrentSlotInfo == null || card.CurrentSlotInfo.SlotType != SlotsTypes.Blueprint)
            {
                return;
            }
            if (__instance.CardTitle != null)
            {
                string title = card.CardName();
                if (__instance.CardTitle.text != title) __instance.CardTitle.text = title;
            }
            BlueprintCards.UpdateBadge(__instance, BlueprintCards.OpenBuild(card) != null);
        }
    }

    /// <summary>
    /// The character sheet lists the perks received from Archipelago (the game's perks and the new
    /// ones of this mod, with their level) next to the character's own, and shows the chosen portrait.
    /// The game builds the sheet once (Init); the mod's entries are rebuilt every time it opens.
    /// </summary>
    internal static class CharacterSheet
    {
        private static readonly List<GameObject> added = new List<GameObject>();
        /// <summary>Entries the mod added the last time (for the self-test).</summary>
        public static int Added { get { return added.Count(entry => entry != null); } }

        public static void Refresh(CharacterScreen screen)
        {
            foreach (GameObject old in added) if (old != null) Object.Destroy(old);
            added.Clear();
            if (screen == null || !ApGame.Active) return;
            var fields = Traverse.Create(screen);
            Sprite portrait = Portraits.Current;
            if (portrait != null)
            {
                Image big = fields.Field("CharacterPortrait").GetValue<Image>();
                Image tab = fields.Field("CharacterTabIcon").GetValue<Image>();
                if (big != null) big.overrideSprite = portrait;
                if (tab != null) tab.overrideSprite = portrait;
            }
            RectTransform parent = fields.Field("PerksParent").GetValue<RectTransform>();
            MenuPerkPreview prefab = fields.Field("PerkPreviewPrefab").GetValue<MenuPerkPreview>();
            if (parent == null || prefab == null) return;

            var received = ApGame.State.Received.Select((id, index) => new { id, index })
                .Where(pair => Registry.Items.ContainsKey(pair.id)).ToList();
            foreach (var pair in received.Where(pair => Registry.Items[pair.id].Kind == "perk").GroupBy(pair => pair.id).Select(group => group.First()))
            {
                CharacterPerk perk = UniqueIDScriptable.GetFromID<CharacterPerk>(Registry.Items[pair.id].PerkId);
                if (perk == null) continue;
                MenuPerkPreview preview = Object.Instantiate(prefab, parent);
                preview.DontClearTooltipOnDisable = true;
                preview.Setup(perk);
                preview.SetTooltip(perk.PerkName, "<b>Archipelago</b> (from " + ApGame.State.SenderOf(pair.index) + "), active.\n" + (string)perk.PerkDescription, "");
                added.Add(preview.gameObject);
            }
            foreach (Registry.Entry entry in Registry.Items.Values.Where(entry => entry.Kind == "newperk")
                         .GroupBy(entry => entry.Key).Select(group => group.First()))
            {
                int level = NewPerks.Level(entry.Key);
                if (level <= 0) continue;
                string name = entry.Name.Replace("Progressive ", "") + (entry.Name.StartsWith("Progressive ") ? " " + level : "");
                MenuPerkPreview preview = Object.Instantiate(prefab, parent);
                preview.DontClearTooltipOnDisable = true;
                var previewFields = Traverse.Create(preview);
                Image icon = previewFields.Field("Icon").GetValue<Image>();
                TextMeshProUGUI title = previewFields.Field("Title").GetValue<TextMeshProUGUI>();
                if (icon != null) icon.overrideSprite = ApIcon.Sprite;
                if (title != null) title.text = name;
                preview.SetTooltip(name, "<b>Archipelago perk</b>, active" + (entry.Name.StartsWith("Progressive ") ? " (level " + level + ")" : "") +
                                         ".\n" + Capitalise(entry.Effect ?? ""), "");
                added.Add(preview.gameObject);
            }
        }

        private static string Capitalise(string text)
        {
            return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1) + ".";
        }
    }

    [HarmonyPatch(typeof(CharacterScreen), nameof(CharacterScreen.Init))]
    internal static class CharacterScreenInitPatch
    {
        private static void Postfix(CharacterScreen __instance) { CharacterSheet.Refresh(__instance); }
    }

    [HarmonyPatch(typeof(CharacterScreen), nameof(CharacterScreen.Open))]
    internal static class CharacterScreenOpenPatch
    {
        private static void Postfix(CharacterScreen __instance) { CharacterSheet.Refresh(__instance); }
    }

    /// <summary>
    /// Portrait choice for Archipelago games: any portrait of the game, including the player's own
    /// custom portraits. It is chosen in the Archipelago window (main menu: for new games; in game:
    /// for this save) and only changes the picture, never the character.
    /// </summary>
    internal static class Portraits
    {
        private static GlobalCharacterInfo Info
        {
            get
            {
                GameManager gm = MBSingleton<GameManager>.Instance;
                if (gm != null && gm.Portraits != null) return gm.Portraits;
                MainMenu menu = Object.FindObjectOfType<MainMenu>();
                return menu != null ? menu.CharacterInfos : null;
            }
        }

        public static List<CharacterPortraitRef> All
        {
            get
            {
                GlobalCharacterInfo info = Info;
                return info != null && info.AllPortraits != null ? info.AllPortraits : new List<CharacterPortraitRef>();
            }
        }

        /// <summary>The portrait for new Archipelago games (empty: the character's own).</summary>
        public static string Preferred
        {
            get { return string.IsNullOrEmpty(Plugin.ConfigPortrait.Value) ? null : Plugin.ConfigPortrait.Value; }
        }

        /// <summary>The id shown in the window: this save's portrait in game, the preference in the menu.</summary>
        public static string Selected
        {
            get { return ApGame.Active && ApGame.InGame ? ApGame.State.PortraitId : Preferred; }
        }

        public static Sprite SpriteOf(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (CharacterPortraitRef portrait in All) if (portrait.PortraitID == id) return portrait.PortraitSprite;
            return null;
        }

        /// <summary>The sprite of the current save's chosen portrait, or null.</summary>
        public static Sprite Current
        {
            get { return ApGame.Active ? SpriteOf(ApGame.State.PortraitId) : null; }
        }

        /// <summary>Steps through "the character's own" and every portrait of the game.</summary>
        public static void Step(int direction)
        {
            List<CharacterPortraitRef> all = All;
            if (all.Count == 0) return;
            int index = all.FindIndex(portrait => portrait.PortraitID == Selected);   // -1 = the character's own
            index += direction;
            if (index < -1) index = all.Count - 1;
            if (index >= all.Count) index = -1;
            string id = index < 0 ? null : all[index].PortraitID;
            if (ApGame.Active && ApGame.InGame)
            {
                ApGame.State.PortraitId = id;
                ApGame.State.Write();
                CharacterScreen screen = Object.FindObjectOfType<CharacterScreen>();
                if (screen != null) CharacterSheet.Refresh(screen);
            }
            Plugin.ConfigPortrait.Value = id ?? "";
        }

        public static string Label
        {
            get
            {
                string id = Selected;
                if (string.IsNullOrEmpty(id)) return "character's own";
                int index = All.FindIndex(portrait => portrait.PortraitID == id);
                return index < 0 ? "character's own" : "portrait " + (index + 1) + " of " + All.Count;
            }
        }
    }

    /// <summary>The small portrait of the HUD (the game resets it every frame to the character's).</summary>
    [HarmonyPatch(typeof(ShowCharacterPortrait), "UpdatePortrait")]
    internal static class HudPortraitPatch
    {
        private static bool Prefix(ShowCharacterPortrait __instance)
        {
            Sprite portrait = Portraits.Current;
            if (portrait == null) return true;
            Image image = Traverse.Create(__instance).Field("PortraitImage").GetValue<Image>();
            if (image != null && image.overrideSprite != portrait) image.overrideSprite = portrait;
            return false;
        }
    }

    /// <summary>The ending screen shows the chosen portrait too.</summary>
    [HarmonyPatch(typeof(EndgameMenu), nameof(EndgameMenu.Setup))]
    internal static class EndingPortraitPatch
    {
        private static void Postfix(EndgameMenu __instance)
        {
            Sprite portrait = Portraits.Current;
            if (portrait == null) return;
            Image image = Traverse.Create(__instance).Field("CharacterPortrait").GetValue<Image>();
            if (image != null) image.overrideSprite = portrait;
        }
    }
}
