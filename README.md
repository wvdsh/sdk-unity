# Wavedash SDK for Unity

A Unity package for WebGL builds to interact with the WavedashJS SDK in the browser. Provides seamless JavaScript interop for Unity games exported to WebGL/WASM.

## Installation

1. In Unity, open Window → Package Manager
2. Click the + button → Install package from git URL...
3. Enter https://github.com/wvdsh/sdk-unity.git and install

## Quick Start

### Initialize the SDK
```csharp
using Wavedash;

// Initialize once at game start
Wavedash.SDK.Init(new Dictionary<string, object>
{
    { "debug", true }
});
```

### Check if Ready & Get User
```csharp
if (Wavedash.SDK.IsReady()) {
    var user = Wavedash.SDK.GetUser();
    Debug.Log($"User ID: {user["id"]}");
}
```

## Events

The SDK provides events for lobby and P2P networking:

```csharp
void Awake()
{
    // Lobby events
    Wavedash.SDK.OnLobbyJoined += HandleLobbyJoined;
    Wavedash.SDK.OnLobbyLeft += HandleLobbyLeft;
    Wavedash.SDK.OnLobbyMessage += HandleLobbyMessage;

    // P2P events
    Wavedash.SDK.OnP2PConnectionEstablished += HandleP2PConnected;
    Wavedash.SDK.OnP2PConnectionFailed += HandleP2PFailed;
    Wavedash.SDK.OnP2PPeerDisconnected += HandlePeerDisconnected;
}

void OnDestroy()
{
    Wavedash.SDK.OnLobbyJoined -= HandleLobbyJoined;
    Wavedash.SDK.OnLobbyLeft -= HandleLobbyLeft;
    Wavedash.SDK.OnLobbyMessage -= HandleLobbyMessage;
    Wavedash.SDK.OnP2PConnectionEstablished -= HandleP2PConnected;
    Wavedash.SDK.OnP2PConnectionFailed -= HandleP2PFailed;
    Wavedash.SDK.OnP2PPeerDisconnected -= HandlePeerDisconnected;
}

void HandleLobbyJoined(Dictionary<string, object> lobbyData) {
    string lobbyId = lobbyData["data"].ToString();
    Debug.Log($"Joined lobby: {lobbyId}");
}
```

## Lobbies

```csharp
// Create a lobby (lobbyType: 0=private, 1=friends, 2=public)
string lobbyId = await Wavedash.SDK.CreateLobby(lobbyType: 2, maxPlayers: 4);

// Join an existing lobby
await Wavedash.SDK.JoinLobby(lobbyId);

// Leave a lobby
await Wavedash.SDK.LeaveLobby(lobbyId);

// List available public lobbies
var lobbies = await Wavedash.SDK.ListAvailableLobbies();

// Get the host of a lobby
string hostId = Wavedash.SDK.GetLobbyHostId(lobbyId);
```

## P2P Messaging

Send messages directly between players in a lobby:

```csharp
// Broadcast to all peers
byte[] payload = System.Text.Encoding.UTF8.GetBytes("Hello everyone!");
Wavedash.SDK.BroadcastP2PMessage(payload, channel: 0, reliable: true);

// Send to a specific player
Wavedash.SDK.SendP2PMessage(targetUserId, payload, channel: 0, reliable: true);

// Receive messages - call this in Update()
var messages = new List<P2PMessage>();
int count = Wavedash.SDK.DrainP2PChannel(channel: 0, messages);
foreach (var msg in messages) {
    Debug.Log($"From {msg.SenderId}: {System.Text.Encoding.UTF8.GetString(msg.Payload)}");
}
```

The `P2PMessage` struct contains:
- `SenderId` - The user ID of the sender
- `Channel` - The channel the message was sent on
- `Payload` - The raw byte data

## Leaderboards

```csharp
// Get or create a leaderboard
var leaderboard = await Wavedash.SDK.GetOrCreateLeaderboard(
    "HighScores",
    sortMethod: 0,  // 0=descending, 1=ascending
    displayType: 0  // 0=numeric, 1=time
);

// Get an existing leaderboard
var leaderboard = await Wavedash.SDK.GetLeaderboard("HighScores");

// Upload a score
await Wavedash.SDK.UploadLeaderboardScore(
    leaderboardId,
    score: 1000,
    keepBest: true,
    ugcId: null  // Optional: attach UGC to the score
);

// Get entries
var entries = await Wavedash.SDK.ListLeaderboardEntries(leaderboardId, offset: 0, limit: 10);
var aroundMe = await Wavedash.SDK.ListLeaderboardEntriesAroundUser(leaderboardId, countAhead: 5, countBehind: 5);
var myEntries = await Wavedash.SDK.GetMyLeaderboardEntries(leaderboardId);

// Get total entry count
int count = Wavedash.SDK.GetLeaderboardEntryCount(leaderboardId);
```

## Remote File Storage

Save and load files from the cloud. Paths should use `Application.persistentDataPath`:

```csharp
string savePath = $"{Application.persistentDataPath}/saves/game.sav";

// Upload a file
await Wavedash.SDK.UploadRemoteFile(savePath);

// Download a file
await Wavedash.SDK.DownloadRemoteFile(savePath);

// Download an entire directory
await Wavedash.SDK.DownloadRemoteDirectory($"{Application.persistentDataPath}/saves");

// List files in a remote directory
var files = await Wavedash.SDK.ListRemoteDirectory($"{Application.persistentDataPath}/saves");
```

## User Generated Content (UGC)

```csharp
// Create a UGC item
string ugcId = await Wavedash.SDK.CreateUGCItem(
    ugcType: 0,
    title: "My Level",
    description: "A custom level",
    visibility: WavedashConstants.UGCVisibility.PUBLIC,
    filePath: $"{Application.persistentDataPath}/levels/mylevel.dat"
);

// Download a UGC item
await Wavedash.SDK.DownloadUGCItem(ugcId, $"{Application.persistentDataPath}/downloads/level.dat");
```

## Project Structure

- `/Runtime/` - C# scripts
  - `Wavedash.cs` - Main SDK class
  - `/Example/` - Example scripts
- `/Plugins/WebGL/` - JavaScript interop
  - `WavedashPlugin.jslib` - JS bridge functions
- `/Docs/` - Additional documentation

## Requirements

- Unity 6000.0 or higher
- WebGL build target
- WavedashJS SDK loaded in the hosting page (wavedash.com handles this)