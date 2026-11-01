// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class BossDodgeTests : GameTestBase
{
  [TestMethod]
  [DataRow('U')]
  [DataRow('A')]
  [DataRow('H')]
  public void EveryBossDashesAwayAfterSecondActualHeadLanding(char marker)
  {
    var game = Arena(marker);
    var boss = game.Boss!;
    boss.Direction = 0;
    boss.ThrowTicksRemaining = boss.ProjectileAttackTicks = boss.CaneAttackTicksRemaining = 10000;
    LandOnHead(game, boss);
    Assert.AreEqual(1, boss.BossHeadStomps);
    Assert.AreEqual(0, boss.BossDodgeTicksRemaining);
    LandOnHead(game, boss);
    Assert.AreEqual(2, boss.BossHeadStomps);
    Assert.AreEqual(Enemy.BossDodgeDurationTicks, boss.BossDodgeTicksRemaining);
    Assert.AreEqual(0, boss.StunTicksRemaining);
    Assert.AreEqual(8, boss.Health);
    Assert.IsTrue(game.VelocityY < 0);
    double x = boss.X;
    game.Paused = true;
    Tick(game, 90);
    Assert.AreEqual(x, boss.X);
    Assert.AreEqual(Enemy.BossDodgeDurationTicks, boss.BossDodgeTicksRemaining);
    game.Paused = false;
    Tick(game, 12);
    Assert.IsTrue(Math.Abs(boss.X - x) > 2.5, "The dash must clear the stationary player's landing position.");
    Assert.IsTrue(Math.Abs(boss.X - game.X) > Game.BodyWidth);
    Tick(game, 6);
    Assert.AreEqual(0, boss.BossDodgeTicksRemaining);
    Assert.AreEqual(2, boss.BossHeadStomps, "The player cannot keep bouncing on the unmoving boss.");
    Assert.IsFalse(game.IsDying);
  }

  [TestMethod]
  [DataRow(EnemyKind.DungBag)]
  [DataRow(EnemyKind.OldMan)]
  [DataRow(EnemyKind.EmoBoss)]
  public void OnlyHeadStompsCountAndEverySecondSurvivingStompTriggersDodge(EnemyKind kind)
  {
    var boss = new Enemy(14, 0, kind);
    boss.Hit(1);
    Assert.AreEqual(0, boss.BossHeadStomps);
    Assert.AreEqual(0, boss.BossDodgeTicksRemaining);
    for (int hit = 1; hit <= 4; hit++)
    {
      boss.BossDodgeTicksRemaining = 0;
      Assert.IsFalse(boss.Stomp());
      Assert.AreEqual(hit, boss.BossHeadStomps);
      Assert.AreEqual(hit % 2 == 0 ? Enemy.BossDodgeDurationTicks : 0, boss.BossDodgeTicksRemaining);
    }

    boss.Hit(12);
    Assert.IsFalse(boss.Alive);
    Assert.AreEqual(0, boss.BossDodgeTicksRemaining);
    var ordinary = new Enemy(0, 0, settings: new EnemySettings(0, 4, 0));
    ordinary.Stomp();
    ordinary.Stomp();
    Assert.AreEqual(0, ordinary.BossDodgeTicksRemaining);
  }

  [TestMethod]
  public void DashingBossStaysInItsArenaAndTurnsAtSolidWalls()
  {
    var game = Arena('H');
    var boss = game.Boss!;
    boss.X = boss.PatrolOriginX + 3;
    boss.Direction = 1;
    boss.BossDodgeTicksRemaining = Enemy.BossDodgeDurationTicks;
    boss.ProjectileAttackTicks = 10000;
    game.Level.UseBlock(18, 18);
    game.Level.UseBlock(18, 17);
    for (int tick = 0; tick < Enemy.BossDodgeDurationTicks; tick++)
    {
      game.Update(0, false);
      Assert.IsTrue(boss.X >= boss.PatrolOriginX - 5 && boss.X <= boss.PatrolOriginX + 4);
      Assert.IsTrue(boss.X + Game.BodyWidth <= 18.001);
    }
  }

  [TestMethod]
  public void EmoGauntletHasWhipAfterGrowthAndKeepsItsMiddleEnemies()
  {
    string path = Path.Combine(AppContext.BaseDirectory, "Levels", "03-06-emo-kid-boss.txt");
    var level = Level.Load(path);
    Assert.AreEqual(256, level.Width);
    Assert.IsFalse(level.StartsWithShotgun);
    for (int y = 0; y < level.Height; y++)
      for (int x = 0; x < level.Width; x++)
        Assert.IsFalse("FQ".Contains(level.Tile(x, y)), "The gauntlet supplies a whip rather than guns.");

    Assert.AreEqual('M', level.Tile(18, 16));
    Assert.AreEqual('W', level.Tile(24, 16));
    Assert.AreEqual(1, Enumerable.Range(0, level.Width).Sum(x =>
      Enumerable.Range(0, level.Height).Count(y => level.Tile(x, y) == 'W')));

    int middleEnemies = level.FlySpawns.Concat(level.WormSpawns).Concat(level.BluePunkSpawns).Concat(level.PurplePunkSpawns)
      .Count(spawn => spawn.X >= 40 && spawn.X < 180);
    Assert.IsTrue(middleEnemies >= 12);
    Assert.IsTrue(level.EmoBossSpawns.Single().X >= 190);
    Assert.IsNotNull(level.ExitPipe);
    Assert.IsTrue(level.ExitPipe.X > level.EmoBossSpawns.Single().X);
  }

  private Game Arena(char marker)
  {
    string[] rows = Enumerable.Repeat(new string('.', 32), 20).Concat([new string('#', 32)]).ToArray();
    char[] start = rows[1].ToCharArray();
    start[14] = 'S';
    rows[1] = new string(start);
    char[] floor = rows[19].ToCharArray();
    floor[14] = marker;
    floor[30] = 'G';
    rows[19] = new string(floor);
    return Map(rows);
  }

  private static void LandOnHead(Game game, Enemy boss)
  {
    for (int tick = 0; tick < 90 && game.VelocityY < 1; tick++)
      game.Update(0, true);

    boss.X = game.X;
    boss.Y = game.Y + game.PlayerHeight + 0.01;
    boss.VelocityY = 0;
    game.Update(0, true);
  }
}
