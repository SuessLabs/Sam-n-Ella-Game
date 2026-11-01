// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Screens;

internal static class DeathScreen
{
  public static bool HandleInput(Game game, IReadOnlyList<int> keyPresses, bool windowActive,
    bool mobileControls = false, bool deathSoundPlaying = false)
  {
    if (!windowActive || !game.AwaitingRestart || deathSoundPlaying || keyPresses.Count == 0)
      return false;

    if (mobileControls && !keyPresses.Any(code => code is 0x0D or GameKeyboard.PauseKey))
      return false;

    if (!mobileControls && game.Lives == 0 && !keyPresses.Contains(0x0D)) // Enter
      return false;

    return game.ContinueAfterDeath();
  }
}
