// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class OverloadTests : GameTestBase
{
  [TestMethod]
  public void ExtraGrowthTriggersOversizedExplosionWithExactFrameTiming()
  {
    var game = CreateGame();
    Overload(game);

    Assert.AreEqual(2, game.Lives);
    Assert.AreEqual(1, game.TotalDeaths);
    Assert.IsTrue(game.IsDying);
    Assert.AreEqual(Game.OverloadedBodyHeight, game.PlayerHeight);
    Assert.IsGreaterThan(Game.LargeBodyHeight, game.PlayerHeight);
    Assert.AreEqual('X', game.Level.Tile(7, 4));
    Assert.AreEqual(200, game.Score);
    Assert.AreEqual(0, game.Coins);
    Assert.AreEqual(120, game.DeathTicksRemaining);

    double x = game.X, y = game.Y;
    var level = game.Level;
    game.Paused = true;
    Tick(game, 120, 1, true);
    Assert.AreEqual(120, game.DeathTicksRemaining);
    Assert.AreEqual(0, game.ExplosionFrameIndex);
    game.Paused = false;

    int[] frameEndTicks = [6, 12, 18, 24];
    int elapsedTicks = 0;
    for (int frame = 0; frame < 4; frame++)
    {
      Assert.AreEqual(frame, game.ExplosionFrameIndex);
      Tick(game, frameEndTicks[frame] - elapsedTicks - 1, 1, true);
      Assert.AreEqual(frame, game.ExplosionFrameIndex, "Each frame must last exactly 100 ms at 60 Hz.");
      Assert.AreEqual(x, game.X);
      Assert.AreEqual(y, game.Y);
      Assert.AreSame(level, game.Level);
      Assert.AreEqual(2, game.Lives, "The explosion must only consume one life.");
      Tick(game, 1);
      elapsedTicks = frameEndTicks[frame];
    }

    Assert.IsTrue(game.IsExploding, "The explosion death state should remain active until respawn.");
    Assert.IsFalse(game.IsExplosionVisible, "The player must disappear after the 400 ms animation.");
    Assert.AreEqual(3, game.ExplosionFrameIndex);
    Tick(game, 95, 1, true);
    Assert.AreEqual(1, game.DeathTicksRemaining);
    Assert.IsFalse(game.IsExplosionVisible);
    Assert.AreEqual(3, game.ExplosionFrameIndex);
    Assert.AreEqual(x, game.X);
    Assert.AreEqual(y, game.Y);
    Assert.AreSame(level, game.Level);
    Tick(game, 1);

    Assert.IsTrue(game.AwaitingRestart);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.IsFalse(game.IsExploding);
    Assert.IsFalse(game.IsDying);
    Assert.IsFalse(game.IsLarge);
    Assert.AreNotSame(level, game.Level);
    Assert.AreEqual(2, game.Lives);
    Assert.AreEqual(200, game.Score);
  }

  [TestMethod]
  public void TwoReleasedHeartsOnlyExceedTheLimitWhenCollected()
  {
    var game = Map(
      "....................", "....................", "....................", "....................",
      ".MM.................", "....................", "....................",
      ".S................G.", "####################");
    Tick(game, 10);
    Tick(game, 2, 1);
    game.QueueJump();
    for (int tick = 0; tick < 90 && game.GrowthHearts.Count == 0; tick++)
      game.Update(0, true);

    Assert.HasCount(2, game.GrowthHearts);
    Assert.IsFalse(game.IsExploding, "Releasing hearts must not count as collecting power-ups.");
    Assert.IsFalse(game.IsLarge);
    Assert.AreEqual(0, game.Score);
    TouchHeart(game);
    Assert.IsTrue(game.IsExploding);
    Assert.AreEqual('X', game.Level.Tile(1, 4));
    Assert.AreEqual('X', game.Level.Tile(2, 4));
    Assert.AreEqual(2, game.Lives);
  }

  [TestMethod]
  public void LastLifeShowsAllFourFramesThenRestartsGame()
  {
    var game = CreateGame();
    for (int remaining = 2; remaining >= 0; remaining--)
    {
      Overload(game);
      Assert.AreEqual(remaining, game.Lives);
      Tick(game, 119);
      Assert.IsTrue(game.IsExploding);
      Assert.AreEqual(3, game.ExplosionFrameIndex);
      Tick(game, 1);
      Assert.IsTrue(game.ContinueAfterDeath());
      Assert.IsFalse(game.IsExploding);
    }

    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(0, game.Score);
    Assert.AreEqual(Game.BodyHeight, game.PlayerHeight);
  }

  [TestMethod]
  public void RestartImmediatelyClearsExplosion()
  {
    var game = CreateGame();
    Overload(game);
    Tick(game, 16);
    game.Restart();
    Assert.IsFalse(game.IsExploding);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(0, game.ExplosionFrameIndex);
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(Game.BodyHeight, game.PlayerHeight);
  }

  [TestMethod]
  [DataRow(0, 0)]
  [DataRow(1, 6)]
  [DataRow(2, 12)]
  [DataRow(3, 18)]
  [DataRow(3, 23)]
  public void EachExplosionFrameRendersItsEditableBloodAndGutsSprite(int index, int elapsedTicks)
  {
    var game = CreateGame();
    Overload(game);
    Tick(game, elapsedTicks);
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var sprite = Sprite.Load(Path.Combine(assets, $"player-explosion-{index + 1}.sprite"), palette);
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    int left = Math.Clamp((int)Math.Round((game.X + (Game.BodyWidth / 2)) * 16) - 32, 0, PixelRenderer.Width - 64);
    int top = Math.Clamp(28 + (int)Math.Round((game.Y + game.PlayerHeight) * 16) - 64, 28, PixelRenderer.Height - 14 - 64);

    for (int y = 0; y < sprite.Height; y++)
    {
      for (int x = 0; x < sprite.Width; x++)
      {
        if (sprite.Pixel(x, y) != 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
      }
    }

    Assert.IsTrue(renderer.Frame.Pixels.Contains(palette['E']), "The frame must include blood.");
    Assert.IsTrue(renderer.Frame.Pixels.Contains(palette['H']), "The frame must include organ fragments.");
    Assert.IsTrue(renderer.Frame.Pixels.Contains(palette['T']), "The frame must retain clothing fragments.");
  }

  [TestMethod]
  [DataRow(24)]
  [DataRow(119)]
  public void PlayerDisappearsAfterLastExplosionFrameUntilRespawn(int elapsedTicks)
  {
    var game = CreateGame();
    Overload(game);
    Tick(game, elapsedTicks);

    Assert.IsTrue(game.IsDying);
    Assert.IsFalse(game.IsExplosionVisible);
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    foreach (char color in new[] { 'E', 'F', 'H', 'T' })
      Assert.IsFalse(renderer.Frame.Pixels.Contains(palette[color]), "No explosion, regular player, or death splatter should remain visible.");

    Tick(game, Game.DeathDurationTicks - elapsedTicks);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.IsFalse(game.IsDying);
    renderer.Draw(game, 0, 0);
    Assert.IsTrue(renderer.Frame.Pixels.Contains(palette['T']), "The player must become visible again upon respawn.");
  }

  private Game CreateGame() => Map(
    "....................", "....................", "....................", "....................",
    ".M.....M............", "....................", "....................",
    ".S................G.", "####################");

  private static void Overload(Game game)
  {
    Tick(game, 10);
    game.QueueJump();
    for (int tick = 0; tick < 90 && !game.IsLarge; tick++)
    {
      game.Update(0, true);
      if (game.GrowthHearts.Count > 0)
        TouchHeart(game);
    }
    Assert.IsTrue(game.IsLarge);
    Tick(game, 60);
    Tick(game, 30, 1);
    game.QueueJump();
    for (int tick = 0; tick < 90 && !game.IsExploding; tick++)
    {
      game.Update(0, true);
      if (game.GrowthHearts.Count > 0)
        TouchHeart(game);
    }
    Assert.IsTrue(game.IsExploding, "A second growth reward should exceed the single-growth limit.");
  }
}
