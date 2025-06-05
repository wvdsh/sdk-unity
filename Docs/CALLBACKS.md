# Wavedash SDK - JavaScript to Unity Callbacks

## Overview

The Wavedash SDK supports bidirectional communication between JavaScript and Unity. WavedashJS can notify Unity when backend events occur (like lobby joins) without exposing the Unity instance globally.

## Available Events

```csharp
Wavedash.OnLobbyJoined    // Called when player joins a lobby
Wavedash.OnLobbyLeft      // Called when player leaves a lobby
```

## Unity Usage

### Subscribe to Events

```csharp
using Wavedash;

void Start() {
    Wavedash.SDK.OnLobbyJoined += HandleLobbyJoined;
}

void OnDestroy() {
    Wavedash.SDK.OnLobbyJoined -= HandleLobbyJoined;
}

void HandleLobbyJoined(Dictionary<string, object> lobbyData) {
    string lobbyId = lobbyData["lobbyId"].ToString();
    Debug.Log($"Joined lobby: {lobbyId}");
}
```

## How It Works

1. Unity SDK calls `Wavedash.Init()`
2. Unity creates a hidden callback receiver GameObject
3. Unity passes its instance to `WavedashJS.setUnityInstance()`
4. WavedashJS stores the reference internally (not on window)
5. Backend events trigger WavedashJS methods
6. WavedashJS uses the stored instance to send messages to Unity

## Data Format

All callback data is passed as JSON and automatically parsed into Dictionary<string, object>:

```javascript
// JavaScript - Send complex data
WavedashJS.notifyLobbyJoined({
  lobbyId: "123",
  name: "Battle Arena",
  settings: {
    gameMode: "deathmatch",
    timeLimit: 300
  },
  players: ["player1", "player2"]
});
```

```csharp
// Unity - Receive and parse
void HandleLobbyJoined(Dictionary<string, object> data) {
    string lobbyId = data["lobbyId"].ToString();
    
    // Access nested data
    if (data["settings"] is Dictionary<string, object> settings) {
        string gameMode = settings["gameMode"].ToString();
    }
    
    // Access arrays
    if (data["players"] is List<object> players) {
        foreach (var player in players) {
            Debug.Log(player.ToString());
        }
    }
}
```

## Security Benefits

- ✅ Unity instance is never exposed on `window`
- ✅ Only WavedashJS has access to trigger Unity events
- ✅ Clean separation between Unity and web page
- ✅ No global variables that could be manipulated

## Testing Callbacks

In the browser console:
```javascript
// Test a callback
WavedashJS.notifyLobbyJoined({
  lobbyId: "test-123",
  name: "Test Lobby",
  playerCount: 1,
  maxPlayers: 4
});
```

## Best Practices

1. **Always unsubscribe** from events in OnDisable/OnDestroy
2. **Handle null data** gracefully in callbacks
3. **Use try-catch** when parsing data
4. **Check key existence** before accessing dictionary values
5. **Log events** for debugging multiplayer issues 