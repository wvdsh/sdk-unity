using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Transport layer for Mirror that uses Wavedash SDK for P2P communication.
/// Assumes all members of a Wavedash Lobby have open WebRTC connections to each other.
/// Lobby host is the Mirror Network host; other members are clients.
/// </summary>
public class WavedashTransport : Transport
{
    string currentLobbyId;
    string hostUserId;

    readonly Dictionary<int, string> connectionToUserId = new Dictionary<int, string>();
    readonly Dictionary<string, int> userIdToConnection = new Dictionary<string, int>();
    int nextConnectionId = 1;

    bool _serverActive;
    bool _clientConnected;

    readonly List<Wavedash.P2PMessage> messageBuffer = new List<Wavedash.P2PMessage>();
    readonly HashSet<string> connectedPeers = new HashSet<string>();

    void OnEnable()
    {
        Wavedash.SDK.OnLobbyJoined += OnLobbyJoined;
        Wavedash.SDK.OnLobbyKicked += OnLobbyKicked;
        Wavedash.SDK.OnLobbyUsersUpdated += OnLobbyUsersUpdated;
        Wavedash.SDK.OnP2PConnectionEstablished += OnP2PConnectionEstablished;
        Wavedash.SDK.OnP2PPeerDisconnected += OnP2PPeerDisconnected;
        Wavedash.SDK.OnP2PConnectionFailed += OnP2PConnectionFailed;
    }

    void OnDisable()
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

        if (_clientConnected)
        {
            _clientConnected = false;
            OnClientDisconnected?.Invoke();
        }

        if (_serverActive)
        {
            ServerStop();
        }
    }

    public event Action OnHostMigration;

    void OnLobbyUsersUpdated(Dictionary<string, object> data)
    {
        if (currentLobbyId == null) return;

        string previousHost = hostUserId;
        hostUserId = Wavedash.SDK.GetLobbyHostId(currentLobbyId);

        if (previousHost != hostUserId)
        {
            OnHostMigration?.Invoke();
        }
    }

    void OnP2PConnectionEstablished(Dictionary<string, object> data)
    {
        string userId = data["userId"].ToString();
        connectedPeers.Add(userId);

        if (_serverActive)
        {
            if (!userIdToConnection.ContainsKey(userId))
            {
                int connId = nextConnectionId++;
                connectionToUserId[connId] = userId;
                userIdToConnection[userId] = connId;
                Debug.Log($"[WavedashTransport] Server: assigned connId {connId} to {userId}");
                OnServerConnectedWithAddress?.Invoke(connId, userId);
            }
        }

        if (!_clientConnected && userId == hostUserId)
        {
            _clientConnected = true;
            Debug.Log("[WavedashTransport] Client: connected to host");
            OnClientConnected?.Invoke();
        }
    }

    void OnP2PPeerDisconnected(Dictionary<string, object> data)
    {
        string userId = data["userId"].ToString();
        connectedPeers.Remove(userId);

        if (_serverActive && userIdToConnection.TryGetValue(userId, out int connId))
        {
            connectionToUserId.Remove(connId);
            userIdToConnection.Remove(userId);
            OnServerDisconnected?.Invoke(connId);
        }

        if (_clientConnected && userId == hostUserId)
        {
            _clientConnected = false;
            OnClientDisconnected?.Invoke();
        }
    }

    void OnP2PConnectionFailed(Dictionary<string, object> data)
    {
        string userId = data.ContainsKey("userId") ? data["userId"].ToString() : "unknown";
        string reason = data.ContainsKey("reason") ? data["reason"].ToString() : "unknown";

        if (_serverActive && userIdToConnection.TryGetValue(userId, out int connId))
        {
            OnServerError?.Invoke(connId, TransportError.Refused, reason);
        }

        if (!_clientConnected && userId == hostUserId)
        {
            OnClientError?.Invoke(TransportError.Refused, reason);
        }
    }

    // --- Transport Implementation ---

    public override bool Available() =>
        Application.platform == RuntimePlatform.WebGLPlayer;

    public override int GetMaxPacketSize(int channelId = Channels.Reliable) => Wavedash.SDK.MAX_PAYLOAD_SIZE;

    // --- Client ---

    public override void ClientConnect(string address)
    {
        if (currentLobbyId == null)
        {
            Debug.LogError("[WavedashTransport] Cannot connect: not in a lobby. Join a Wavedash lobby first.");
            OnClientError?.Invoke(TransportError.Unexpected, "Not in a lobby");
            return;
        }

        // P2P connections are established by the SDK automatically.
        // If we already have a connection to the host, fire connected immediately.
        if (_clientConnected)
        {
            OnClientConnected?.Invoke();
        }
    }

    public override bool ClientConnected() => _clientConnected;

    public override void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable)
    {
        if (!_clientConnected || hostUserId == null) return;
        Wavedash.SDK.SendP2PMessage(hostUserId, segment, channelId, channelId == Channels.Reliable);
    }

    public override void ClientDisconnect()
    {
        if (_clientConnected)
        {
            _clientConnected = false;
            OnClientDisconnected?.Invoke();
        }
    }

    public override void ClientEarlyUpdate()
    {
        if (!_clientConnected) return;

        DrainAndDispatchClient(Channels.Reliable);
        DrainAndDispatchClient(Channels.Unreliable);
    }

    void DrainAndDispatchClient(int channel)
    {
        // Only messages from the host are relevant to the client.
        // Messages from other peers are consumed and discarded, which is correct
        // for Mirror's client-server model where all traffic routes through the host.
        int count = Wavedash.SDK.DrainP2PChannel(channel, messageBuffer);
        for (int i = 0; i < count; i++)
        {
            var msg = messageBuffer[i];
            if (msg.SenderId == hostUserId)
            {
                OnClientDataReceived?.Invoke(new ArraySegment<byte>(msg.Payload), channel);
            }
        }
    }

    // --- Server ---

    public override void ServerStart()
    {
        _serverActive = true;
        Debug.Log("[WavedashTransport] Server started");

        // Register peers whose P2P connections were established before the server started.
        string localUserId = Wavedash.SDK.GetUserId();
        foreach (string userId in connectedPeers)
        {
            if (userId == localUserId) continue;
            if (userIdToConnection.ContainsKey(userId)) continue;

            int connId = nextConnectionId++;
            connectionToUserId[connId] = userId;
            userIdToConnection[userId] = connId;
            Debug.Log($"[WavedashTransport] Server: registered existing peer {userId} as connId {connId}");
            OnServerConnectedWithAddress?.Invoke(connId, userId);
        }
    }

    public override bool ServerActive() => _serverActive;

    public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable)
    {
        if (!_serverActive) return;
        if (!connectionToUserId.TryGetValue(connectionId, out string userId)) return;
        Wavedash.SDK.SendP2PMessage(userId, segment, channelId, channelId == Channels.Reliable);
    }

    public override void ServerDisconnect(int connectionId)
    {
        if (connectionToUserId.TryGetValue(connectionId, out string userId))
        {
            connectionToUserId.Remove(connectionId);
            userIdToConnection.Remove(userId);
            OnServerDisconnected?.Invoke(connectionId);
        }
    }

    public override string ServerGetClientAddress(int connectionId)
    {
        return connectionToUserId.TryGetValue(connectionId, out string userId) ? userId : "";
    }

    public override void ServerStop()
    {
        connectionToUserId.Clear();
        userIdToConnection.Clear();
        _serverActive = false;
        Debug.Log("[WavedashTransport] Server stopped");
    }

    public override Uri ServerUri() => new Uri("wavedash://lobby");

    public override void ServerEarlyUpdate()
    {
        if (!_serverActive) return;

        DrainAndDispatchServer(Channels.Reliable);
        DrainAndDispatchServer(Channels.Unreliable);
    }

    void DrainAndDispatchServer(int channel)
    {
        int count = Wavedash.SDK.DrainP2PChannel(channel, messageBuffer);
        for (int i = 0; i < count; i++)
        {
            var msg = messageBuffer[i];
            if (userIdToConnection.TryGetValue(msg.SenderId, out int connId))
            {
                OnServerDataReceived?.Invoke(connId, new ArraySegment<byte>(msg.Payload), channel);
            }
        }
    }

    // --- Lifecycle ---

    public override void Shutdown()
    {
        ClientDisconnect();
        ServerStop();
        connectedPeers.Clear();
        currentLobbyId = null;
        hostUserId = null;
        nextConnectionId = 1;
    }
}
