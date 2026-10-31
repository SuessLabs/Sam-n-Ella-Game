using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Audio;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class SpeedRunsTests : GameTestBase
{
  private static string SourcePath => Path.Combine(AppContext.BaseDirectory, "Levels", "04-01-speed-runs.txt");

  [TestMethod]
  public void CampaignEndsWithSpeedRunsAndItsToiletInsteadOfAFlag()
  {
    var level = Level.Load(SourcePath);
    Assert.AreEqual("04-01", level.SectionLevelNumber);
    Assert.AreEqual("Speed Runs", level.DisplayName);
    Assert.IsTrue(level.IsSpeedRun);
    Assert.AreEqual(10, level.TimeLimitSeconds);
    Assert.IsTrue(Enumerable.Range(0, level.Width).Any(x => level.Tile(x, 19) == 'T'));
    Assert.IsFalse(Enumerable.Range(0, level.Height).Any(y => Enumerable.Range(0, level.Width).Any(x => level.Tile(x, y) == 'G')));
    int[] bottles = Enumerable.Range(0, level.Width).Where(x => level.Tile(x, 19) == 'k').ToArray();
    CollectionAssert.AreEqual(new[] { 100, 196, 292 }, bottles);
    Assert.IsTrue(bottles.Zip(bottles.Skip(1), (left, right) => right - left).All(gap => gap == 96));
    Assert.HasCount(8, level.EnemySpawns);
    Assert.HasCount(4, level.WormSpawns);
    Assert.HasCount(4, level.FlySpawns);
    Assert.IsTrue(Enumerable.Range(0, level.Width).Count(x => level.Tile(x, 19) == 'B') >= 16);
    var game = new Game([SourcePath]);
    int reliefPipes = Enumerable.Range(0, level.Height).Sum(y => Enumerable.Range(0, level.Width)
      .Count(x => game.CanUseReliefPipe(x, y) && level.Tile(x - 1, y) != level.Tile(x, y)));
    Assert.AreEqual(2, reliefPipes);
    string[] files = LevelSelectScreen.Discover(Path.Combine(AppContext.BaseDirectory, "Levels"));
    Assert.AreEqual(SourcePath, files.Single(path => Level.Load(path).SectionLevelNumber == "04-01"));
  }

  [TestMethod]
  public void TenSecondClockPausesAndTimeoutIsAPantsAccident()
  {
    var game = CountdownMap();
    Assert.AreEqual(600, game.LevelTicksRemaining);
    game.Paused = true;
    Tick(game, 600);
    Assert.AreEqual(600, game.LevelTicksRemaining);
    game.Paused = false;
    Tick(game, 599);
    Assert.AreEqual(1, game.LevelTicksRemaining);
    Assert.IsFalse(game.IsDying);
    Tick(game, 1);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(0, game.LevelTicksRemaining);
    Assert.AreEqual(2, game.Lives);
    StringAssert.Contains(game.DeathQuote, "shit your pants");
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(600, game.LevelTicksRemaining);
  }

  [TestMethod]
  public void PinkBottleResetsClockToExactlyTenSecondsAndIsConsumedOnce()
  {
    var game = CountdownMap(bottle: true);
    Tick(game, 120);
    Assert.AreEqual(480, game.LevelTicksRemaining);
    for (int tick = 0; tick < 40 && game.Level.Tile(6, 7) == 'k'; tick++)
      game.Update(1, false);

    Assert.AreEqual('.', game.Level.Tile(6, 7));
    Assert.AreEqual(600, game.LevelTicksRemaining);
    Assert.AreEqual(100, game.Score);
    Tick(game, 30);
    Assert.AreEqual(570, game.LevelTicksRemaining);
    game.Restart();
    Assert.AreEqual('k', game.Level.Tile(6, 7));
  }

  [TestMethod]
  public void EachPipeGrantsFiveSecondsOnceAndReliefPausesWithGame()
  {
    var game = PipeMap(speedRun: true);
    Tick(game, 6);
    int before = game.LevelTicksRemaining;
    Assert.IsTrue(game.CanUseReliefPipe(4, 6));
    game.Update(0, false, downHeld: true);
    Assert.AreEqual(before + 300, game.LevelTicksRemaining);
    Assert.AreEqual(Game.PipeReliefDurationTicks, game.PipeReliefTicksRemaining);
    Assert.IsFalse(game.CanUseReliefPipe(4, 6));
    Assert.IsFalse(game.CanUseReliefPipe(5, 6), "The other half of a wide pipe must not grant another bonus.");
    Assert.IsTrue(game.CanUseReliefPipe(12, 6));
    game.Paused = true;
    Tick(game, 100);
    Assert.AreEqual(before + 300, game.LevelTicksRemaining);
    Assert.AreEqual(Game.PipeReliefDurationTicks, game.PipeReliefTicksRemaining);
    game.Paused = false;
    Tick(game, Game.PipeReliefDurationTicks);
    before = game.LevelTicksRemaining;
    game.Update(0, false, downHeld: true);
    Assert.AreEqual(before - 1, game.LevelTicksRemaining);
    Assert.AreEqual(0, game.PipeReliefTicksRemaining);
    game.Restart();
    Assert.IsTrue(game.CanUseReliefPipe(4, 6));
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam, -1, true)]
  [DataRow(PlayerCharacter.Sam, 1, true)]
  [DataRow(PlayerCharacter.Ella, -1, true)]
  [DataRow(PlayerCharacter.Ella, 1, true)]
  [DataRow(PlayerCharacter.Sam, 1, false)]
  [DataRow(PlayerCharacter.Ella, -1, false)]
  public void PipeReliefKeepsPlayerSizeAndAnimatesBehindTheirFacingDirection(PlayerCharacter character, int direction, bool grown)
  {
    Map("SpeedRun=true", $"StartingPower={(grown ? "Growth" : "None")}",
      "................................", "................................", "................................", "................................",
      "................................", "....S...........................", "....PP......p...................",
      "....PP......p.................T.", "################################");
    var game = new Game([LevelPath]) { Character = character };
    Tick(game, 6);
    game.Update(direction, false);
    Tick(game, 6);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, direction, 0);
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    string spriteName = (character == PlayerCharacter.Ella ? "ella" : "player") + (grown ? "-large-idle" : "-idle");
    var sprite = Sprite.Load(Path.Combine(assets, spriteName + ".sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    int clock = game.LevelTicksRemaining;
    double height = game.PlayerHeight;
    game.Update(0, false, downHeld: true);
    Assert.AreEqual(clock + 300, game.LevelTicksRemaining);
    Assert.AreEqual(Game.PipeReliefDurationTicks, game.PipeReliefTicksRemaining);
    Assert.AreEqual(grown, game.IsLarge);
    Assert.AreEqual(height, game.PlayerHeight);

    int headY = 28 + (int)Math.Round((game.Y + height) * 16) - (grown ? 32 : 16);
    int headX = (int)Math.Round((game.X + Game.BodyWidth / 2) * 16) - 8;
    for (int age = 0; age < 10; age++)
    {
      renderer.Draw(game, 0, 0);
      for (int y = headY; y < headY + 8; y++)
        for (int x = headX; x < headX + 16; x++)
        {
          uint expected = sprite.Pixel(direction < 0 ? 15 - (x - headX) : x - headX, y - headY);
          if (expected != 0)
            Assert.AreEqual(expected, renderer.Frame.Pixels[y * PixelRenderer.Width + x],
              "The relief pose must preserve the standing sprite's head and full height.");
        }


      int puffY = 28 + (int)Math.Round((game.Y + height - 0.25) * 16) - 3;
      int[] puffPixels = Enumerable.Range(0, PixelRenderer.Width)
        .Where(x => renderer.Frame.Pixels[puffY * PixelRenderer.Width + x] == 0xFF754A44).ToArray();
      Assert.IsNotEmpty(puffPixels);
      Assert.IsTrue(puffPixels.All(x => direction > 0 ? x < game.X * 16 : x > (game.X + Game.BodyWidth) * 16));
      if (age == 0)
        SavePreview(renderer, $"pipe-relief-{character}-{direction}-{grown}.png");

      Tick(game, 1);
    }

    Tick(game, Game.PipeReliefDurationTicks - 10);
    Assert.AreEqual(grown, game.IsLarge);
    Assert.AreEqual(height, game.PlayerHeight);
  }

  [TestMethod]
  public void OrdinaryLevelPipesKeepTheirExistingBehavior()
  {
    var game = PipeMap(speedRun: false);
    Tick(game, 6);
    int before = game.LevelTicksRemaining;
    game.Update(0, false, downHeld: true);
    Assert.AreEqual(before - 1, game.LevelTicksRemaining);
    Assert.AreEqual(0, game.PipeReliefTicksRemaining);
    Assert.IsFalse(game.CanUseReliefPipe(4, 6));
  }

  [TestMethod]
  public void ToiletFreezesCountdownAndPlaysTwoFartsBeforeCredits()
  {
    var game = FinishMap();
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    ReachToilet(game);
    int clock = game.LevelTicksRemaining;
    Assert.IsFalse(game.Won);
    Tick(game, 30);
    Assert.IsTrue(game.IsSeatedOnToilet);
    game.Paused = true;
    Tick(game, 240);
    Assert.AreEqual(30, game.ToiletFinishAgeTicks);
    Assert.IsEmpty(sounds);
    game.Paused = false;
    Tick(game, 74);
    Assert.IsEmpty(sounds);
    Tick(game, 1);
    CollectionAssert.AreEqual(new[] { GameSound.FartShort }, sounds);
    Tick(game, 45);
    CollectionAssert.AreEqual(new[] { GameSound.FartShort, GameSound.FartLong }, sounds);
    Assert.IsFalse(game.Won);
    Assert.AreEqual(clock, game.LevelTicksRemaining);
    Tick(game, 59);
    Assert.IsFalse(game.Won);
    Tick(game, 1);
    Assert.IsTrue(game.Won);
    Assert.AreEqual(1000, game.Score);
    Assert.AreEqual(0L, game.CreditsTicks);
    Tick(game, 100);
    Assert.HasCount(2, sounds);
    Assert.AreEqual(1000, game.Score);
  }

  [TestMethod]
  public void ToiletAdvancesToNextLevelOnlyAfterAnimationAndRetryCancelsIt()
  {
    FinishMap();
    File.WriteAllLines(NextPath, ["................................", "................................", "................................",
      "................................", "................................", "................................", "................................",
      "...S..........................G.", "################################"]);
    var game = new Game([LevelPath, NextPath]);
    ReachToilet(game);
    Tick(game, Game.ToiletFinishDurationTicks - 1);
    Assert.AreEqual(1, game.LevelNumber);
    Tick(game, 1);
    Assert.AreEqual(2, game.LevelNumber);
    Assert.IsFalse(game.IsFinishingToilet);
    Assert.IsFalse(game.Won);
    Assert.AreEqual(100, game.LevelSecondsRemaining);
    game = FinishMap();
    ReachToilet(game);
    game.Restart();
    Assert.IsFalse(game.IsFinishingToilet);
    Assert.AreEqual(600, game.LevelTicksRemaining);
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam)]
  [DataRow(PlayerCharacter.Ella)]
  public void SittingAndCurtainHaveDistinctFramesForBothCharacters(PlayerCharacter character)
  {
    var game = FinishMap(character);
    ReachToilet(game);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    Tick(game, 30);
    renderer.Draw(game, 0, 0);
    uint[] seated = (uint[])renderer.Frame.Pixels.Clone();
    SavePreview(renderer, $"toilet-seated-{character}.png");
    Tick(game, 30);
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(seated.SequenceEqual(renderer.Frame.Pixels));
    uint[] halfCurtain = (uint[])renderer.Frame.Pixels.Clone();
    Tick(game, 30);
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(halfCurtain.SequenceEqual(renderer.Frame.Pixels));
    SavePreview(renderer, $"toilet-curtain-{character}.png");
  }

  [TestMethod]
  public void PinkBottlesRenderAndCountdownIsCenteredAtTop()
  {
    var game = CountdownMap(bottle: true);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    PixelFont.Draw(expected, "10", (PixelRenderer.Width - 14) / 2, 11, 0xFFFFD56B, 2);
    for (int index = 0; index < expected.Pixels.Length; index++)
      if (expected.Pixels[index] != 0)
        Assert.AreEqual(expected.Pixels[index], renderer.Frame.Pixels[index]);

    Assert.IsTrue(renderer.Frame.Pixels.Contains(0xFFF47CB8));
    SavePreview(renderer, "speed-runs-start.png");
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void FullRouteNeedsRefillsAndCanReachToiletWithNormalJumps(bool collectBottles)
  {
    var game = new Game([SourcePath]);
    if (!collectBottles)
    {
      game.Enemies.Clear(); // Isolate timeout from combat in the no-refill check.
      for (int y = 0; y < game.Level.Height; y++)
        for (int x = 0; x < game.Level.Width; x++)
          if (game.Level.Tile(x, y) == 'k')
            game.Level.Clear(x, y);
    }

    var sounds = new List<GameSound>();
    var refillTimes = new List<int>();
    game.SoundRequested += sounds.Add;
    // Patrol phases vary. Try a few launch timings for the same route.
    int attempts = collectBottles ? 61 : 1;
    for (int delay = 0; delay < attempts && !game.Won; delay++)
    {
      if (delay > 0)
      {
        game.Restart();
        sounds.Clear();
        refillTimes.Clear();
        Tick(game, delay);
      }

      int groundedTicks = 0;
      for (int tick = 0; tick < 6000 && !game.Won && !game.IsDying; tick++)
      {
        groundedTicks = game.Grounded ? groundedTicks + 1 : 0;
        bool obstacle = Enumerable.Range(1, 3).Any(ahead => game.Level.Solid((int)Math.Floor(game.X) + ahead, (int)Math.Floor(game.Y + game.PlayerHeight - 0.2)));
        bool pit = Enumerable.Range(1, 2).Any(ahead => !game.Level.Solid((int)Math.Floor(game.X + Game.BodyWidth) + ahead, 20));
        bool enemyAhead = game.Enemies.Any(enemy => enemy.CanInteract && enemy.X + Game.BodyWidth > game.X
          && enemy.X - game.X < 3 && enemy.Y + enemy.BodyHeight > game.Y);
        if (game.Grounded && groundedTicks >= 2 && (obstacle || pit || enemyAhead))
          game.QueueJump();

        int previousClock = game.LevelTicksRemaining;
        game.Update(1, true);
        if (game.LevelTicksRemaining > previousClock)
          refillTimes.Add(previousClock - 1);
      }
    }

    if (collectBottles)
    {
      Assert.IsTrue(game.Won, $"Stopped at ({game.X:F2},{game.Y:F2}), timer {game.LevelTicksRemaining}, dying={game.IsDying}.");
      Assert.AreEqual(3, game.Lives);
      Assert.HasCount(3, refillTimes);
      Assert.IsTrue(refillTimes.All(ticks => ticks is >= 90 and <= 135),
        $"A running player should reach bottles with about two seconds left: {string.Join(", ", refillTimes)} ticks.");
      Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.FartShort));
      Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.FartLong));
    }
    else
    {
      Assert.IsFalse(game.Won);
      Assert.IsTrue(game.IsDying);
      StringAssert.Contains(game.DeathQuote, "shit your pants");
    }
  }

  [TestMethod]
  public void ModeHeaderEnforcesTenSecondsAndRejectsInvalidValues()
  {
    var level = Level.Parse("speed", ["TimeLimit=999", "SpeedRun=true", "..........", ".S......T.", "##########"]);
    Assert.AreEqual(10, level.TimeLimitSeconds);
    Assert.IsTrue(level.IsSpeedRun);
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("speed", ["SpeedRun=maybe", "..........", ".S......T.", "##########"]));
  }

  private Game CountdownMap(bool bottle = false) => Map("SpeedRun=true",
    "................................", "................................", "................................", "................................",
    "................................", "................................", "................................",
    bottle ? "...S..k.......................T." : "...S..........................T.", "################################");

  private Game PipeMap(bool speedRun) => Map($"SpeedRun={speedRun}",
    "................................", "................................", "................................", "................................",
    "................................", "....S...........................", "....PP......p...................",
    "....PP......p.................T.", "################################");

  private Game FinishMap(PlayerCharacter character = PlayerCharacter.Sam)
  {
    Map("SpeedRun=true", "................................", "................................", "................................", "................................",
      "................................", "................................", "................................", "...ST...........................", "################################");
    return new Game([LevelPath]) { Character = character };
  }

  private static void ReachToilet(Game game)
  {
    for (int tick = 0; tick < 30 && !game.IsFinishingToilet; tick++)
      game.Update(1, false);

    Assert.IsTrue(game.IsFinishingToilet);
  }

  private static void SavePreview(PixelRenderer renderer, string filename)
  {
    string? directory = Environment.GetEnvironmentVariable("SAMELLA_SPEED_RUNS_PREVIEW");
    if (directory is not null)
      renderer.Frame.SavePng(Path.Combine(directory, filename));
  }
}
