using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;

namespace Wavedash
{
    /// <summary>
    /// Main entry point for the Wavedash SDK
    /// Usage: Wavedash.Init(config); GetUser(); GetLeaderboard("highscores").Then(...);
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
        internal static readonly Dictionary<string, Action<Dictionary<string, object>>> _pendingCallbacks = new ();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WavedashJS_Init(string configJson);

        [DllImport("__Internal")]
        private static extern int WavedashJS_IsReady();

        [DllImport("__Internal")]
        private static extern string WavedashJS_GetUser();

        [DllImport("__Internal")]
        private static extern void WavedashJS_RegisterUnityCallbacks(string gameObjectName);

        [DllImport("__Internal")]
        private static extern void WavedashJS_GetLeaderboard(
            string leaderboardName,
            int sortMethod,
            int displayType,
            string gameObjectName,
            string methodName,
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

            // Register Unity callbacks with JavaScript
            WavedashJS_RegisterUnityCallbacks(_callbackReceiver.gameObject.name);

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
        public static LeaderboardRequest GetLeaderboard(string leaderboardName, int sortMethod, int displayType)
        {
            EnsureCallbackReceiver();
            string requestId = Guid.NewGuid().ToString("N");

#if UNITY_WEBGL && !UNITY_EDITOR
            WavedashJS_GetLeaderboard(
                leaderboardName,
                sortMethod,
                displayType,
                _callbackReceiver.gameObject.name,
                "OnLeaderboardCallback",
                requestId
            );
#else
            // In editor/unsupported builds, immediately invoke callbacks with null
            _pendingCallbacks[requestId] = data => { };
#endif

            return new LeaderboardRequest(requestId);
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
        /// Request handle that supports .Then() chaining.
        /// </summary>
        public class LeaderboardRequest
        {
            private readonly string _requestId;

            internal LeaderboardRequest(string requestId)
            {
                _requestId = requestId;
            }

            public LeaderboardRequest Then(Action<Dictionary<string, object>> continuation)
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
        /// Internal class to receive callbacks from JavaScript
        /// </summary>
        private class WavedashCallbackReceiver : MonoBehaviour
        {
            // Called by JavaScript via SendMessage
            public void OnLobbyJoinedCallback(string dataJson)
            {
                if (_debug)
                {
                    Debug.Log("OnLobbyJoinedCallback triggered with: " + dataJson);
                }
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
                {
                    Debug.Log("OnLobbyLeftCallback triggered with: " + dataJson);
                }
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
                {
                    Debug.Log("OnLobbyMessageCallback triggered with: " + dataJson);
                }
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

            public void OnLeaderboardCallback(string dataJson)
            {
                if (_debug)
                    Debug.Log("OnLeaderboardCallback triggered with: " + dataJson);

                try
                {
                    var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);

                    if (root != null &&
                        root.TryGetValue("requestId", out var reqObj) &&
                        reqObj is string requestId &&
                        _pendingCallbacks.TryGetValue(requestId, out var cb))
                    {
                        _pendingCallbacks.Remove(requestId);

                        // Error path
                        if (root.TryGetValue("error", out var err))
                        {
                            cb?.Invoke(new Dictionary<string, object> { { "error", err } });
                            return;
                        }

                        // Success path: unwrap response.data
                        if (root.TryGetValue("response", out var respObj) &&
                            respObj is Newtonsoft.Json.Linq.JObject respToken)
                        {
                            var resp = respToken.ToObject<Dictionary<string, object>>();

                            if (resp != null && resp.TryGetValue("data", out var dataField))
                            {
                                if (dataField is Newtonsoft.Json.Linq.JToken dataToken)
                                {
                                    var unwrapped = dataToken.ToObject<Dictionary<string, object>>();
                                    cb?.Invoke(unwrapped);
                                    return;
                                }
                            }

                            // Fallback: give them the whole response
                            cb?.Invoke(resp);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse leaderboard data: {e.Message}");
                }
            }
        }
    }
}
