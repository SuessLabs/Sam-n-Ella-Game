// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class PlayerSettingsTests : GameTestBase
{
  [TestMethod]
  [DataRow(2, 1, 2)]
  [DataRow(2, 2, 1)]
  [DataRow(3, 3, 1)]
  [DataRow(10, 3, 4)]
  [DataRow(10, 10, 1)]
  public void ConfiguredStompPowerSubtractsFromEnemyHealth(int health, int damage, int expectedHits)
  {
    var enemy = new Enemy(0, 0, EnemyKind.ToasterCat, new EnemySettings(4, health, 7));
    for (int hit = 1; hit <= expectedHits; hit++)
      Assert.AreEqual(hit == expectedHits, enemy.Stomp(damage));

    Assert.IsFalse(enemy.Alive);
    Assert.AreEqual(0, enemy.Health);
  }

  [TestMethod]
  public void EachFormUsesItsConfiguredSpeedJumpAndPowerAndShrinkSwitchesBack()
  {
    CreateGame();
    File.WriteAllLines(NextPath,
      ["[Player.PowerLevel1]", "Speed=6", "PowerLevel=1", "HopVelocity=12",
       "[Player.PowerLevel2]", "Speed=9", "PowerLevel=3", "HopVelocity=18"]);

    var game = new Game([LevelPath], enemySettingsPath: NextPath);

    Tick(game, 10);
    Assert.AreEqual(6, game.PlayerSettings.Speed);
    Assert.AreEqual(12, game.PlayerSettings.HopVelocity);

    double x = game.X;
    game.Update(1, false);
    Assert.AreEqual(x + (6 * Game.Step), game.X, 0.00001);

    game.QueueJump();
    game.Update(0, true);
    Assert.AreEqual(-12 + (40 * Game.Step), game.VelocityY, 0.00001);

    Tick(game, 60);
    Grow(game);
    Assert.AreEqual(3, game.PlayerPowerLevel);
    Assert.AreEqual(9, game.PlayerSettings.Speed);
    Assert.AreEqual(18, game.PlayerSettings.HopVelocity);

    x = game.X;
    game.Update(1, false);
    Assert.AreEqual(x + (9 * Game.Step), game.X, 0.00001);

    Tick(game, 15, 1);
    game.QueueJump();
    game.Update(0, true);
    Assert.AreEqual(-18 + (40 * Game.Step), game.VelocityY, 0.00001);

    game.Enemies.Add(new Enemy(game.X + 0.2, game.Y) { Direction = 0 });
    game.Update(0, false);
    Assert.IsFalse(game.IsLarge);
    Assert.AreEqual(1, game.PlayerPowerLevel);
    Assert.AreEqual(6, game.PlayerSettings.Speed);
    Assert.AreEqual(12, game.PlayerSettings.HopVelocity);
    Assert.AreEqual(3, game.Lives);
  }

  [TestMethod]
  [DataRow("Player.PowerLevel1", "PowerLevel=0")]
  [DataRow("Player.PowerLevel1", "Speed=-1")]
  [DataRow("Player.PowerLevel1", "HopVelocity=NaN")]
  [DataRow("Player.PowerLevel2", "PowerLevel=11")]
  [DataRow("Player.PowerLevel2", "Speed=Infinity")]
  [DataRow("Player.PowerLevel2", "HopVelocity=21")]
  public void InvalidPlayerSettingsIdentifyTheFileAndLine(string section, string setting)
  {
    File.WriteAllLines(NextPath, [$"[{section}]", setting]);

    var exception = Assert.ThrowsExactly<InvalidDataException>(() => EnemyConfiguration.Load(NextPath));
    Assert.IsTrue(exception.Message.Contains(NextPath));
    Assert.IsTrue(exception.Message.Contains(":2:"));
  }

  [TestMethod]
  public void OldEnemyOnlyConfigsKeepOriginalPlayerMovementDefaults()
  {
    File.WriteAllLines(NextPath, ["[Walker]", "Speed=3", "PowerLevel=1", "HopVelocity=0"]);
    var config = EnemyConfiguration.Load(NextPath);
    Assert.AreEqual(12, config.PlayerPowerLevel1.Speed);
    Assert.AreEqual(15, config.PlayerPowerLevel1.HopVelocity);
    Assert.AreEqual(1, config.PlayerPowerLevel1.PowerLevel);
    Assert.AreEqual(12, config.PlayerPowerLevel2.Speed);
    Assert.AreEqual(15, config.PlayerPowerLevel2.HopVelocity);
    Assert.AreEqual(2, config.PlayerPowerLevel2.PowerLevel);
  }

  [TestMethod]
  [DataRow(false, 1)]
  [DataRow(true, 2)]
  public void SmallPlayerNeedsTwoCatStompsAndLargePlayerNeedsOne(bool large, int damage)
  {
    var game = CreateGame();
    if (large)
      Grow(game);
    else
      Tick(game, 10);

    Assert.AreEqual(damage, game.PlayerPowerLevel);
    Tick(game, 15, 1);
    game.QueueJump();
    Tick(game, 10, jump: true);
    var cat = new Enemy(game.X, 4.1, EnemyKind.ToasterCat, new EnemySettings(0, 2, 0));
    game.Enemies.Add(cat);

    for (int tick = 0; tick < 90 && cat.Health == 2; tick++)
      game.Update(0, true);

    Assert.AreEqual(2 - damage, cat.Health);
    Assert.AreEqual(!large, cat.Alive);
    Assert.AreEqual(3, game.Lives);
    if (!large)
    {
      Assert.AreEqual(0, game.Score);
      for (int tick = 0; tick < 120 && cat.Alive; tick++)
        game.Update(0, true);

      Assert.IsFalse(cat.Alive);
    }

    Assert.AreEqual(large ? 500 : 400, game.Score);
    Assert.AreEqual(Enemy.SquashDurationTicks, cat.SquashTicksRemaining);
  }

  private static void Grow(Game game)
  {
    Tick(game, 10);
    game.QueueJump();
    for (int tick = 0; tick < 120 && !game.IsLarge; tick++)
    {
      game.Update(0, true);
      if (game.GrowthHearts.Count > 0)
        TouchHeart(game);
    }

    Assert.IsTrue(game.IsLarge);
    Tick(game, 60);
  }

  private Game CreateGame() => Map(
    "....................",
    "....................",
    ".M..................",
    "....................",
    ".S................G.",
    "####################");
}
