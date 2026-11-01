// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class DungBagTests : GameTestBase
{
  [TestMethod]
  public void BossLevelIsHalfLengthWithPowerUpBeforeBossAndGoalBeyondIt()
  {
    string directory = Path.Combine(AppContext.BaseDirectory, "Levels");
    var previous = Level.Load(Path.Combine(directory, "01-05-pipe-terraces.txt"));
    var level = Level.Load(Path.Combine(directory, "01-06-Boss-Fight.txt"));
    Assert.AreEqual(previous.Width / 2, level.Width);
    Assert.AreEqual("Boss Fight - Dung-Bag", level.DisplayName);
    Assert.HasCount(1, level.BossSpawns);
    var spawn = level.BossSpawns.Single();
    Assert.IsTrue(spawn.X > level.Width * 0.8);
    Assert.IsTrue(Enumerable.Range(spawn.X + 1, level.Width - spawn.X - 1)
      .Any(x => level.Tile(x, spawn.Y) == 'G'));
    Assert.AreEqual('M', level.Tile(spawn.X - 17, spawn.Y - 3));
    Assert.AreEqual('.', level.Tile(spawn.X - 17, spawn.Y - 2));
    Assert.AreEqual('.', level.Tile(spawn.X - 17, spawn.Y - 1));
  }

  [TestMethod]
  public void SmallPlayerNeedsTenActualHeadLandingsEvenWithCustomPower()
  {
    File.WriteAllLines(NextPath, ["[Player.PowerLevel1]", "PowerLevel=3"]);
    Map("........................", ".S......................", "........................",
      "........................", "........................", "........................", "........................",
      "........................", "........................", "..............U.......G.", "########################");
    var game = new Game([LevelPath], enemySettingsPath: NextPath);
    var boss = game.Boss!;
    boss.Direction = 0;
    boss.ThrowTicksRemaining = 10000;
    for (int hit = 1; hit <= 10; hit++)
    {
      for (int tick = 0; tick < 90 && game.VelocityY < 1; tick++)
        game.Update(0, true);

      boss.X = game.X;
      boss.Y = game.Y + game.PlayerHeight + 0.01;
      boss.VelocityY = 0;
      game.Update(0, true);
      Assert.AreEqual(10 - hit, boss.Health, $"Stomp {hit}");
      Assert.AreEqual(hit < 10, boss.Alive);
      Assert.IsTrue(game.VelocityY < 0, "Each stomp must bounce the player.");
      Assert.IsFalse(game.IsDying);
    }

    Assert.AreEqual(2000, game.Score);
    Assert.IsNull(game.Boss);
  }

  [TestMethod]
  public void BossPatrolsBothDirectionsInsideArenaAndSleepsUntilApproached()
  {
    var game = CreateArena();
    var boss = game.Boss!;
    boss.ThrowTicksRemaining = 10000;
    bool left = false, right = false;
    for (int tick = 0; tick < 480; tick++)
    {
      game.Update(0, false);
      left |= boss.Direction < 0;
      right |= boss.Direction > 0;
      Assert.IsTrue(boss.X >= boss.PatrolOriginX - 5 - 0.001 && boss.X <= boss.PatrolOriginX + 4 + 0.001);
    }

    Assert.IsTrue(left && right);
    string file = Path.Combine(AppContext.BaseDirectory, "Levels", "01-06-Boss-Fight.txt");
    var campaign = new Game([file]);
    double initialX = campaign.Boss!.X;
    Tick(campaign, 300);
    Assert.AreEqual(initialX, campaign.Boss.X);
    Assert.IsEmpty(campaign.ThrownPoop);
  }

  [TestMethod]
  public void ThrownPoopArcsLandsAndSlowsPlayerWhenSteppedOn()
  {
    var game = CreateArena();
    var boss = game.Boss!;
    boss.Direction = 0;
    boss.ThrowTicksRemaining = 1;
    Tick(game, 1);
    Assert.HasCount(1, game.ThrownPoop);
    var poop = game.ThrownPoop.Single();
    double x = poop.X, y = poop.Y;
    Tick(game, 15);
    Assert.IsTrue(poop.X < x);
    Assert.IsTrue(poop.Y < y, "The throw rises first.");
    bool descending = false;
    for (int tick = 0; tick < 90 && game.ThrownPoop.Count > 0; tick++)
    {
      descending |= poop.VelocityY > 0;
      game.Update(0, false);
    }

    Assert.IsTrue(descending);
    Assert.IsEmpty(game.ThrownPoop);
    Assert.HasCount(1, game.PoopPiles);
    var pile = game.PoopPiles.Single();
    Assert.IsTrue(game.Level.Solid(pile.X, pile.Y));
    boss.Squash();
    for (int tick = 0; tick < 120 && game.SlowTicksRemaining == 0; tick++)
      game.Update(1, false);

    Assert.AreEqual(Game.PoopSlowDurationTicks, game.SlowTicksRemaining);
    Assert.AreEqual(Game.PoopSplatterDurationTicks, game.PoopSplatterTicksRemaining);
    Assert.IsFalse(game.IsDying);
  }

  [TestMethod]
  public void PauseFreezesThrowsAndRetryRestoresBossAndClearsPoop()
  {
    var game = CreateArena();
    var boss = game.Boss!;
    boss.Direction = 0;
    boss.ThrowTicksRemaining = 1;
    Tick(game, 1);
    var poop = game.ThrownPoop.Single();
    double x = poop.X, y = poop.Y;
    game.Paused = true;
    Tick(game, 200);
    Assert.AreEqual(x, poop.X);
    Assert.AreEqual(y, poop.Y);
    Assert.AreEqual(Enemy.BossThrowIntervalTicks, boss.ThrowTicksRemaining);
    boss.Stomp();
    game.Restart();
    Assert.AreEqual(10, game.Boss!.Health);
    Assert.IsEmpty(game.ThrownPoop);
    Assert.IsEmpty(game.PoopPiles);
  }

  [TestMethod]
  public void GoalRemainsLockedUntilDungBagIsDefeated()
  {
    var game = Map("........................", "........................", "........................",
      "........................", "........................", "........................", "........................",
      "........................", "........................", ".S.G..........U.........", "########################");
    Tick(game, 10, 1);
    Assert.IsFalse(game.Won);
    StringAssert.Contains(game.Message, "Defeat Dung-Bag");
    game.Boss!.Squash();
    Tick(game, 1);
    Assert.IsTrue(game.Won);
  }

  [TestMethod]
  [DataRow("U.........", "S........G", "##########")]
  [DataRow(".....B....", "S....U...G", "##########")]
  public void BossRequiresHeadroom(string top, string middle, string bottom)
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("boss", [top, middle, bottom]));
  }

  [TestMethod]
  public void BossAndHealthBarRenderAndDeathUsesSquashedArt()
  {
    var game = CreateArena();
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0);
    uint[] alive = (uint[])renderer.Frame.Pixels.Clone();
    Assert.IsTrue(alive.Contains(0xFFE83B3B), "Boss health bar is red.");
    string? preview = Environment.GetEnvironmentVariable("SAMELLA_BOSS_PREVIEW");
    if (preview is not null)
      renderer.Frame.SavePng(preview);

    game.Boss!.Squash();
    renderer.Draw(game, 0);
    Assert.IsFalse(alive.SequenceEqual(renderer.Frame.Pixels));
  }

  [TestMethod]
  public void LargeStompsAndWeaponsUseNormalDamage()
  {
    var boss = new Enemy(10, 0, EnemyKind.DungBag);
    for (int hit = 0; hit < 4; hit++)
      Assert.IsFalse(boss.Stomp(2));

    Assert.AreEqual(2, boss.Health);
    Assert.IsTrue(boss.Stomp(2));
    var armedBoss = new Enemy(10, 0, EnemyKind.DungBag);
    Assert.IsFalse(armedBoss.Hit(4));
    Assert.AreEqual(6, armedBoss.Health);
  }

  [TestMethod]
  public void JumpingOverLandedPoopAvoidsSlowdown()
  {
    var game = CreateArena();
    var boss = game.Boss!;
    boss.Direction = 0;
    boss.ThrowTicksRemaining = 1;
    Tick(game, 90);
    Assert.HasCount(1, game.PoopPiles);
    int pileX = game.PoopPiles.Single().X;
    boss.Squash();
    for (int tick = 0; tick < 100 && game.X < pileX - 2; tick++)
      game.Update(1, false);

    game.QueueJump();
    for (int tick = 0; tick < 45 && game.X < pileX + 1; tick++)
      game.Update(1, true);

    Assert.IsTrue(game.X > pileX + 0.85);
    Assert.AreEqual(0, game.SlowTicksRemaining);
    Assert.HasCount(1, game.PoopPiles);
  }

  [TestMethod]
  public void AirbornePoopContactSlowsWithoutCostingALife()
  {
    var game = CreateArena();
    game.Boss!.Direction = 0;
    game.Boss.ThrowTicksRemaining = 1;
    Tick(game, 1);
    var poop = game.ThrownPoop.Single();
    poop.X = game.X;
    poop.Y = game.Y;
    Tick(game, 1);
    Assert.IsEmpty(game.ThrownPoop);
    Assert.AreEqual(Game.PoopSlowDurationTicks, game.SlowTicksRemaining);
    Assert.AreEqual(3, game.Lives);
    Assert.IsFalse(game.IsDying);
  }

  private Game CreateArena() => Map("........................", "........................", "........................",
    "........................", "........................", "........................", "........................",
    "........................", "........................", ".S............U.......G.", "########################");
}
