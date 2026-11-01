// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Effects;

namespace SamElla.Tests;

[TestClass]
public sealed class DogTests : GameTestBase
{
  [TestMethod]
  public void BundledDogsStartAtLevelFiveAndHaveIndependentSettings()
  {
    string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    for (int index = 0; index < paths.Length; index++)
    {
      var level = Level.Load(paths[index]);
      Assert.AreEqual(index >= 4 && level.Section is 1 or 2 && level.BossSpawns.Count == 0 && level.OldManSpawns.Count == 0, level.DogSpawns.Count > 0, paths[index]);
      foreach (var spawn in level.DogSpawns)
        Assert.AreEqual('.', level.Tile(spawn.X, spawn.Y));
    }

    File.WriteAllLines(NextPath, ["[Dog]", "Speed=2.5", "PowerLevel=4", "HopVelocity=1"]);
    var config = EnemyConfiguration.Load(NextPath);
    Assert.AreEqual(2.5, config.Dog.Speed);
    Assert.AreEqual(4, config.Dog.PowerLevel);
    Assert.AreEqual(1, config.Dog.HopVelocity);
    Assert.AreEqual(EnemyConfiguration.DefaultWalker, config.Walker);
  }

  [TestMethod]
  public void DogLeavesPileAfterThreeGroundedSecondsAndStopsWhenDead()
  {
    var game = CreateGame();
    var dog = AddDog(game, 6);
    Tick(game, Enemy.PoopIntervalTicks - 1);
    Assert.IsEmpty(game.PoopPiles);

    Tick(game, 1);
    Assert.HasCount(1, game.PoopPiles);
    Assert.AreEqual(new PoopPile(7, 5), game.PoopPiles[0]);

    dog.Squash();
    Tick(game, Enemy.PoopIntervalTicks * 2);
    Assert.HasCount(1, game.PoopPiles);

    game.Restart();
    Assert.IsEmpty(game.PoopPiles);
  }

  [TestMethod]
  public void PoopHalvesSpeedForFiveSecondsAndSplashesForTwo()
  {
    var game = PreparePile();
    WalkIntoPile(game);
    Assert.AreEqual(300, game.SlowTicksRemaining);
    Assert.AreEqual(120, game.PoopSplatterTicksRemaining);
    Assert.AreEqual(game.PlayerSettings.Speed / 2, game.PlayerSpeed);
    Assert.IsEmpty(game.PoopPiles);
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(0, game.Score);

    double x = game.X;
    Tick(game, 120, 1);
    Assert.AreEqual(x + (game.PlayerSettings.Speed / 2 * 2), game.X, 0.00001);
    Assert.AreEqual(0, game.PoopSplatterTicksRemaining);
    Assert.AreEqual(180, game.SlowTicksRemaining);

    Tick(game, 179);
    Assert.AreEqual(1, game.SlowTicksRemaining);

    Tick(game, 1);
    Assert.AreEqual(0, game.SlowTicksRemaining);
    Assert.AreEqual(game.PlayerSettings.Speed, game.PlayerSpeed);
  }

  [TestMethod]
  public void PauseFreezesDogsAndBothPoopTimersAndFreshPileRefreshesThem()
  {
    var game = PreparePile();
    WalkIntoPile(game);
    var dog = AddDog(game, 25);
    game.Paused = true;
    Tick(game, 500);
    Assert.AreEqual(300, game.SlowTicksRemaining);
    Assert.AreEqual(120, game.PoopSplatterTicksRemaining);
    Assert.AreEqual(Enemy.PoopIntervalTicks, dog.PoopTicksRemaining);

    game.Paused = false;
    Tick(game, 60);
    game.Enemies.Clear();
    dog = AddDog(game, (int)Math.Floor(game.X) - 1);
    dog.PoopTicksRemaining = 1;
    Tick(game, 1);
    Assert.AreEqual(300, game.SlowTicksRemaining);
    Assert.AreEqual(120, game.PoopSplatterTicksRemaining);

    game.Restart();
    Assert.AreEqual(0, game.SlowTicksRemaining);
    Assert.AreEqual(0, game.PoopSplatterTicksRemaining);
  }

  [TestMethod]
  public void JumpingOverPileDoesNotSlowPlayer()
  {
    var game = PreparePile();
    game.QueueJump();
    Tick(game, 32, 1, true);
    Assert.IsGreaterThan(7, game.X);
    Assert.IsFalse(game.Grounded);
    Assert.HasCount(1, game.PoopPiles);
    Assert.AreEqual(0, game.SlowTicksRemaining);
  }

  [TestMethod]
  public void AirborneDogDoesNotLeaveFloatingPoop()
  {
    var game = CreateGame();
    var dog = AddDog(game, 12);
    dog.Y = 0;
    dog.PoopTicksRemaining = 1;
    game.Update(0, false);
    Assert.IsEmpty(game.PoopPiles);
    Assert.AreEqual(1, dog.PoopTicksRemaining);
  }

  [TestMethod]
  [DataRow("dog-walk-1", 0.0, false)]
  [DataRow("dog-walk-2", 0.2, false)]
  [DataRow("dog-squashed", 0.0, true)]
  public void DogUsesItsOwnEditableWalkAndSplatterSprites(string name, double elapsed, bool dead)
  {
    var game = CreateGame();
    var dog = AddDog(game, 6);

    if (dead)
      dog.Squash();

    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var sprite = Sprite.Load(Path.Combine(assets, name + ".sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, elapsed);

    int left = (int)Math.Round((dog.X + (Game.BodyWidth / 2)) * 16) - (sprite.Width / 2);
    int top = 28 + (int)Math.Round((dog.Y + dog.BodyHeight) * 16) - sprite.Height;

    for (int y = 0; y < sprite.Height; y++)
      for (int x = 0; x < sprite.Width; x++)
        if (sprite.Pixel(x, y) != 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
  }

  [TestMethod]
  public void ScreenSplatterDisappearsWhileSlowdownRemains()
  {
    var game = PreparePile();
    WalkIntoPile(game);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);

    uint[] splashed = (uint[])renderer.Frame.Pixels.Clone();
    Tick(game, 119);
    renderer.Draw(game, 0, 0);
    Assert.IsTrue(splashed.AsSpan(28 * PixelRenderer.Width)
      .SequenceEqual(renderer.Frame.Pixels.AsSpan(28 * PixelRenderer.Width)), "Only the HUD countdown should change before the splatter expires.");

    Tick(game, 1);
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(splashed.AsSpan(28 * PixelRenderer.Width)
      .SequenceEqual(renderer.Frame.Pixels.AsSpan(28 * PixelRenderer.Width)));
    Assert.AreEqual(180, game.SlowTicksRemaining);
  }

  [TestMethod]
  public void TranslucentSpriteBlendsWithoutReplacingTheWorld()
  {
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var sprite = Sprite.Load(Path.Combine(assets, "poop-screen-splatter.sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    var frame = new PixelBuffer(128, 96);

    frame.Clear(0xFFFFFFFF);
    frame.DrawTranslucent(sprite, 0, 0, 128);

    for (int y = 0; y < sprite.Height; y++)
      for (int x = 0; x < sprite.Width; x++)
      {
        uint source = sprite.Pixel(x, y);
        uint actual = frame.Pixels[(y * frame.Width) + x];
        if (source == 0)
          Assert.AreEqual(0xFFFFFFFF, actual);
        else
        {
          Assert.AreNotEqual(source, actual);
          Assert.AreNotEqual(0xFFFFFFFF, actual);
          uint red = ((((source >> 16) & 255) * 128) + (255 * 127)) / 255;
          Assert.AreEqual(red, (actual >> 16) & 255);
        }
      }

    // Zero opacity is a no-op even when the sprite is clipped at the frame edges.
    uint[] previous = (uint[])frame.Pixels.Clone();
    frame.DrawTranslucent(sprite, -60, -40, 0);
    CollectionAssert.AreEqual(previous, frame.Pixels);
  }

  private Game CreateGame() => Map(
    new string('.', 64), new string('.', 64), new string('.', 64), new string('.', 64),
    ".S" + new string('.', 60) + "G.", new string('#', 64));

  private static Enemy AddDog(Game game, double x)
  {
    var dog = new Enemy(x, 4.1, EnemyKind.Dog, new EnemySettings(0, 1, 0));
    game.Enemies.Add(dog);
    return dog;
  }

  private Game PreparePile()
  {
    var game = CreateGame();

    AddDog(game, 6);
    Tick(game, Enemy.PoopIntervalTicks);
    game.Enemies.Clear();

    return game;
  }

  private static void WalkIntoPile(Game game)
  {
    for (int tick = 0; tick < 60 && game.SlowTicksRemaining == 0; tick++)
      game.Update(1, false);

    Assert.IsGreaterThan(0, game.SlowTicksRemaining);
  }
}
