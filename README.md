# Wavedash SDK for Unity

A Unity package for WebGL builds to interact with the WavedashJS SDK in the browser. Provides seamless JavaScript interop for Unity games exported to WebGL/WASM.

## Installation

1. In Unity, open Window → Package Manager
2. Click the + button → Install package from git URL...
3. Enter https://github.com/wvdsh/sdk-unity.git and install

## Testing in the Editor

Outside a WebGL build (Play Mode in the editor, desktop players) every SDK call is served by `SDK.Mock`, an in-memory stand-in for the Wavedash service. You are always the player `TEST_USER` (`test_user`): lobbies you create have you as host, leaderboards hold your single entry, `TriggerPaywall` grants the content, and stats, achievements, cloud saves and UGC persist under `Application.persistentDataPath/WavedashMockRemote` between sessions. Awaited calls resolve on a later frame and events arrive through the same receiver as in a WebGL build, so ordering matches production.

To shape a scenario from a test script, seed the public state on `SDK.Mock` (`Friends`, `Entitlements`, `Lobbies`, `Leaderboards`, `LaunchParams`, `PaywallAccepts`), queue inbound P2P traffic with `SDK.Mock.ReceiveP2PMessage(...)`, or fire any event at your handlers with `SDK.Mock.Emit("LobbyKicked", payload)`. `SDK.Mock` is compiled out of WebGL player builds entirely, so wrap those references in `#if UNITY_EDITOR`.

## Read the Docs
https://docs.wavedash.com/