// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class StartAndGameOverScreenTests : GameTestBase
{
  [TestMethod]
  public void LevelStartSnapshotsNumberNameLivesAndRandomQuoteForThreeSeconds()
  {
    var game = CreateMap();
    string[] quotes = ["Try not to die this time", "You dropped your pocket"];
    File.WriteAllLines(NextPath, new[] { "; ignored", "", "  " }.Concat(quotes));

    var screen = new LevelStartScreen(NextPath);
    screen.Show(game);
    Assert.AreEqual(1, screen.LevelNumber);
    Assert.AreEqual(game.Level.DisplayName, screen.LevelName);
    Assert.AreEqual(3, screen.Lives);
    Assert.AreEqual(3, screen.RemainingSeconds);

    CollectionAssert.Contains(quotes, screen.Quote);
    string quote = screen.Quote;
    screen.Update(2.9, false, true);
    Assert.IsTrue(screen.IsActive);
    Assert.AreEqual(quote, screen.Quote);

    screen.Update(0.2, false, true);
    Assert.IsFalse(screen.IsActive);
    Assert.AreEqual(0, screen.RemainingSeconds);
  }

  [TestMethod]
  public void AnyFreshKeySkipsIntroButHeldKeyAndLostFocusDoNot()
  {
    var screen = new LevelStartScreen(NextPath);
    screen.Show(CreateMap());
    screen.Update(10, true, false);
    Assert.IsTrue(screen.IsActive);
    Assert.AreEqual(3, screen.RemainingSeconds);

    var keyboard = new GameKeyboard();
    keyboard.KeyDown(0x20);
    keyboard.BeginFrame();
    keyboard.KeyDown(0x20, isRepeat: true);
    screen.Update(0.1, keyboard.AnyKeyPressed, true);
    Assert.IsTrue(screen.IsActive);

    keyboard.KeyUp(0x20);
    keyboard.KeyDown(0x20);
    screen.Update(0, keyboard.AnyKeyPressed, true);
    Assert.IsFalse(screen.IsActive);
  }

  [TestMethod]
  public void NewLevelAndRespawnShowUpdatedLivesAndNumber()
  {
    CreateMap();
    File.Copy(LevelPath, NextPath);
    var game = new Game([LevelPath, NextPath]);
    var screen = new LevelStartScreen(Path.Combine(AppContext.BaseDirectory, "Assets", "uncle-quotes.txt"));
    screen.Show(game);
    Level previous = game.Level;
    for (int tick = 0; tick < 200 && game.LevelNumber == 1; tick++)
      game.Update(1, false);

    Assert.AreNotSame(previous, game.Level);

    screen.Show(game);
    Assert.AreEqual(2, screen.LevelNumber);
    Assert.IsTrue(screen.IsActive);

    screen.Update(0, true, true);
    game.CompleteLevelStart();
    double previousX = game.X;
    game.Update(1, false);
    Assert.IsGreaterThan(previousX, game.X, "Skipping the page must also skip the old transition delay.");

    Tick(game, 40);
    for (int x = 0; x < game.Level.Width; x++)
      game.Level.Clear(x, 5);

    Die(game);
    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.ContinueAfterDeath());

    screen.Show(game);
    Assert.AreEqual(2, screen.LevelNumber);
    Assert.AreEqual(2, screen.Lives);
    Assert.AreEqual(3, screen.RemainingSeconds);
  }

  [TestMethod]
  public void LevelNameComesFromFilenameWithoutNumberPrefix()
  {
    var level = Level.Parse("04-zombie-ate-my", [".S........", ".........G", "##########"]);
    Assert.AreEqual("zombie ate my", level.DisplayName);
    Assert.AreEqual("custom name", Level.Parse("custom_name", [".S........", ".........G", "##########"]).DisplayName);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void IntroMissingOrEmptyQuotesUseFallback(bool createFile)
  {
    if (createFile)
      File.WriteAllLines(NextPath, ["", "; comment"]);
    var screen = new LevelStartScreen(NextPath);
    screen.Show(CreateMap());
    Assert.AreEqual("Try not to die this time", screen.Quote);
  }

  [TestMethod]
  public void LevelStartRendersNumberLivesAndQuoteIncludingCheatLives()
  {
    CreateMap();
    var game = new Game([LevelPath], konamiCheat: true) { Character = PlayerCharacter.Ella };
    File.WriteAllText(NextPath, "You dropped your pocket");
    var screen = new LevelStartScreen(NextPath);
    screen.Show(game);

    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawLevelStart(screen);

    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    PixelFont.Draw(expected, "LEVEL 1", 138, 32, 0xFFFFD56B, 4);
    PixelFont.Draw(expected, "LIVES 30", 180, 125, 0xFFF6F4DF, 2);

    int quoteX = (PixelRenderer.Width - (((screen.Quote.Length * 4) - 1) * 2)) / 2;
    PixelFont.Draw(expected, screen.Quote, quoteX, 170, 0xFFF6F4DF, 2);
    AssertTextPixels(expected, renderer.Frame);
  }

  [TestMethod]
  public void LastLifeShowsGameOverAfterDeathAnimationAndQuoteRemainsUntilRestart()
  {
    CreatePit();
    File.WriteAllLines(NextPath, ["; ignored", "", "Bet you wish you could rewind? Nope."]);
    var game = new Game([LevelPath], gameOverQuotesPath: NextPath);
    LoseAllLives(game);

    Assert.AreEqual("Bet you wish you could rewind? Nope.", game.GameOverQuote);
    Assert.IsFalse(game.AwaitingRestart);

    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.AwaitingRestart);

    Tick(game, 500);
    Assert.AreEqual("Bet you wish you could rewind? Nope.", game.GameOverQuote);

    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    Assert.AreEqual(0xFF781C29u, renderer.Frame.Pixels[0]);

    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    PixelFont.Draw(expected, "GAME OVER", 122, 24, 0xFFF6F4DF, 4);

    int quoteX = (PixelRenderer.Width - (((game.GameOverQuote.Length * 4) - 1) * 2)) / 2;
    PixelFont.Draw(expected, game.GameOverQuote, quoteX, 171, 0xFFF6F4DF, 2);

    const string RestartPrompt = "PRESS ENTER TO START OVER";
    int promptX = (PixelRenderer.Width - ((RestartPrompt.Length * 4) - 1)) / 2;

    PixelFont.Draw(expected, RestartPrompt, promptX, 226, 0xFFFFD56B);

    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var stone = Sprite.Load(Path.Combine(assets, "tombstone.sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    int stoneX = (PixelRenderer.Width - (stone.Width * 3)) / 2;

    for (int y = 0; y < stone.Height; y++)
    {
      for (int x = 0; x < stone.Width; x++)
      {
        uint color = stone.Pixel(x, y);
        if ((color >> 24) != 0)
          expected.Fill(stoneX + (x * 3), 72 + (y * 3), 3, 3, color);
      }
    }

    AssertTextPixels(expected, renderer.Frame);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(string.Empty, game.GameOverQuote);

    renderer.Draw(game, 0, 0);
    Assert.IsFalse(renderer.Frame.Pixels.Contains(0xFFFF6B6B));
  }

  [TestMethod]
  [DataRow(0x20)] // Space
  [DataRow(0x52)] // R
  [DataRow(0x41)] // A
  [DataRow(0x27)] // Right
  [DataRow(0x1B)] // Escape
  public void GameOverIgnoresOtherKeysAndStartsFreshOnlyOnEnter(int code)
  {
    CreatePit();
    var game = new Game([LevelPath]);
    LoseAllLives(game);
    Assert.IsFalse(DeathScreen.HandleInput(game, [code], true), "Other keys cannot bypass the last death animation.");
    Tick(game, Game.DeathDurationTicks);
    Assert.IsFalse(DeathScreen.HandleInput(game, [code], true));
    Assert.AreEqual(0, game.Lives);
    Assert.IsTrue(game.AwaitingRestart);
    Assert.IsFalse(DeathScreen.HandleInput(game, [0x0D], false), "Unfocused Enter is ignored.");
    Assert.IsTrue(DeathScreen.HandleInput(game, [0x0D], true));
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(1, game.LevelNumber);
    Assert.AreEqual(0, game.Score);
    Assert.AreEqual(0, game.TotalDeaths);
    Assert.IsFalse(game.IsDying);
  }

  [TestMethod]
  public void GameOverNeedsAFreshEnterPressButOrdinaryDeathsStillAcceptAnyKey()
  {
    CreatePit();
    var game = new Game([LevelPath]);

    Die(game);
    Assert.IsTrue(DeathScreen.HandleInput(game, [0x20], true));
    Assert.AreEqual(2, game.Lives);

    Die(game);
    Assert.IsTrue(DeathScreen.HandleInput(game, [0x41], true));

    Die(game);
    Tick(game, Game.DeathDurationTicks);
    var keyboard = new GameKeyboard();
    keyboard.KeyDown(0x0D);
    keyboard.BeginFrame();
    keyboard.KeyDown(0x0D, isRepeat: true);
    Assert.IsFalse(DeathScreen.HandleInput(game, keyboard.KeyPresses, true));
    keyboard.KeyUp(0x0D);
    keyboard.KeyDown(0x0D);
    Assert.IsTrue(DeathScreen.HandleInput(game, keyboard.KeyPresses, true));
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void GameOverMissingOrEmptyQuotesUseFallback(bool createFile)
  {
    CreatePit();
    if (createFile)
      File.WriteAllLines(NextPath, ["", "; ignored"]);
    var game = new Game([LevelPath], gameOverQuotesPath: NextPath);
    LoseAllLives(game);
    Assert.AreEqual("Bet you wish you could rewind? Nope.", game.GameOverQuote);
  }

  [TestMethod]
  public void BundledQuotesContainThirtyDistinctUncleLinesAndRequestedGameOverLines()
  {
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    string[] quotes = File.ReadAllLines(Path.Combine(assets, "uncle-quotes.txt"))
      .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray();

    Assert.IsTrue(quotes.Length >= 30);
    Assert.HasCount(quotes.Length, quotes.Distinct().ToArray());

    CollectionAssert.Contains(quotes, "Try not to die this time");
    CollectionAssert.Contains(quotes, "You dropped your pocket");

    string[] gameOver = File.ReadAllLines(Path.Combine(assets, "game-over-quotes.txt"));
    CollectionAssert.Contains(gameOver, "There are no saves, just like real life.");
    CollectionAssert.Contains(gameOver, "Bet you wish you could rewind? Nope.");
  }

  private Game CreateMap() => Map(
    "........................",
    "........................",
    "........................",
    "........................",
    ".S....................G.",
    "########################");

  private void CreatePit() => Map(
    "........................",
    "........................",
    "........................",
    ".S....................G.",
    "........................");

  private static void Die(Game game)
  {
    for (int tick = 0; tick < 180 && !game.IsDying; tick++)
      game.Update(0, false);

    Assert.IsTrue(game.IsDying);
  }

  private static void LoseAllLives(Game game)
  {
    for (int life = 0; life < 3; life++)
    {
      Die(game);

      if (life < 2)
      {
        Assert.AreEqual(string.Empty, game.GameOverQuote);

        Tick(game, Game.DeathDurationTicks);
        Assert.IsTrue(game.ContinueAfterDeath());
      }
    }
  }

  private static void AssertTextPixels(PixelBuffer expected, PixelBuffer actual)
  {
    for (int index = 0; index < expected.Pixels.Length; index++)
      if (expected.Pixels[index] != 0)
        Assert.AreEqual(expected.Pixels[index], actual.Pixels[index]);
  }
}
