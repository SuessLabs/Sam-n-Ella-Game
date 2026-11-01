// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class MenuNavigationTests : GameTestBase
{
  [TestMethod]
  public void TitleSettingsDoesNotStartAndPlayerSelectionStaysIndependent()
  {
    var title = new TitleScreen();
    Assert.AreEqual(0, title.SelectedMenuIndex);
    Assert.AreEqual(MenuAction.None, title.HandleInput(0, [0x28, 0x27], true));
    Assert.AreEqual(1, title.SelectedMenuIndex);
    Assert.AreEqual(PlayerCharacter.Ella, title.SelectedCharacter);
    Assert.AreEqual(MenuAction.Settings, title.HandleInput(0, [0x0D], true));
    Assert.IsTrue(title.IsActive);
    Assert.AreEqual(1, title.SelectedMenuIndex);
    Assert.AreEqual(MenuAction.Primary, title.HandleInput(0, [0x26, 0x0D], true));
    Assert.IsFalse(title.IsActive);
    Assert.AreEqual(PlayerCharacter.Ella, title.SelectedCharacter);
  }

  [TestMethod]
  public void TitleIgnoresUnfocusedNavigationAndRequiresEnterToConfirm()
  {
    var title = new TitleScreen();
    Assert.AreEqual(MenuAction.None, title.HandleInput(0, [0x28, 0x0D], false));
    Assert.AreEqual(0, title.SelectedMenuIndex);
    Assert.AreEqual(MenuAction.None, title.HandleInput(0, [0x20, 0x50], true));
    Assert.IsTrue(title.IsActive);
    title.HandleInput(0, [0x26], true);
    Assert.AreEqual(2, title.SelectedMenuIndex);
    title.HandleInput(0, [0x28], true);
    Assert.AreEqual(0, title.SelectedMenuIndex);
  }

  [TestMethod]
  public void EnterOpensPauseAndSettingsKeepsGamePausedUntilResumeIsConfirmed()
  {
    var game = CreateGame();
    var pause = new PauseScreen();
    Assert.AreEqual(MenuAction.None, pause.HandleInput(game, [0x0D], true));
    Assert.IsTrue(game.Paused, "Opening Enter must not also activate Resume Game.");
    Assert.AreEqual(MenuAction.Settings, pause.HandleInput(game, [0x28, 0x0D], true));
    Assert.IsTrue(game.Paused);
    double x = game.X;
    double y = game.Y;
    Tick(game, 60, 1, true);
    Assert.AreEqual(x, game.X);
    Assert.AreEqual(y, game.Y);
    Assert.AreEqual(1, pause.SelectedIndex);
    Assert.AreEqual(MenuAction.Primary, pause.HandleInput(game, [0x26, 0x0D], true));
    Assert.IsFalse(game.Paused);
    pause.HandleInput(game, [0x0D], true);
    Assert.IsTrue(game.Paused);
    Assert.AreEqual(0, pause.SelectedIndex);
  }

  [TestMethod]
  public void PAndSpaceDoNotPauseOrResumeAndUnfocusedMenusIgnoreInput()
  {
    var game = CreateGame();
    var pause = new PauseScreen();
    pause.HandleInput(game, [0x50, 0x20], true);
    Assert.IsFalse(game.Paused);
    pause.Open(game);
    pause.HandleInput(game, [0x28, 0x0D], false);
    Assert.AreEqual(0, pause.SelectedIndex);
    pause.HandleInput(game, [0x50, 0x20], true);
    Assert.IsTrue(game.Paused);
    Assert.AreEqual(MenuAction.Primary, pause.HandleInput(game, [0x0D], true));
    Assert.IsFalse(game.Paused);
  }

  [TestMethod]
  public void CreditsCanResumeAfterAutomaticFocusPause()
  {
    var game = CreateGame();
    Tick(game, 360, 1);
    Assert.IsTrue(game.Won);
    var pause = new PauseScreen();
    pause.Open(game);
    long creditsTicks = game.CreditsTicks;
    Tick(game, 60);
    Assert.AreEqual(creditsTicks, game.CreditsTicks);
    Assert.AreEqual(MenuAction.Primary, pause.HandleInput(game, [0x0D], true));
    Assert.IsFalse(game.Paused);
  }

  private Game CreateGame() => Map(
    "....................",
    ".S................G.",
    "####################");
}
