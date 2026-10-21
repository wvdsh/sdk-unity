public static class WavedashConstants
{
  public static class LanguageCode
  {
    public const string ARABIC = "ar";
    public const string BULGARIAN = "bg";
    public const string CATALAN = "ca";
    public const string CHINESE_SIMPLIFIED = "zh-CN";
    public const string CHINESE_TRADITIONAL = "zh-TW";
    public const string CROATIAN = "hr";
    public const string CZECH = "cs";
    public const string DANISH = "da";
    public const string DUTCH = "nl";
    public const string ENGLISH = "en";
    public const string ESPERANTO = "eo";
    public const string FINNISH = "fi";
    public const string FRENCH = "fr";
    public const string GALICIAN = "gl";
    public const string GERMAN = "de";
    public const string GREEK = "el";
    public const string HUNGARIAN = "hu";
    public const string INDONESIAN = "id";
    public const string ITALIAN = "it";
    public const string JAPANESE = "ja";
    public const string KOREAN = "ko";
    public const string LATVIAN = "lv";
    public const string LITHUANIAN = "lt";
    public const string MALAY = "ms";
    public const string NORWEGIAN = "no";
    public const string POLISH = "pl";
    public const string PORTUGUESE = "pt";
    public const string PORTUGUESE_BRAZIL = "pt-BR";
    public const string ROMANIAN = "ro";
    public const string RUSSIAN = "ru";
    public const string SPANISH = "es";
    public const string SPANISH_LATIN_AMERICA = "es-419";
    public const string SWEDISH = "sv";
    public const string THAI = "th";
    public const string TURKISH = "tr";
    public const string UKRAINIAN = "uk";
    public const string VIETNAMESE = "vi";
  }

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

  /// <summary>
  /// type field on the OnPurchaseCompleted payload.
  /// </summary>
  public static class PurchaseType
  {
    public const string DURABLE = "DURABLE";
    public const string CONSUMABLE = "CONSUMABLE";
  }

  /// <summary>
  /// status field on the FulfillPurchase result.
  /// </summary>
  public static class FulfillPurchaseStatus
  {
    /// <summary>Marked fulfilled by this call.</summary>
    public const string FULFILLED = "FULFILLED";
    /// <summary>Also success: the game or its backend fulfilled it earlier, or it's a durable.</summary>
    public const string ALREADY_FULFILLED = "ALREADY_FULFILLED";
    /// <summary>Unknown purchase, or it was refunded. Don't grant it.</summary>
    public const string NOT_FOUND = "NOT_FOUND";
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
    public const int SMALL = 64;
    /// <summary>128x128 - Profile cards</summary>
    public const int MEDIUM = 128;
    /// <summary>256x256 - Large displays</summary>
    public const int LARGE = 256;
  }
}