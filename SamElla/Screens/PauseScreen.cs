namespace SamElla.Screens;

internal sealed class PauseScreen
{
  private readonly ScreenMenu _menu = new();

  public int SelectedIndex => _menu.SelectedIndex;

  public void Open(Game game)
  {
    if (!game.Paused)
      _menu.Reset();

    game.Paused = true;
  }

  public MenuAction HandleInput(Game game, IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!windowActive || game.IsDying || game.Lives == 0)
      return MenuAction.None;

    if (!game.Paused)
    {
      if (keyPresses.Contains(0x0D))
        Open(game);

      // Enter opens the pause menu; selecting an item requires a fresh press.
      return MenuAction.None;
    }

    var action = _menu.HandleInput(keyPresses, windowActive);
    if (action == MenuAction.Primary)
      game.Paused = false;

    return action;
  }
}
