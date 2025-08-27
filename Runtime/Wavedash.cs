using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;
using Newtonsoft.Json.Linq;

namespace Wavedash
{
    /// <summary>
    /// Main entry point for the Wavedash SDK
    /// Usage: Wavedash.Init(config); GetUser(); etc.;
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

        // Pending callbacks by requestId
        internal static readonly Dictionary<string, Action<object>> _pendingCallbacks = new();

        // jslib -> Unity callbacks
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void LeaderboardCallback(string responseJson);
        private static LeaderboardCallback _lbCallback; // keep alive

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
#endif

        /// <summary>
        /// Initialize the Wavedash SDK
        /// </summary>
        public static void Init(Dictionary<string, object> config)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Ensure callback receiver exists
            EnsureCallbackReceiver();

            // Set debug mode
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
        /// Request leaderboard data
        /// </summary>
        public static LeaderboardRequest GetOrCreateLeaderboard(string leaderboardName, int sortMethod, int displayType)
        {
            string requestId = Guid.NewGuid().ToString("N");

            // Reserve slot in pending callbacks
            _pendingCallbacks[requestId] = null;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_lbCallback == null)
            {
                _lbCallback = LeaderboardCallbackImpl; // assign once
            }

            IntPtr fnPtr = Marshal.GetFunctionPointerForDelegate(_lbCallback);

            WavedashJS_GetOrCreateLeaderboard(
                leaderboardName,
                sortMethod,
                displayType,
                fnPtr,
                requestId
            );
#endif

            return new LeaderboardRequest(requestId);
        }

        /// <summary>
        /// Request leaderboard data
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(LeaderboardCallback))]
        private static void LeaderboardCallbackImpl(string responseJson)
        {
            try
            {
                if (string.IsNullOrEmpty(responseJson))
                {
                    Debug.LogError("Leaderboard callback received empty JSON.");
                    return;
                }

                var root = JObject.Parse(responseJson);

                // Must have requestId
                var reqId = root.Value<string>("requestId");
                if (string.IsNullOrEmpty(reqId))
                {
                    Debug.LogError("Leaderboard response missing 'requestId'.");
                    return;
                }

                // Must have callback
                if (!_pendingCallbacks.TryGetValue(reqId, out var cb))
                {
                    Debug.LogWarning($"No pending callback for requestId {reqId}");
                    return;
                }
                _pendingCallbacks.Remove(reqId);

                // Must have response string
                var respToken = root["response"];
                if (respToken?.Type != JTokenType.String)
                {
                    Debug.LogError($"Leaderboard response {reqId} missing or invalid 'response' field.");
                    cb?.Invoke(null);
                    return;
                }

                var resp = JObject.Parse(respToken.Value<string>());

                // Check success
                if (!(resp.Value<bool?>("success") ?? false))
                {
                    Debug.LogWarning($"Leaderboard request {reqId} failed: {resp.Value<string>("message")}");
                }

                // Invoke callback with data
                var dataObj = resp["data"] as JObject;
                cb?.Invoke(dataObj?.ToObject<Dictionary<string, object>>());
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse leaderboard data: {e.Message}");
            }
        }

        /// <summary>
        /// Request handle that supports .Then() chaining.
        /// </summary>
        public class LeaderboardRequest
        {
            private readonly string _requestId;

            internal LeaderboardRequest(string requestId)
            {
                _requestId = requestId;
            }

            public LeaderboardRequest Then(Action<object> continuation)
            {
                if (_pendingCallbacks.TryGetValue(_requestId, out var existing))
                {
                    _pendingCallbacks[_requestId] = data =>
                    {
                        existing?.Invoke(data);
                        continuation?.Invoke(data);
                    };
                }
                else
                {
                    _pendingCallbacks[_requestId] = continuation;
                }
                return this;
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
        /// Internal class to receive other callbacks from JavaScript
        /// </summary>
        private class WavedashCallbackReceiver : MonoBehaviour
        {
            // Called by JavaScript via SendMessage
            public void OnLobbyJoinedCallback(string dataJson)
            {
                if (_debug)
                    Debug.Log("OnLobbyJoinedCallback triggered with: " + dataJson);

                try
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                    OnLobbyJoined?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse lobby joined data: {e.Message}");
                }
            }

            public void OnLobbyLeftCallback(string dataJson)
            {
                if (_debug)
                    Debug.Log("OnLobbyLeftCallback triggered with: " + dataJson);

                try
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                    OnLobbyLeft?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse lobby left data: {e.Message}");
                }
            }

            public void OnLobbyMessageCallback(string dataJson)
            {
                if (_debug)
                    Debug.Log("OnLobbyMessageCallback triggered with: " + dataJson);

                try
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                    OnLobbyMessage?.Invoke(data);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse lobby message data: {e.Message}");
                }
            }
        }
    }
}
