using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class EnemyLedgeTests : GameTestBase
{
  [TestMethod]
  [DataRow(EnemyKind.Walker)]
  [DataRow(EnemyKind.ToasterCat)]
  [DataRow(EnemyKind.Zombie)]
  [DataRow(EnemyKind.Dog)]
  [DataRow(EnemyKind.CreepyCrawler)]
  public void GuardedPatrolStaysOnItsIslandForThirtySecondsIncludingWhileHopping(EnemyKind kind)
  {
    var game = CreateIsland();
    var enemy = new Enemy(24, 6 - Height(kind), kind);
    if (enemy.IsBuried)
    {
      enemy.AdvanceEmergence(enemy.X - 1, enemy.Y);
      for (int tick = 0; tick < Enemy.CrawlerEmergenceDurationTicks; tick++)
        enemy.AdvanceEmergence(enemy.X - 1, enemy.Y);
    }

    game.Enemies.Add(enemy);
    bool sawLeft = false, sawRight = false, hopped = false;
    for (int tick = 0; tick < 1800; tick++)
    {
      game.Update(0, false);
      Assert.IsTrue(enemy.Alive, $"{kind}: patrol fell into the void at tick {tick}.");
      Assert.IsTrue(enemy.X >= 22 - 0.00001 && enemy.X + Game.BodyWidth <= 27 + 0.00001);
      Assert.IsTrue(enemy.Y + enemy.BodyHeight <= 6.00001);
      sawLeft |= enemy.Direction < 0;
      sawRight |= enemy.Direction > 0;
      hopped |= enemy.VelocityY < 0;
    }

    Assert.IsTrue(sawLeft && sawRight);
    Assert.AreEqual(kind == EnemyKind.ToasterCat, hopped);
  }

  [TestMethod]
  public void HighSpeedEnemyCannotStepOffEitherSideOfASingleBrick()
  {
    var game = CreateIsland(singleBrick: true);
    var enemy = new Enemy(24, 5.1, settings: new EnemySettings(30, 1, 0));
    game.Enemies.Add(enemy);
    for (int tick = 0; tick < 300; tick++)
      game.Update(0, false);

    Assert.IsTrue(enemy.Alive);
    Assert.IsTrue(enemy.X >= 24 && enemy.X + Game.BodyWidth <= 25);
  }

  [TestMethod]
  public void DestroyedSupportStillMakesGuardedEnemiesFall()
  {
    var game = CreateIsland();
    var enemy = new Enemy(24, 5.1);
    game.Enemies.Add(enemy);
    for (int x = 22; x < 27; x++)
      game.Level.Clear(x, 6);

    for (int tick = 0; tick < 180 && enemy.Alive; tick++)
      game.Update(0, false);

    Assert.IsFalse(enemy.Alive);
  }

  [TestMethod]
  public void IslandHeadersPreserveCoordinatesAndOlderLevelsKeepTheirLedgeBehavior()
  {
    var map = Level.Parse("header", ["EnemyLedges=Turn", "Background=Night", "",
      ".S........", ".........G", "BBBBBBBBBB"]);
    Assert.IsTrue(map.EnemiesAvoidLedges);
    Assert.AreEqual(BackgroundTheme.Night, map.Background);
    Assert.AreEqual((1, 0), map.Spawn);
    Assert.AreEqual(3, map.Height);
    var old = Level.Parse("old", [".S........", ".........G", "##########"]);
    Assert.IsFalse(old.EnemiesAvoidLedges);
    string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    for (int index = 0; index < files.Length; index++)
      Assert.AreEqual(index >= 8, Level.Load(files[index]).EnemiesAvoidLedges);
  }

  [TestMethod]
  public void InvalidLedgeSettingExplainsAllowedValues()
  {
    var error = Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("bad",
      ["EnemyLedges=Maybe", ".S........", ".........G", "##########"]));
    StringAssert.Contains(error.Message, "EnemyLedges=Turn/Fall");
  }

  private Game CreateIsland(bool singleBrick = false) => Map(
    "Background=Night", "EnemyLedges=Turn", "",
    new string('.', 36), new string('.', 36), new string('.', 36),
    new string('.', 36), new string('.', 36),
    ".S...G" + new string('.', 30),
    "######" + new string('.', singleBrick ? 18 : 16)
      + new string('B', singleBrick ? 1 : 5) + new string('.', singleBrick ? 11 : 9),
    new string('.', 36), new string('.', 36));

  private static double Height(EnemyKind kind) => kind == EnemyKind.Zombie ? Game.LargeBodyHeight
    : kind == EnemyKind.CreepyCrawler ? Enemy.CrawlerBodyHeight : Game.BodyHeight;
}
