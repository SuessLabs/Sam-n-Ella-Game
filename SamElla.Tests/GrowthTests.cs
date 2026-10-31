using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class GrowthTests : GameTestBase
{
  [TestMethod]
  public void GrowthBlockIsUsedOnceAndPlayerGrowsWithoutEnteringSolidTiles()
  {
    var game = CreateGame();
    Grow(game);
    Tick(game, 60);

    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(Game.LargeBodyHeight, game.PlayerHeight);
    Assert.AreEqual(5, game.Y + game.PlayerHeight, 0.00001);
    Assert.AreEqual('X', game.Level.Tile(1, 2));
    Assert.AreEqual(100, game.Score);
    Assert.AreEqual(0, game.Coins);
    Assert.IsEmpty(game.RewardPopups, "Growth should not display a coin's +1 popup.");

    game.QueueJump();
    Tick(game, 60, jump: true);

    Assert.AreEqual(100, game.Score);
    Assert.AreEqual(Game.LargeBodyHeight, game.PlayerHeight);
  }

  [TestMethod]
  public void GrowthWaitsUntilPlayerLeavesLowCeiling()
  {
    var game = Map(
      "....................", "....................", "....................",
      ".M..................", ".S................G.", "####################");
    Tick(game, 10);
    game.QueueJump();
    Tick(game, 30, jump: true);

    Assert.AreEqual('X', game.Level.Tile(1, 3));
    Assert.HasCount(1, game.GrowthHearts);
    TouchHeart(game);
    Assert.IsFalse(game.IsLarge, "A one-tile corridor has no room to grow.");
    Tick(game, 10, 1);
    Assert.IsTrue(game.IsLarge, "Growth should complete when the player reaches open space.");
    Assert.AreEqual(5, game.Y + game.PlayerHeight, 0.00001);
  }

  [TestMethod]
  public void EnemyHitShrinksPlayerAndProtectionPreventsRepeatedDamage()
  {
    var game = CreateGame();
    Grow(game);
    Tick(game, 60);
    var enemy = new Enemy(game.X + 0.4, game.Y) { Direction = 0 };
    game.Enemies.Add(enemy);
    game.Update(0, false);

    Assert.IsFalse(game.IsLarge, "Contact with the enlarged upper body should also cause damage.");
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(5, game.Y + game.PlayerHeight, 0.00001);
    Assert.AreEqual(Game.DamageProtectionTicks, game.DamageTicksRemaining);
    Assert.AreEqual(0, game.TotalDeaths, "Shrinking should not count as a death.");
    Assert.IsTrue(enemy.Alive);

    // Keep the enemy touching the smaller player to exercise repeated contact.
    enemy.Y = game.Y;
    game.Paused = true;
    Tick(game, 120);
    Assert.AreEqual(Game.DamageProtectionTicks, game.DamageTicksRemaining);
    game.Paused = false;
    Tick(game, Game.DamageProtectionTicks - 1);
    Assert.AreEqual(1, game.DamageTicksRemaining);
    Assert.IsFalse(game.IsDying);
    Tick(game, 1);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(2, game.Lives);
    Assert.AreEqual(1, game.TotalDeaths);
  }

  [TestMethod]
  public void LargePlayerCannotWalkThroughAnOverhangAtHeadHeight()
  {
    var game = Map(
      "....................", "....................", ".M..................",
      ".......PPP..........", ".S................G.", "####################");
    Grow(game);
    Tick(game, 60);
    Tick(game, 50, 1);

    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(7, game.X + Game.BodyWidth, 0.00001);
    Assert.AreEqual(5, game.Y + game.PlayerHeight, 0.00001);
  }

  [TestMethod]
  public void SpikeLandingShrinksLargePlayerAndPlayerCanEscape()
  {
    var game = Map(
      "....................", "....................", ".M..................",
      "....................", ".S...^............G.", "####################");
    Grow(game);
    Tick(game, 60);
    Tick(game, 30, 1);
    game.QueueJump();

    for (int tick = 0; tick < 90 && game.IsLarge; tick++)
      game.Update(tick < 8 ? 1 : 0, true);

    Assert.IsFalse(game.IsLarge);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(3, game.Lives);
    Tick(game, 30, 1);
    Assert.IsFalse(game.IsDying);
    Assert.IsGreaterThan(6, game.X);
  }

  [TestMethod]
  public void PitStillKillsLargePlayerAndRespawnIsSmall()
  {
    var game = Map(
      "....................", "....................", ".M..................",
      "....................", ".S................G.", "########.....#######");
    Grow(game);
    Tick(game, 60);
    for (int tick = 0; tick < 150 && !game.IsDying; tick++)
      game.Update(1, false);

    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(2, game.Lives);
    Assert.IsFalse(game.IsLarge);
    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.IsFalse(game.IsLarge);
    Assert.IsFalse(game.IsDying);
  }

  [TestMethod]
  public void LargePlayerCanStillStompEnemiesWithoutShrinking()
  {
    var game = CreateGame();
    Grow(game);
    Tick(game, 60);
    Tick(game, 15, 1);
    game.QueueJump();
    Tick(game, 10, jump: true);
    var enemy = new Enemy(game.X, 4.1) { Direction = 0 };
    game.Enemies.Add(enemy);

    for (int tick = 0; tick < 90 && enemy.Alive; tick++)
      game.Update(0, true);

    Assert.IsFalse(enemy.Alive);
    Assert.AreEqual(Enemy.SquashDurationTicks, enemy.SquashTicksRemaining);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(3, game.Lives);
  }

  [TestMethod]
  public void GrowthSurvivesLevelTransitionButManualRestartClearsIt()
  {
    var game = CreateGame();
    File.WriteAllLines(NextPath,
      ["....................", "....................", "....................",
       "....................", ".S................G.", "####################"]);
    game = new Game([LevelPath, NextPath]);
    Grow(game);
    Tick(game, 110, 1);

    Assert.AreEqual(2, game.LevelNumber);
    Assert.IsTrue(game.IsLarge);
    game.Restart();
    Assert.IsFalse(game.IsLarge);
    Assert.AreEqual(0, game.DamageTicksRemaining);
  }

  [TestMethod]
  [DataRow(0, false, 0.0, "idle")]
  [DataRow(1, false, 0.0, "run-1")]
  [DataRow(1, false, 0.2, "run-2")]
  [DataRow(0, true, 0.0, "jump")]
  public void LargePlayerRendersEditableSpriteAtFeet(int direction, bool jump, double elapsed, string pose)
  {
    var game = CreateGame();
    Grow(game);
    Tick(game, 60);
    if (jump)
    {
      game.QueueJump();
      game.Update(0, true);
    }

    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var sprite = Sprite.Load(Path.Combine(assets, $"player-large-{pose}.sprite"), palette);
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, direction, elapsed);
    int left = (int)Math.Round((game.X + (Game.BodyWidth / 2)) * 16) - 8;
    int top = 28 + (int)Math.Round((game.Y + game.PlayerHeight) * 16) - 32;

    for (int y = 0; y < sprite.Height; y++)
    {
      for (int x = 0; x < sprite.Width; x++)
      {
        if (sprite.Pixel(x, y) != 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
      }
    }
  }

  private Game CreateGame() => Map(
    "....................", "....................", ".M..................",
    "....................", ".S................G.", "####################");

  private static void Grow(Game game)
  {
    Tick(game, 10);
    game.QueueJump();
    for (int tick = 0; tick < 90 && !game.IsLarge; tick++)
    {
      game.Update(0, true);
      if (game.GrowthHearts.Count > 0)
        TouchHeart(game);
      for (int y = (int)Math.Floor(game.Y + 0.00001); y <= (int)Math.Floor(game.Y + game.PlayerHeight - 0.00001); y++)
      {
        Assert.IsFalse(game.Level.Solid((int)game.X, y), "Growth must not put the player inside a solid block.");
      }
    }

    Assert.IsTrue(game.IsLarge, "The growth reward should enlarge the player once there is headroom.");
  }
}
