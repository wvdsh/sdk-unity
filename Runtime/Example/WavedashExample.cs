using UnityEngine;
using Wavedash;
using System.Collections.Generic;
using Newtonsoft.Json;

public class WavedashExample : MonoBehaviour
{
    void Awake()
    {
        Wavedash.SDK.OnLobbyJoined += HandleLobbyJoined;
        Wavedash.SDK.OnReady += HandleWavedashReady;
    }

    void Start()
    {
        // Initialize Wavedash with your configuration
        var config = new Dictionary<string, object>
        {
            { "gameId", "hello-world" },
            { "debug", true }
        };
        
        // Simple global call
        Wavedash.SDK.Init(config);
    }

    void OnDestroy()
    {
        // Unsubscribe from events when the GameObject is destroyed
        Wavedash.SDK.OnLobbyJoined -= HandleLobbyJoined;
        Wavedash.SDK.OnReady -= HandleWavedashReady;
    }

    void HandleLobbyJoined(Dictionary<string, object> lobbyData)
    {
        string lobbyId = lobbyData["id"].ToString();
        string lobbyName = lobbyData["name"].ToString();
        Debug.Log($"Joined lobby: {lobbyId}");
        Debug.Log($"Lobby name: {lobbyName}");
        Debug.Log($"Lobby data: {JsonConvert.SerializeObject(lobbyData)}");
    }

    void HandleWavedashReady()
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