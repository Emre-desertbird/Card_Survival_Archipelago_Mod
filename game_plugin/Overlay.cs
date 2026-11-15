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
    /// - in the research tab, what each available research holds and for whom.
    /// </summary>
    internal sealed class Overlay : MonoBehaviour
    {
        private bool panelOpen;
        private string chat = "";
        private Vector2 researchScroll;
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
            DrawMessages();
            if (BlueprintRando.ResearchViewOpen && ApGame.Active) DrawResearchPreview();
        }

        private void DrawStatus()
        {
            string text = "Archipelago: " + ApConnection.StatusText;
            if (ApGame.Active)
            {
                text += "  |  " + ApGame.CheckedCount + "/" + Registry.Locations.Count + " checks" +
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
            var area = new Rect(10, 30, 360, ApConnection.IsConnected ? 250 : 215);
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
            foreach (var pair in BlueprintRando.Shuffled)
            {
                if (!gm.PurchasableBlueprintCards.Contains(pair.Key) || ApGame.State.IsChecked(pair.Value.Id)) continue;
                string holds = ApConnection.Describe(pair.Value.Id) ?? "(connect to see)";
                lines.Add((string)pair.Key.CardName + ": " + holds);
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
