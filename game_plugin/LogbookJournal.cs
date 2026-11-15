using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// Shows the Archipelago logbook inside the game's own journal book. The game instantiates the
    /// journal from its prefab every time it is opened and then opens a page (ContentDisplayer.Init).
    /// In a save with the logbook, the mod builds one page per chapter from the current state (done
    /// tasks, progress, Determination), styled like the original pages, links them with the arrow
    /// buttons, and opens the first one. Other saves and the guide are left alone.
    /// </summary>
    [HarmonyPatch(typeof(ContentDisplayer), nameof(ContentDisplayer.Init))]
    internal static class LogbookJournal
    {
        private const string PagePrefix = "ArchipelagoLogbook_";
        /// <summary>The pages of the last opened journal (rebuilt on every opening).</summary>
        public static List<ContentPage> LastPages = new List<ContentPage>();

        private static bool IsJournal(ContentDisplayer displayer, GameManager gm)
        {
            ContentDisplayer journal = gm != null ? gm.GetJournal : null;
            return journal != null && displayer != journal && displayer != gm.GetGuide && displayer.name.StartsWith(journal.name);
        }

        private static void Postfix(ContentDisplayer __instance)
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (!IsJournal(__instance, gm)) return;
            var displayer = Traverse.Create(__instance);
            ContentPage original = displayer.Field("DefaultPage").GetValue<ContentPage>();
            bool logbook = ApGame.Active && ApGame.State.LocationKinds.Contains("logbook");
            if (!logbook)
            {
                // A normal save must never reopen a logbook page remembered from another save.
                var opened = Traverse.Create(typeof(ContentDisplayer)).Field("OpenDisplayers").GetValue<Dictionary<string, ContentPage>>();
                ContentPage remembered;
                if (original != null && opened != null && opened.TryGetValue(__instance.name, out remembered) &&
                    (remembered == null || remembered.name.StartsWith(PagePrefix)))
                {
                    __instance.OpenPage(original);
                }
                return;
            }
            List<ContentPage> pages = Build(original);
            if (pages.Count == 0) return;
            displayer.Field("DefaultPage").SetValue(pages[0]);
            displayer.Field("ExplicitPageContent").SetValue(pages);
            __instance.FillAllPagesList();
            __instance.OpenPage(pages[0]);
            foreach (ContentPage old in LastPages) if (old != null) Object.Destroy(old);
            LastPages = pages;
        }

        private static List<ContentPage> Build(ContentPage original)
        {
            PieceOfInfo template = default(PieceOfInfo);
            ContentSection sectionTemplate = default(ContentSection);
            if (original != null && original.Sections != null && original.Sections.Length > 0)
            {
                sectionTemplate = original.Sections[0];
                if (sectionTemplate.Entries != null && sectionTemplate.Entries.Length > 0) template = sectionTemplate.Entries[0];
            }
            template.RelatedObjective = null;
            template.PageLinks = new ContentPageLink[0];
            template.WebLinks = new WebLink[0];
            template.Image = null;
            if (template.OrderOfElements == null || template.OrderOfElements.Length == 0) template.OrderOfElements = new[] { 0, 1, 2 };

            var pages = new List<ContentPage>();
            var tasks = Registry.Watched.Where(task => task.Kind == "logbook" && Checks.Enabled(task)).ToList();
            int done = tasks.Count(task => ApGame.State.IsChecked(task.Id));
            foreach (var chapter in tasks.GroupBy(task => task.Chapter))
            {
                var entries = new List<PieceOfInfo>();
                foreach (Registry.Entry task in chapter)
                {
                    bool finished = ApGame.State.IsChecked(task.Id);
                    string progress = "";
                    if (!finished && task.Watch.Count == 1 && task.Watch[0].Count > 1)
                    {
                        progress = "   " + Mathf.Min(Checks.Progress(task.Watch[0]), task.Watch[0].Count) + "/" + task.Watch[0].Count;
                    }
                    PieceOfInfo entry = template;
                    entry.Title = new LocalizedString { DefaultText = (finished ? "[x] " : "[  ] ") + task.Name.Replace("Logbook - ", "") + progress };
                    entry.Content = new LocalizedString { DefaultText = "+" + task.Determination + " Determination" };
                    entries.Add(entry);
                }
                ContentSection section = sectionTemplate;
                section.Entries = entries.ToArray();
                ContentPage page = ScriptableObject.CreateInstance<ContentPage>();
                page.name = PagePrefix + chapter.Key;
                page.PageTitle = new LocalizedString { DefaultText = "ARCHIPELAGO LOGBOOK - " + chapter.Key.ToUpperInvariant() + "  (" + done + "/" + tasks.Count + ")" };
                if (original != null)
                {
                    page.PageDisplay = original.PageDisplay;
                    page.SpaceBtwnSections = original.SpaceBtwnSections;
                    page.TitleSeparator = original.TitleSeparator;
                }
                page.Sections = new[] { section };
                page.GlobalPageLinks = new ContentPageLink[0];
                pages.Add(page);
            }
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].LeftButton = i > 0 ? pages[i - 1] : null;
                pages[i].RightButton = i < pages.Count - 1 ? pages[i + 1] : null;
            }
            return pages;
        }
    }
}
