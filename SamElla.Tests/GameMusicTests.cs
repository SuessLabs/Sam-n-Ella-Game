using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class GameMusicTests : GameTestBase
{
  [TestMethod]
  [DataRow("Title", 16.0, true)]
  [DataRow("GameOver", 5.0, false)]
  [DataRow("Credits", 32 * 60.0 / 132, true)]
  [DataRow("Day1", 32 * 60.0 / 138, true)]
  [DataRow("Day2", 32 * 60.0 / 150, true)]
  [DataRow("Day3", 32 * 60.0 / 126, true)]
  [DataRow("Night1", 32 * 60.0 / 72, true)]
  [DataRow("Night2", 32 * 60.0 / 84, true)]
  [DataRow("Night3", 32 * 60.0 / 66, true)]
  public void TracksHaveExpectedDurationLoopPolicyAndCleanPcm(string name, double seconds, bool loops)
  {
    GameMusic music = Enum.Parse<GameMusic>(name);
    short[] samples = GameMusicTracks.Create(music);
    Assert.AreEqual((int)Math.Round(seconds * GameSounds.SampleRate), samples.Length);
    Assert.AreEqual(loops, GameMusicTracks.Loops(music));
    Assert.AreEqual(0, samples[0]);
    Assert.IsTrue(Math.Abs((int)samples[^1]) < 100);
    Assert.IsTrue(samples.Any(sample => Math.Abs((int)sample) > 2000));
    Assert.IsTrue(samples.All(sample => Math.Abs((int)sample) < short.MaxValue));
  }

  [TestMethod]
  public void GameOverMusicStartsAfterFinalDeathAnimationAndClearsOnRestart()
  {
    var game = Map("..........", ".S.....G..", "##########");
    Assert.AreEqual(GameMusic.Title, GameMusicTracks.ForScreen(true, game));
    Assert.AreEqual(GameMusic.Day1, GameMusicTracks.ForScreen(false, game));
    for (int life = 0; life < 3; life++)
    {
      for (int x = 0; x < game.Level.Width; x++)
        game.Level.Clear(x, 2);

      for (int tick = 0; tick < 180 && !game.IsDying; tick++)
        game.Update(0, false);

      Assert.IsTrue(game.IsDying);
      Assert.AreEqual(GameMusic.None, GameMusicTracks.ForScreen(false, game));
      Tick(game, Game.DeathDurationTicks);
      Assert.AreEqual(life == 2 ? GameMusic.GameOver : GameMusic.None, GameMusicTracks.ForScreen(false, game));
      if (life < 2)
        Assert.IsTrue(game.ContinueAfterDeath());
    }

    Tick(game, 360);
    Assert.AreEqual(GameMusic.GameOver, GameMusicTracks.ForScreen(false, game));
    game.Restart();
    Assert.AreEqual(GameMusic.Day1, GameMusicTracks.ForScreen(false, game));
  }

  [TestMethod]
  public void VictorySelectsCreditsMusicAndRestartStopsIt()
  {
    var game = Map("..........", ".SG.......", "##########");
    Tick(game, 60, 1);
    Assert.IsTrue(game.Won);
    Assert.AreEqual(GameMusic.Credits, GameMusicTracks.ForScreen(false, game));
    Tick(game, 180);
    Assert.AreEqual(GameMusic.Credits, GameMusicTracks.ForScreen(false, game));
    game.Restart();
    Assert.AreEqual(GameMusic.Day1, GameMusicTracks.ForScreen(false, game));
  }

  [TestMethod]
  [DataRow(BackgroundTheme.Day, "Day")]
  [DataRow(BackgroundTheme.Night, "Night")]
  public void LevelSongsCycleThroughThreeTracks(BackgroundTheme theme, string prefix)
  {
    for (int level = 1; level <= 9; level++)
    {
      GameMusic expected = Enum.Parse<GameMusic>(prefix + ((level - 1) % 3 + 1));
      Assert.AreEqual(expected, GameMusicTracks.ForLevel(theme, level));
      Assert.IsTrue(GameMusicTracks.Loops(expected));
    }

  }

  [TestMethod]
  [DataRow("Day", "Day", "Day2")]
  [DataRow("Day", "Night", "Night1")]
  [DataRow("Night", "Day", "Day1")]
  [DataRow("Night", "Night", "Night2")]
  public void ThemeSongOrderAdvancesOnlyForMatchingLevelsAndResetsOnRestart(string first, string second, string expected)
  {
    Map("Background=" + first, "..........", ".SG.......", "##########");
    File.WriteAllLines(NextPath, ["Background=" + second, "..........", ".SG.......", "##########"]);
    var game = new Game([LevelPath, NextPath]);
    for (int tick = 0; tick < 60 && game.LevelNumber == 1; tick++)
      game.Update(1, false);

    Assert.AreEqual(2, game.LevelNumber);
    Assert.AreEqual(Enum.Parse<GameMusic>(expected), GameMusicTracks.ForScreen(false, game));
    game.Restart();
    Assert.AreEqual(Enum.Parse<GameMusic>(first + "1"), GameMusicTracks.ForScreen(false, game));
  }

  [TestMethod]
  public void SixLevelSongsHaveDistinctAudio()
  {
    string[] names = ["Day1", "Day2", "Day3", "Night1", "Night2", "Night3"];
    var tracks = names.Select(name => GameMusicTracks.Create(Enum.Parse<GameMusic>(name))
      .Take(GameSounds.SampleRate * 2).ToArray()).ToArray();
    for (int i = 0; i < tracks.Length; i++)
    {
      for (int j = i + 1; j < tracks.Length; j++)
        Assert.IsFalse(tracks[i].SequenceEqual(tracks[j]), $"{names[i]} and {names[j]} must sound different.");
    }

  }

}
