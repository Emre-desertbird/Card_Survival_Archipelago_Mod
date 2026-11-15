using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CardSurvivalArchipelago
{
    /// <summary>Recent Archipelago messages (items, checks, chat, server notices).</summary>
    internal static class Messages
    {
        public const int Keep = 8;
        public static readonly List<KeyValuePair<float, string>> Recent = new List<KeyValuePair<float, string>>();

        public static void Add(string text)
        {
            Plugin.Log.LogInfo("[AP] " + text);
            Recent.Add(new KeyValuePair<float, string>(Time.unscaledTime, text));
            if (Recent.Count > 50) Recent.RemoveAt(0);
        }
    }

    /// <summary>
    /// The mod's own interface, drawn with Unity's immediate-mode GUI so it does not depend on
    /// the game's UI prefabs:
    /// - the Archipelago panel (server, slot, password, connect, chat), always open in the main
    ///   menu and toggled with F8 in a game;
    /// - a status line and the latest messages;
    /// - in the research tab, what each available research holds and for whom;
    /// - with the panel, the finds buffer: what waits, from whom, and buttons to take it;
    /// - the Archipelago logbook: chapters, tasks, progress and Determination.
    /// </summary>
    internal sealed class Overlay : MonoBehaviour
    {
        private bool panelOpen, logbookOpen;
        private string chat = "";
        private Vector2 researchScroll, bufferScroll, logbookScroll;
        private GUIStyle box, small;

        private static bool InMainMenu
        {
            get { return MBSingleton<GameManager>.Instance == null && GameLoad.Instance != null; }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8)) panelOpen = !panelOpen;
        }

        private void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontSize = 13 };
                small = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 13 };
            }
            DrawStatus();
            if (InMainMenu || panelOpen) DrawPanel();
            if (panelOpen && ApGame.Active && ApGame.InGame) DrawBuffer();
            if (panelOpen && logbookOpen && ApGame.Active && ApGame.State.LocationKinds.Contains("logbook")) DrawLogbook();
            DrawMessages();
            if (BlueprintRando.ResearchViewOpen && ApGame.Active) DrawResearchPreview();
        }

        private void DrawStatus()
        {
            string text = "Archipelago: " + ApConnection.StatusText;
            if (ApGame.Active)
            {
                text += "  |  " + ApGame.CheckedCount + " checks" +
                        (Finds.Buffer.Count > 0 ? "  |  buffer: " + Finds.Buffer.Count : "") +
                        (ApGame.State.GoalDone ? "  |  goal done" : "");
            }
            else if (!InMainMenu)
            {
                text += "  |  normal game (not randomized)";
            }
            text += "  |  F8";
            GUI.Label(new Rect(Screen.width - 620, 4, 612, 22), text, small);
        }

        private void DrawPanel()
        {
            var area = new Rect(10, 30, 360, ApConnection.IsConnected ? 360 : 215);
            GUILayout.BeginArea(area, "Archipelago", GUI.skin.window);
            if (Registry.Error != null)
            {
                GUILayout.Label(Registry.Error, small);
            }
            bool locked = ApConnection.State == ApConnection.Status.Connecting;
            GUI.enabled = !locked && !ApConnection.IsConnected;
            Plugin.ConfigHost.Value = Field("Server", Plugin.ConfigHost.Value);
            Plugin.ConfigSlot.Value = Field("Slot", Plugin.ConfigSlot.Value);
            Plugin.ConfigPassword.Value = Field("Password", Plugin.ConfigPassword.Value, true);
            GUI.enabled = !locked;
            if (ApConnection.IsConnected)
            {
                if (GUILayout.Button("Disconnect")) ApConnection.Disconnect();
            }
            else if (GUILayout.Button(ApGame.Active ? "Connect (reconnect this save)" : "Connect"))
            {
                ConnectNow();
            }
            GUI.enabled = true;
            GUILayout.Label(InMainMenu
                ? (ApConnection.IsConnected ? "Start a NEW game to play this seed, or load its save."
                                            : "Connect first, then start a new game. Without a connection the game is normal.")
                : (ApGame.Active ? "Checks found offline are sent when you reconnect." : "This save is not randomized."), small);
            if (ApConnection.IsConnected)
            {
                GUILayout.BeginHorizontal();
                chat = GUILayout.TextField(chat, GUILayout.Width(260));
                if (GUILayout.Button("Send") || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && chat.Length > 0))
                {
                    ApConnection.Say(chat);
                    chat = "";
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Chat and commands, e.g. !hint <item>", small);
                if (InMainMenu && CharacterLock.Active)
                {
                    GUILayout.Label("New games start as " + CharacterLock.CharacterName + ", the Archipelago character.", small);
                }
                if (ApGame.Active)
                {
                    string perks = string.Join(", ", Registry.Items.Values.Where(entry => entry.Kind == "newperk")
                        .GroupBy(entry => entry.Key).Select(group => group.First())
                        .Where(entry => NewPerks.Level(entry.Key) > 0)
                        .Select(entry => entry.Name.Replace("Progressive ", "") + " " + NewPerks.Level(entry.Key)).ToArray());
                    if (perks.Length > 0) GUILayout.Label("New perks: " + perks, small);
                    if (HutUpgrades.KitsLeft > 0 || HutUpgrades.ExtensionsLeft > 0)
                    {
                        GUILayout.BeginHorizontal();
                        GUI.enabled = HutUpgrades.CanUpgrade;
                        if (GUILayout.Button("Upgrade this hut (" + HutUpgrades.KitsLeft + ")")) HutUpgrades.Upgrade();
                        GUI.enabled = HutUpgrades.CanExtend;
                        if (GUILayout.Button("Extend storage (" + HutUpgrades.ExtensionsLeft + ")")) HutUpgrades.Extend();
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }
                    if (Traps.Queue.Count > 0) GUILayout.Label("Traps waiting: " + Traps.Queue.Count + " (one per day)", small);
                }
                if (ApGame.Active && ApGame.State.LocationKinds.Contains("logbook") &&
                         GUILayout.Button(logbookOpen ? "Hide logbook" : "Logbook"))
                {
                    logbookOpen = !logbookOpen;
                }
            }
            GUILayout.EndArea();
        }

        public static void ConnectNow()
        {
            Plugin.Settings.Save();
            ApConnection.Connect(Plugin.ConfigHost.Value.Trim(), Plugin.ConfigSlot.Value.Trim(), Plugin.ConfigPassword.Value, ok =>
            {
                if (ok && ApGame.Active && !ApGame.MatchesConnection())
                {
                    ApConnection.Disconnect();
                    Messages.Add("This save belongs to " + ApGame.State.SlotName + " on seed " + ApGame.State.SeedName + ".");
                }
            });
        }

        private static string Field(string label, string value, bool secret = false)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(70));
            value = secret ? GUILayout.PasswordField(value ?? "", '*') : GUILayout.TextField(value ?? "");
            GUILayout.EndHorizontal();
            return value;
        }

        private void DrawBuffer()
        {
            int count = Finds.Buffer.Count;
            var area = new Rect(10, 400, 360, Mathf.Min(90 + 24 * count, Screen.height * 0.45f));
            GUILayout.BeginArea(area, "Finds buffer (" + count + ")", GUI.skin.window);
            if (count == 0)
            {
                GUILayout.Label("Empty. At most " + Finds.PerHour + " finds per in-game hour land on the ground; the rest waits here.", small);
            }
            else
            {
                if (GUILayout.Button("Take " + Mathf.Min(count, Finds.PerHour) + " to this area")) Finds.Release();
                bufferScroll = GUILayout.BeginScrollView(bufferScroll);
                foreach (int index in Finds.Buffer.ToList())
                {
                    Registry.Entry entry;
                    if (index >= ApGame.State.Received.Count || !Registry.Items.TryGetValue(ApGame.State.Received[index], out entry)) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Finds.Describe(entry) + "  (from " + ApGame.State.SenderOf(index) + ")", small, GUILayout.Width(270));
                    if (GUILayout.Button("Take")) Finds.Release(index);
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        private void DrawLogbook()
        {
            var area = new Rect(380, 30, 440, Screen.height * 0.6f);
            int done = Registry.Watched.Count(entry => entry.Kind == "logbook" && Checks.Enabled(entry) && ApGame.State.IsChecked(entry.Id));
            int total = Registry.Watched.Count(entry => entry.Kind == "logbook" && Checks.Enabled(entry));
            GUILayout.BeginArea(area, "Archipelago logbook (" + done + "/" + total + ")", GUI.skin.window);
            logbookScroll = GUILayout.BeginScrollView(logbookScroll);
            string chapter = null;
            foreach (Registry.Entry task in Registry.Watched)
            {
                if (task.Kind != "logbook" || !Checks.Enabled(task)) continue;
                if (task.Chapter != chapter)
                {
                    chapter = task.Chapter;
                    GUILayout.Label("<b>" + chapter + "</b>", small);
                }
                bool finished = ApGame.State.IsChecked(task.Id);
                string progress = "";
                if (!finished && task.Watch.Count == 1 && task.Watch[0].Count > 1)
                {
                    progress = "  " + Mathf.Min(Checks.Progress(task.Watch[0]), task.Watch[0].Count) + "/" + task.Watch[0].Count;
                }
                GUILayout.Label((finished ? "[x] " : "[  ] ") + task.Name.Replace("Logbook - ", "") + progress +
                                "  (+" + task.Determination + " Determination)", small);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawMessages()
        {
            var shown = Messages.Recent.Where(m => Time.unscaledTime - m.Key < 12f).Reverse().Take(Messages.Keep).Reverse().ToList();
            if (shown.Count == 0) return;
            float height = 20 * shown.Count + 8;
            GUI.Box(new Rect(10, Screen.height - height - 70, 620, height),
                    string.Join("\n", shown.Select(m => m.Value).ToArray()), box);
        }

        private void DrawResearchPreview()
        {
            GameManager gm = MBSingleton<GameManager>.Instance;
            if (gm == null) return;
            var lines = new List<string>();
            // Every researchable blueprint with an open check: shuffled ones and survival basics.
            foreach (CardData card in gm.PurchasableBlueprintCards)
            {
                Registry.Entry location;
                if (card == null || !Registry.ResearchByCard.TryGetValue(card.UniqueID, out location) ||
                    ApGame.State.IsChecked(location.Id)) continue;
                string holds = ApConnection.Describe(location.Id) ?? "(connect to see)";
                lines.Add((string)card.CardName + ": " + holds);
            }
            lines.Sort();
            var area = new Rect(Screen.width - 430, 30, 420, Mathf.Min(40 + 20 * lines.Count, Screen.height * 0.45f));
            GUILayout.BeginArea(area, "Research holds", GUI.skin.window);
            researchScroll = GUILayout.BeginScrollView(researchScroll);
            foreach (string line in lines) GUILayout.Label(line, small);
            if (lines.Count == 0) GUILayout.Label("No research available right now.", small);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
