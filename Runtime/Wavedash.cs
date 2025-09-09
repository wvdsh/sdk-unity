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

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WavedashJS_Init(string configJson);

        [DllImport("__Internal")]
        private static extern int WavedashJS_IsReady();

        [DllImport("__Internal")]
        private static extern string WavedashJS_GetUser();

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
#endif

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
        /// Check if the SDK is ready
        /// </summary>
        public static bool IsReady()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return WavedashJS_IsReady() == 1;
#else
            return false;
#endif
        }

        /// <summary>
        /// Get the current user data
        /// </summary>
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

        /// <summary>
        /// Request leaderboard data if it exists, or create it if it doesn't
        /// </summary>
        public static Task<Dictionary<string, object>> GetOrCreateLeaderboard(
            string leaderboardName, int sortMethod, int displayType)
        {
            string requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<Dictionary<string, object>>();
            _pending[requestId] = tcs;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_callbackDelegate == null)
                _callbackDelegate = LeaderboardCallbackImpl;

            IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(_callbackDelegate);

            WavedashJS_GetOrCreateLeaderboard(
                leaderboardName,
                sortMethod,
                displayType,
                fnPtr,
                requestId
            );
#else
            tcs.SetResult(new Dictionary<string, object> { { "dummy", "editor" } });
#endif

            return tcs.Task;
        }

        /// <summary>
        /// Request leaderboard data
        /// </summary>
        public static Task<Dictionary<string, object>> GetLeaderboard(string leaderboardName)
        {
            string requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<Dictionary<string, object>>();
            _pending[requestId] = tcs;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_callbackDelegate == null)
                _callbackDelegate = LeaderboardCallbackImpl;

            IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(_callbackDelegate);

            WavedashJS_GetLeaderboard(
                leaderboardName,
                fnPtr,
                requestId
            );
#else
            tcs.SetResult(new Dictionary<string, object> { { "dummy", "editor" } });
#endif

            return tcs.Task;
        }

        /// <summary>
        /// Callback from JS side
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(JsCallback))]
        private static void LeaderboardCallbackImpl(string responseJson)
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

                var respToken = root["response"];
                if (respToken == null || respToken.Type != JTokenType.Object)
                {
                    (tcsObj as TaskCompletionSource<Dictionary<string, object>>)
                        ?.SetException(new Exception("Invalid response format"));
                    return;
                }

                var resp = (JObject)respToken;
                var success = resp.Value<bool>("success");
                if (!success)
                {
                    var message = resp.Value<string>("message");
                    (tcsObj as TaskCompletionSource<Dictionary<string, object>>)
                        ?.SetException(new Exception($"Request failed: {message}"));
                    return;
                }

                var dataToken = resp["data"];
                var dict = dataToken?.ToObject<Dictionary<string, object>>();
                (tcsObj as TaskCompletionSource<Dictionary<string, object>>)?.SetResult(dict);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse response: {e.Message}");
            }
        }

        /// <summary>
        /// Ensures the callback receiver GameObject exists
        /// </summary>
        private static void EnsureCallbackReceiver()
        {
            if (_callbackReceiver == null)
            {
                GameObject go = new GameObject("WavedashCallbackReceiver");
                _callbackReceiver = go.AddComponent<WavedashCallbackReceiver>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }
        }

        /// <summary>
        /// Internal class to receive lobby events from JavaScript
        /// </summary>
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
