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
Wavedash.Init(new Dictionary<string, object>
{
    { "gameId", "your-game-id" },
    { "debug", true }
});
```

### Check if Ready & Get User
```csharp
if (Wavedash.IsReady()) {
    var user = Wavedash.GetUser();
    Debug.Log($"User: {user}");
}
```

### Handle Backend Events
```csharp
// Subscribe to events
Wavedash.OnLobbyJoined += (lobbyData) => {
    string lobbyName = lobbyData["name"].ToString();
    Debug.Log($"Joined lobby: {lobbyName}");
};
```

## JavaScript → Unity Callbacks

The SDK supports receiving events from the Wavedash backend through WavedashJS:

```javascript

window.WavedashJS = {
  // Unity SDK automatically registers itself
  setUnityInstance: function(unityInstance, gameObjectName) {
    this._unityInstance = unityInstance;
    this._unityGameObjectName = gameObjectName;
  },
  
  // Wavedash backend calls this to notify Unity
  notifyLobbyJoined: function(lobbyData) {
    this._unityInstance.SendMessage(
      this._unityGameObjectName,
      'OnLobbyJoinedCallback',
      JSON.stringify(lobbyData)
    );
  }

  // ...
};
```

## Available Events

- `Wavedash.OnLobbyJoined` - Player joined a lobby
- `Wavedash.OnLobbyLeft` - Player left a lobby

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
- WavedashJS SDK loaded in the web page that runs the game (Wavedash.gg handles this)

## Testing

See the example implementations in `/Runtime/Example/` and `/Docs/` for complete working examples.