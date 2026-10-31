using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class PlayerRecordsTests : GameTestBase
{
  private static readonly DateTimeOffset FirstDate = new(2026, 10, 29, 12, 0, 0, TimeSpan.FromHours(-4));

  [TestMethod]
  public void OnlyImprovedRecordsReplaceTheirValuesAndDates()
  {
    var records = new PlayerRecords();
    Assert.IsTrue(records.RecordProgress(100, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(20), FirstDate));
    Assert.IsFalse(records.RecordProgress(100, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(20), FirstDate.AddDays(1)));
    Assert.IsFalse(records.RecordProgress(50, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(30), FirstDate.AddDays(1)));
    Assert.AreEqual(FirstDate, records.HighScoreDate);
    Assert.AreEqual(FirstDate, records.FastestCompletionDate);
    Assert.AreEqual(FirstDate, records.FastestGameOverDate);
    Assert.IsTrue(records.RecordProgress(200, null, null, FirstDate.AddDays(2)));
    Assert.AreEqual(200, records.HighScore);
    Assert.AreEqual(FirstDate.AddDays(2), records.HighScoreDate);
    Assert.AreEqual(FirstDate, records.FastestCompletionDate);
    Assert.IsTrue(records.RecordProgress(0, TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(10), FirstDate.AddDays(3)));
    Assert.AreEqual(TimeSpan.FromSeconds(45), records.FastestCompletion);
    Assert.AreEqual(TimeSpan.FromSeconds(10), records.FastestGameOver);
    Assert.AreEqual(FirstDate.AddDays(3), records.FastestCompletionDate);
    Assert.AreEqual(FirstDate.AddDays(3), records.FastestGameOverDate);
    Assert.AreEqual(200, records.HighScore);
  }

  [TestMethod]
  public void EmptyAndInvalidRunsDoNotCreateTimedRecords()
  {
    var records = new PlayerRecords();
    Assert.IsFalse(records.RecordProgress(0, null, null, FirstDate));
    Assert.IsFalse(records.RecordProgress(-10, TimeSpan.Zero, TimeSpan.FromSeconds(-1), FirstDate));
    Assert.IsNull(records.HighScoreDate);
    Assert.IsNull(records.FastestCompletion);
    Assert.IsNull(records.FastestGameOver);
  }

  [TestMethod]
  public void SettingsRoundTripPreservesAudioAndRecordDatesWithoutLosingSubseconds()
  {
    var settings = new GameSettings();
    settings.Audio.MusicEnabled = false;
    settings.Audio.MusicVolume = 35;
    settings.Audio.SoundEffectsEnabled = false;
    settings.Audio.SoundEffectsVolume = 65;
    var completion = TimeSpan.FromTicks(123456789);
    var gameOver = TimeSpan.FromTicks(1234567);
    settings.Records.RecordProgress(12345, completion, gameOver, FirstDate);
    settings.Save(NextPath);
    var loaded = GameSettings.Load(NextPath);
    Assert.AreEqual(12345, loaded.Records.HighScore);
    Assert.AreEqual(completion, loaded.Records.FastestCompletion);
    Assert.AreEqual(gameOver, loaded.Records.FastestGameOver);
    Assert.AreEqual(FirstDate, loaded.Records.HighScoreDate);
    Assert.AreEqual(FirstDate.Offset, loaded.Records.HighScoreDate!.Value.Offset);
    Assert.AreEqual(FirstDate, loaded.Records.FastestCompletionDate);
    Assert.AreEqual(FirstDate, loaded.Records.FastestGameOverDate);
    Assert.IsFalse(loaded.Audio.MusicEnabled);
    Assert.IsFalse(loaded.Audio.SoundEffectsEnabled);
    Assert.AreEqual(35, loaded.Audio.MusicVolume);
    Assert.AreEqual(65, loaded.Audio.SoundEffectsVolume);
    loaded.Audio.MusicVolume = 80;
    loaded.Save(NextPath);
    var savedAgain = GameSettings.Load(NextPath);
    Assert.AreEqual(80, savedAgain.Audio.MusicVolume);
    Assert.AreEqual(12345, savedAgain.Records.HighScore);
    Assert.AreEqual(completion, savedAgain.Records.FastestCompletion);
    Assert.AreEqual(FirstDate, savedAgain.Records.FastestGameOverDate);
  }

  [TestMethod]
  public void MissingAndMalformedRecordsDoNotDiscardValidSettingsOrOtherRecords()
  {
    var empty = GameSettings.Load(NextPath);
    Assert.AreEqual(0, empty.Records.HighScore);
    Assert.IsNull(empty.Records.FastestGameOver);
    Assert.IsTrue(empty.Audio.MusicEnabled);
    File.WriteAllLines(NextPath,
    [
      "MusicEnabled=false", "MusicVolume=40", "HighScore=123", $"HighScoreDate={FirstDate:O}",
      "FastestCompletionTicks=-100", $"FastestCompletionDate={FirstDate:O}",
      "FastestGameOverTicks=10000", "FastestGameOverDate=invalid", "garbage",
    ]);
    var loaded = GameSettings.Load(NextPath);
    Assert.AreEqual(123, loaded.Records.HighScore);
    Assert.IsNull(loaded.Records.FastestCompletion);
    Assert.IsNull(loaded.Records.FastestGameOver);
    Assert.IsFalse(loaded.Audio.MusicEnabled);
    Assert.AreEqual(40, loaded.Audio.MusicVolume);
  }

  [TestMethod]
  public void FailedSavePreservesExistingSettingsAndCanBeRetried()
  {
    if (!OperatingSystem.IsWindows())
      return;

    var settings = new GameSettings();
    settings.Records.RecordProgress(100, null, null, FirstDate);
    settings.Save(NextPath);
    settings.Records.RecordProgress(200, null, null, FirstDate.AddDays(1));
    using (var locked = File.Open(NextPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
      settings.Save(NextPath);
      Assert.AreEqual(100, GameSettings.Load(NextPath).Records.HighScore);
    }

    settings.Save(NextPath);
    Assert.AreEqual(200, GameSettings.Load(NextPath).Records.HighScore);
  }

  [TestMethod]
  public void FinalGoalRecordsCompletionAndHighScoreBeforeCreditsAndRestart()
  {
    var game = Map("....................", "....................", ".S.G................", "####################");
    var settings = new GameSettings();
    Assert.IsFalse(settings.RecordProgress(game, FirstDate));
    for (int tick = 0; tick < 120 && !game.Won; tick++)
    {
      game.Update(1, false);
      settings.RecordProgress(game, FirstDate);
    }

    Assert.IsTrue(game.Won);
    Assert.AreEqual(game.Score, settings.Records.HighScore);
    Assert.AreEqual(game.CompletionTime, settings.Records.FastestCompletion);
    Assert.IsNull(settings.Records.FastestGameOver);
    Tick(game, 500);
    Assert.IsFalse(settings.RecordProgress(game, FirstDate.AddHours(1)));
    game.Restart();
    Assert.IsFalse(settings.RecordProgress(game, FirstDate.AddHours(2)));
    settings.Save(NextPath);
    Assert.AreEqual(1000, GameSettings.Load(NextPath).Records.HighScore);
    Assert.AreEqual(FirstDate, settings.Records.FastestCompletionDate);
  }

  [TestMethod]
  public void GameOverRecordsFinalLifeLossInsteadOfOrdinaryDeathOrWaitingTime()
  {
    var game = Map("....................", "....................", ".S................G.", "....................");
    var settings = new GameSettings();
    for (int life = 0; life < 3; life++)
    {
      for (int tick = 0; tick < 180 && !game.IsDying; tick++)
      {
        game.Update(0, false);
        settings.RecordProgress(game, FirstDate);
      }

      Assert.IsTrue(game.IsDying);
      if (life < 2)
      {
        Assert.IsNull(settings.Records.FastestGameOver);
        game.ContinueAfterDeath();
      }
    }

    Assert.AreEqual(0, game.Lives);
    TimeSpan finalDeathTime = game.CompletionTime;
    Assert.AreEqual(finalDeathTime, settings.Records.FastestGameOver);
    Tick(game, Game.DeathDurationTicks + 500);
    Assert.IsFalse(settings.RecordProgress(game, FirstDate.AddHours(1)));
    Assert.AreEqual(finalDeathTime, settings.Records.FastestGameOver);
    Assert.AreEqual(FirstDate, settings.Records.FastestGameOverDate);
    Assert.IsNull(settings.Records.FastestCompletion);
    game.ContinueAfterDeath();
    Assert.IsFalse(settings.RecordProgress(game, FirstDate.AddHours(2)));
  }
}
