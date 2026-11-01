// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Audio;
using SamElla.Effects;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class UnderworldTests : GameTestBase
{
  private static readonly string[] Names = ["03-01-drainpipe-descent.txt", "03-02-sludge-run.txt", "03-03-rusted-mains.txt",
    "03-04-bottle-alley.txt", "03-05-toxic-reservoir.txt", "03-06-emo-kid-boss.txt"];

  [TestMethod]
  public void SectionThreeHasFiveCombatLevelsThenBossAndBothPipePassages()
  {
    string directory = Path.Combine(AppContext.BaseDirectory, "Levels");
    var levels = LevelSelectScreen.Discover(directory).Select(Level.Load).Where(level => level.Section == 3).ToArray();
    Assert.HasCount(6, levels);
    CollectionAssert.AreEqual(Enumerable.Range(1, 6).ToArray(), levels.Select(level => level.SectionLevel!.Value).ToArray());
    foreach (var level in levels)
    {
      Assert.AreEqual(BackgroundTheme.Underworld, level.Background);
      Assert.IsTrue(Enumerable.Range(0, level.Width).Any(x => level.Tile(x, 20) == '~'));
    }

    foreach (var level in levels.Take(5))
    {
      Assert.IsNotEmpty(level.FlySpawns);
      Assert.IsNotEmpty(level.WormSpawns);
      Assert.IsNotEmpty(level.BluePunkSpawns);
      Assert.IsNotEmpty(level.PurplePunkSpawns);
      Assert.IsEmpty(level.EmoBossSpawns);
    }

    Assert.IsNotNull(levels[0].EntryPipe);
    Assert.HasCount(1, levels[5].EmoBossSpawns);
    Assert.IsNotNull(levels[5].ExitPipe);
  }

  [TestMethod]
  public void EntranceRequiresDownPausesDuringTravelAndRetryResetsDaylight()
  {
    var game = PipeMap(entry: true);
    Tick(game, 6);
    Assert.AreEqual(BackgroundTheme.Day, game.CurrentBackground);
    Assert.IsFalse(game.HasEnteredUnderworld);
    Assert.AreEqual(0, game.PipeTravelTicksRemaining);
    game.Update(0, false, downHeld: true);
    Assert.AreEqual(Game.PipeTravelDurationTicks, game.PipeTravelTicksRemaining);
    game.Paused = true;
    Tick(game, 120);
    Assert.AreEqual(Game.PipeTravelDurationTicks, game.PipeTravelTicksRemaining);
    game.Paused = false;
    Tick(game, Game.PipeTravelDurationTicks);
    Assert.IsTrue(game.HasEnteredUnderworld);
    Assert.AreEqual(BackgroundTheme.Underworld, game.CurrentBackground);
    Assert.AreEqual(14.0, game.X);
    Assert.AreEqual(GameMusic.Night2, GameMusicTracks.ForScreen(false, game));
    game.Restart();
    Assert.AreEqual(BackgroundTheme.Day, game.CurrentBackground);
    Assert.AreEqual(0, game.PipeTravelTicksRemaining);
    Assert.IsEmpty(game.EnemyProjectiles);
  }

  [TestMethod]
  public void ExitPipeLocksForBossThenReturnsToDayBeforeVictory()
  {
    var game = PipeMap(entry: false);
    Tick(game, 6);
    game.Update(0, false, downHeld: true);
    Assert.AreEqual(0, game.PipeTravelTicksRemaining);
    StringAssert.Contains(game.Message, "Defeat Emo Kid");
    game.Boss!.Squash();
    game.Update(0, false, downHeld: true);
    Tick(game, Game.PipeTravelDurationTicks);
    Assert.IsTrue(game.ReturnedToDaylight);
    Assert.AreEqual(BackgroundTheme.Day, game.CurrentBackground);
    Assert.AreEqual(GameMusic.Day1, GameMusicTracks.ForScreen(false, game));
    Assert.IsFalse(game.Won);
    Tick(game, 140, 1);
    Assert.IsTrue(game.Won);
  }

  [TestMethod]
  public void CirclingFliesIgnoreGravityAndWormsAreWeakGroundCrawlers()
  {
    var game = Arena();
    var fly = new Enemy(15, 3, EnemyKind.Fly);
    var worm = new Enemy(20, 7.5, EnemyKind.Worm);
    game.Enemies.Add(fly);
    game.Enemies.Add(worm);
    bool left = false, right = false;
    double minY = 10, maxY = 0;
    for (int tick = 0; tick < 152; tick++)
    {
      game.Update(0, false);
      left |= fly.X < 15;
      right |= fly.X > 15;
      minY = Math.Min(minY, fly.Y);
      maxY = Math.Max(maxY, fly.Y);
      Assert.IsTrue(fly.X >= 13.75 && fly.X <= 16.25);
    }

    Assert.IsTrue(left && right);
    Assert.IsTrue(maxY - minY > 1.5);
    Assert.AreEqual(1, worm.Health);
    Assert.IsTrue(worm.X < 20);
    Assert.IsTrue(worm.Y + worm.BodyHeight <= 8.001);
    Assert.IsTrue(worm.Stomp());
  }

  [TestMethod]
  [DataRow(EnemyKind.PunkBlue)]
  [DataRow(EnemyKind.PunkPurple)]
  public void PunksThrowArcingBottlesAndBossAlternatesNeedles(EnemyKind kind)
  {
    var game = Arena();
    var punk = new Enemy(9, 6.2, kind) { Direction = 0, ProjectileAttackTicks = 1 };
    game.Enemies.Add(punk);
    Tick(game, 1);
    Assert.HasCount(1, game.EnemyProjectiles);
    var bottle = game.EnemyProjectiles.Single();
    Assert.AreEqual(EnemyProjectileKind.BeerBottle, bottle.Kind);
    double y = bottle.Y;
    Tick(game, 5);
    Assert.IsTrue(bottle.Y < y);
    Assert.IsTrue(bottle.VelocityY > -6);
    punk.Squash();
    Tick(game, 1);
    Assert.IsEmpty(game.EnemyProjectiles);
    var boss = new Enemy(10, 5.3, EnemyKind.EmoBoss) { Direction = 0, ProjectileAttackTicks = 1 };
    game.Enemies.Add(boss);
    Tick(game, 1);
    Assert.AreEqual(EnemyProjectileKind.BeerBottle, game.EnemyProjectiles.Single().Kind);
    boss.ProjectileAttackTicks = 1;
    Tick(game, 1);
    Assert.IsTrue(game.EnemyProjectiles.Any(projectile => projectile.Kind == EnemyProjectileKind.Needle));
    Assert.AreEqual(10, boss.Health);
    Assert.IsTrue(boss.BodyHeight > punk.BodyHeight);
  }

  [TestMethod]
  [DataRow(EnemyProjectileKind.BeerBottle)]
  [DataRow(EnemyProjectileKind.Needle)]
  public void ProjectilesHitPlayerAndPauseFreezesThem(EnemyProjectileKind kind)
  {
    var game = Arena();
    var punk = new Enemy(9, 6.2, EnemyKind.PunkBlue) { Direction = 0, ProjectileAttackTicks = 1 };
    game.Enemies.Add(punk);
    if (kind == EnemyProjectileKind.Needle)
    {
      punk.Squash();
      var boss = new Enemy(9, 5.3, EnemyKind.EmoBoss) { Direction = 0, ProjectileAttackTicks = 1, ProjectileCount = 1 };
      game.Enemies.Add(boss);
    }

    Tick(game, 1);
    var projectile = game.EnemyProjectiles.Single();
    double x = projectile.X, y = projectile.Y;
    game.Paused = true;
    Tick(game, 100);
    Assert.AreEqual(x, projectile.X);
    Assert.AreEqual(y, projectile.Y);
    game.Paused = false;
    projectile.X = game.X + 0.2;
    projectile.Y = game.Y + 0.25;
    projectile.VelocityY = 0;
    Tick(game, 1);
    Assert.IsTrue(game.IsDying);
    Assert.IsEmpty(game.EnemyProjectiles);
  }

  [TestMethod]
  public void SolidWallStopsNeedleBeforeItCanHitPlayer()
  {
    var game = Arena();
    var boss = new Enemy(9, 5.3, EnemyKind.EmoBoss) { Direction = 0, ProjectileAttackTicks = 1, ProjectileCount = 1 };
    game.Enemies.Add(boss);
    Tick(game, 1);
    var needle = game.EnemyProjectiles.Single();
    needle.X = 5.8;
    needle.Y = 7.4;
    needle.VelocityY = 0;
    game.Level.UseBlock(5, 7);
    Tick(game, 10);
    Assert.IsEmpty(game.EnemyProjectiles);
    Assert.IsFalse(game.IsDying);
  }

  [TestMethod]
  public void BossCyclesAllSixQuotesAndTeenStompDeathGroansOnce()
  {
    var game = Arena();
    var boss = new Enemy(10, 5.3, EnemyKind.EmoBoss) { Direction = 0, ProjectileAttackTicks = 10000 };
    game.Enemies.Add(boss);
    string[] quotes = ["use toxic straws", "i'm confused", "i want free stuff", "i hate working", "responsibility is dumb", "Ok boomer"];
    foreach (string quote in quotes)
    {
      boss.SpeechTicks = 1;
      Tick(game, 1);
      Assert.AreEqual(quote, game.EnemyQuote);
      Tick(game, 150);
    }

    boss.Squash();
    var punk = new Enemy(22, 6.2, EnemyKind.PunkPurple) { Direction = 0, ProjectileAttackTicks = 10000 };
    punk.Hit(3);
    game.Enemies.Add(punk);
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    game.QueueJump();
    for (int tick = 0; tick < 100 && game.VelocityY < 1; tick++)
      game.Update(0, true);

    punk.X = game.X;
    punk.Y = game.Y + game.PlayerHeight + 0.01;
    punk.VelocityY = 0;
    game.Update(0, true);
    Assert.IsFalse(punk.Alive);
    Assert.AreEqual(Game.TeenDeathQuote, game.EnemyQuote);
    Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.TeenGroan));
    Tick(game, 10);
    Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.TeenGroan));
  }

  [TestMethod]
  public void SludgeSlowsMovementAndJumpingClearsIt()
  {
    var game = new Game([Path.Combine(AppContext.BaseDirectory, "Levels", Names[1])]);
    game.Enemies.Clear();
    Tick(game, 6);
    Assert.IsTrue(game.IsInSludge);
    Assert.AreEqual(8.4, game.PlayerSpeed, 0.00001);
    game.QueueJump();
    game.Update(0, true);
    Assert.IsFalse(game.IsInSludge);
    Assert.AreEqual(12.0, game.PlayerSpeed);
  }

  [TestMethod]
  public void SmallPlayerCanJumpOntoTallBossForTheFinalHit()
  {
    var game = Arena();
    var boss = new Enemy(4.6, 5.5, EnemyKind.EmoBoss) { Direction = 0, ProjectileAttackTicks = 10000 };
    boss.Hit(9);
    game.Enemies.Add(boss);
    Tick(game, 6);
    game.QueueJump();
    for (int tick = 0; tick < 30 && game.Y + game.PlayerHeight > boss.Y - 0.03; tick++)
      game.Update(0, true);

    Assert.IsTrue(game.Y + game.PlayerHeight < boss.Y);
    for (int tick = 0; tick < 20 && boss.Alive && !game.IsDying; tick++)
      game.Update(1, true);

    Assert.IsFalse(game.IsDying);
    Assert.IsFalse(boss.Alive);
    Assert.AreEqual(Game.TeenDeathQuote, game.EnemyQuote);
  }

  [TestMethod]
  public void SewageSceneryAndEveryNewActorRender()
  {
    var game = new Game([Path.Combine(AppContext.BaseDirectory, "Levels", Names[1])]);
    game.Enemies.Clear();
    game.Enemies.Add(new Enemy(7, 17, EnemyKind.Fly));
    game.Enemies.Add(new Enemy(9, 19.55, EnemyKind.Worm));
    game.Enemies.Add(new Enemy(11, 18.2, EnemyKind.PunkBlue) { Direction = 0 });
    game.Enemies.Add(new Enemy(13, 18.2, EnemyKind.PunkPurple) { Direction = 0 });
    game.Enemies.Add(new Enemy(15, 17.3, EnemyKind.EmoBoss) { Direction = 0, SpeechTicks = 1, ProjectileAttackTicks = 1 });
    game.Update(0, false);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    Assert.IsTrue(renderer.Frame.Pixels.Contains(0xFF142522));
    Assert.IsTrue(renderer.Frame.Pixels.Contains(0xFF8745B5));
    string? directory = Environment.GetEnvironmentVariable("SAMELLA_UNDERWORLD_PREVIEW");
    if (directory is not null)
      renderer.Frame.SavePng(Path.Combine(directory, "underworld-enemies.png"));
  }

  [TestMethod]
  [DataRow("EntryPipe=4,6,14")]
  [DataRow("EntryPipe=-1,6,14,7")]
  [DataRow("EntryPipe=30,6,14,7")]
  [DataRow("EntryPipe=4,6,14,8")]
  [DataRow("EntryPipe=4,6,14,0")]
  public void InvalidPipeCoordinatesAreRejected(string header)
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("bad-pipe", ["Background=Underworld", header,
      "................................", "................................", "................................",
      "................................", "................................", "....S...........................",
      "....PP..........................", "....PP.......................G..", "################################"]));
  }

  [TestMethod]
  public void DownAndSMapToPipeControl()
  {
    foreach (int code in new[] { 0x28, 0x53 })
    {
      var keyboard = new GameKeyboard();
      keyboard.KeyDown(code);
      Assert.IsTrue(keyboard.Down(GameKey.Down));
      keyboard.KeyUp(code);
      Assert.IsFalse(keyboard.Down(GameKey.Down));
    }
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(1)]
  [DataRow(2)]
  [DataRow(3)]
  [DataRow(4)]
  [DataRow(5)]
  public void EverySewageRouteCanReachItsExitWithNormalJumps(int index)
  {
    var game = new Game([Path.Combine(AppContext.BaseDirectory, "Levels", Names[index])]);
    game.Enemies.Clear(); // Combat is checked separately; this validates the level geometry.
    int goal = game.Level.Width - 5;
    int groundedTicks = 0;
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    bool saved = false;
    for (int tick = 0; tick < 6000 && !game.Won && !game.IsDying; tick++)
    {
      var pipe = game.AvailablePipe;
      double target = pipe is null ? goal : pipe.X + 0.3;
      int direction = game.X < target - 0.1 ? 1 : game.X > target + 0.5 ? -1 : 0;
      groundedTicks = game.Grounded ? groundedTicks + 1 : 0;
      bool obstacle = Enumerable.Range(1, 3).Any(ahead => game.Level.Solid((int)Math.Floor(game.X) + ahead, (int)Math.Floor(game.Y + game.PlayerHeight - 0.2)));
      bool pit = Enumerable.Range(1, 2).Any(ahead => !game.Level.Solid((int)Math.Floor(game.X + Game.BodyWidth) + ahead, 20));
      if (game.Grounded && groundedTicks >= 2 && direction != 0 && (obstacle || pit))
        game.QueueJump();

      game.Update(direction, true, fireHeld: index == 1 && tick % 32 == 0, downHeld: true);
      if (!saved && game.CurrentBackground == BackgroundTheme.Underworld && game.X > (index == 0 ? 35 : 10))
      {
        string? directory = Environment.GetEnvironmentVariable("SAMELLA_UNDERWORLD_PREVIEW");
        if (directory is not null)
        {
          renderer.Draw(game, 0, 0);
          renderer.Frame.SavePng(Path.Combine(directory, $"underworld-{index + 1}.png"));
        }

        saved = true;
      }
    }

    Assert.IsTrue(game.Won, $"{Names[index]}: stopped at ({game.X:F2},{game.Y:F2}), dying={game.IsDying}, pipe={game.AvailablePipe}.");
    Assert.AreEqual(3, game.Lives);
    if (index == 0)
      Assert.IsTrue(game.HasEnteredUnderworld);
    if (index == 5)
      Assert.IsTrue(game.ReturnedToDaylight);
  }

  [TestMethod]
  [DataRow(53, 4, false)]
  [DataRow(53, 4, true)]
  [DataRow(101, 3, false)]
  [DataRow(101, 3, true)]
  [DataRow(178, 4, false)]
  [DataRow(178, 4, true)]
  public void ReservoirPitsHaveJumpClearanceForBothPlayerSizes(int pitStart, int pitWidth, bool grown)
  {
    string[] source = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Levels", Names[4]));
    int mapStart = Array.FindIndex(source, line => line.StartsWith("...."));
    char[] spawnRow = source[mapStart + 19].Replace('S', '.').ToCharArray();
    spawnRow[pitStart - 6] = 'S';
    source[mapStart + 19] = new string(spawnRow);
    var game = Map(new[] { $"StartingPower={(grown ? "Shotgun" : "None")}" }.Concat(source).ToArray());
    game.Enemies.Clear();
    int landing = pitStart + pitWidth + 2;
    for (int tick = 0; tick < 180 && game.X < landing && !game.IsDying; tick++)
    {
      bool approachingPit = game.X + Game.BodyWidth >= pitStart - 2;
      if (game.Grounded && approachingPit)
        game.QueueJump();

      game.Update(1, true);
    }

    Assert.IsFalse(game.IsDying, $"Pit at {pitStart}, grown={grown}: fell at {game.X:F2}, {game.Y:F2}.");
    Assert.IsTrue(game.X >= landing);
    Assert.AreEqual(grown, game.IsLarge);
  }

  private Game Arena() => Map("................................", "................................", "................................",
    "................................", "................................", "................................", "................................",
    "...S.........................G..", "################################");

  private Game PipeMap(bool entry) => Map("Background=Underworld", $"{(entry ? "EntryPipe" : "ExitPipe")}=4,6,14,7",
    "................................", "................................", "................................",
    "................................", "................................", "....S...........................",
    "....PP..........................", "....PP................H......G..", "################################");
}
