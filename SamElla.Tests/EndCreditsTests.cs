using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class EndCreditsTests : GameTestBase
{
  [TestMethod]
  public void FinalGoalCreatesCreditsWithNamesAndFrozenStatistics()
  {
    var game = CreateFinishGame();
    Finish(game);
    Assert.IsTrue(game.Won);
    Assert.IsNotNull(game.Credits);
    string[] lines = game.Credits.Lines.Select(line => line.Text).ToArray();
    foreach (string name in new[] { "Damian Suess", "Suess Labs", "Xeno Innovations", "Little Stinker" })
      CollectionAssert.Contains(lines, name);
    Assert.AreEqual(1100, game.Score);
    Assert.AreEqual(3, game.ItemsMissed);
    Assert.AreEqual(0, game.TotalDeaths);
    CollectionAssert.Contains(lines, "POINTS 1100");
    CollectionAssert.Contains(lines, "ITEMS MISSED 3");
    CollectionAssert.Contains(lines, "TIMES DIED 0");
    Assert.IsTrue(lines.Any(line => line.StartsWith("Time Wasted: ", StringComparison.Ordinal)));
    Assert.IsFalse(string.IsNullOrWhiteSpace(game.Credits.OpeningQuote));
    Assert.IsFalse(string.IsNullOrWhiteSpace(game.Credits.ClosingQuote));
    long ticks = game.RunTicks;
    TimeSpan time = game.CompletionTime;
    string opening = game.Credits.OpeningQuote;
    string closing = game.Credits.ClosingQuote;
    Tick(game, 600);
    Assert.AreEqual(600L, game.CreditsTicks);
    Assert.AreEqual(ticks, game.RunTicks);
    Assert.AreEqual(time, game.CompletionTime);
    Assert.AreEqual(opening, game.Credits.OpeningQuote);
    Assert.AreEqual(closing, game.Credits.ClosingQuote);
    game.Restart();
    Assert.IsFalse(game.Won);
    Assert.IsNull(game.Credits);
    Assert.AreEqual(0L, game.RunTicks);
    Assert.AreEqual(0L, game.CreditsTicks);
    Assert.AreEqual(0, game.ItemsMissed);
    Assert.AreEqual(0, game.TotalDeaths);
  }

  [TestMethod]
  public void IntermediateGoalDoesNotStartCreditsAndMissedItemsAccumulate()
  {
    Map("....................", "....................", ".S.G....o...........", "####################");
    File.WriteAllLines(NextPath,
      ["....................", "....................", ".S.G....M...........", "####################"]);
    var game = new Game([LevelPath, NextPath]);
    Tick(game, 15, 1);
    Assert.IsFalse(game.Won);
    Assert.IsNull(game.Credits);
    Assert.AreEqual(1, game.ItemsMissed);
    Tick(game, 60, 1);
    Assert.IsTrue(game.Won);
    Assert.AreEqual(2, game.ItemsMissed);
    Assert.AreEqual(2000, game.Score);
  }

  [TestMethod]
  public void ItemsCollectedBeforeDeathAreNotCountedAsMissedOnRetry()
  {
    var game = Map("....................", ".SG.................", ".o..................",
      "....................", "....................");
    for (int tick = 0; tick < 120 && !game.IsDying; tick++)
      game.Update(0, false);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(1, game.Coins);
    Assert.AreEqual(1, game.TotalDeaths);
    long deathStart = game.RunTicks;
    Tick(game, Game.DeathDurationTicks);
    Assert.AreEqual(deathStart + Game.DeathDurationTicks, game.RunTicks);
    long waiting = game.RunTicks;
    Tick(game, 600);
    Assert.AreEqual(waiting, game.RunTicks, "Waiting for a respawn key should not inflate completion time.");
    Assert.IsTrue(game.ContinueAfterDeath());
    Tick(game, 5, 1);
    Assert.IsTrue(game.Won);
    Assert.AreEqual(0, game.ItemsMissed, "The coin was already collected on the previous attempt.");
    Assert.AreEqual(1, game.TotalDeaths);
    Assert.AreEqual(1100, game.Score);
  }

  [TestMethod]
  [DataRow(false, 1)]
  [DataRow(true, 0)]
  public void ReleasedHeartsOnlyCountAsFoundWhenCollected(bool collect, int missed)
  {
    var game = Map("....................", "....................", ".M..................",
      "....................", ".S................G.", "####################");
    Tick(game, 10);
    game.QueueJump();
    for (int tick = 0; tick < 90 && game.GrowthHearts.Count == 0; tick++)
      game.Update(0, true);
    Assert.HasCount(1, game.GrowthHearts);
    if (collect)
      TouchHeart(game);
    else
      game.GrowthHearts[0].Y = game.Level.Height + 3;
    Tick(game, 60);
    Tick(game, 100, 1);
    Assert.IsTrue(game.Won);
    Assert.AreEqual(missed, game.ItemsMissed);
  }

  [TestMethod]
  public void CreditsScrollUpOverTranslucentBlackAndRunnersKeepMovingAtTheEnd()
  {
    var game = CreateFinishGame();
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    uint background = renderer.Frame.Pixels[(80 * PixelRenderer.Width) + 383];
    Finish(game);
    renderer.Draw(game, 0, 0);
    uint darkened = renderer.Frame.Pixels[(80 * PixelRenderer.Width) + 383];
    Assert.AreNotEqual(background, darkened);
    Assert.AreNotEqual(0xFF000000u, darkened, "The scene should remain visible through the black overlay.");
    var credits = game.Credits!;
    var author = credits.Lines.Single(line => line.Text == "Damian Suess");
    Tick(game, (198 + author.Y - 80) * 2);
    renderer.Draw(game, 0, 0);
    AssertCreditText(renderer.Frame, author, 80);
    Tick(game, 30);
    renderer.Draw(game, 0, 0);
    AssertCreditText(renderer.Frame, author, 65);
    Tick(game, 6000);
    renderer.Draw(game, 0, 0);
    uint[] finished = (uint[])renderer.Frame.Pixels.Clone();
    Tick(game, 60);
    renderer.Draw(game, 0, 0);
    Assert.IsTrue(finished.AsSpan(0, 198 * PixelRenderer.Width)
      .SequenceEqual(renderer.Frame.Pixels.AsSpan(0, 198 * PixelRenderer.Width)), "The closing quote should remain readable.");
    Assert.IsFalse(finished.AsSpan(206 * PixelRenderer.Width)
      .SequenceEqual(renderer.Frame.Pixels.AsSpan(206 * PixelRenderer.Width)), "Characters should keep running across the bottom.");
    game.Paused = true;
    renderer.Draw(game, 0, 0);
    uint[] paused = (uint[])renderer.Frame.Pixels.Clone();
    long creditsTicks = game.CreditsTicks;
    Tick(game, 120);
    renderer.Draw(game, 0, 1);
    CollectionAssert.AreEqual(paused, renderer.Frame.Pixels);
    Assert.AreEqual(creditsTicks, game.CreditsTicks);
  }

  [TestMethod]
  public void ActiveRunTimeExcludesPause()
  {
    var game = CreateFinishGame();
    Tick(game, 60);
    Assert.AreEqual(TimeSpan.FromSeconds(1), game.CompletionTime);
    game.Paused = true;
    Tick(game, 600);
    Assert.AreEqual(TimeSpan.FromSeconds(1), game.CompletionTime);
  }

  [TestMethod]
  public void OverlayBlackBlendsRatherThanReplacingTheBackground()
  {
    var frame = new PixelBuffer(1, 1);
    frame.Clear(0xFF804020);
    frame.OverlayBlack(0);
    Assert.AreEqual(0xFF804020u, frame.Pixels[0]);
    frame.OverlayBlack(128);
    Assert.AreEqual(0xFF3F1F0Fu, frame.Pixels[0]);
    frame.OverlayBlack(255);
    Assert.AreEqual(0xFF000000u, frame.Pixels[0]);
  }

  [TestMethod]
  public void CreditsReadEditableOpeningAndClosingQuotes()
  {
    string folder = Path.Combine(Path.GetTempPath(), $"samella-credits-{Guid.NewGuid():N}");
    Directory.CreateDirectory(folder);
    string opening = Path.Combine(folder, "credits-opening-quotes.txt");
    string closing = Path.Combine(folder, "credits-closing-quotes.txt");
    try
    {
      File.WriteAllLines(opening, ["; comment", "", "You survived the custom ending."]);
      File.WriteAllLines(closing, ["; comment", "Custom reaper demands a rematch."]);
      var credits = new EndCreditsScreen(folder, TimeSpan.FromSeconds(65), 4, 1234, 7);
      Assert.AreEqual("You survived the custom ending.", credits.OpeningQuote);
      Assert.AreEqual("Custom reaper demands a rematch.", credits.ClosingQuote);
      Assert.AreEqual(credits.OpeningQuote, credits.Lines[0].Text);
      CollectionAssert.Contains(credits.Lines.Select(line => line.Text).ToArray(), "Time Wasted: 00:01:05");
      File.WriteAllText(opening, new string('A', 200));
      File.WriteAllText(closing, "; no quotes");
      credits = new EndCreditsScreen(folder, TimeSpan.Zero, 0, 0, 0);
      Assert.IsTrue(credits.Lines.All(line => ((line.Text.Length * 4) - 1) * line.Scale <= PixelRenderer.Width));
      Assert.IsFalse(string.IsNullOrWhiteSpace(credits.ClosingQuote));
    }
    finally
    {
      File.Delete(opening);
      File.Delete(closing);
      Directory.Delete(folder);
    }
  }

  [TestMethod]
  public void ScrollingTextClipsToCreditsViewport()
  {
    var frame = new PixelBuffer(12, 12);
    PixelFont.Draw(frame, "A", 0, 0, 0xFFFFFFFF, 2, 3, 7);
    Assert.IsTrue(frame.Pixels.Any(pixel => pixel != 0));
    for (int y = 0; y < frame.Height; y++)
    {
      if (y < 3 || y >= 7)
        Assert.IsTrue(frame.Pixels.AsSpan(y * frame.Width, frame.Width).ToArray().All(pixel => pixel == 0));
    }
  }

  private Game CreateFinishGame() => Map("....................", "..........o?M.......",
    ".S.o.G..............", "####################");

  private static void Finish(Game game)
  {
    for (int tick = 0; tick < 60 && !game.Won; tick++)
      game.Update(1, false);
    Assert.IsTrue(game.Won);
  }

  private static void AssertCreditText(PixelBuffer actual, CreditLine line, int y)
  {
    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    int x = (PixelRenderer.Width - (((line.Text.Length * 4) - 1) * line.Scale)) / 2;
    PixelFont.Draw(expected, line.Text, x, y, 0xFFFFD56B, line.Scale, preserveCase: line.PreserveCase);
    for (int i = 0; i < expected.Pixels.Length; i++)
    {
      if (expected.Pixels[i] != 0)
        Assert.AreEqual(expected.Pixels[i], actual.Pixels[i]);
    }
  }
}
