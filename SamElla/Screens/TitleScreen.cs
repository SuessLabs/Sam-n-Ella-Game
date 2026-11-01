// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;
using SamElla.Screens;

namespace SamElla;

internal sealed class TitleScreen
{
  private static readonly int[] KonamiCode = [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x42, 0x41];
  private readonly Queue<int> _recentKeys = new();
  private readonly ScreenMenu _menu = new(includeLeaderboard: true);

  public bool IsActive { get; private set; } = true;
  public bool KonamiCodeEnabled { get; private set; }
  public double Elapsed { get; private set; }
  public PlayerCharacter SelectedCharacter { get; private set; } = PlayerCharacter.Sam;
  public int SelectedMenuIndex => _menu.SelectedIndex;

  public void HandleKeyPress(int code)
  {
    if (!IsActive)
      return;

    _recentKeys.Enqueue(code);
    if (_recentKeys.Count > KonamiCode.Length)
      _recentKeys.Dequeue();
    if (_recentKeys.SequenceEqual(KonamiCode))
      KonamiCodeEnabled = true;
  }

  public MenuAction HandleInput(double elapsed, IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!IsActive || !windowActive)
      return MenuAction.None;

    foreach (int code in keyPresses)
      HandleKeyPress(code);

    int selectionDirection = (keyPresses.Any(code => code is 0x27 or 0x44) ? 1 : 0)
      - (keyPresses.Any(code => code is 0x25 or 0x41) ? 1 : 0);

    var action = _menu.HandleInput(keyPresses, windowActive);
    Update(elapsed, action == MenuAction.Primary, windowActive, selectionDirection);
    return action;
  }

  public void Update(double elapsed, bool startPressed, bool windowActive, int selectionDirection = 0)
  {
    if (!IsActive || !windowActive)
      return;

    Elapsed += elapsed;
    if (selectionDirection < 0)
      SelectedCharacter = PlayerCharacter.Sam;
    else if (selectionDirection > 0)
      SelectedCharacter = PlayerCharacter.Ella;

    if (startPressed)
      IsActive = false;
  }
}
