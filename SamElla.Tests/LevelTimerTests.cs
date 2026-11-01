// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class LevelTimerTests : GameTestBase
{
  [TestMethod]
  public void OldLevelsStartAtOneHundredSecondsAndCountDownOnExactSimulationTicks()
  {
    var game = CreateLevel();
    Assert.AreEqual(100, game.Level.TimeLimitSeconds);
    Assert.AreEqual(6000, game.LevelTicksRemaining);
    Assert.AreEqual(100, game.LevelSecondsRemaining);
    Tick(game, 59);
    Assert.AreEqual(100, game.LevelSecondsRemaining);
    Tick(game, 1);
    Assert.AreEqual(99, game.LevelSecondsRemaining);
    Assert.AreEqual(5940, game.LevelTicksRemaining);
    Tick(game, 60);
    Assert.AreEqual(98, game.LevelSecondsRemaining);
  }

  [TestMethod]
  [DataRow(1)]
  [DataRow(150)]
  [DataRow(9999)]
  public void PerLevelTimeLimitCombinesWithOtherHeadersWithoutMovingTheMap(int seconds)
  {
    var level = Level.Parse("timed", ["EnemyLedges=Turn", $"TimeLimit={seconds}", "Background=Night", "",
      ".S........", ".........G", "BBBBBBBBBB"]);
    Assert.AreEqual(seconds, level.TimeLimitSeconds);
    Assert.AreEqual((1, 0), level.Spawn);
    Assert.AreEqual(3, level.Height);
    Assert.AreEqual(BackgroundTheme.Night, level.Background);
    Assert.IsTrue(level.EnemiesAvoidLedges);
  }

  [TestMethod]
  [DataRow("0")]
  [DataRow("-1")]
  [DataRow("10000")]
  [DataRow("1.5")]
  [DataRow("forever")]
  [DataRow("")]
  [DataRow("99999999999999")]
  public void InvalidTimeLimitReportsTheFileAndAllowedRange(string value)
  {
    var error = Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("bad-timer", [$"TimeLimit={value}",
      ".S........", ".........G", "##########"]));
    StringAssert.Contains(error.Message, "bad-timer");
    StringAssert.Contains(error.Message, "TimeLimit=1..9999");
  }

  [TestMethod]
  public void ZeroCostsExactlyOneLifeAndRetryRestoresTheLevelsTimer()
  {
    var game = CreateLevel(1);
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    Tick(game, 59);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(1, game.LevelSecondsRemaining);
    Tick(game, 1);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(0, game.LevelTicksRemaining);
    Assert.AreEqual(2, game.Lives);
    Assert.AreEqual("TIME'S UP!", game.Message);
    Assert.AreEqual("Time's up!", game.DeathQuote);
    Tick(game, 300);
    Assert.AreEqual(2, game.Lives);
    CollectionAssert.AreEqual(new[] { GameSound.Died }, sounds);
    long runTicks = game.RunTicks;
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(60, game.LevelTicksRemaining);
    Assert.AreEqual(runTicks, game.RunTicks, "Retry must preserve the whole run's elapsed time.");
    game.Restart();
    Assert.AreEqual(60, game.LevelTicksRemaining);
    Assert.AreEqual(0L, game.RunTicks);
  }

  [TestMethod]
  public void PauseSettingsAndLevelIntroDoNotConsumeTheCountdown()
  {
    var game = CreateLevel(2);
    Tick(game, 15);
    int ticks = game.LevelTicksRemaining;
    game.Paused = true;
    var settings = new SettingsScreen(new AudioSettings());
    settings.Open();
    settings.HandleInput([0x25], true);
    Tick(game, 600);
    Assert.AreEqual(ticks, game.LevelTicksRemaining);
    game.Paused = false;
    var intro = new LevelStartScreen(NextPath);
    intro.Show(game);
    intro.Update(10, false, true);
    Assert.AreEqual(ticks, game.LevelTicksRemaining);
    Tick(game, 1);
    Assert.AreEqual(ticks - 1, game.LevelTicksRemaining);
  }

  [TestMethod]
  public void ReachingNextLevelLoadsItsOwnLimitAndTransitionDoesNotSpendIt()
  {
    CreateLevel(5);
    File.WriteAllLines(NextPath, ["TimeLimit=42", "", "....................", ".S.G................", "####################"]);
    var game = new Game([LevelPath, NextPath]);
    for (int tick = 0; tick < 300 && game.LevelNumber == 1; tick++)
      game.Update(1, false);

    Assert.AreEqual(2, game.LevelNumber);
    Assert.AreEqual(42 * 60, game.LevelTicksRemaining);
    Tick(game, 30);
    Assert.AreEqual(42 * 60, game.LevelTicksRemaining);
    game.CompleteLevelStart();
    Tick(game, 1);
    Assert.AreEqual(42 * 60 - 1, game.LevelTicksRemaining);
  }

  [TestMethod]
  public void CreditsUseExactTimeWastedLabelAndFreezeTheFinalTimers()
  {
    var game = Map("TimeLimit=7", "", "....................", ".S.G................", "####################");
    Tick(game, 60);
    for (int tick = 0; tick < 60 && !game.Won; tick++)
      game.Update(1, false);

    Assert.IsTrue(game.Won);
    var line = game.Credits!.Lines.Single(line => line.Text.StartsWith("Time Wasted: ", StringComparison.Ordinal));
    Assert.AreEqual("Time Wasted: 00:00:01", line.Text);
    Assert.IsTrue(line.PreserveCase);
    long runTicks = game.RunTicks;
    int ticks = game.LevelTicksRemaining;
    Tick(game, 600);
    Assert.AreEqual(runTicks, game.RunTicks);
    Assert.AreEqual(ticks, game.LevelTicksRemaining);
  }

  private Game CreateLevel(int seconds = 100) => Map($"TimeLimit={seconds}", "",
    "....................", ".S................G.", "####################");
}
