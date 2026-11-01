// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Effects;

namespace SamElla.Tests;

[TestClass]
public sealed class GrowthHeartTests : GameTestBase
{
  [TestMethod]
  public void BlockReleasesMovingHeartWithoutGivingGrowthOrPoints()
  {
    var game = CreateGame();
    var heart = Release(game);
    Assert.IsFalse(game.IsLarge);
    Assert.AreEqual(0, game.Score);
    Assert.AreEqual(0, game.Coins);
    Assert.AreEqual('X', game.Level.Tile(1, 2));
    Assert.AreEqual(0, heart.AgeTicks);

    double x = heart.X, y = heart.Y;
    game.Paused = true;
    Tick(game, 120);
    Assert.AreEqual(x, heart.X);
    Assert.AreEqual(y, heart.Y);
    Assert.AreEqual(0, heart.AgeTicks);

    game.Paused = false;
    Tick(game, 12);
    Assert.IsGreaterThan(x, heart.X);
    Assert.AreEqual(12, heart.AgeTicks);
    Assert.IsFalse(game.IsLarge);
  }

  [TestMethod]
  public void DeathFreezesHeartAndConfirmedRespawnClearsIt()
  {
    var game = CreateGame();
    var heart = Release(game);

    game.Enemies.Add(new Enemy(game.X + 0.4, game.Y) { Direction = 0 });
    game.Update(0, false);
    Assert.IsTrue(game.IsDying);

    double x = heart.X, y = heart.Y;
    int age = heart.AgeTicks;
    Tick(game, 240);
    Assert.AreEqual(x, heart.X);
    Assert.AreEqual(y, heart.Y);
    Assert.AreEqual(age, heart.AgeTicks);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.IsEmpty(game.GrowthHearts);

    Release(game);
    game.Restart();
    Assert.IsEmpty(game.GrowthHearts);
  }

  [TestMethod]
  [DataRow('P')]
  [DataRow('p')]
  [DataRow('#')]
  [DataRow('B')]
  public void HeartBouncesOffSolidObstaclesAndFloor(char obstacle)
  {
    string[] rows = ["....................", "....................", ".M..................",
      "....................", ".S................G.", "####################"];
    char[] row = rows[4].ToCharArray();
    row[6] = obstacle;
    rows[4] = new string(row);
    var game = Map(rows);
    var heart = Release(game);
    heart.X = 5.2;
    heart.Y = 4.1;
    heart.Direction = 1;
    heart.VelocityY = 0;
    game.Update(0, false);

    Assert.AreEqual(-1, heart.Direction);
    Assert.IsLessThan(0, heart.VelocityY, "Landing should make the heart hop.");
    Assert.IsLessThanOrEqualTo(6, heart.X + Game.BodyWidth);
    Assert.AreEqual(obstacle, game.Level.Tile(6, 4));
  }

  [TestMethod]
  [DataRow(0, 2)]
  [DataRow(6, 1)]
  public void HeartRendersItsEditableBeatSprite(int ticks, int spriteNumber)
  {
    var game = CreateGame();
    var heart = Release(game);
    Tick(game, ticks);
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var sprite = Sprite.Load(Path.Combine(assets, $"heart-{spriteNumber}.sprite"), palette);
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    int left = (int)Math.Round((heart.X + (Game.BodyWidth / 2)) * 16) - 8;
    int top = 28 + (int)Math.Round((heart.Y + Game.BodyHeight) * 16) - 16;
    for (int y = 0; y < 16; y++)
    {
      for (int x = 0; x < 16; x++)
      {
        if (sprite.Pixel(x, y) != 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
      }
    }
  }

  [TestMethod]
  [DataRow(0, 1)]
  [DataRow(6, 0)]
  [DataRow(12, 1)]
  [DataRow(18, 0)]
  [DataRow(35, 0)]
  [DataRow(36, 1)]
  public void HeartUsesDoubleBeatAnimation(int age, int expectedFrame)
  {
    var heart = new GrowthHeart(0, 0, 1) { AgeTicks = age };
    Assert.AreEqual(expectedFrame, heart.FrameIndex);
  }

  [TestMethod]
  public void MissedHeartFallsIntoPitWithoutAwardingPower()
  {
    var game = Map(
      "....................",
      "....................",
      ".M..................",
      "....................",
      ".S................G.",
      "########.....#######");

    var heart = Release(game);
    heart.X = 9;
    heart.Y = 4.1;
    heart.Direction = 0;
    heart.VelocityY = 28;
    Tick(game, 60);
    Assert.IsEmpty(game.GrowthHearts);
    Assert.IsFalse(game.IsLarge);
    Assert.AreEqual(0, game.Score);
  }

  [TestMethod]
  public void OpeningExtraGrowthBlockDoesNotOverloadUntilHeartIsCollected()
  {
    var game = Map(
      "....................",
      "....................",
      "....................",
      "....................",
      ".M.....M............",
      "....................",
      "....................",
      ".S................G.",
      "####################");

    Release(game);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.IsLarge);

    Tick(game, 30, 1);
    Release(game);
    Assert.IsTrue(game.IsLarge);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(100, game.Score);

    TouchHeart(game);
    Assert.IsTrue(game.IsExploding);
    Assert.AreEqual(200, game.Score);
    Assert.IsEmpty(game.GrowthHearts);
  }

  [TestMethod]
  public void PlayerMustChaseAndTouchHeartToGrow()
  {
    var game = CreateGame();
    Release(game);
    Tick(game, 30);
    Assert.IsFalse(game.IsLarge, "Standing under the block must not grant growth.");
    Assert.HasCount(1, game.GrowthHearts);

    for (int tick = 0; tick < 180 && !game.IsLarge; tick++)
    {
      double distance = game.GrowthHearts[0].X - game.X;
      game.Update(Math.Abs(distance) < 0.1 ? 0 : Math.Sign(distance), false);
    }

    Assert.IsTrue(game.IsLarge);
    Assert.IsEmpty(game.GrowthHearts);
    Assert.IsGreaterThan(1, game.X);
    Assert.AreEqual(100, game.Score);
    Assert.AreEqual(3, game.Lives);

    Tick(game, 120);
    Assert.AreEqual(100, game.Score, "A collected heart must not award points again.");
  }

  private static GrowthHeart Release(Game game)
  {
    Tick(game, 10);
    game.QueueJump();
    for (int tick = 0; tick < 90 && game.GrowthHearts.Count == 0; tick++)
      game.Update(0, true);

    Assert.HasCount(1, game.GrowthHearts);
    return game.GrowthHearts[0];
  }

  private Game CreateGame() => Map(
    "....................",
    "....................",
    ".M..................",
    "....................",
    ".S................G.",
    "####################");
}
