#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Wavedash
{
    public static partial class SDK
    {
        /// <summary>
        /// Serves every SDK call when the game is not running as a WebGL build (the Unity editor,
        /// desktop players). Calls behave like the live service against local state, so SDK flows
        /// can be exercised in Play Mode without building for Web. The player is always
        /// <see cref="UserId"/> / <see cref="Username"/>. Collections are public so a test script can
        /// seed friends, lobbies, entitlements or leaderboard entries, and <see cref="Emit"/> fires
        /// any SDK event at the game's handlers. This class does not exist in WebGL player builds,
        /// so wrap any reference to it in <c>#if UNITY_EDITOR</c>.
        /// </summary>
        public static class Mock
        {
            public const string UserId = "test_user";
            public const string Username = "TEST_USER";
            public const int DefaultMaxPlayers = 100;

            public static Dictionary<string, string> LaunchParams = new();
            public static List<Dictionary<string, object>> Friends = new();
            public static HashSet<string> Entitlements = new();
            public static bool PaywallAccepts = true;
            public static Dictionary<string, MockLobby> Lobbies = new();
            public static string CurrentLobbyId;
            public static Dictionary<string, MockLeaderboard> Leaderboards = new();
            public static Dictionary<string, Dictionary<string, object>> UGCItems = new();
            public static Dictionary<string, object> Stats = new();
            public static HashSet<string> Achievements = new();
            public static Dictionary<string, object> Presence = new();
            public static bool IsFullscreen;
            public static bool IsMuted;
            public static string RemoteRoot => Path.Combine(Application.persistentDataPath, "WavedashMockRemote");

            public class MockLobby
            {
                public string LobbyId;
                public string HostId;
                public int Visibility;
                public int MaxPlayers;
                public List<Dictionary<string, object>> Users = new();
                public Dictionary<string, object> Metadata = new();

                public Dictionary<string, object> ToLobby() => new()
                {
                    { "lobbyId", LobbyId },
                    { "visibility", Visibility },
                    { "maxPlayers", MaxPlayers },
                    { "playerCount", Users.Count },
                    { "metadata", Metadata }
                };
            }

            public class MockLeaderboard
            {
                public string Id;
                public string Name;
                public int SortOrder;
                public int DisplayType;
                public List<Dictionary<string, object>> Entries = new();

                public Dictionary<string, object> ToLeaderboard() => new()
                {
                    { "id", Id },
                    { "name", Name },
                    { "sortOrder", SortOrder },
                    { "displayType", DisplayType },
                    { "totalEntries", Entries.Count }
                };

                public bool Beats(int a, int b) => SortOrder == WavedashConstants.LeaderboardSortMethod.ASCENDING ? a < b : a > b;

                public List<Dictionary<string, object>> Ranked()
                {
                    var ordered = Entries.OrderBy(e => Convert.ToInt32(e["score"]) * (SortOrder == WavedashConstants.LeaderboardSortMethod.ASCENDING ? 1 : -1)).ToList();
                    for (int i = 0; i < ordered.Count; i++) ordered[i]["globalRank"] = i + 1;
                    return ordered;
                }
            }

            private static readonly Queue<Action> _queue = new();
            private static readonly Dictionary<int, Queue<(string from, byte[] data)>> _p2pInbox = new();
            private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
            private static bool _deferEvents;
            private static bool _connected;
            private static int _lobbyCounter;
            private static int _ugcCounter;
            private static int _messageCounter;

            private static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            private static void Defer(Action action)
            {
                EnsureCallbackReceiver();
                _queue.Enqueue(action);
            }

            internal static void Pump()
            {
                int count = _queue.Count;
                while (count-- > 0)
                {
                    try { _queue.Dequeue()(); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }

            private static T Json<T>(object value) =>
                value == null ? default : JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

            private static Task<T> Resolve<T>(object value)
            {
                var tcs = new TaskCompletionSource<T>();
                Defer(() => tcs.SetResult(Json<T>(value)));
                return tcs.Task;
            }

            private static Task<T> Fail<T>(string message)
            {
                var tcs = new TaskCompletionSource<T>();
                Defer(() => tcs.SetException(new Exception($"Request failed: {message}")));
                return tcs.Task;
            }

            /// <summary>
            /// Fires an SDK event at the game's handlers on the next frame, through the same
            /// receiver the WebGL bridge uses. <paramref name="eventName"/> is the event name
            /// without the On prefix, e.g. "LobbyKicked"; <paramref name="payload"/> is serialized to JSON.
            /// </summary>
            public static void Emit(string eventName, object payload)
            {
                string json = JsonConvert.SerializeObject(payload ?? new Dictionary<string, object>());
                Defer(() => _callbackReceiver.gameObject.SendMessage(eventName, json));
            }

            private static void Log(string message)
            {
                if (_debug) Debug.Log($"[Wavedash Mock] {message}");
            }

            private static object Config(Dictionary<string, object> config, string key) =>
                config != null && config.TryGetValue(key, out var value) ? value : null;

            public static void Init(Dictionary<string, object> config)
            {
                EnsureCallbackReceiver();
                _debug = Config(config, "debug") as bool? == true;
                _deferEvents = Config(config, "deferEvents") as bool? == true;
                var p2p = Json<Dictionary<string, object>>(Config(config, "p2p"));
                MAX_PAYLOAD_SIZE = Convert.ToInt32(Config(p2p, "messageSize") ?? 2048);
                _maxIncomingMessages = Convert.ToInt32(Config(p2p, "maxIncomingMessages") ?? 1024);
                LoadStats();
                Debug.Log($"[Wavedash] Not a WebGL build: SDK calls are served by SDK.Mock as {Username}. Remote files persist under {RemoteRoot}");
                if (!_deferEvents) ReadyForEvents();
            }

            public static void ReadyForEvents()
            {
                if (_connected) return;
                _connected = true;
                Emit("BackendConnected", new { isConnected = true, hasEverConnected = true, connectionCount = 1, connectionRetries = 0 });
            }

            public static Dictionary<string, string> GetLaunchParams() => new(LaunchParams);

            public static Dictionary<string, object> GetUser() => Json<Dictionary<string, object>>(new { id = UserId, username = Username });

            public static string GetUserId() => UserId;

            public static string GetUsername() => Username;

            private static Dictionary<string, object> FindUser(string userId) =>
                Friends.Concat(Lobbies.Values.SelectMany(l => l.Users)).FirstOrDefault(u => Convert.ToString(u["userId"]) == userId);

            public static string GetUsername(string userId) =>
                userId == UserId ? Username : FindUser(userId)?["username"] as string;

            public static string GetUserAvatarUrl(string userId, int size)
            {
                var user = FindUser(userId);
                if (user == null) return null;
                return (user.TryGetValue("avatarUrl", out var url) ? url : user.TryGetValue("userAvatarUrl", out url) ? url : null) as string;
            }

            public static Task<string> GetUserJwt() => Resolve<string>("mock-jwt-unsigned");

            private static Dictionary<string, object> LobbyUser(MockLobby lobby, string userId, string username) => new()
            {
                { "lobbyId", lobby.LobbyId },
                { "userId", userId },
                { "username", username },
                { "isHost", lobby.HostId == userId }
            };

            private static void Enter(MockLobby lobby)
            {
                if (CurrentLobbyId != null) Exit(CurrentLobbyId);
                CurrentLobbyId = lobby.LobbyId;
                lobby.Users.Add(LobbyUser(lobby, UserId, Username));
                Emit("LobbyJoined", new { lobbyId = lobby.LobbyId, hostId = lobby.HostId, users = lobby.Users, metadata = lobby.Metadata });
            }

            private static void Exit(string lobbyId)
            {
                if (Lobbies.TryGetValue(lobbyId, out var lobby))
                {
                    lobby.Users.RemoveAll(u => Convert.ToString(u["userId"]) == UserId);
                    if (lobby.Users.Count == 0) Lobbies.Remove(lobbyId);
                }
                if (CurrentLobbyId == lobbyId) CurrentLobbyId = null;
            }

            public static Task<string> CreateLobby(int lobbyVisibility, int maxPlayers)
            {
                var lobby = new MockLobby
                {
                    LobbyId = $"mock_lobby_{++_lobbyCounter}",
                    HostId = UserId,
                    Visibility = lobbyVisibility,
                    MaxPlayers = maxPlayers > 0 ? maxPlayers : DefaultMaxPlayers
                };
                Lobbies[lobby.LobbyId] = lobby;
                Enter(lobby);
                Log($"CreateLobby -> {lobby.LobbyId}");
                return Resolve<string>(lobby.LobbyId);
            }

            public static Task<bool> JoinLobby(string lobbyId)
            {
                if (!Lobbies.TryGetValue(lobbyId, out var lobby)) return Fail<bool>($"Lobby {lobbyId} not found");
                if (lobby.Users.Count >= lobby.MaxPlayers) return Fail<bool>("Lobby is full");
                Enter(lobby);
                return Resolve<bool>(true);
            }

            public static Task<string> LeaveLobby(string lobbyId)
            {
                if (CurrentLobbyId != lobbyId) return Fail<string>($"Not in lobby {lobbyId}");
                Exit(lobbyId);
                return Resolve<string>(lobbyId);
            }

            public static Task<List<Dictionary<string, object>>> ListAvailableLobbies(bool friendsOnly) =>
                Resolve<List<Dictionary<string, object>>>(Lobbies.Values
                    .Where(l => l.Visibility != WavedashConstants.LobbyVisibility.PRIVATE)
                    .Select(l => l.ToLobby()).ToList());

            public static Task<Dictionary<string, object>> GetLobby(string lobbyId) =>
                Lobbies.TryGetValue(lobbyId, out var lobby)
                    ? Resolve<Dictionary<string, object>>(lobby.ToLobby())
                    : Fail<Dictionary<string, object>>($"Lobby {lobbyId} not found");

            public static string GetLobbyHostId(string lobbyId) =>
                Lobbies.TryGetValue(lobbyId, out var lobby) ? lobby.HostId : null;

            private static object LobbyValue(string lobbyId, string key) =>
                Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.Metadata.TryGetValue(key, out var value) ? value : null;

            private static double JsNumber(object value) => value switch
            {
                bool b => b ? 1 : 0,
                string s => s.Trim() == "" ? 0 : double.TryParse(s, NumberStyles.Float, Inv, out var d) ? d : double.NaN,
                _ => Convert.ToDouble(value, Inv)
            };

            private static string JsString(object value) => value switch
            {
                bool b => b ? "true" : "false",
                string s => s,
                _ => Convert.ToDouble(value, Inv).ToString("R", Inv)
            };

            public static bool HasLobbyData(string lobbyId, string key) => LobbyValue(lobbyId, key) != null;

            public static string GetLobbyDataString(string lobbyId, string key)
            {
                var value = LobbyValue(lobbyId, key);
                return value == null ? null : JsString(value);
            }

            public static int GetLobbyDataInt(string lobbyId, string key)
            {
                double number = GetLobbyDataDouble(lobbyId, key);
                return double.IsFinite(number) ? unchecked((int)(long)Math.Truncate(number)) : 0;
            }

            public static float GetLobbyDataFloat(string lobbyId, string key) => (float)GetLobbyDataDouble(lobbyId, key);

            public static double GetLobbyDataDouble(string lobbyId, string key)
            {
                var value = LobbyValue(lobbyId, key);
                return value == null ? 0.0 : JsNumber(value);
            }

            public static long GetLobbyDataLong(string lobbyId, string key) => (long)GetLobbyDataDouble(lobbyId, key);

            public static bool GetLobbyDataBool(string lobbyId, string key)
            {
                var value = LobbyValue(lobbyId, key);
                if (value == null) return false;
                if (value is bool b) return b;
                if (value is string s) return s != "";
                double number = JsNumber(value);
                return number != 0 && !double.IsNaN(number);
            }

            public static bool SetLobbyData(string lobbyId, string key, object value)
            {
                if (!Lobbies.TryGetValue(lobbyId, out var lobby) || lobby.HostId != UserId) return false;
                if (value is float f) value = (double)f;
                if (value is double d && !double.IsFinite(d)) return false;
                lobby.Metadata[key] = value;
                Emit("LobbyDataUpdated", lobby.Metadata);
                return true;
            }

            public static bool DeleteLobbyData(string lobbyId, string key)
            {
                if (!Lobbies.TryGetValue(lobbyId, out var lobby) || lobby.HostId != UserId) return false;
                lobby.Metadata.Remove(key);
                Emit("LobbyDataUpdated", lobby.Metadata);
                return true;
            }

            public static List<Dictionary<string, object>> GetLobbyUsers(string lobbyId) =>
                Lobbies.TryGetValue(lobbyId, out var lobby) ? Json<List<Dictionary<string, object>>>(lobby.Users) : new();

            public static int GetNumLobbyUsers(string lobbyId) =>
                Lobbies.TryGetValue(lobbyId, out var lobby) ? lobby.Users.Count : 0;

            public static bool SendLobbyChatMessage(string lobbyId, string message)
            {
                if (CurrentLobbyId != lobbyId) return false;
                Emit("LobbyMessage", new { messageId = $"mock_message_{++_messageCounter}", lobbyId, userId = UserId, username = Username, message, timestamp = Now });
                return true;
            }

            public static Task<string> GetLobbyInviteLink(bool copyToClipboard)
            {
                if (CurrentLobbyId == null) return Fail<string>("Not in a lobby");
                string link = $"https://wavedash.com/play/mock-game?lobby={CurrentLobbyId}";
                if (copyToClipboard) GUIUtility.systemCopyBuffer = link;
                return Resolve<string>(link);
            }

            public static Task<bool> InviteUserToLobby(string lobbyId, string userId)
            {
                Log($"InviteUserToLobby {userId} -> {lobbyId}");
                return Resolve<bool>(Lobbies.ContainsKey(lobbyId));
            }

            public static bool BroadcastP2PMessage(ArraySegment<byte> payload, int channel, bool reliable) =>
                CurrentLobbyId != null && payload.Count > 0 && payload.Count <= MAX_PAYLOAD_SIZE;

            public static bool SendP2PMessage(string targetUserId, ArraySegment<byte> payload, int channel, bool reliable) =>
                BroadcastP2PMessage(payload, channel, reliable) && FindUser(targetUserId) != null;

            /// <summary>
            /// Queues an incoming P2P message from <paramref name="fromUserId"/> for the next
            /// <see cref="SDK.DrainP2PChannel"/> on <paramref name="channel"/>.
            /// </summary>
            public static void ReceiveP2PMessage(string fromUserId, int channel, byte[] payload)
            {
                if (!_p2pInbox.TryGetValue(channel, out var queue)) _p2pInbox[channel] = queue = new();
                queue.Enqueue((fromUserId, payload));
            }

            public static int DrainP2PChannel(int channel, List<P2PMessage> messages)
            {
                if (!_p2pInbox.TryGetValue(channel, out var queue)) return 0;
                while (queue.Count > 0)
                {
                    var (from, data) = queue.Dequeue();
                    messages.Add(new P2PMessage { SenderId = from, Channel = channel, Payload = new ArraySegment<byte>(data) });
                }
                return messages.Count;
            }

            private static MockLeaderboard LeaderboardById(string leaderboardId) =>
                Leaderboards.Values.FirstOrDefault(l => l.Id == leaderboardId);

            public static Task<Dictionary<string, object>> GetOrCreateLeaderboard(string leaderboardName, int sortMethod, int displayType)
            {
                if (!Leaderboards.TryGetValue(leaderboardName, out var leaderboard))
                {
                    Leaderboards[leaderboardName] = leaderboard = new MockLeaderboard
                    {
                        Id = $"mock_leaderboard_{leaderboardName}",
                        Name = leaderboardName,
                        SortOrder = sortMethod,
                        DisplayType = displayType
                    };
                }
                return Resolve<Dictionary<string, object>>(leaderboard.ToLeaderboard());
            }

            public static Task<Dictionary<string, object>> GetLeaderboard(string leaderboardName) =>
                Leaderboards.TryGetValue(leaderboardName, out var leaderboard)
                    ? Resolve<Dictionary<string, object>>(leaderboard.ToLeaderboard())
                    : Fail<Dictionary<string, object>>($"Leaderboard {leaderboardName} not found");

            public static int GetLeaderboardEntryCount(string leaderboardId) => LeaderboardById(leaderboardId)?.Entries.Count ?? 0;

            public static Task<List<Dictionary<string, object>>> GetMyLeaderboardEntries(string leaderboardId)
            {
                var leaderboard = LeaderboardById(leaderboardId);
                if (leaderboard == null) return Fail<List<Dictionary<string, object>>>($"Leaderboard {leaderboardId} not found");
                return Resolve<List<Dictionary<string, object>>>(leaderboard.Ranked().Where(e => Convert.ToString(e["userId"]) == UserId).ToList());
            }

            public static Task<Dictionary<string, object>> UploadLeaderboardScore(string leaderboardId, int score, bool keepBest, string ugcId, Dictionary<string, object> metadata)
            {
                var leaderboard = LeaderboardById(leaderboardId);
                if (leaderboard == null) return Fail<Dictionary<string, object>>($"Leaderboard {leaderboardId} not found");
                var entry = leaderboard.Entries.FirstOrDefault(e => Convert.ToString(e["userId"]) == UserId);
                int submittedRank = 1 + leaderboard.Entries.Count(e => e != entry && leaderboard.Beats(Convert.ToInt32(e["score"]), score));
                bool write = entry == null || !keepBest || leaderboard.Beats(score, Convert.ToInt32(entry["score"]));
                if (entry == null) leaderboard.Entries.Add(entry = new() { { "userId", UserId }, { "username", Username } });
                if (write)
                {
                    entry["score"] = score;
                    entry["timestamp"] = Now;
                    entry["ugcId"] = ugcId;
                    entry["metadata"] = metadata != null && metadata.Count > 0 ? metadata : null;
                }
                leaderboard.Ranked();
                return Resolve<Dictionary<string, object>>(new Dictionary<string, object>
                {
                    { "entryId", $"mock_entry_{leaderboard.Id}_{UserId}" },
                    { "score", entry["score"] },
                    { "scoreChanged", write },
                    { "globalRank", entry["globalRank"] },
                    { "submittedScore", score },
                    { "submittedRank", submittedRank },
                    { "metadata", entry["metadata"] },
                    { "userId", UserId },
                    { "username", Username }
                });
            }

            private static bool IsFriendOrSelf(Dictionary<string, object> entry)
            {
                string userId = Convert.ToString(entry["userId"]);
                return userId == UserId || Friends.Any(f => Convert.ToString(f["userId"]) == userId);
            }

            public static Task<List<Dictionary<string, object>>> ListLeaderboardEntries(string leaderboardId, int offset, int limit, bool friendsOnly)
            {
                var leaderboard = LeaderboardById(leaderboardId);
                if (leaderboard == null) return Fail<List<Dictionary<string, object>>>($"Leaderboard {leaderboardId} not found");
                var ranked = leaderboard.Ranked().Where(e => !friendsOnly || IsFriendOrSelf(e));
                return Resolve<List<Dictionary<string, object>>>(ranked.Skip(offset).Take(limit).ToList());
            }

            public static Task<List<Dictionary<string, object>>> ListLeaderboardEntriesAroundUser(string leaderboardId, int countAhead, int countBehind, bool friendsOnly)
            {
                var leaderboard = LeaderboardById(leaderboardId);
                if (leaderboard == null) return Fail<List<Dictionary<string, object>>>($"Leaderboard {leaderboardId} not found");
                var ranked = leaderboard.Ranked().Where(e => !friendsOnly || IsFriendOrSelf(e)).ToList();
                int index = ranked.FindIndex(e => Convert.ToString(e["userId"]) == UserId);
                if (index < 0) return Resolve<List<Dictionary<string, object>>>(new List<Dictionary<string, object>>());
                int start = Math.Max(0, index - countAhead);
                return Resolve<List<Dictionary<string, object>>>(ranked.Skip(start).Take(index - start + 1 + countBehind).ToList());
            }

            private static string RemotePath(string localPath) =>
                Path.Combine(RemoteRoot, Path.GetRelativePath(Application.persistentDataPath, Path.GetFullPath(localPath)));

            private static void CopyFile(string from, string to)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(from, to, true);
            }

            public static Task<string> UploadRemoteFile(string path)
            {
                if (!File.Exists(path)) return Fail<string>($"File not found: {path}");
                CopyFile(path, RemotePath(path));
                return Resolve<string>(path);
            }

            public static Task<string> DownloadRemoteFile(string path)
            {
                if (!File.Exists(RemotePath(path))) return Fail<string>($"Remote file not found: {path}");
                CopyFile(RemotePath(path), path);
                return Resolve<string>(path);
            }

            public static Task<bool> RemoteFileExists(string path) => Resolve<bool>(File.Exists(RemotePath(path)));

            public static Task<string> DeleteRemoteFile(string path)
            {
                File.Delete(RemotePath(path));
                return Resolve<string>(path);
            }

            public static Task<string> DownloadRemoteDirectory(string path)
            {
                string remoteDir = RemotePath(path);
                if (Directory.Exists(remoteDir))
                {
                    foreach (var file in Directory.EnumerateFiles(remoteDir, "*", SearchOption.AllDirectories))
                    {
                        CopyFile(file, Path.Combine(path, Path.GetRelativePath(remoteDir, file)));
                    }
                }
                return Resolve<string>(path);
            }

            public static Task<List<Dictionary<string, object>>> ListRemoteDirectory(string path)
            {
                string remoteDir = RemotePath(path);
                var files = !Directory.Exists(remoteDir)
                    ? new List<object>()
                    : Directory.EnumerateFiles(remoteDir, "*", SearchOption.AllDirectories).Select(file => (object)new
                    {
                        exists = true,
                        key = Path.Combine(path, Path.GetRelativePath(remoteDir, file)),
                        name = Path.GetRelativePath(remoteDir, file).Replace('\\', '/'),
                        lastModified = new DateTimeOffset(File.GetLastWriteTimeUtc(file)).ToUnixTimeMilliseconds(),
                        size = new FileInfo(file).Length,
                        etag = File.GetLastWriteTimeUtc(file).Ticks.ToString("x")
                    }).ToList();
                return Resolve<List<Dictionary<string, object>>>(files);
            }

            private static string UGCFile(string ugcId) => Path.Combine(RemoteRoot, "ugc", ugcId);

            private static string UGCRecords => Path.Combine(RemoteRoot, "ugc.json");

            private static void SaveUGC()
            {
                Directory.CreateDirectory(RemoteRoot);
                File.WriteAllText(UGCRecords, JsonConvert.SerializeObject(UGCItems));
            }

            private static void LoadUGC()
            {
                if (UGCItems.Count == 0 && File.Exists(UGCRecords))
                    UGCItems = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(File.ReadAllText(UGCRecords));
            }

            public static Task<string> CreateUGCItem(int ugcType, string title, string description, int visibility, string filePath)
            {
                if (filePath != null && !File.Exists(filePath)) return Fail<string>($"File not found: {filePath}");
                LoadUGC();
                string ugcId = $"mock_ugc_{++_ugcCounter}";
                while (UGCItems.ContainsKey(ugcId)) ugcId = $"mock_ugc_{++_ugcCounter}";
                UGCItems[ugcId] = new Dictionary<string, object>
                {
                    { "ugcId", ugcId },
                    { "ugcType", ugcType },
                    { "visibility", visibility },
                    { "title", title },
                    { "description", description },
                    { "creatorId", UserId },
                    { "creatorUsername", Username },
                    { "createdAt", Now },
                    { "downloadUrl", filePath != null ? new Uri(UGCFile(ugcId)).AbsoluteUri : null }
                };
                if (filePath != null) CopyFile(filePath, UGCFile(ugcId));
                SaveUGC();
                return Resolve<string>(ugcId);
            }

            public static Task<string> DownloadUGCItem(string ugcId, string filePath)
            {
                LoadUGC();
                if (!UGCItems.ContainsKey(ugcId) || !File.Exists(UGCFile(ugcId))) return Fail<string>($"UGC item {ugcId} has no file");
                CopyFile(UGCFile(ugcId), filePath);
                return Resolve<string>(filePath);
            }

            public static Task<string> DeleteUGCItem(string ugcId)
            {
                LoadUGC();
                if (!UGCItems.Remove(ugcId)) return Fail<string>($"UGC item {ugcId} not found");
                File.Delete(UGCFile(ugcId));
                SaveUGC();
                return Resolve<string>(ugcId);
            }

            public static Task<string> UpdateUGCItem(string ugcId, string title, string description, int? visibility, string filePath)
            {
                LoadUGC();
                if (!UGCItems.TryGetValue(ugcId, out var item)) return Fail<string>($"UGC item {ugcId} not found");
                if (filePath != null && !File.Exists(filePath)) return Fail<string>($"File not found: {filePath}");
                if (title != null) item["title"] = title;
                if (description != null) item["description"] = description;
                if (visibility != null) item["visibility"] = visibility.Value;
                if (filePath != null)
                {
                    CopyFile(filePath, UGCFile(ugcId));
                    item["downloadUrl"] = new Uri(UGCFile(ugcId)).AbsoluteUri;
                }
                SaveUGC();
                return Resolve<string>(ugcId);
            }

            public static Task<Dictionary<string, object>> ListUGCItems(string createdBy, int? ugcType, string titleSearch, int? numItems, string continueCursor)
            {
                LoadUGC();
                var page = UGCItems.Values
                    .Where(i => Convert.ToString(i["creatorId"]) == UserId || Convert.ToInt32(i["visibility"]) == WavedashConstants.UGCVisibility.PUBLIC)
                    .Where(i => createdBy == null || Convert.ToString(i["creatorId"]) == createdBy)
                    .Where(i => ugcType == null || Convert.ToInt32(i["ugcType"]) == ugcType)
                    .Where(i => string.IsNullOrEmpty(titleSearch) || (Convert.ToString(i["title"]) ?? "").IndexOf(titleSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Take(numItems ?? int.MaxValue)
                    .ToList();
                return Resolve<Dictionary<string, object>>(new { page, isDone = true, continueCursor = "" });
            }

            public static Task<bool> UpdateUserPresence(Dictionary<string, object> data)
            {
                Presence = data ?? new();
                Log($"UpdateUserPresence {JsonConvert.SerializeObject(Presence)}");
                return Resolve<bool>(true);
            }

            private static string StatsFile => Path.Combine(RemoteRoot, "stats.json");

            private static void LoadStats()
            {
                if (!File.Exists(StatsFile)) return;
                var saved = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(StatsFile));
                Stats = Json<Dictionary<string, object>>(saved["stats"]) ?? new();
                Achievements = Json<HashSet<string>>(saved["achievements"]) ?? new();
            }

            public static Task<bool> RequestStats()
            {
                LoadStats();
                return Resolve<bool>(true);
            }

            public static bool SetStatInt(string statName, int value, bool storeNow)
            {
                Stats[statName] = value;
                return !storeNow || StoreStats();
            }

            public static int GetStatInt(string statName) =>
                Stats.TryGetValue(statName, out var value) ? Convert.ToInt32(value) : 0;

            public static bool SetStatFloat(string statName, float value, bool storeNow)
            {
                Stats[statName] = value;
                return !storeNow || StoreStats();
            }

            public static float GetStatFloat(string statName) =>
                Stats.TryGetValue(statName, out var value) ? Convert.ToSingle(value) : 0f;

            public static bool SetAchievement(string achievementName, bool storeNow)
            {
                Achievements.Add(achievementName);
                return !storeNow || StoreStats();
            }

            public static bool GetAchievement(string achievementName) => Achievements.Contains(achievementName);

            public static bool StoreStats()
            {
                Directory.CreateDirectory(RemoteRoot);
                File.WriteAllText(StatsFile, JsonConvert.SerializeObject(new { stats = Stats, achievements = Achievements }));
                Emit("StatsStored", new { success = true });
                return true;
            }

            public static void ToggleOverlay() => Debug.Log("[Wavedash Mock] ToggleOverlay");

            public static Task<bool> RequestFullscreen(bool fullscreen)
            {
                if (IsFullscreen != fullscreen)
                {
                    IsFullscreen = fullscreen;
                    Emit("FullscreenChanged", new { isFullscreen = fullscreen });
                }
                return Resolve<bool>(true);
            }

            public static Task<bool> ToggleFullscreen() => RequestFullscreen(!IsFullscreen);

            public static Task<bool> RequestMute(bool muted)
            {
                if (IsMuted != muted)
                {
                    IsMuted = muted;
                    Emit("MuteChanged", new { isMuted = muted });
                }
                return Resolve<bool>(true);
            }

            public static Task<bool> ToggleMute() => RequestMute(!IsMuted);

            public static Task<bool> IsEntitled(string contentIdentifier) => Resolve<bool>(Entitlements.Contains(contentIdentifier));

            public static Task<List<string>> GetEntitlements() => Resolve<List<string>>(Entitlements.ToList());

            public static Task<bool> TriggerPaywall(string contentIdentifier)
            {
                if (Entitlements.Contains(contentIdentifier)) return Resolve<bool>(true);
                if (!PaywallAccepts)
                {
                    Log($"TriggerPaywall {contentIdentifier} declined (PaywallAccepts is false)");
                    return Resolve<bool>(false);
                }
                Entitlements.Add(contentIdentifier);
                Log($"TriggerPaywall {contentIdentifier} granted");
                Emit("EntitlementsGranted", new { contentIdentifiers = new[] { contentIdentifier } });
                return Resolve<bool>(true);
            }

            public static Task<List<Dictionary<string, object>>> ListFriends() => Resolve<List<Dictionary<string, object>>>(Friends);
        }

        private partial class WavedashCallbackReceiver
        {
            private void Update() => Mock.Pump();
        }
    }
}
#endif
