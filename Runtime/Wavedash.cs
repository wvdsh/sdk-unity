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
    /// Main entry point for the Wavedash SDK
    /// Usage: await SDK.GetLeaderboard("name"); etc.
    /// </summary>
    public static class SDK
    {
        // Events that JavaScript can trigger
        public static event Action<Dictionary<string, object>> OnLobbyJoined;
        public static event Action<Dictionary<string, object>> OnLobbyLeft;
        public static event Action<Dictionary<string, object>> OnLobbyMessage;

        // Internal callback receiver instance
        private static WavedashCallbackReceiver _callbackReceiver;
        private static bool _debug = false;

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
        private static extern void WavedashJS_BindFS();

        [DllImport("__Internal")]
        private static extern int WavedashJS_IsReady();

        [DllImport("__Internal")]
        private static extern string WavedashJS_GetUser();

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
            WavedashJS_BindFS();
#else
            Debug.LogWarning("Wavedash.Init() is only supported in WebGL builds");
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

        public static Dictionary<string, object> GetUser()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            string userJson = WavedashJS_GetUser();
            if (!string.IsNullOrEmpty(userJson))
            {
                try
                {
                    return JsonConvert.DeserializeObject<Dictionary<string, object>>(userJson);
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
            public void OnLobbyJoinedCallback(string dataJson)
            {
                if (_debug) Debug.Log("OnLobbyJoined: " + dataJson);
                TryInvoke(dataJson, OnLobbyJoined);
            }

            public void OnLobbyLeftCallback(string dataJson)
            {
                if (_debug) Debug.Log("OnLobbyLeft: " + dataJson);
                TryInvoke(dataJson, OnLobbyLeft);
            }

            public void OnLobbyMessageCallback(string dataJson)
            {
                if (_debug) Debug.Log("OnLobbyMessage: " + dataJson);
                TryInvoke(dataJson, OnLobbyMessage);
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
