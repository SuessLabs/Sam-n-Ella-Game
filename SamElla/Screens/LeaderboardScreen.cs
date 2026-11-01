// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Screens;

internal enum LeaderboardContext
{
  Title,
  Victory,
  GameOver,
}

internal sealed class LeaderboardScreen(Leaderboard board)
{
  private const string InitialCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
  private const int VK_0 = 0x30;
  private const int VK_9 = 0x39;
  private const int VK_A = 0x41;
  private const int VK_BACK = 0x08;
  private const int VK_DOWN = 0x28;
  private const int VK_ESCAPE = 0x1B;
  private const int VK_LEFT = 0x25;
  private const int VK_NUMPAD0 = 0x60;
  private const int VK_NUMPAD9 = 0x69;
  private const int VK_RETURN = 0x0D;
  private const int VK_RIGHT = 0x27;
  private const int VK_SPACE = 0x20;
  private const int VK_UP = 0x26;
  private const int VK_Z = 0x5A;

  private Game? _observedGame;
  private bool _outcomeHandled;
  private int _typingIndex;
  public Leaderboard Board { get; } = board;

  public LeaderboardContext Context { get; private set; }
  public string Initials { get; private set; } = string.Empty;
  public bool IsActive { get; private set; }

  public bool IsEnteringInitials { get; private set; }
  public int Score { get; private set; }
  public int SelectedInitialIndex => Math.Min(_typingIndex, 2);
  public int SubmittedRank { get; private set; }

  public bool HasHandled(Game game) => ReferenceEquals(_observedGame, game) && _outcomeHandled;

  // Return true only when a newly submitted entry needs to be saved.
  public bool HandleInput(IReadOnlyList<int> keyPresses, bool windowActive, DateTimeOffset recordedAt,
    bool allowCancel = false, bool mobileControls = false)
  {
    if (!IsActive || !windowActive)
      return false;

    foreach (int code in keyPresses)
    {
      if (allowCancel && code == VK_ESCAPE)
      {
        IsEnteringInitials = IsActive = false;
        return false;
      }

      if (!IsEnteringInitials)
      {
        if (code == VK_RETURN || (code == VK_ESCAPE && Context == LeaderboardContext.Title))
        {
          IsActive = false;
          return false;
        }

        continue;
      }

      if (code is VK_LEFT or VK_RIGHT)
      {
        _typingIndex = Math.Clamp(SelectedInitialIndex + (code == VK_LEFT ? -1 : 1), 0, 2);
      }
      else if (code is VK_UP or VK_DOWN)
      {
        _typingIndex = SelectedInitialIndex;
        int index = InitialCharacters.IndexOf(Initials[_typingIndex]);
        int direction = code == VK_UP ? 1 : -1;
        SetInitial(InitialCharacters[(index + direction + InitialCharacters.Length) % InitialCharacters.Length]);
      }
      else if (code == VK_BACK)
      {
        // Backspace edits the previous typed slot; all three slots remain selectable.
        _typingIndex = Math.Max(0, _typingIndex - 1);
        SetInitial(' ');
      }
      else if (code == VK_RETURN || (mobileControls && code == GameKeyboard.PauseKey))
      {
        if (mobileControls && code == VK_RETURN && SelectedInitialIndex < 2)
        {
          _typingIndex = SelectedInitialIndex + 1;
          continue;
        }

        int rank = Board.RankFor(Score);
        if (Board.TryAdd(Initials, Score, recordedAt))
        {
          SubmittedRank = rank;
          IsEnteringInitials = false;
          return true; // The saving Enter must not also close the board.
        }
      }
      else if (_typingIndex < 3)
      {
        char? character = code switch
        {
          >= VK_A and <= VK_Z or >= VK_0 and <= VK_9 => (char)code,
          >= VK_NUMPAD0 and <= VK_NUMPAD9 => (char)('0' + code - VK_NUMPAD0),
          VK_SPACE => ' ',
          _ => null,
        };

        if (character.HasValue)
        {
          SetInitial(character.Value);
          _typingIndex++;
        }
      }
    }

    return false;
  }

  public void OpenViewer()
  {
    Context = LeaderboardContext.Title;
    IsEnteringInitials = false;
    SubmittedRank = 0;
    IsActive = true;
  }

  public bool TryOpenFor(Game game)
  {
    if (IsActive)
      return false;

    if (!ReferenceEquals(_observedGame, game))
    {
      _observedGame = game;
      _outcomeHandled = false;
    }

    if (!game.Won && game.Lives > 0)
    {
      _outcomeHandled = false;
      return false;
    }

    if (_outcomeHandled || (!game.Won && !game.AwaitingRestart))
      return false;

    _outcomeHandled = true;
    if (!Board.Qualifies(game.Score))
      return false;

    Context = game.Won ? LeaderboardContext.Victory : LeaderboardContext.GameOver;
    Score = game.Score;
    Initials = "AAA";
    _typingIndex = 0;
    SubmittedRank = 0;
    IsEnteringInitials = IsActive = true;

    return true;
  }

  private void SetInitial(char character)
  {
    char[] initials = Initials.ToCharArray();
    initials[SelectedInitialIndex] = character;
    Initials = new string(initials);
  }
}
