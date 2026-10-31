namespace SamElla.Audio;

// Independent pause reasons must never cancel one another.
internal sealed class MusicPauseState
{
  private readonly HashSet<object> _notifications = [];

  public bool Enabled { get; set; } = true;
  public bool GamePaused { get; set; }
  public bool ShouldPause => !Enabled || GamePaused || _notifications.Count > 0;

  public void StartNotification(object playback) => _notifications.Add(playback);

  public bool CompleteNotification(object playback) => _notifications.Remove(playback);

  public void ClearNotifications() => _notifications.Clear();
}
