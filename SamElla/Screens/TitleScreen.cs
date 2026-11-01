// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;
using SamElla.Screens;

namespace SamElla;

internal sealed class TitleScreen
{
  private static readonly int[] KonamiCode = [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x42, 0x41];
  private readonly ScreenMenu _menu = new(includeLeaderboard: true);
  private readonly Queue<int> _recentKeys = new();
  public double Elapsed { get; private set; }
  public bool IsActive { get; private set; } = true;
  public bool KonamiCodeEnabled { get; private set; }
  public bool KonamiCodeJustActivated { get; private set; }
  public PlayerCharacter SelectedCharacter { get; private set; } = PlayerCharacter.Sam;
  public int SelectedMenuIndex => _menu.SelectedIndex;

  public MenuAction HandleInput(double elapsed, IReadOnlyList<int> keyPresses, bool windowActive, bool mobileControls = false)
  {
    KonamiCodeJustActivated = false;
    if (!IsActive || !windowActive)
      return MenuAction.None;

    bool codeCompleted = false;
    foreach (int code in keyPresses)
    {
      int cheatKey = mobileControls ? code switch
      {
        0x1B => 0x42, // Attack is B; it remains Cancel elsewhere in mobile menus.
        0x0D => 0x41, // Jump is A; normally it confirms the selected menu item.
        _ => code,
      } : code;
      codeCompleted |= HandleKeyPress(cheatKey);
    }

    int selectionDirection = (keyPresses.Any(code => code is 0x27 or 0x44) ? 1 : 0)
      - (keyPresses.Any(code => code is 0x25 or 0x41) ? 1 : 0);

    // Completing the mobile code consumes its Jump press. A fresh press accepts afterward.
    var action = mobileControls && codeCompleted ? MenuAction.None : _menu.HandleInput(keyPresses, windowActive);
    Update(elapsed, action == MenuAction.Primary, windowActive, selectionDirection);
    return action;
  }

  public bool HandleKeyPress(int code)
  {
    if (!IsActive)
      return false;

    _recentKeys.Enqueue(code);
    if (_recentKeys.Count > KonamiCode.Length)
      _recentKeys.Dequeue();
    if (_recentKeys.SequenceEqual(KonamiCode))
    {
      KonamiCodeEnabled = true;
      KonamiCodeJustActivated = true;
      _recentKeys.Clear();
      return true;
    }

    return false;
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
