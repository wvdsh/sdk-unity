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