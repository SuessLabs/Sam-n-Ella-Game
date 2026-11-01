// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class KonamiCodeTests : GameTestBase
{
  private static readonly int[] Code = [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x42, 0x41];

  [TestMethod]
  [DataRow(PlayerCharacter.Sam, 0x25)]
  [DataRow(PlayerCharacter.Ella, 0x27)]
  public void CodeKeepsTitleOpenAllowsPlayerSelectionAndStartsCheatGame(PlayerCharacter character, int selectionKey)
  {
    var title = new TitleScreen();
    foreach (int key in Code)
    {
      title.HandleInput(Game.Step, [key], true);
      Assert.IsTrue(title.IsActive, "Up must not trigger the game's jump/start shortcut on the title screen.");
    }
    Assert.IsTrue(title.KonamiCodeEnabled);
    title.HandleInput(Game.Step, [selectionKey], true);
    Assert.AreEqual(character, title.SelectedCharacter);
    Assert.IsTrue(title.KonamiCodeEnabled);
    title.HandleInput(Game.Step, [0x0D], true);
    Assert.IsFalse(title.IsActive);

    CreateMap();
    var game = new Game([LevelPath], konamiCheat: title.KonamiCodeEnabled) { Character = title.SelectedCharacter };
    Assert.AreEqual(character, game.Character);
    Assert.AreEqual(30, game.Lives);
    Assert.IsTrue(game.IsLarge);
    Assert.IsTrue(game.HasFlamethrower);
    Assert.AreEqual(2, game.PowerUpLevel);
    game.Update(0, false, true);
    Assert.IsNotNull(game.Flame);
  }

  [TestMethod]
  public void MobileCodeConsumesFinalJumpButNormalJumpStillAcceptsMenus()
  {
    var title = new TitleScreen();
    int[] touchCode = [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x1B, 0x0D];
    foreach (int key in touchCode)
    {
      Assert.AreEqual(MenuAction.None, title.HandleInput(Game.Step, [key], true, mobileControls: true));
      Assert.IsTrue(title.IsActive);
    }

    Assert.IsTrue(title.KonamiCodeEnabled);
    Assert.AreEqual(MenuAction.Primary, title.HandleInput(Game.Step, [0x0D], true, mobileControls: true));
    Assert.IsFalse(title.IsActive);
    var normal = new TitleScreen();
    Assert.AreEqual(MenuAction.Primary, normal.HandleInput(Game.Step, [0x0D], true, mobileControls: true));
    Assert.IsFalse(normal.KonamiCodeEnabled);
  }

  [TestMethod]
  public void BatchedMobileCodeDoesNotConfirmAnyMenuSelectionInItsCompletionFrame()
  {
    var title = new TitleScreen();
    int[] keys = [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x1B, 0x0D, 0x0D];
    Assert.AreEqual(MenuAction.None, title.HandleInput(Game.Step, keys, true, mobileControls: true));
    Assert.IsTrue(title.IsActive);
    Assert.IsTrue(title.KonamiCodeEnabled);
  }

  [TestMethod]
  [DataRow(false)]
  [DataRow(true)]
  public void ActivationNotificationOccursOnlyWhenACompleteCodeIsEntered(bool mobileControls)
  {
    var title = new TitleScreen();
    int[] keys = mobileControls ? [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x1B, 0x0D] : Code;
    foreach (int key in keys)
      title.HandleInput(Game.Step, [key], true, mobileControls);

    Assert.IsTrue(title.KonamiCodeJustActivated);
    title.HandleInput(Game.Step, [], true, mobileControls);
    Assert.IsFalse(title.KonamiCodeJustActivated);
    Assert.IsTrue(title.KonamiCodeEnabled);
    foreach (int key in keys)
      title.HandleInput(Game.Step, [key], true, mobileControls);

    Assert.IsTrue(title.KonamiCodeJustActivated, "Entering the code again can intentionally replay its cue.");
  }

  [TestMethod]
  public void IncorrectOrPartialCodeDoesNotEnableCheatAndValidRetryWorks()
  {
    var title = new TitleScreen();
    title.HandleInput(0, [0x26, 0x26, 0x28], true);
    Assert.IsFalse(title.KonamiCodeEnabled);
    title.HandleInput(0, [0x28, 0x25, 0x27, 0x25, 0x27, 0x41, 0x42], true);
    Assert.IsFalse(title.KonamiCodeEnabled);
    title.HandleInput(0, [0x26], true);
    title.HandleInput(0, Code, true);
    Assert.IsTrue(title.KonamiCodeEnabled, "Extra leading Up presses should not prevent a valid sequence.");
  }

  [TestMethod]
  public void KeyboardKeepsKeyOrderAndIgnoresAutoRepeat()
  {
    var keyboard = new GameKeyboard();
    keyboard.KeyDown(0x26);
    keyboard.KeyDown(0x26, isRepeat: true);
    Assert.HasCount(1, keyboard.KeyPresses);
    keyboard.Clear();
    foreach (int key in Code)
    {
      keyboard.KeyDown(key);
      keyboard.KeyUp(key);
    }
    CollectionAssert.AreEqual(Code, new List<int>(keyboard.KeyPresses));
    var title = new TitleScreen();
    title.HandleInput(0, keyboard.KeyPresses, true);
    Assert.IsTrue(title.KonamiCodeEnabled);
    keyboard.BeginFrame();
    Assert.IsEmpty(keyboard.KeyPresses);
  }

  [TestMethod]
  public void InactiveOrStartedTitleCannotAcceptCheat()
  {
    var title = new TitleScreen();
    title.HandleInput(0, Code, false);
    Assert.IsFalse(title.KonamiCodeEnabled);
    title.HandleInput(0, [0x0D], true);
    title.HandleInput(0, Code, true);
    Assert.IsFalse(title.KonamiCodeEnabled);
  }

  [TestMethod]
  public void NormalGameStartsWithThreeLivesAndNoPowers()
  {
    var game = CreateMap();
    Assert.AreEqual(3, game.Lives);
    Assert.IsFalse(game.IsLarge);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.IsFalse(game.KonamiCheatEnabled);
  }

  [TestMethod]
  public void CheatCostsLivesNormallyAndRestartRestoresCheatStart()
  {
    CreateMap();
    var game = new Game([LevelPath], konamiCheat: true);
    // Falling out of the map still costs a life, regardless of the starting powers.
    for (int x = 0; x < game.Level.Width; x++)
      game.Level.Clear(x, 5);
    Tick(game, 120);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(29, game.Lives);
    Assert.IsFalse(game.HasFlamethrower);
    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(29, game.Lives);
    Assert.IsFalse(game.IsLarge);
    game.Restart();
    Assert.AreEqual(30, game.Lives);
    Assert.IsTrue(game.IsLarge);
    Assert.IsTrue(game.HasFlamethrower);
  }

  private Game CreateMap() => Map(
    "........................", "........................", "........................",
    "........................", ".S....................G.", "########################");
}
