public static class WavedashConstants
{
  public static class UGCItemType
  {
    public const int SCREENSHOT = 0;
    public const int VIDEO = 1;
    public const int COMMUNITY = 2;
    public const int GAME_MANAGED = 3;
    public const int OTHER = 4;
  }

  public static class UGCVisibility
  {
    public const int PUBLIC = 0;
    public const int FRIENDS_ONLY = 1;
    public const int PRIVATE = 2;
  }

  public static class LobbyVisibility
  {
    public const int PUBLIC = 0;
    public const int FRIENDS_ONLY = 1;
    public const int PRIVATE = 2;
  }

  public static class LobbyKickedReason
  {
    public const string KICKED = "KICKED";
    public const string ERROR = "ERROR";
  }

  public static class LobbyUserUpdate
  {
    public const string JOINED = "JOINED";
    public const string LEFT = "LEFT";
    public const string KICKED = "KICKED";
    public const string BANNED = "BANNED";
  }

  /// <summary>
  /// Direction field on the OnP2PPacketDropped payload.
  /// </summary>
  public static class P2PPacketDirection
  {
    public const string SEND = "SEND";
    public const string RECEIVE = "RECEIVE";
  }

  /// <summary>
  /// Reason field on the OnP2PPacketDropped payload. Each reason implies a
  /// different remedy — see comments below.
  /// </summary>
  public static class P2PPacketDropReason
  {
    /// <summary>Receive queue overflowed. Throttle sends, bundle updates into fewer packets, or raise maxIncomingMessages in P2PConfig.</summary>
    public const string QUEUE_FULL = "QUEUE_FULL";
    /// <summary>Payload exceeds configured slot size. Reduce payload or raise messageSize in P2PConfig.</summary>
    public const string PAYLOAD_TOO_LARGE = "PAYLOAD_TOO_LARGE";
    /// <summary>Programming error — payloadSize was 0, negative, larger than the buffer, or payload was null.</summary>
    public const string INVALID_PAYLOAD_SIZE = "INVALID_PAYLOAD_SIZE";
    /// <summary>Channel index was out of range. SDK version skew or malicious peer.</summary>
    public const string INVALID_CHANNEL = "INVALID_CHANNEL";
    /// <summary>Wire data too short to parse a channel header. Channel will be -1.</summary>
    public const string MALFORMED = "MALFORMED";
    /// <summary>P2P not initialized, target peer's channel missing/closed, or send failed on an open channel. Wait for OnP2PConnectionEstablished and watch OnP2PPeerDisconnected.</summary>
    public const string PEER_NOT_READY = "PEER_NOT_READY";
  }

  public static class LeaderboardSortMethod
  {
    public const int ASCENDING = 0;
    public const int DESCENDING = 1;
  }

  public static class LeaderboardDisplayType
  {
    public const int NUMERIC = 0;
    public const int TIME_SECONDS = 1;
    public const int TIME_MILLISECONDS = 2;
    public const int TIME_GAME_TICKS = 3;
  }

  /// <summary>
  /// Avatar size constants for GetUserAvatarUrl
  /// </summary>
  public static class AvatarSize
  {
    /// <summary>64x64 - Lists, chat bubbles</summary>
    public const int SMALL = 0;
    /// <summary>128x128 - Profile cards</summary>
    public const int MEDIUM = 1;
    /// <summary>256x256 - Large displays</summary>
    public const int LARGE = 2;
  }
}