using System.Runtime.InteropServices;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using System;

namespace Wavedash
{
    /// <summary>
    /// Main entry point for the Wavedash SDK
    /// Usage: Wavedash.SDK.Init(config); Wavedash.SDK.GetUser(); etc.
    /// </summary>
    public static class SDK
    {
        // Events that JavaScript can trigger
        public static event Action OnReady;
        public static event Action<Dictionary<string, object>> OnLobbyJoined;
        public static event Action<Dictionary<string, object>> OnLobbyLeft;
        
        // Internal callback receiver instance
        private static WavedashCallbackReceiver _callbackReceiver;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void WavedashJS_Init(string configJson);
        
        [DllImport("__Internal")]
        private static extern int WavedashJS_IsReady();
        
        [DllImport("__Internal")]
        private static extern string WavedashJS_GetUser();
        
        [DllImport("__Internal")]
        private static extern void WavedashJS_RegisterUnityCallbacks(string gameObjectName);
#endif

        /// <summary>
        /// Initialize the Wavedash SDK
        /// </summary>
        public static void Init(Dictionary<string, object> config)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Ensure callback receiver exists
            EnsureCallbackReceiver();
            
            string configJson = JsonConvert.SerializeObject(config);
            WavedashJS_Init(configJson);
            
            // Register Unity callbacks with JavaScript
            WavedashJS_RegisterUnityCallbacks(_callbackReceiver.gameObject.name);
#else
            Debug.LogWarning("Wavedash.SDK.Init() is only supported in WebGL builds");
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
        /// Internal class to receive callbacks from JavaScript
        /// </summary>
        private class WavedashCallbackReceiver : MonoBehaviour
        {
            // Called by JavaScript via SendMessage
            public void OnLobbyJoinedCallback(string dataJson)
            {
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

            public void OnReadyCallback()
            {
                OnReady?.Invoke();
            }
        }
    }
}  