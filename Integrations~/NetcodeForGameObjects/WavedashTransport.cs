using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Transport layer for Unity Netcode for GameObjects that uses Wavedash SDK for P2P communication.
/// Assumes all members of a Wavedash Lobby have open WebRTC connections to each other.
/// Lobby host is the NGO Network host; other members are clients.
///
/// Order-independent: StartHost/StartClient can be called before or after OnLobbyJoined —
/// the transport connects lazily once lobby info and P2P connections are both available.
/// </summary>
public class WavedashTransport : NetworkTransport
{
    const int ReliableChannel = 0;
    const int UnreliableChannel = 1;

    string currentLobbyId;
    string hostUserId;

    readonly Dictionary<ulong, string> connectionToUserId = new Dictionary<ulong, string>();
    readonly Dictionary<string, ulong> userIdToConnection = new Dictionary<string, ulong>();
    ulong nextConnectionId = 1;

    bool _serverActive;
    bool _clientStarted;
    bool _clientConnected;

    struct PendingEvent
    {
        public NetworkEvent Type;
        public ulong ClientId;
    }
    readonly Queue<PendingEvent> _eventQueue = new Queue<PendingEvent>();

    readonly List<Wavedash.P2PMessage> messageBuffer = new List<Wavedash.P2PMessage>();
    int _messageIndex;
    int _drainPhase; // 0 = reliable next, 1 = unreliable next, 2 = done this cycle
    readonly HashSet<string> connectedPeers = new HashSet<string>();

    public override ulong ServerClientId => 0;

    public event Action<string> OnHostMigration;

    void Awake()
    {
        Wavedash.SDK.OnLobbyJoined += OnLobbyJoined;
        Wavedash.SDK.OnLobbyKicked += OnLobbyKicked;
        Wavedash.SDK.OnLobbyUsersUpdated += OnLobbyUsersUpdated;
        Wavedash.SDK.OnP2PConnectionEstablished += OnP2PConnectionEstablished;
        Wavedash.SDK.OnP2PPeerDisconnected += OnP2PPeerDisconnected;
        Wavedash.SDK.OnP2PConnectionFailed += OnP2PConnectionFailed;
    }

    void OnDestroy()
    {
        Wavedash.SDK.OnLobbyJoined -= OnLobbyJoined;
        Wavedash.SDK.OnLobbyKicked -= OnLobbyKicked;
        Wavedash.SDK.OnLobbyUsersUpdated -= OnLobbyUsersUpdated;
        Wavedash.SDK.OnP2PConnectionEstablished -= OnP2PConnectionEstablished;
        Wavedash.SDK.OnP2PPeerDisconnected -= OnP2PPeerDisconnected;
        Wavedash.SDK.OnP2PConnectionFailed -= OnP2PConnectionFailed;
    }

    // --- SDK Event Handlers ---

    void OnLobbyJoined(Dictionary<string, object> data)
    {
        currentLobbyId = data["lobbyId"].ToString();
        hostUserId = data["hostId"].ToString();
    }

    void OnLobbyKicked(Dictionary<string, object> data)
    {
        currentLobbyId = null;
        hostUserId = null;
        DisconnectAllPeers();
    }

    void OnLobbyUsersUpdated(Dictionary<string, object> data)
    {
        if (currentLobbyId == null) return;

        string previousHost = hostUserId;
        hostUserId = Wavedash.SDK.GetLobbyHostId(currentLobbyId);

        if (previousHost != hostUserId)
        {
            OnHostMigration?.Invoke(hostUserId);
        }
    }

    void OnP2PConnectionEstablished(Dictionary<string, object> data)
    {
        string userId = data["userId"].ToString();
        connectedPeers.Add(userId);

        if (_serverActive && !userIdToConnection.ContainsKey(userId))
        {
            ulong connId = nextConnectionId++;
            connectionToUserId[connId] = userId;
            userIdToConnection[userId] = connId;
            Debug.Log($"[WavedashTransport] Server: assigned connId {connId} to {userId}");
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Connect,
                ClientId = connId
            });
        }

        TryDeferredConnect();
    }

    void OnP2PPeerDisconnected(Dictionary<string, object> data)
    {
        string userId = data["userId"].ToString();
        DisconnectPeer(userId);
    }

    void OnP2PConnectionFailed(Dictionary<string, object> data)
    {
        string userId = data.ContainsKey("userId") ? data["userId"].ToString() : null;
        string reason = data.ContainsKey("reason") ? data["reason"].ToString() : "unknown";
        Debug.LogWarning($"[WavedashTransport] P2P connection failed for {userId}: {reason}");
        if (userId != null)
            DisconnectPeer(userId);
    }

    /// <summary>
    /// Idempotent: removes a peer and enqueues NGO disconnect events.
    /// Safe to call multiple times for the same userId (e.g. from both
    /// OnP2PConnectionFailed and OnP2PPeerDisconnected).
    /// </summary>
    void DisconnectPeer(string userId)
    {
        connectedPeers.Remove(userId);

        if (_serverActive && userIdToConnection.TryGetValue(userId, out ulong connId))
        {
            connectionToUserId.Remove(connId);
            userIdToConnection.Remove(userId);
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Disconnect,
                ClientId = connId
            });
        }

        if (_clientConnected && userId == hostUserId)
        {
            _clientConnected = false;
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Disconnect,
                ClientId = ServerClientId
            });
        }
    }

    /// <summary>
    /// Disconnects all peers and resets server/client state.
    /// Used when kicked from lobby.
    /// </summary>
    void DisconnectAllPeers()
    {
        if (_clientConnected)
        {
            _clientConnected = false;
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Disconnect,
                ClientId = ServerClientId
            });
        }

        if (_serverActive)
        {
            foreach (var kvp in connectionToUserId)
            {
                _eventQueue.Enqueue(new PendingEvent
                {
                    Type = NetworkEvent.Disconnect,
                    ClientId = kvp.Key
                });
            }
            _serverActive = false;
            connectionToUserId.Clear();
            userIdToConnection.Clear();
        }

        connectedPeers.Clear();
    }

    /// <summary>
    /// Called when lobby info or P2P state changes. If the client was started
    /// but not yet connected (because lobby info or P2P wasn't ready), try now.
    /// </summary>
    void TryDeferredConnect()
    {
        if (_clientStarted && !_clientConnected &&
            hostUserId != null && connectedPeers.Contains(hostUserId))
        {
            _clientConnected = true;
            Debug.Log("[WavedashTransport] Client: connected to host (deferred)");
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Connect,
                ClientId = ServerClientId
            });
        }
    }

    // --- Transport Implementation ---

    public override void Initialize(NetworkManager networkManager = null) { }

    public override bool StartClient()
    {
        _clientStarted = true;

        // If lobby info and P2P are already available, connect immediately
        if (hostUserId != null && connectedPeers.Contains(hostUserId))
        {
            _clientConnected = true;
            Debug.Log("[WavedashTransport] Client: connected to host");
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Connect,
                ClientId = ServerClientId
            });
        }
        // Otherwise, OnP2PConnectionEstablished will call TryDeferredConnect

        return true;
    }

    public override bool StartServer()
    {
        _serverActive = true;
        Debug.Log("[WavedashTransport] Server started");

        string localUserId = Wavedash.SDK.GetUserId();
        foreach (string userId in connectedPeers)
        {
            if (userId == localUserId) continue;
            if (userIdToConnection.ContainsKey(userId)) continue;

            ulong connId = nextConnectionId++;
            connectionToUserId[connId] = userId;
            userIdToConnection[userId] = connId;
            Debug.Log($"[WavedashTransport] Server: registered existing peer {userId} as connId {connId}");
            _eventQueue.Enqueue(new PendingEvent
            {
                Type = NetworkEvent.Connect,
                ClientId = connId
            });
        }

        return true;
    }

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
    {
        bool reliable = networkDelivery != NetworkDelivery.Unreliable
                     && networkDelivery != NetworkDelivery.UnreliableSequenced;
        int channel = reliable ? ReliableChannel : UnreliableChannel;

        if (_serverActive && connectionToUserId.TryGetValue(clientId, out string userId))
        {
            Wavedash.SDK.SendP2PMessage(userId, payload, channel, reliable);
        }
        else if (_clientConnected && hostUserId != null)
        {
            Wavedash.SDK.SendP2PMessage(hostUserId, payload, channel, reliable);
        }
    }

    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0;
        payload = default;
        receiveTime = Time.realtimeSinceStartup;

        // Return queued connection/disconnect events first
        if (_eventQueue.Count > 0)
        {
            var evt = _eventQueue.Dequeue();
            clientId = evt.ClientId;
            return evt.Type;
        }

        // Drain P2P channels one at a time, consuming all messages from each
        // drain before starting the next. This is required because message
        // payloads reference the shared SDK drain buffer.
        while (true)
        {
            while (_messageIndex < messageBuffer.Count)
            {
                var msg = messageBuffer[_messageIndex++];

                if (_serverActive && userIdToConnection.TryGetValue(msg.SenderId, out ulong connId))
                {
                    clientId = connId;
                    payload = msg.Payload;
                    return NetworkEvent.Data;
                }

                if (_clientConnected && msg.SenderId == hostUserId)
                {
                    clientId = ServerClientId;
                    payload = msg.Payload;
                    return NetworkEvent.Data;
                }
            }

            if (_drainPhase == 0)
            {
                _drainPhase = 1;
                Wavedash.SDK.DrainP2PChannel(ReliableChannel, messageBuffer);
                _messageIndex = 0;
                if (messageBuffer.Count > 0) continue;
            }

            if (_drainPhase == 1)
            {
                _drainPhase = 2;
                Wavedash.SDK.DrainP2PChannel(UnreliableChannel, messageBuffer);
                _messageIndex = 0;
                if (messageBuffer.Count > 0) continue;
            }

            // All channels drained — reset for next poll cycle
            _drainPhase = 0;
            return NetworkEvent.Nothing;
        }
    }

    public override void DisconnectRemoteClient(ulong clientId)
    {
        if (connectionToUserId.TryGetValue(clientId, out string userId))
        {
            connectionToUserId.Remove(clientId);
            userIdToConnection.Remove(userId);
        }
    }

    public override void DisconnectLocalClient()
    {
        _clientConnected = false;
        _clientStarted = false;
    }

    public override ulong GetCurrentRtt(ulong clientId) => 0;

    public override void Shutdown()
    {
        _clientConnected = false;
        _clientStarted = false;
        _serverActive = false;
        connectionToUserId.Clear();
        userIdToConnection.Clear();
        _eventQueue.Clear();
        messageBuffer.Clear();
        _messageIndex = 0;
        _drainPhase = 0;
        connectedPeers.Clear();
        currentLobbyId = null;
        hostUserId = null;
        nextConnectionId = 1;
    }
}
