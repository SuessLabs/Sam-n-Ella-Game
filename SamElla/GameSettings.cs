// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Audio;
using SamElla.Mobile;
using SamElla.Updater;

namespace SamElla;

internal sealed class GameSettings
{
  public AudioSettings Audio { get; private init; } = new();

  public Leaderboard Leaderboard { get; private init; } = new();
  public MobileSettings Mobile { get; private init; } = new();
  public PlayerRecords Records { get; private init; } = new();
  public UpdateSettings Updates { get; private init; } = new();

  public static GameSettings Load(string path) => new()
  {
    Audio = AudioSettings.Load(path),
    Mobile = MobileSettings.Load(path),
    Updates = UpdateSettings.Load(path),
    Records = PlayerRecords.Load(path),
    Leaderboard = Leaderboard.Load(path),
  };

  public bool RecordProgress(Game game, DateTimeOffset recordedAt)
  {
    return Records.RecordProgress(game.Score, game.Won ? game.CompletionTime : null,
      game.Lives == 0 ? game.CompletionTime : null, recordedAt);
  }

  public void Save(string path)
  {
    string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
      File.WriteAllLines(temporaryPath, Audio.ToLines().Concat(Mobile.ToLines()).Concat(Updates.ToLines()).Concat(Records.ToLines()).Concat(Leaderboard.ToLines()));
      File.Move(temporaryPath, path, overwrite: true);
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Keep playing with session settings if preferences cannot be written.
    }
    finally
    {
      try
      {
        if (File.Exists(temporaryPath))
          File.Delete(temporaryPath);
      }
      catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
      {
        // A locked temporary file must not interrupt the game.
      }
    }
  }
}
