// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class ZombieTests : GameTestBase
{
  [TestMethod]
  public void BundledLevelsIntroduceZombiesFromLevelFourOnward()
  {
    string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    for (int index = 0; index < paths.Length; index++)
    {
      var level = Level.Load(paths[index]);
      Assert.AreEqual(index >= 3 && level.Section is 1 or 2 && level.BossSpawns.Count == 0 && level.OldManSpawns.Count == 0, level.ZombieSpawns.Count > 0, paths[index]);
    }
  }

  [TestMethod]
  public void ZombieSpawnsAtPoweredUpHeightAndWalksSlowlyWithoutHopping()
  {
    var game = CreateLevel();
    var zombie = game.Enemies.Single();
    Assert.AreEqual(EnemyKind.Zombie, zombie.Kind);
    Assert.AreEqual(Game.LargeBodyHeight, zombie.BodyHeight);
    Assert.AreEqual(4.9, zombie.Y + zombie.BodyHeight, 0.00001);
    Assert.AreEqual('.', game.Level.Tile(12, 4));
    double startX = zombie.X;
    Tick(game, 60);
    Assert.AreEqual(startX - 1, zombie.X, 0.00001);
    Assert.AreEqual(5, zombie.Y + zombie.BodyHeight, 0.00001);
    Assert.AreEqual(0, zombie.VelocityY);
    game.Paused = true;
    Tick(game, 60);
    Assert.AreEqual(startX - 1, zombie.X, 0.00001);
  }

  [TestMethod]
  public void ZombieSettingsAreIndependentAndOptional()
  {
    var defaults = EnemyConfiguration.Load(NextPath);
    Assert.AreEqual(1, defaults.Zombie.Speed);
    Assert.AreEqual(3, defaults.Zombie.PowerLevel);
    Assert.AreEqual(0, defaults.Zombie.HopVelocity);
    File.WriteAllLines(NextPath, ["[Zombie]", "Speed=0.5", "PowerLevel=4", "HopVelocity=0"]);
    var config = EnemyConfiguration.Load(NextPath);
    Assert.AreEqual(0.5, config.Zombie.Speed);
    Assert.AreEqual(4, config.Zombie.PowerLevel);
    Assert.AreEqual(defaults.Walker, config.Walker);
    Assert.AreEqual(defaults.ToasterCat, config.ToasterCat);
  }

  [TestMethod]
  [DataRow("Z.........", "S........G", "##########")]
  [DataRow(".....B....", "S....Z...G", "##########")]
  public void ZombieNeedsHeadroom(string top, string middle, string bottom)
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("blocked-zombie", [top, middle, bottom]));
  }

  [TestMethod]
  public void ZombieCannotWalkUnderOneTileOverhang()
  {
    var game = CreateLevel();
    var zombie = game.Enemies.Single();
    game.Level.UseBlock(11, 3);
    game.Update(0, false);
    Assert.AreEqual(12, zombie.X, 0.00001);
    Assert.AreEqual(1, zombie.Direction);
  }

  [TestMethod]
  public void ContactWithUpperBodyDamagesPlayer()
  {
    var game = CreateAirborneLevel();
    var zombie = game.Enemies.Single();
    zombie.X = game.X;
    zombie.Y = game.Y + 0.5;
    game.Update(0, false);
    Assert.IsTrue(game.IsDying);
  }

  [TestMethod]
  public void FallingOnTallHeadStompsAndFinalHitLeavesSplatter()
  {
    var game = CreateAirborneLevel();
    Tick(game, 10);
    var zombie = game.Enemies.Single();
    zombie.X = game.X;
    zombie.Y = game.Y + Game.BodyHeight + 0.01;
    zombie.StunTicksRemaining = 20;
    zombie.Stomp(2);
    game.Update(0, false);
    Assert.IsFalse(zombie.Alive);
    Assert.IsFalse(game.IsDying);
    Assert.IsLessThan(0, game.VelocityY);
    Assert.AreEqual(600, game.Score);
    Assert.AreEqual(60, zombie.SquashTicksRemaining);
    Tick(game, 60);
    Assert.AreEqual(0, zombie.SquashTicksRemaining);
  }

  [TestMethod]
  [DataRow("zombie-walk-1", 0.0, false)]
  [DataRow("zombie-walk-2", 0.4, false)]
  [DataRow("zombie-squashed", 0.0, true)]
  public void ZombieSpritesRenderAtItsFeet(string name, double elapsed, bool dead)
  {
    var game = CreateLevel();
    var zombie = game.Enemies.Single();
    if (dead)
      zombie.Squash();
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var sprite = Sprite.Load(Path.Combine(assets, name + ".sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, elapsed);
    int left = (int)Math.Round((zombie.X + (Game.BodyWidth / 2)) * 16) - (sprite.Width / 2);
    int top = 28 + (int)Math.Round((zombie.Y + zombie.BodyHeight) * 16) - sprite.Height;
    for (int y = 0; y < sprite.Height; y++)
      for (int x = 0; x < sprite.Width; x++)
        if (sprite.Pixel(x, y) != 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
  }

  private Game CreateLevel() => Map(
    "........................",
    "........................",
    "........................",
    "........................",
    ".S..........Z.........G.",
    "########################");

  private Game CreateAirborneLevel() => Map(
    "........................",
    ".S......................",
    "........................",
    "........................",
    "............Z.........G.",
    "########################");
}
