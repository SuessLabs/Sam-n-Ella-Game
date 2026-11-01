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
public sealed class LevelSelectTests
{
  private string _directory = string.Empty;

  [TestInitialize]
  public void CreateLevels()
  {
    _directory = Path.Combine(Path.GetTempPath(), "samella-level-select-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(_directory);
    for (int number = 17; number >= 1; number--)
      WriteLevel(number);

  }

  [TestCleanup]
  public void DeleteLevels()
  {
    foreach (string file in Directory.GetFiles(_directory))
      File.Delete(file);

    Directory.Delete(_directory);
  }

  [TestMethod]
  public void DiscoverySortsLevelsAndAddsNewFilesWithNamesAndThemes()
  {
    var screen = new LevelSelectScreen(LevelSelectScreen.Discover(_directory));
    Assert.HasCount(17, screen.Levels);
    Assert.AreEqual(1, screen.Levels[0].Number);
    Assert.AreEqual("debug level 01", screen.Levels[0].Name);
    Assert.AreEqual(BackgroundTheme.Day, screen.Levels[0].Theme);
    Assert.AreEqual(BackgroundTheme.Night, screen.Levels[1].Theme);
    Assert.AreEqual(17, screen.Levels[^1].Number);

    WriteLevel(18);
    File.WriteAllText(Path.Combine(_directory, "ignored.csv"), "not a level");
    var refreshed = new LevelSelectScreen(LevelSelectScreen.Discover(_directory));
    Assert.HasCount(18, refreshed.Levels);
    Assert.AreEqual("debug level 18", refreshed.Levels[^1].Name);
    Assert.AreEqual(BackgroundTheme.Night, refreshed.Levels[^1].Theme);
  }

  [TestMethod]
  [DataRow(1, 1)]
  [DataRow(8, 1)]
  [DataRow(9, 2)]
  [DataRow(16, 2)]
  [DataRow(17, 3)]
  public void PaginationIncludesPartialFinalPage(int count, int pages)
  {
    var screen = new LevelSelectScreen(LevelSelectScreen.Discover(_directory).Take(count).ToArray());
    Assert.AreEqual(pages, screen.PageCount);
    for (int i = 0; i < count + 2; i++)
      screen.HandleInput([0x28], true);

    Assert.AreEqual(count - 1, screen.SelectedIndex);
    Assert.AreEqual(pages - 1, screen.Page);
    screen.HandleInput([0x22], true);
    Assert.AreEqual(count - 1, screen.SelectedIndex);
  }

  [TestMethod]
  public void PageNavigationFocusFreshKeysConfirmAndCancel()
  {
    var screen = new LevelSelectScreen(LevelSelectScreen.Discover(_directory));
    screen.HandleInput([0x28, 0x22, 0x0D], false);
    Assert.AreEqual(0, screen.SelectedIndex);
    Assert.IsTrue(screen.IsActive);

    var keyboard = new GameKeyboard();
    keyboard.KeyDown(0x28);
    screen.HandleInput(keyboard.KeyPresses, true);
    keyboard.BeginFrame();
    keyboard.KeyDown(0x28, isRepeat: true);
    screen.HandleInput(keyboard.KeyPresses, true);
    Assert.AreEqual(1, screen.SelectedIndex);

    screen.HandleInput([0x27], true);
    Assert.AreEqual(9, screen.SelectedIndex);

    screen.HandleInput([0x22], true);
    Assert.AreEqual(16, screen.SelectedIndex);

    screen.HandleInput([0x21], true);
    Assert.AreEqual(8, screen.SelectedIndex);

    screen.HandleInput([0x25, 0x25, 0x26], true);
    Assert.AreEqual(0, screen.SelectedIndex);

    screen.HandleInput([0x0D, 0x28], true);
    Assert.IsTrue(screen.Confirmed);
    Assert.IsFalse(screen.IsActive);
    Assert.AreEqual(0, screen.SelectedIndex);

    var cancelled = new LevelSelectScreen(LevelSelectScreen.Discover(_directory));
    cancelled.HandleInput([0x1B, 0x0D], true);
    Assert.IsFalse(cancelled.IsActive);
    Assert.IsFalse(cancelled.Confirmed);
  }

  [TestMethod]
  public void SelectedLevelKeepsOriginalNumberProgressionAndCharacter()
  {
    string[] files = LevelSelectScreen.Discover(_directory);
    var game = new Game(files, startingLevelIndex: 8) { Character = PlayerCharacter.Ella };
    Assert.AreEqual(9, game.LevelNumber);
    Assert.AreEqual(17, game.LevelCount);
    Assert.AreEqual("debug level 09", game.Level.DisplayName);
    Assert.AreEqual(5, game.ThemeLevelNumber);
    for (int tick = 0; tick < 120 && game.LevelNumber == 9; tick++)
      game.Update(1, false);

    Assert.AreEqual(10, game.LevelNumber);
    Assert.AreEqual(PlayerCharacter.Ella, game.Character);
    game.Restart();
    Assert.AreEqual(1, game.LevelNumber);
    Assert.AreEqual(PlayerCharacter.Ella, game.Character);
  }

  [TestMethod]
  public void SelectedLevelRespawnsOnSameLevelAndSupportsKonamiCheat()
  {
    string[] files = LevelSelectScreen.Discover(_directory);
    var game = new Game(files, startingLevelIndex: 8, konamiCheat: true);
    Assert.AreEqual(30, game.Lives);
    Assert.IsTrue(game.HasFlamethrower);
    for (int x = 0; x < game.Level.Width; x++)
      game.Level.Clear(x, 2);

    for (int tick = 0; tick < 180 && !game.IsDying; tick++)
      game.Update(0, false);

    Assert.IsTrue(game.IsDying);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(9, game.LevelNumber);
    Assert.AreEqual(29, game.Lives);
  }

  [TestMethod]
  public void RendererHandlesLongNamesAndPartialPages()
  {
    string longPath = Path.Combine(_directory, "18-" + new string('a', 80) + ".txt");
    File.Copy(LevelSelectScreen.Discover(_directory)[0], longPath);
    var screen = new LevelSelectScreen(LevelSelectScreen.Discover(_directory));
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawLevelSelect(screen);
    uint[] first = (uint[])renderer.Frame.Pixels.Clone();
    screen.HandleInput([0x22, 0x22, 0x28], true);
    renderer.DrawLevelSelect(screen);
    Assert.AreEqual(17, screen.SelectedIndex);
    Assert.IsFalse(first.SequenceEqual(renderer.Frame.Pixels));
    Assert.IsTrue(renderer.Frame.Pixels.Any(pixel => pixel == 0xFFFFD56B));
  }

  private void WriteLevel(int number)
  {
    File.WriteAllLines(Path.Combine(_directory, $"{number:00}-debug-level-{number:00}.txt"),
      [number % 2 == 0 ? "Background=Night" : "Background=Day", "..........", ".S......G.", "##########"]);
  }

}
