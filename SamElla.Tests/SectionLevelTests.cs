using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class SectionLevelTests
{
  [TestMethod]
  [DataRow("Name=The Boss's Arena!")]
  [DataRow(" name = The Boss's Arena! ")]
  public void NameHeaderOverridesFilenameAndPreservesMapCoordinates(string header)
  {
    var level = Level.Parse("02-03-unused-file-name", ["Background=Night", header, "TimeLimit=42", "",
      ".S........", ".........G", "##########"]);
    Assert.AreEqual("The Boss's Arena!", level.DisplayName);
    Assert.AreEqual("02-03-unused-file-name", level.Name);
    Assert.AreEqual(2, level.Section);
    Assert.AreEqual(3, level.SectionLevel);
    Assert.AreEqual("02-03", level.SectionLevelNumber);
    Assert.AreEqual((1, 0), level.Spawn);
    Assert.AreEqual(3, level.Height);
    Assert.AreEqual(42, level.TimeLimitSeconds);
    Assert.AreEqual(BackgroundTheme.Night, level.Background);
  }

  [TestMethod]
  public void NameAcceptsExactlyThirtyTwoCharacters()
  {
    string name = new('A', 32);
    var level = Level.Parse("01-01-test", [$"Name={name}", ".S........", ".........G", "##########"]);
    Assert.AreEqual(name, level.DisplayName);
  }

  [TestMethod]
  [DataRow("")]
  [DataRow("   ")]
  [DataRow("123456789012345678901234567890123")]
  [DataRow("Bad\tName")]
  public void InvalidNamesReportTheLimit(string name)
  {
    var error = Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("01-01-test",
      [$"Name={name}", ".S........", ".........G", "##########"]));
    StringAssert.Contains(error.Message, "Name must contain 1..32");
  }

  [TestMethod]
  public void LegacyFilesStillLoadAndSectionPrefixesAreRemovedFromFallbackNames()
  {
    string[] rows = [".S........", ".........G", "##########"];
    Assert.AreEqual("pipe gardens", Level.Parse("01-03-pipe-gardens", rows).DisplayName);
    Assert.AreEqual("pipe gardens", Level.Parse("03-pipe-gardens", rows).DisplayName);
    Assert.IsNull(Level.Parse("03-pipe-gardens", rows).SectionLevelNumber);
    Assert.AreEqual(32, Level.Parse("01-01-" + new string('a', 80), rows).DisplayName.Length);
  }

  [TestMethod]
  public void CampaignHasFiveLevelsThenBossAndSectionTwo()
  {
    string[] files = LevelSelectScreen.Discover(Path.Combine(AppContext.BaseDirectory, "Levels"));
    CollectionAssert.AreEqual(new[] { "01-01", "01-02", "01-03", "01-04", "01-05", "01-06", "02-01", "02-02", "02-03", "02-04", "02-05", "02-06", "03-01", "03-02", "03-03", "03-04", "03-05", "03-06", "04-01" },
      files.Select(file => Level.Load(file).SectionLevelNumber).ToArray());
    foreach (string file in files)
    {
      StringAssert.Matches(Path.GetFileName(file), new System.Text.RegularExpressions.Regex(@"^\d{2}-\d{2}-[A-Za-z]+(?:-[A-Za-z]+)*\.txt$"));
      Assert.IsTrue(File.ReadLines(file).Any(line => line.StartsWith("Name=")));
      Assert.IsTrue(Level.Load(file).DisplayName.Length is >= 1 and <= 32);
    }

    Assert.AreEqual("01-06-boss-fight.txt", Path.GetFileName(files[5]).ToLowerInvariant());
    var game = new Game(files, startingLevelIndex: 5);
    Assert.AreEqual("Boss Fight - Dung-Bag", game.Level.DisplayName);
    Assert.IsNotNull(game.Boss);
    game.Boss.Squash();
    for (int tick = 0; tick < 600 && game.LevelNumber == 6; tick++)
      game.Update(1, false);

    Assert.AreEqual(7, game.LevelNumber);
    Assert.AreEqual("02-01", game.LevelLabel);
    Assert.AreEqual(3, game.Lives);
  }

  [TestMethod]
  public void SelectionAndIntroUseSectionNumbersAndHeaderNames()
  {
    string[] files = LevelSelectScreen.Discover(Path.Combine(AppContext.BaseDirectory, "Levels"));
    var selection = new LevelSelectScreen(files);
    Assert.AreEqual("01-06", selection.Levels[5].DisplayNumber);
    Assert.AreEqual("Boss Fight - Dung-Bag", selection.Levels[5].Name);
    var game = new Game(files, startingLevelIndex: 6);
    var intro = new LevelStartScreen(Path.Combine(AppContext.BaseDirectory, "Assets", "uncle-quotes.txt"));
    intro.Show(game);
    Assert.AreEqual("02-01", intro.LevelLabel);
    Assert.AreEqual("Ruins Crossing", intro.LevelName);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawLevelStart(intro);
    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    string title = "LEVEL 02-01";
    PixelFont.Draw(expected, title, (PixelRenderer.Width - (title.Length * 4 - 1) * 4) / 2, 32, 0xFFFFD56B, 4);
    for (int pixel = 0; pixel < expected.Pixels.Length; pixel++)
    {
      if (expected.Pixels[pixel] != 0)
        Assert.AreEqual(expected.Pixels[pixel], renderer.Frame.Pixels[pixel]);
    }
  }
}
