using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class DeathRestartTests : GameTestBase
{
  [TestMethod]
  public void DeathWaitsIndefinitelyForConfirmationAfterTwoSeconds()
  {
    var game = Map("....................", "....................", "....................",
      ".S................G.", "....................");
    Assert.IsFalse(game.ContinueAfterDeath());
    for (int tick = 0; tick < 120 && !game.IsDying; tick++)
      game.Update(0, false);

    Assert.IsTrue(game.IsDying);
    var level = game.Level;
    string quote = game.DeathQuote;
    double x = game.X, y = game.Y;
    Tick(game, Game.DeathDurationTicks - 1);
    Assert.AreEqual(1, game.DeathTicksRemaining);
    Tick(game, 1);
    Assert.IsTrue(game.AwaitingRestart);
    Tick(game, 600, 1, true);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(0, game.DeathTicksRemaining);
    Assert.AreEqual(2, game.Lives);
    Assert.AreSame(level, game.Level);
    Assert.AreEqual(quote, game.DeathQuote);
    Assert.AreEqual(x, game.X);
    Assert.AreEqual(y, game.Y);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.IsFalse(game.IsDying);
    Assert.IsFalse(game.AwaitingRestart);
    Assert.AreNotSame(level, game.Level);
    Assert.AreEqual(2, game.Lives);
    Assert.IsFalse(game.ContinueAfterDeath());
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(30)]
  [DataRow(119)]
  public void FreshKeyCanRestartAtAnyPointDuringDeath(int elapsed)
  {
    var game = Map(
      "....................",
      "....................",
      "....................",
      ".S................G.",
      "....................");

    for (int tick = 0; tick < 120 && !game.IsDying; tick++)
      game.Update(0, false);

    var oldLevel = game.Level;
    Tick(game, elapsed);
    Assert.IsGreaterThan(0, game.DeathTicksRemaining);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreNotSame(oldLevel, game.Level);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(0, game.DeathTicksRemaining);
    Assert.AreEqual(2, game.Lives);
  }

  [TestMethod]
  public void EarlyKeyAfterLastLifeRestartsGameImmediately()
  {
    var game = Map(
      "....................",
      "....................",
      "....................",
      ".S................G.",
      "....................");

    for (int life = 0; life < 3; life++)
    {
      for (int tick = 0; tick < 120 && !game.IsDying; tick++)
        game.Update(0, false);
      Assert.AreEqual(2 - life, game.Lives);
      Assert.IsTrue(game.ContinueAfterDeath());
    }

    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(0, game.TotalDeaths);
    Assert.IsFalse(game.IsDying);
  }

  [TestMethod]
  public void ConfirmationReloadsCurrentLevelAndPreservesProgress()
  {
    Map("....................", "....................", ".S.G................", "####################");
    File.WriteAllLines(NextPath, ["....................", "....................", "....................",
      ".S................G.", "...................."]);
    var game = new Game([LevelPath, NextPath]);
    Tick(game, 15, 1);
    Assert.AreEqual(2, game.LevelNumber);
    for (int tick = 0; tick < 180 && !game.IsDying; tick++)
      game.Update(0, false);
    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(2, game.LevelNumber);
    Assert.AreEqual(1000, game.Score);
    Assert.AreEqual(2, game.Lives);
  }

  [TestMethod]
  [DataRow(0x0D)]
  [DataRow(0x51)]
  [DataRow(0x20)]
  [DataRow(0x1B)]
  public void AnyFreshKeyIsDetectedIncludingUnmappedKeys(int code)
  {
    var keyboard = new GameKeyboard();
    keyboard.KeyDown(code);
    Assert.IsTrue(keyboard.AnyKeyPressed);
    keyboard.BeginFrame();
    Assert.IsFalse(keyboard.AnyKeyPressed);
    keyboard.KeyDown(code);
    Assert.IsFalse(keyboard.AnyKeyPressed, "Holding a key or OS repeat must not dismiss the death screen.");
    keyboard.KeyUp(code);
    keyboard.KeyDown(code);
    Assert.IsTrue(keyboard.AnyKeyPressed);
  }

  [TestMethod]
  public void FocusLossClearsHeldKeysAndPressEdges()
  {
    var keyboard = new GameKeyboard();
    keyboard.KeyDown(0x44);
    Assert.IsTrue(keyboard.Down(GameKey.Right));
    Assert.IsTrue(keyboard.Pressed(GameKey.Right));
    keyboard.Clear();
    Assert.IsFalse(keyboard.AnyKeyPressed);
    Assert.IsFalse(keyboard.Down(GameKey.Right));
    Assert.IsFalse(keyboard.Pressed(GameKey.Right));
    keyboard.KeyDown(0x44, isRepeat: true);
    Assert.IsFalse(keyboard.AnyKeyPressed, "An OS repeat after focus returns must not count as a new press.");
    keyboard.KeyUp(0x44);
    keyboard.KeyDown(0x44);
    Assert.IsTrue(keyboard.AnyKeyPressed);
  }
}
