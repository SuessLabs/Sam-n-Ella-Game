// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class DeathMessageTests : GameTestBase
{
  [TestMethod]
  public void EditableQuoteIsChosenOncePerDeathAndClearedOnRespawn()
  {
    CreatePit();
    string[] quotes = ["You bring shame to your family", "Dishonorable and destitute", "Gravity wins again."];
    File.WriteAllLines(NextPath, new[] { "; ignored comment", "", "   " }.Concat(quotes));
    var game = new Game([LevelPath], NextPath);

    for (int life = 0; life < 3; life++)
    {
      Die(game);
      string selected = game.DeathQuote;
      CollectionAssert.Contains(quotes, selected);
      game.Paused = true;
      Tick(game, 120);
      Assert.AreEqual(selected, game.DeathQuote);
      game.Paused = false;
      Tick(game, Game.DeathDurationTicks - 1);
      Assert.AreEqual(selected, game.DeathQuote, "The quote must not change between frames.");
      Tick(game, 1);
      Assert.AreEqual(selected, game.DeathQuote);
      Assert.IsTrue(game.ContinueAfterDeath());
      Assert.AreEqual(string.Empty, game.DeathQuote);
    }
  }

  [TestMethod]
  public void EditingQuoteFileChangesNextGameSession()
  {
    CreatePit();
    File.WriteAllText(NextPath, "First custom quote.");
    var first = new Game([LevelPath], NextPath);
    Die(first);
    Assert.AreEqual("First custom quote.", first.DeathQuote);

    File.WriteAllText(NextPath, "Another custom quote!");
    var second = new Game([LevelPath], NextPath);
    Die(second);
    Assert.AreEqual("Another custom quote!", second.DeathQuote);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void EmptyOrMissingQuoteFileUsesFallback(bool createFile)
  {
    CreatePit();
    if (createFile)
      File.WriteAllLines(NextPath, ["", " ; comment only", "  "]);

    var game = new Game([LevelPath], NextPath);
    Die(game);
    Assert.AreEqual("Gravity wins again.", game.DeathQuote);
  }

  [TestMethod]
  public void DeathTitleAndSmallerQuoteRenderThenDisappearOnRespawn()
  {
    CreatePit();
    File.WriteAllText(NextPath, "You bring shame to your family");
    var game = new Game([LevelPath], NextPath);
    Die(game);
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    PixelFont.Draw(expected, "YOU DIED!", 122, 38, 0xFFFF6B6B, 4);
    int quoteX = (PixelRenderer.Width - (((game.DeathQuote.Length * 4) - 1) * 2)) / 2;
    PixelFont.Draw(expected, game.DeathQuote, quoteX, 66, 0xFFF6F4DF, 2);
    AssertTextPixels(expected, renderer.Frame);

    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.AwaitingRestart);
    renderer.Draw(game, 0, 0);
    const string Prompt = "PRESS ANY KEY TO CONTINUE";
    int promptX = (PixelRenderer.Width - (((Prompt.Length * 4) - 1) * 2)) / 2;
    PixelFont.Draw(expected, Prompt, promptX, 112, 0xFFFFD56B, 2);
    AssertTextPixels(expected, renderer.Frame);
    Assert.IsTrue(game.ContinueAfterDeath());
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(renderer.Frame.Pixels.Contains(0xFFFF6B6B), "The death title must disappear after respawning.");
  }

  [TestMethod]
  public void LongEditedQuoteWrapsAndTruncatesWithinScreen()
  {
    CreatePit();
    File.WriteAllText(NextPath, new string('A', 200));
    var game = new Game([LevelPath], NextPath);
    Die(game);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    string[] lines = [new string('A', 42), new string('A', 42), new string('A', 39) + "..."];
    for (int index = 0; index < lines.Length; index++)
    {
      int x = (PixelRenderer.Width - (((lines[index].Length * 4) - 1) * 2)) / 2;
      PixelFont.Draw(expected, lines[index], x, 66 + (index * 12), 0xFFF6F4DF, 2);
    }
    AssertTextPixels(expected, renderer.Frame);
  }

  private void CreatePit() => Map(
    "....................", "....................", "....................",
    ".S................G.", "....................");

  private static void Die(Game game)
  {
    for (int tick = 0; tick < 120 && !game.IsDying; tick++)
      game.Update(0, false);
    Assert.IsTrue(game.IsDying);
  }

  private static void AssertTextPixels(PixelBuffer expected, PixelBuffer actual)
  {
    for (int index = 0; index < expected.Pixels.Length; index++)
    {
      if (expected.Pixels[index] != 0)
        Assert.AreEqual(expected.Pixels[index], actual.Pixels[index]);
    }
  }
}
