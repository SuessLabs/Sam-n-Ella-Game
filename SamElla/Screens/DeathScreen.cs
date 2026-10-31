namespace SamElla.Screens;

internal static class DeathScreen
{
  public static bool HandleInput(Game game, IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!windowActive || !game.IsDying || keyPresses.Count == 0)
      return false;

    if (game.Lives == 0 && !keyPresses.Contains(0x0D)) // Enter
      return false;

    return game.ContinueAfterDeath();
  }
}
