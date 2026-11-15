using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.MessageLog.Messages;

namespace CardSurvivalArchipelago
{
    /// <summary>
    /// The connection to the Archipelago server (Archipelago.MultiClient.Net).
    /// Network callbacks arrive on other threads; everything the game sees is handed to the
    /// Unity main thread through <see cref="RunOnMainThread"/>.
    /// </summary>
    internal static class ApConnection
    {
        public const string GameName = "Card Survival: Tropical Island";
        private static readonly Version ClientVersion = new Version(0, 6, 7);

        public enum Status { Disconnected, Connecting, Connected, Failed }

        public static Status State { get; private set; }
        public static string StatusText { get; private set; } = "Not connected";
        public static ArchipelagoSession Session { get; private set; }
        public static string SeedName { get; private set; }
        public static string SlotName { get; private set; }
        public static int Slot { get; private set; } = -1;
        public static int Team { get; private set; } = -1;
        public static Dictionary<string, object> SlotData { get; private set; }

        /// <summary>Location id -> what it holds, filled by LocationScouts after connecting.</summary>
        public static readonly Dictionary<long, ScoutedItemInfo> Scouted = new Dictionary<long, ScoutedItemInfo>();

        private static readonly Queue<Action> mainThread = new Queue<Action>();
        private static int connectGeneration;

        public static bool IsConnected
        {
            get { return State == Status.Connected && Session != null && Session.Socket.Connected; }
        }

        public static void RunOnMainThread(Action action)
        {
            lock (mainThread) mainThread.Enqueue(action);
        }

        /// <summary>Called every frame from the plugin.</summary>
        public static void Pump()
        {
            while (true)
            {
                Action next;
                lock (mainThread)
                {
                    if (mainThread.Count == 0) return;
                    next = mainThread.Dequeue();
                }
                try
                {
                    next();
                }
                catch (Exception error)
                {
                    Plugin.Log.LogError(error);
                }
            }
        }

        /// <summary>Starts connecting in the background; <paramref name="done"/> runs on the main thread.</summary>
        public static void Connect(string host, string slot, string password, Action<bool> done)
        {
            Disconnect();
            int generation = ++connectGeneration;
            State = Status.Connecting;
            StatusText = "Connecting to " + host + " ...";
            new Thread(() =>
            {
                ArchipelagoSession session = null;
                string failure = null;
                LoginSuccessful success = null;
                try
                {
                    session = ArchipelagoSessionFactory.CreateSession(host);
                    session.MessageLog.OnMessageReceived += message => OnMessage(message);
                    session.Socket.SocketClosed += reason => RunOnMainThread(() => OnClosed(session, reason));
                    LoginResult result = session.TryConnectAndLogin(GameName, slot, ItemsHandlingFlags.AllItems,
                        ClientVersion, new string[0], null, string.IsNullOrEmpty(password) ? null : password, true);
                    success = result as LoginSuccessful;
                    if (success == null)
                    {
                        var failed = result as LoginFailure;
                        failure = failed != null && failed.Errors.Length > 0 ? string.Join("; ", failed.Errors) : "Login failed";
                    }
                }
                catch (Exception error)
                {
                    failure = error.GetBaseException().Message;
                }
                RunOnMainThread(() =>
                {
                    if (generation != connectGeneration)
                    {
                        return;   // a newer attempt replaced this one
                    }
                    if (success == null)
                    {
                        State = Status.Failed;
                        StatusText = "Connection failed: " + failure;
                        Plugin.Log.LogWarning(StatusText);
                        done?.Invoke(false);
                        return;
                    }
                    Session = session;
                    SlotName = slot;
                    Slot = success.Slot;
                    Team = success.Team;
                    SlotData = success.SlotData ?? new Dictionary<string, object>();
                    SeedName = session.RoomState.Seed;
                    string problem = CheckSlotData();
                    if (problem != null)
                    {
                        Disconnect();
                        State = Status.Failed;
                        StatusText = problem;
                        done?.Invoke(false);
                        return;
                    }
                    State = Status.Connected;
                    StatusText = "Connected as " + slot + " (seed " + SeedName + ")";
                    Plugin.Log.LogInfo(StatusText);
                    ScoutAll();
                    done?.Invoke(true);
                });
            }) { IsBackground = true, Name = "Archipelago connect" }.Start();
        }

        private static string CheckSlotData()
        {
            object version;
            if (!SlotData.TryGetValue("registry_version", out version) || Convert.ToInt32(version) != Registry.Version)
            {
                return "This seed was generated with a different APWorld version (registry " + version +
                       ", the mod has " + Registry.Version + "). Install matching versions.";
            }
            return null;
        }

        public static void Disconnect()
        {
            connectGeneration++;
            ArchipelagoSession session = Session;
            Session = null;
            Scouted.Clear();
            State = Status.Disconnected;
            StatusText = "Not connected";
            if (session != null)
            {
                new Thread(() =>
                {
                    try { session.Socket.DisconnectAsync().Wait(2000); } catch (Exception) { }
                }) { IsBackground = true }.Start();
            }
        }

        private static void OnClosed(ArchipelagoSession session, string reason)
        {
            if (session != Session) return;
            Session = null;
            State = Status.Failed;
            StatusText = "Connection lost" + (string.IsNullOrEmpty(reason) ? "" : ": " + reason) + ". Reconnect in the Archipelago panel (F8).";
            Messages.Add(StatusText);
        }

        private static void OnMessage(LogMessage message)
        {
            string text = message.ToString();
            RunOnMainThread(() => Messages.Add(text));
        }

        /// <summary>Asks the server what every location of this slot holds (no hints are created).</summary>
        private static void ScoutAll()
        {
            ArchipelagoSession session = Session;
            long[] ids = session.Locations.AllLocations.ToArray();
            if (ids.Length == 0) return;
            session.Locations.ScoutLocationsAsync(HintCreationPolicy.None, ids).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled) return;
                Dictionary<long, ScoutedItemInfo> result = task.Result;
                RunOnMainThread(() =>
                {
                    if (session != Session) return;
                    foreach (var pair in result) Scouted[pair.Key] = pair.Value;
                });
            });
        }

        public static void SendChecks(IEnumerable<long> locations)
        {
            if (!IsConnected) return;
            long[] ids = locations.ToArray();
            if (ids.Length > 0)
            {
                Session.Locations.CompleteLocationChecksAsync(ids);
            }
        }

        public static void SendGoal()
        {
            if (IsConnected) Session.SetGoalAchieved();
        }

        public static void Say(string text)
        {
            if (IsConnected && !string.IsNullOrEmpty(text)) Session.Say(text);
        }

        /// <summary>"Item for Player" as shown next to a research, or null if not scouted yet.</summary>
        public static string Describe(long location)
        {
            ScoutedItemInfo info;
            if (!Scouted.TryGetValue(location, out info)) return null;
            string owner = info.Player.Slot == Slot ? "you" : info.Player.Name;
            string kind = (info.Flags & ItemFlags.Advancement) != 0 ? " (progression)"
                        : (info.Flags & ItemFlags.NeverExclude) != 0 ? " (useful)"
                        : (info.Flags & ItemFlags.Trap) != 0 ? " (trap)" : "";
            return info.ItemDisplayName + " for " + owner + kind;
        }
    }
}
