# Wavedash SDK for Unity

A Unity package for WebGL builds to interact with the WavedashJS SDK in the browser. Provides seamless JavaScript interop for Unity games exported to WebGL/WASM.

## Installation

1. In Unity, open Window → Package Manager
2. Click the + button → Install package from git URL...
3. Enter https://github.com/wvdsh/sdk-unity.git and install

## Paid Content (Experimental)

> ⚠️ **Experimental / early access.** These methods map to the `_EXPERIMENTAL` paid-content
> API on WavedashJS and may change. Configure the content identifier, price, and modal copy
> on the Wavedash dashboard under **Monetization → Paid Content**.

```csharp
using Wavedash;

// Check whether the player already owns the content.
bool owned = await SDK.IsEntitled("full-version");

// List every entitlement the player owns.
List<string> entitlements = await SDK.GetEntitlements();

// Prompt the player to purchase. The Wavedash parent site fetches the offer and
// renders the modal. Returns true if the player owns the content afterwards
// (already owned, or just purchased), false if they dismissed the modal.
if (await SDK.TriggerPaywall("full-version"))
{
    // unlock the content
}
```

| Method | Returns | Description |
| --- | --- | --- |
| `IsEntitled(contentId)` | `Task<bool>` | Whether the user owns `contentId`. |
| `GetEntitlements()` | `Task<List<string>>` | Every content identifier the user owns. |
| `TriggerPaywall(contentIdentifier)` | `Task<bool>` | Shows the purchase modal; resolves true if owned afterward. Short-circuits when already entitled. |

## Read the Docs
https://docs.wavedash.com/