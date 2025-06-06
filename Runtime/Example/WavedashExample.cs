using UnityEngine;
using Wavedash;
using System.Collections.Generic;
using Newtonsoft.Json;

public class WavedashExample : MonoBehaviour
{
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
        
        // Subscribe to events after initialization
        Wavedash.SDK.OnLobbyJoined += HandleLobbyJoined;
        
        // Check if ready after a short delay
        Invoke(nameof(CheckWavedashStatus), 0.5f);
    }

    void OnDestroy() {
        // Unsubscribe from events when the GameObject is destroyed
        Wavedash.SDK.OnLobbyJoined -= HandleLobbyJoined;
    }

    void HandleLobbyJoined(Dictionary<string, object> lobbyData) {
        Debug.Log("Custom LobbyJoined callback triggered");
        string lobbyId = lobbyData["id"].ToString();
        string lobbyName = lobbyData["name"].ToString();
        Debug.Log($"Joined lobby: {lobbyId}");
        Debug.Log($"Lobby name: {lobbyName}");
        Debug.Log($"Lobby data: {JsonConvert.SerializeObject(lobbyData)}");
    }

    void CheckWavedashStatus()
    {
        // Simple global call to check status
        bool isReady = Wavedash.SDK.IsReady();
        
        if (isReady)
        {
            Debug.Log("WavedashJS SDK is ready!");
            
            // Get user data with simple global call
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
        else
        {
            Debug.Log("WavedashJS SDK is not ready yet");
            // Retry after a delay
            Invoke(nameof(CheckWavedashStatus), 1f);
        }
    }
    
    // Example of checking status on demand
    public void CheckStatus()
    {
        CheckWavedashStatus();
    }
} 