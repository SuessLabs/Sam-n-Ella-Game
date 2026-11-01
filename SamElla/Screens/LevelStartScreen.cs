// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;

namespace SamElla.Screens;

internal sealed class LevelStartScreen
{
  public const double DurationSeconds = 3;

  private readonly string[] _quotes;

  public LevelStartScreen(string quotesPath)
  {
    _quotes = File.Exists(quotesPath)
      ? File.ReadAllLines(quotesPath).Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray()
      : [];

    if (_quotes.Length == 0)
      _quotes = ["Try not to die this time"];
  }

  public PlayerCharacter Character { get; private set; }
  public bool IsActive { get; private set; }
  public bool IsLarge { get; private set; }
  public string LevelLabel { get; private set; } = string.Empty;
  public string LevelName { get; private set; } = string.Empty;
  public int LevelNumber { get; private set; }
  public int Lives { get; private set; }
  public string Quote { get; private set; } = string.Empty;
  public double RemainingSeconds { get; private set; }

  public void Show(Game game)
  {
    LevelNumber = game.LevelNumber;
    LevelLabel = game.LevelLabel;
    LevelName = game.Level.DisplayName;
    Lives = game.Lives;
    Character = game.Character;
    IsLarge = game.IsLarge;
    Quote = _quotes[Random.Shared.Next(_quotes.Length)];
    RemainingSeconds = DurationSeconds;
    IsActive = true;
  }

  public void Update(double elapsed, bool anyKeyPressed, bool windowActive)
  {
    if (!IsActive || !windowActive)
      return;

    RemainingSeconds = Math.Max(0, RemainingSeconds - elapsed);
    if (anyKeyPressed || RemainingSeconds <= 0)
      IsActive = false;
  }
}
