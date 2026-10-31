using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class OldManTests : GameTestBase
{
  [TestMethod]
  public void SectionTwoEndsWithNewLevelAndOldManArena()
  {
    string directory = Path.Combine(AppContext.BaseDirectory, "Levels");
    string[] files = LevelSelectScreen.Discover(directory);
    int index = Array.FindIndex(files, file => Level.Load(file).SectionLevelNumber == "02-05");
    var game = new Game(files, startingLevelIndex: index);
    Assert.AreEqual("02-05", game.LevelLabel);
    game = new Game(files, startingLevelIndex: index + 1);
    Assert.AreEqual("02-06", game.LevelLabel);
    Assert.AreEqual(EnemyKind.OldMan, game.Boss!.Kind);
    Assert.AreEqual(10, game.Boss.Health);
    double x = game.Boss.X;
    Tick(game, 240);
    Assert.AreEqual(x, game.Boss.X, "The boss waits until the player approaches.");
    Assert.AreEqual(0, game.Boss.CaneSwingTicksRemaining);
    Assert.AreEqual(0, game.Boss.AirWaveLength);
  }

  [TestMethod]
  [DataRow(-1)]
  [DataRow(1)]
  public void CaneHasWindupAndWaveReachesExactlyTwoBlocks(int direction)
  {
    var game = Arena();
    var boss = PrepareAttack(game, direction, 2.1);
    Tick(game, 18);
    Assert.AreEqual(19, boss.CaneSwingTicksRemaining);
    Assert.AreEqual(direction, boss.AttackDirection);
    Assert.AreEqual(0, boss.AirWaveLength);
    Tick(game, 1);
    Assert.IsGreaterThan(0.0, boss.AirWaveLength);
    Tick(game, 17);
    Assert.AreEqual(2.0, boss.AirWaveLength, 0.00001);
    Assert.IsFalse(game.IsDying, "A player beyond two blocks stays safe.");
    Tick(game, 1);
    Assert.AreEqual(0.0, boss.AirWaveLength);
    Assert.IsEmpty(game.ThrownPoop);
  }

  [TestMethod]
  [DataRow(-1)]
  [DataRow(1)]
  public void WaveDamagesPlayerInRange(int direction)
  {
    var game = Arena();
    PrepareAttack(game, direction, 1.2);
    Tick(game, 36);
    Assert.IsTrue(game.IsDying);
  }

  [TestMethod]
  public void JumpClearsWaveAndPauseFreezesAttack()
  {
    var game = Arena();
    var boss = PrepareAttack(game, 1, 1.2);
    Tick(game, 17);
    game.Paused = true;
    int ticks = boss.CaneSwingTicksRemaining;
    Tick(game, 180);
    Assert.AreEqual(ticks, boss.CaneSwingTicksRemaining);
    game.Paused = false;
    game.QueueJump();
    Tick(game, 19, jump: true);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(2.0, boss.AirWaveLength, 0.00001);
    game.Paused = true;
    Tick(game, 180);
    Assert.AreEqual(2.0, boss.AirWaveLength, 0.00001);
    game.Restart();
    Assert.AreEqual(10, game.Boss!.Health);
    Assert.AreEqual(0.0, game.Boss.AirWaveLength);
    Assert.AreEqual(0, game.Boss.CaneSwingTicksRemaining);
  }

  [TestMethod]
  public void TerrainStopsWaveAndStompInterruptsIt()
  {
    var game = Arena();
    var boss = PrepareAttack(game, 1, 2.1);
    int wallX = (int)Math.Ceiling(boss.X + Game.BodyWidth);
    game.Level.UseBlock(wallX, 9);
    Tick(game, 36);
    Assert.IsTrue(boss.AirWaveLength < 1);
    boss.Stomp();
    Tick(game, 1);
    Assert.AreEqual(0.0, boss.AirWaveLength);
    Assert.AreEqual(0, boss.CaneSwingTicksRemaining);
    Assert.AreEqual(9, boss.Health);
  }

  [TestMethod]
  public void BossLocksGoalAndDefeatOpensExitAndClearsAttack()
  {
    var game = Map("........................", "........................", "........................",
      "........................", "........................", "........................", "........................",
      "........................", "........................", ".S.G..........A.........", "########################");
    Tick(game, 10, 1);
    Assert.IsFalse(game.Won);
    StringAssert.Contains(game.Message, "Defeat Old Man");
    var boss = game.Boss!;
    Assert.IsFalse(boss.Hit(4));
    Assert.IsFalse(boss.Hit(4));
    Assert.IsTrue(boss.Hit(2));
    Assert.IsNull(game.Boss);
    Assert.AreEqual(0.0, boss.AirWaveLength);
    Tick(game, 1);
    Assert.IsTrue(game.Won);
  }

  [TestMethod]
  [DataRow("A.........", "S........G", "##########")]
  [DataRow(".....B....", "S....A...G", "##########")]
  public void OldManRequiresHeadroom(string top, string middle, string bottom)
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("boss", [top, middle, bottom]));
  }

  [TestMethod]
  public void OldManAndWaveRenderWithDistinctAttackPoses()
  {
    var game = Arena();
    var boss = PrepareAttack(game, 1, 2.1);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    Tick(game, 1);
    renderer.Draw(game, 0, 0);
    uint[] windup = (uint[])renderer.Frame.Pixels.Clone();
    Tick(game, 35);
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(windup.SequenceEqual(renderer.Frame.Pixels));
    string? preview = Environment.GetEnvironmentVariable("SAMELLA_OLD_MAN_PREVIEW");
    if (preview is not null)
      renderer.Frame.SavePng(preview);

    boss.Squash();
    renderer.Draw(game, 0, 0);
  }

  private Game Arena() => Map("................................", "................................", "................................",
    "................................", "................................", "................................", "................................",
    "................................", "................................", "..........S...A...............G.", "################################");

  private static Enemy PrepareAttack(Game game, int direction, double gap)
  {
    Tick(game, 3);
    var boss = game.Boss!;
    boss.X = direction > 0 ? game.X - gap - Game.BodyWidth : game.X + Game.BodyWidth + gap;
    boss.Direction = 0;
    boss.CaneAttackTicksRemaining = 1;
    return boss;
  }
}
