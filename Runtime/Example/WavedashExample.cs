using UnityEngine;
using Wavedash;
using System.Collections.Generic;
using Newtonsoft.Json;

public class WavedashExample : MonoBehaviour
{
    void Awake()
    {
        Wavedash.SDK.OnLobbyJoined += HandleLobbyJoined;
        Wavedash.SDK.OnLobbyLeft += HandleLobbyLeft;
    }

    void Start()
    {
        // Initialize Wavedash with your configuration
        var config = new Dictionary<string, object>
        {
            { "debug", true }
        };
        
        // Simple global call
        Wavedash.SDK.Init(config);

        // Init is synchronous, SDK should be ready immediately
        if (Wavedash.SDK.IsReady())
        {
            Debug.Log("WavedashJS SDK is ready!");
            Dictionary<string, object> user = Wavedash.SDK.GetUser();
            if (user != null)
            {
                Debug.Log($"User data retrieved: {JsonConvert.SerializeObject(user)}");
            }
            else
            {
                Debug.Log("No user data available");
            }
        }
        
    }

    void OnDestroy()
    {
        // Unsubscribe from events when the GameObject is destroyed
        Wavedash.SDK.OnLobbyJoined -= HandleLobbyJoined;
        Wavedash.SDK.OnLobbyLeft -= HandleLobbyLeft;
    }

    void HandleLobbyJoined(Dictionary<string, object> lobbyData)
    {
        string lobbyId = lobbyData["data"].ToString();
        Debug.Log($"Joined lobby: {lobbyId}");
        Debug.Log($"Lobby data: {JsonConvert.SerializeObject(lobbyData)}");
    }

    void HandleLobbyLeft(Dictionary<string, object> lobbyData)
    {
        string lobbyId = lobbyData["data"].ToString();
        Debug.Log($"Left lobby: {lobbyId}");
        Debug.Log($"Lobby data: {JsonConvert.SerializeObject(lobbyData)}");
    }
} 