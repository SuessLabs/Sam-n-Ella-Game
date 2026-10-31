namespace SamElla.Screens;

internal enum MenuAction
{
  None,
  Primary,
  Settings,
  Leaderboard,
}

internal sealed class ScreenMenu(bool includeLeaderboard = false)
{
  public int SelectedIndex { get; private set; }

  public void Reset() => SelectedIndex = 0;

  public MenuAction HandleInput(IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!windowActive)
      return MenuAction.None;

    foreach (int code in keyPresses)
    {
      int count = includeLeaderboard ? 3 : 2;
      if (code == 0x26)
        SelectedIndex = (SelectedIndex + count - 1) % count;
      else if (code == 0x28)
        SelectedIndex = (SelectedIndex + 1) % count;
      else if (code == 0x0D)
        return SelectedIndex == 0 ? MenuAction.Primary : SelectedIndex == 1 ? MenuAction.Settings : MenuAction.Leaderboard;
    }

    return MenuAction.None;
  }
}
