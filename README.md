# Wavedash SDK for Unity

## Overview

The Wavedash SDK for Unity is a library that allows you to integrate Wavedash services in your Unity games. It provides seamless JavaScript interop for Unity games exported to WebGL/WASM to communicate with the Wavedash JS SDK.

# Wavedash Unity SDK

A Unity package for WebGL builds to interact with the WavedashJS SDK in the browser.

## Installation

1. In Unity, open Window → Package Manager
2. Click the + button → Add package from disk...
3. Select the `package.json` file from this repository

## Quick Start

### Initialize the SDK
```csharp
using Wavedash;

// Initialize once at game start
Wavedash.SDK.Init(new Dictionary<string, object>
{
    { "gameId", "your-game-id" },
    { "debug", true }
});
```

### Check if Ready & Get User
```csharp
if (Wavedash.SDK.IsReady()) {
    var user = Wavedash.SDK.GetUser();
    Debug.Log($"User: {user}");
}
```

### Handle Backend Events
```csharp
// Subscribe to events
void Awake()
{
  // Events can be subscribed to before or after initialization
  Wavedash.SDK.OnLobbyJoined += HandleLobbyJoined;
}

// Initialize SDK and get the current user
void Start()
{
  // Simple global call
  Wavedash.SDK.Init(new Dictionary<string, object>
  {
      { "gameId", "hello-world" },
      { "debug", true }
  });

  // Init is synchronous, SDK should be ready immediately
  if (Wavedash.SDK.IsReady()) {
    var user = Wavedash.SDK.GetUser();
    Debug.Log($"User: {user}");
  }
}

// Remove the callback on destroy to ensure proper cleanup when your component is destroyed
void OnDestroy()
{
  Wavedash.SDK.OnLobbyJoined -= HandleLobbyJoined;
}

void HandleLobbyJoined(Dictionary<string, object> lobbyData) {
    string lobbyId = lobbyData["id"].ToString();
    Debug.Log($"Joined lobby: {lobbyId}");
};
```

## JavaScript → Unity Callbacks

The SDK supports receiving events from the Wavedash backend through WavedashJS:

## Available Events

- `Wavedash.SDK.OnLobbyJoined` - Player joined a lobby
- `Wavedash.SDK.OnLobbyLeft` - Player left a lobby

## Project Structure

- `/Runtime/` - C# scripts
  - `Wavedash.cs` - Main SDK class
  - `/Example/` - Example scripts
- `/Plugins/WebGL/` - JavaScript interop
  - `WavedashPlugin.jslib` - JS bridge functions
- `/Docs/` - Additional documentation
  - `CALLBACKS.md` - Detailed callback documentation

## Requirements

- Unity 2021.3 or higher
- WebGL build target
- WavedashJS SDK must be loaded in the web page that hosts the game (wavedash.gg handles this)

## Testing

See the example implementations in `/Runtime/Example/` and `/Docs/` for complete working examples.