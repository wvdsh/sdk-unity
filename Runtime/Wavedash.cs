using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wavedash
{
    /// <summary>
    /// Represents a decoded P2P message
    /// </summary>
    public struct P2PMessage
    {
        public string SenderId;
        public int Channel;
        public byte[] Payload;
    }

    /// <summary>
    /// Main entry point for the Wavedash SDK
    /// Usage: await SDK.GetLeaderboard("name"); etc.
    /// </summary>
    public static class SDK
    {
        // Events that JavaScript can trigger
        public static event Action<Dictionary<string, object>> OnLobbyJoined;
        public static event Action<Dictionary<string, object>> OnLobbyLeft;
        public static event Action<Dictionary<string, object>> OnLobbyMessage;
        public static event Action<Dictionary<string, object>> OnP2PConnectionEstablished;
        public static event Action<Dictionary<string, object>> OnP2PConnectionFailed;
        public static event Action<Dictionary<string, object>> OnP2PPeerDisconnected;

        // Internal callback receiver instance
        private static WavedashCallbackReceiver _callbackReceiver;
        private static bool _debug = false;
        
        // Cached user data to avoid repeated JS calls
        private static Dictionary<string, object> _cachedUser;

        // Pending TaskCompletionSources by requestId
        private static readonly Dictionary<string, object> _pending = new();

        // jslib -> Unity callbacks
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void JsCallback(string responseJson);
        private static JsCallback _callbackDelegate; // keep alive

        #region WavedashJS Functions
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WavedashJS_Init(string configJson);

        [DllImport("__Internal")]
        private static extern void WavedashJS_ReadyForEvents();

        [DllImport("__Internal")]
        private static extern int WavedashJS_IsReady();

        [DllImport("__Internal")]
        private static extern string WavedashJS_GetUser();

        // Lobby Functions
        [DllImport("__Internal")]
        private static extern void WavedashJS_CreateLobby(
            int lobbyType,
            int maxPlayers,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_JoinLobby(
            string lobbyId,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_LeaveLobby(
            string lobbyId,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_ListAvailableLobbies(
            bool friendsOnly,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern string WavedashJS_GetLobbyHostId(string lobbyId);

        [DllImport("__Internal")]
        private static extern int WavedashJS_BroadcastP2PMessage(
            int appChannel,
            bool reliable,
            byte[] payload,
            int payloadLength);

        [DllImport("__Internal")]
        private static extern int WavedashJS_SendP2PMessage(
            string targetUserId,
            int appChannel,
            bool reliable,
            byte[] payload,
            int payloadLength);

        [DllImport("__Internal")]
        private static extern int WavedashJS_DrainP2PChannelToBuffer(
            int appChannel,
            byte[] buffer,
            int bufferSize);

        // Leaderboard Functions
        [DllImport("__Internal")]
        private static extern void WavedashJS_GetOrCreateLeaderboard(
            string leaderboardName,
            int sortMethod,
            int displayType,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_GetLeaderboard(
            string leaderboardName,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_GetMyLeaderboardEntries(
            string leaderboardId,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_UploadLeaderboardScore(
            string leaderboardId,
            int score,
            bool keepBest,
            string ugcId,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_ListLeaderboardEntries(
            string leaderboardId,
            int offset,
            int limit,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_ListLeaderboardEntriesAroundUser(
            string leaderboardId,
            int countAhead,
            int countBehind,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern int WavedashJS_GetLeaderboardEntryCount(string leaderboardId);

        // Save state / Remote File Storage
        [DllImport("__Internal")]
        private static extern void WavedashJS_UploadRemoteFile(
            string path,
            string uploadToLocation,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_DownloadRemoteFile(
            string path,
            string downloadToLocation,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_DownloadRemoteDirectory(
            string path,
            IntPtr callbackPtr,
            string requestId);

        [DllImport("__Internal")]
        private static extern void WavedashJS_ListRemoteDirectory(
            string path,
            IntPtr callbackPtr,
            string requestId);

        // User Generated Content (UGC)
        [DllImport("__Internal")]
        private static extern void WavedashJS_CreateUGCItem(
            int ugcType,
            string title,
            string description,
            int visibility,
            string filePath,
            IntPtr callbackPtr, string requestId);
        
        [DllImport("__Internal")]
        private static extern void WavedashJS_DownloadUGCItem(
            string ugcId,
            string filePath,
            IntPtr callbackPtr, string requestId);

#endif
        #endregion

        #region SDK Implementations
        /// <summary>
        /// Initialize the Wavedash SDK
        /// </summary>
        public static void Init(Dictionary<string, object> config)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            EnsureCallbackReceiver();

            _debug = config.ContainsKey("debug") && config["debug"] as bool? == true;

            string configJson = JsonConvert.SerializeObject(config);
            WavedashJS_Init(configJson);
#else
            Debug.LogWarning("Wavedash.Init() is only supported in WebGL builds");
#endif
        }

        /// <summary>
        /// Signals to WavedashJS that Unity is ready to receive events.
        /// Call this after subscribing to all desired event handlers.
        /// </summary>
        public static void ReadyForEvents()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WavedashJS_ReadyForEvents();
#endif
        }

        public static bool IsReady()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WavedashJS_IsReady() == 1;
#else
            return false;
#endif
        }

        /// <summary>
        /// Gets the current user data. Results are cached after the first call.
        /// </summary>
        public static Dictionary<string, object> GetUser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_cachedUser != null)
            {
                return _cachedUser;
            }
            
            string userJson = WavedashJS_GetUser();
            if (!string.IsNullOrEmpty(userJson))
            {
                try
                {
                    _cachedUser = JsonConvert.DeserializeObject<Dictionary<string, object>>(userJson);
                    return _cachedUser;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse user data: {e.Message}");
                }
            }
#endif
            return null;
        }

        private static Task<T> InvokeJs<T>(Action<IntPtr, string> jsInvoker)
        {
            string requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<T>();
            _pending[requestId] = tcs;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_callbackDelegate == null)
                _callbackDelegate = ResponseHandlerCallbackImpl;

            IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(_callbackDelegate);
            jsInvoker(fnPtr, requestId);
#else
            tcs.SetResult(default!);
            _pending.Remove(requestId);
#endif
            return tcs.Task;
        }

        // ===========
        // Lobby
        // ===========

        /// <summary>
        /// Creates a new lobby with the current user as the host.
        /// </summary>
        /// <param name="lobbyType">The type of lobby to create.</param>
        /// <param name="maxPlayers">The maximum number of players in the lobby. If 0, the default max players for the lobby type will be used.</param>
        /// <returns>The ID of the created lobby.</returns>
        /// <remarks>
        /// Triggers the <see cref="OnLobbyJoined"/> event upon success.
        /// </remarks>
        public static Task<string> CreateLobby(int lobbyType, int maxPlayers = 0) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_CreateLobby(lobbyType, maxPlayers, fnPtr, requestId));
#else
            Task.FromResult<string>(null);
#endif

        /// <summary>
        /// Joins a lobby.
        /// </summary>
        /// <param name="lobbyId">The ID of the lobby to join.</param>
        /// <returns>The ID of the joined lobby.</returns>
        /// <remarks>
        /// Triggers the <see cref="OnLobbyJoined"/> event upon success.
        /// </remarks>
        public static Task<string> JoinLobby(string lobbyId) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_JoinLobby(lobbyId, fnPtr, requestId));
#else
            Task.FromResult<string>(null);
#endif

        /// <summary>
        /// Leaves a lobby.
        /// </summary>
        /// <param name="lobbyId">The ID of the lobby to leave.</param>
        /// <returns>The ID of the left lobby.</returns>
        public static Task<string> LeaveLobby(string lobbyId) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_LeaveLobby(lobbyId, fnPtr, requestId));
#else
            Task.FromResult<string>(null);
#endif

        /// <summary>
        /// Lists available lobbies that can be joined.
        /// </summary>
        /// <param name="friendsOnly">If true, only return lobbies with friends.</param>
        public static Task<List<Dictionary<string, object>>> ListAvailableLobbies(bool friendsOnly = false) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<List<Dictionary<string, object>>>((fnPtr, requestId) =>
                WavedashJS_ListAvailableLobbies(friendsOnly, fnPtr, requestId));
#else
            Task.FromResult<List<Dictionary<string, object>>>(null);
#endif

        public static string GetLobbyHostId(string lobbyId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WavedashJS_GetLobbyHostId(lobbyId);
#else
            return null;
#endif
        }

        // ===========
        // P2P Messaging
        // ===========

        // P2P packet header constants
        private const int P2P_USERID_SIZE = 32;
        private const int P2P_CHANNEL_SIZE = 4;
        private const int P2P_DATALENGTH_SIZE = 4;
        private const int P2P_HEADER_SIZE = P2P_USERID_SIZE + P2P_CHANNEL_SIZE + P2P_DATALENGTH_SIZE; // 40 bytes
        private const int P2P_SLOT_HEADER_SIZE = 4; // 4-byte length prefix per message slot

        // Internal buffer for receiving drained P2P messages
        // 64KB handles ~31 max-size (2048 byte) messages per drain call
        // If more messages are queued, they remain in the JS queue for the next drain
        private const int P2P_DRAIN_BUFFER_SIZE = 64 * 1024;
        private static byte[] _p2pDrainBuffer;

        public static bool BroadcastP2PMessage(byte[] payload, int channel = 0, bool reliable = true)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (payload == null || payload.Length == 0) return false;
            return WavedashJS_BroadcastP2PMessage(channel, reliable, payload, payload.Length) == 1;
#else
            return false;
#endif
        }

        public static bool SendP2PMessage(string targetUserId, byte[] payload, int channel = 0, bool reliable = true)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (string.IsNullOrEmpty(targetUserId) || payload == null || payload.Length == 0) return false;
            return WavedashJS_SendP2PMessage(targetUserId, channel, reliable, payload, payload.Length) == 1;
#else
            return false;
#endif
        }

        /// <summary>
        /// Drains P2P messages from a channel into the provided list.
        /// If more messages are queued than fit in the internal buffer, remaining messages
        /// stay in the JS queue and will be returned on the next call.
        /// </summary>
        /// <param name="channel">The P2P channel to drain</param>
        /// <param name="messages">List to populate with decoded messages (will be cleared first)</param>
        /// <returns>Number of messages decoded, or -1 on error</returns>
        public static int DrainP2PChannel(int channel, List<P2PMessage> messages)
        {
            messages.Clear();

#if UNITY_WEBGL && !UNITY_EDITOR
            // Lazy-allocate the drain buffer
            if (_p2pDrainBuffer == null)
            {
                _p2pDrainBuffer = new byte[P2P_DRAIN_BUFFER_SIZE];
            }

            int bytesWritten = WavedashJS_DrainP2PChannelToBuffer(channel, _p2pDrainBuffer, _p2pDrainBuffer.Length);
            if (bytesWritten <= 0) return bytesWritten;

            // Decode messages from buffer
            // Format: [size:4][message:N][size:4][message:N]...
            int readOffset = 0;
            while (readOffset + P2P_SLOT_HEADER_SIZE <= bytesWritten)
            {
                // Read message length (little-endian uint32)
                int messageLength = _p2pDrainBuffer[readOffset]
                    | (_p2pDrainBuffer[readOffset + 1] << 8)
                    | (_p2pDrainBuffer[readOffset + 2] << 16)
                    | (_p2pDrainBuffer[readOffset + 3] << 24);
                readOffset += P2P_SLOT_HEADER_SIZE;

                // Validate message length is sane and fits in remaining buffer
                if (messageLength <= 0 || readOffset + messageLength > bytesWritten)
                {
                    Debug.LogWarning($"[Wavedash] P2P message exceeds buffer: {readOffset + messageLength} > {bytesWritten}");
                    break;
                }

                // Decode the message
                var decoded = DecodeP2PPacket(_p2pDrainBuffer, readOffset, messageLength);
                if (decoded.HasValue)
                {
                    messages.Add(decoded.Value);
                }
                else
                {
                    Debug.LogWarning("[Wavedash] Failed to decode P2P packet");
                }

                readOffset += messageLength;
            }

            return messages.Count;
#else
            return 0;
#endif
        }

        /// <summary>
        /// Decodes a single P2P packet from the buffer.
        /// Format: [fromUserId:32][channel:4][dataLength:4][payload:variable]
        /// </summary>
        private static P2PMessage? DecodeP2PPacket(byte[] buffer, int offset, int length)
        {
            if (length < P2P_HEADER_SIZE) return null;

            // Extract fromUserId (32 bytes, null-padded ASCII)
            int nullIndex = -1;
            for (int i = 0; i < P2P_USERID_SIZE; i++)
            {
                if (buffer[offset + i] == 0)
                {
                    nullIndex = i;
                    break;
                }
            }
            int userIdLength = nullIndex >= 0 ? nullIndex : P2P_USERID_SIZE;
            string senderId = System.Text.Encoding.ASCII.GetString(buffer, offset, userIdLength);

            // Extract channel (little-endian uint32 at offset 32)
            int channelOffset = offset + P2P_USERID_SIZE;
            int msgChannel = buffer[channelOffset]
                | (buffer[channelOffset + 1] << 8)
                | (buffer[channelOffset + 2] << 16)
                | (buffer[channelOffset + 3] << 24);

            // Extract dataLength (little-endian uint32 at offset 36)
            int dataLengthOffset = offset + P2P_USERID_SIZE + P2P_CHANNEL_SIZE;
            int dataLength = buffer[dataLengthOffset]
                | (buffer[dataLengthOffset + 1] << 8)
                | (buffer[dataLengthOffset + 2] << 16)
                | (buffer[dataLengthOffset + 3] << 24);

            // Validate payload length
            int payloadOffset = offset + P2P_HEADER_SIZE;
            if (dataLength < 0 || dataLength > length - P2P_HEADER_SIZE)
            {
                Debug.LogWarning($"[Wavedash] P2P payload length mismatch: {dataLength} > {length - P2P_HEADER_SIZE}");
                return null;
            }

            // Copy payload
            byte[] payload = new byte[dataLength];
            if (dataLength > 0)
            {
                Array.Copy(buffer, payloadOffset, payload, 0, dataLength);
            }

            return new P2PMessage
            {
                SenderId = senderId,
                Channel = msgChannel,
                Payload = payload
            };
        }

        // ===========
        // Leaderboards
        // ===========
        public static Task<Dictionary<string, object>> GetOrCreateLeaderboard(string leaderboardName, int sortMethod, int displayType) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<Dictionary<string, object>>((fnPtr, requestId) =>
                WavedashJS_GetOrCreateLeaderboard(leaderboardName, sortMethod, displayType, fnPtr, requestId));
#else
            Task.FromResult<Dictionary<string, object>>(null);
#endif

        public static Task<Dictionary<string, object>> GetLeaderboard(string leaderboardName) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<Dictionary<string, object>>((fnPtr, requestId) =>
                WavedashJS_GetLeaderboard(leaderboardName, fnPtr, requestId));
#else
            Task.FromResult<Dictionary<string, object>>(null);
#endif

        public static int GetLeaderboardEntryCount(string leaderboardId)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WavedashJS_GetLeaderboardEntryCount(leaderboardId);
#else
            return 0;
#endif
        }

        public static Task<List<Dictionary<string, object>>> GetMyLeaderboardEntries(string leaderboardId) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<List<Dictionary<string, object>>>((fnPtr, requestId) =>
                WavedashJS_GetMyLeaderboardEntries(leaderboardId, fnPtr, requestId));
#else
            Task.FromResult<List<Dictionary<string, object>>>(null);
#endif

        public static Task<Dictionary<string, object>> UploadLeaderboardScore(string leaderboardId, int score, bool keepBest, string ugcId = null) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<Dictionary<string, object>>((fnPtr, requestId) =>
                WavedashJS_UploadLeaderboardScore(leaderboardId, score, keepBest, ugcId, fnPtr, requestId));
#else
            Task.FromResult<Dictionary<string, object>>(null);
#endif

        public static Task<List<Dictionary<string, object>>> ListLeaderboardEntries(string leaderboardId, int offset, int limit) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<List<Dictionary<string, object>>>((fnPtr, requestId) =>
                WavedashJS_ListLeaderboardEntries(leaderboardId, offset, limit, fnPtr, requestId));
#else
            Task.FromResult<List<Dictionary<string, object>>>(null);
#endif

        public static Task<List<Dictionary<string, object>>> ListLeaderboardEntriesAroundUser(string leaderboardId, int countAhead, int countBehind) =>
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeJs<List<Dictionary<string, object>>>((fnPtr, requestId) =>
                WavedashJS_ListLeaderboardEntriesAroundUser(leaderboardId, countAhead, countBehind, fnPtr, requestId));
#else
            Task.FromResult<List<Dictionary<string, object>>>(null);
#endif

        // ===========
        // Remote Files
        // ===========
        public static Task<string> UploadRemoteFile(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!path.StartsWith(UnityEngine.Application.persistentDataPath))
            {
                UnityEngine.Debug.LogWarning($"UploadRemoteFile: You might be missing write permissions to '{path}'. Consider prepending the path with Application.persistentDataPath.");
            }
            return InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_UploadRemoteFile(path, path, fnPtr, requestId));
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static Task<string> DownloadRemoteFile(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!path.StartsWith(UnityEngine.Application.persistentDataPath))
            {
                UnityEngine.Debug.LogWarning($"DownloadRemoteFile: You might be missing write permissions to '{path}'. Consider prepending the path with Application.persistentDataPath.");
            }

            if (!string.IsNullOrEmpty(path))
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!System.IO.Directory.Exists(dir))
                {
                    UnityEngine.Debug.LogWarning($"DownloadRemoteFile: Directory '{dir}' does not exist. It will be created.");
                    System.IO.Directory.CreateDirectory(dir);
                }
            }
            return InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_DownloadRemoteFile(path, path, fnPtr, requestId));
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static Task<string> DownloadRemoteDirectory(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!path.StartsWith(UnityEngine.Application.persistentDataPath))
            {
                UnityEngine.Debug.LogWarning($"DownloadRemoteDirectory: You might be missing write permissions to '{path}'. Consider prepending the path with Application.persistentDataPath.");
            }

            return InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_DownloadRemoteDirectory(path, fnPtr, requestId));
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static Task<List<Dictionary<string, object>>> ListRemoteDirectory(string path)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (!path.StartsWith(UnityEngine.Application.persistentDataPath))
            {
                UnityEngine.Debug.LogWarning($"ListRemoteDirectory: You might be missing write permissions to '{path}'. Consider prepending the path with Application.persistentDataPath.");
            }
            
            return InvokeJs<List<Dictionary<string, object>>>((fnPtr, requestId) =>
                WavedashJS_ListRemoteDirectory(path, fnPtr, requestId));
#else
            return Task.FromResult<List<Dictionary<string, object>>>(null);
#endif
        }

        // ===========
        // User Generated Content (UGC)
        // ===========

        public static Task<string> CreateUGCItem(
            int ugcType,
            string title,
            string description,
            int visibility = WavedashConstants.UGCVisibility.PUBLIC,
            string filePath = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_CreateUGCItem(ugcType, title, description, visibility, filePath, fnPtr, requestId));
#else
            return Task.FromResult<string>(null);
#endif
        }

        public static Task<string> DownloadUGCItem(string ugcId, string localFilePath)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return InvokeJs<string>((fnPtr, requestId) =>
                WavedashJS_DownloadUGCItem(ugcId, localFilePath, fnPtr, requestId));
#else
            return Task.FromResult<string>(null);
#endif
        }
#endregion


        [AOT.MonoPInvokeCallback(typeof(JsCallback))]
        private static void ResponseHandlerCallbackImpl(string responseJson)
        {
            try
            {
                if (string.IsNullOrEmpty(responseJson))
                {
                    Debug.LogError("Callback received empty JSON.");
                    return;
                }

                var root = JObject.Parse(responseJson);
                var reqId = root.Value<string>("requestId");
                if (string.IsNullOrEmpty(reqId))
                {
                    Debug.LogError("Response missing 'requestId'.");
                    return;
                }

                if (!_pending.TryGetValue(reqId, out var tcsObj))
                {
                    Debug.LogWarning($"No pending task for requestId {reqId}");
                    return;
                }
                _pending.Remove(reqId);

                var resp = (JObject)root["response"];
                if (resp == null || !resp.Value<bool>("success"))
                {
                    var msg = resp?.Value<string>("message") ?? "Invalid response";
                    SetResultIfMatch<Dictionary<string, object>>(tcsObj, null, ex: new Exception($"Request failed: {msg}"));
                    SetResultIfMatch<List<Dictionary<string, object>>>(tcsObj, null, ex: new Exception($"Request failed: {msg}"));
                    SetResultIfMatch<string>(tcsObj, null, ex: new Exception($"Request failed: {msg}"));
                    SetResultIfMatch<object>(tcsObj, null, ex: new Exception($"Request failed: {msg}"));
                    return;
                }

                var dataToken = resp["data"];

                if (!SetResultIfMatch<Dictionary<string, object>>(tcsObj, dataToken) &&
                    !SetResultIfMatch<List<Dictionary<string, object>>>(tcsObj, dataToken) &&
                    !SetResultIfMatch<string>(tcsObj, dataToken) &&
                    !SetResultIfMatch<object>(tcsObj, dataToken))
                {
                    Debug.LogError($"Unexpected TaskCompletionSource type for request {reqId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse response: {e.Message}");
            }
        }

        private static bool SetResultIfMatch<T>(object tcsObj, JToken token, Exception ex = null)
        {
            if (tcsObj is TaskCompletionSource<T> tcs)
            {
                if (ex != null)
                {
                    tcs.SetException(ex);
                }
                else
                {
                    var result = token != null ? token.ToObject<T>() : default;
                    tcs.SetResult(result);
                }
                return true;
            }
            return false;
        }

        private static void EnsureCallbackReceiver()
        {
            if (_callbackReceiver == null)
            {
                GameObject go = new GameObject("WavedashCallbackReceiver");
                _callbackReceiver = go.AddComponent<WavedashCallbackReceiver>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
        }

        private class WavedashCallbackReceiver : MonoBehaviour
        {
            public void LobbyJoined(string dataJson)
            {
                if (_debug) Debug.Log("LobbyJoined Signal Received from WavedashJS: " + dataJson);
                TryInvoke(dataJson, OnLobbyJoined);
            }

            public void LobbyLeft(string dataJson)
            {
                if (_debug) Debug.Log("LobbyLeft Signal Received from WavedashJS: " + dataJson);
                TryInvoke(dataJson, OnLobbyLeft);
            }

            public void LobbyMessage(string dataJson)
            {
                if (_debug) Debug.Log("LobbyMessage Signal Received from WavedashJS: " + dataJson);
                TryInvoke(dataJson, OnLobbyMessage);
            }

            public void LobbyDataUpdated(string dataJson)
            {
                if (_debug) Debug.Log("LobbyDataUpdated Signal Received from WavedashJS: " + dataJson);
            }

            public void LobbyUsersUpdated(string dataJson)
            {
                if (_debug) Debug.Log("LobbyUsersUpdated Signal Received from WavedashJS: " + dataJson);
            }

            public void P2PConnectionEstablished(string dataJson)
            {
                if (_debug) Debug.Log("P2PConnectionEstablished Signal Received from WavedashJS: " + dataJson);
                TryInvoke(dataJson, OnP2PConnectionEstablished);
            }

            public void P2PConnectionFailed(string dataJson)
            {
                if (_debug) Debug.Log("P2PConnectionFailed Signal Received from WavedashJS: " + dataJson);
                TryInvoke(dataJson, OnP2PConnectionFailed);
            }

            public void P2PPeerDisconnected(string dataJson)
            {
                if (_debug) Debug.Log("P2PPeerDisconnected Signal Received from WavedashJS: " + dataJson);
                TryInvoke(dataJson, OnP2PPeerDisconnected);
            }

            private void TryInvoke(string json, Action<Dictionary<string, object>> action)
            {
                try
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    action?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse lobby data: {e.Message}");
                }
            }
        }
    }
}
