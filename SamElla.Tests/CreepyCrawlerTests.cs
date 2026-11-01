// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class CreepyCrawlerTests : GameTestBase
{
  [TestMethod]
  public void CampaignIntroducesCreepyCrawlerOnNighttimeLevelFive()
  {
    string[] files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    Assert.IsTrue(files.Take(4).All(path => Level.Load(path).CreepyCrawlerSpawns.Count == 0));
    var level = Level.Load(files[4]);
    Assert.AreEqual(BackgroundTheme.Night, level.Background);
    Assert.IsNotEmpty(level.CreepyCrawlerSpawns);
    for (int index = 0; index < files.Length; index++)
    {
      var map = Level.Load(files[index]);
      Assert.AreEqual(index >= 4 && map.Background != BackgroundTheme.Underworld && map.BossSpawns.Count == 0 && map.OldManSpawns.Count == 0 && map.Background == BackgroundTheme.Night, map.CreepyCrawlerSpawns.Count > 0);
      foreach (var spawn in map.CreepyCrawlerSpawns)
      {
        Assert.AreEqual(BackgroundTheme.Night, map.Background);
        Assert.IsTrue(map.Solid(spawn.X, spawn.Y + 1));
      }
    }
  }

  [TestMethod]
  [DataRow("Day", "##########")]
  [DataRow("Night", "###.######")]
  public void CrawlerRequiresNightAndSolidTerrainBelowItsSpawn(string background, string floor)
  {
    var error = Assert.ThrowsExactly<InvalidDataException>(() =>
      Level.Parse("crawler-test", [$"Background={background}", "", "..........", ".S.R....G.", floor]));
    StringAssert.Contains(error.Message, "Creepy-Crawler");
  }

  [TestMethod]
  public void CrawlerWaitsForProximityEmergesInStagesAndPausesWithGameplay()
  {
    var game = CreateLevel();
    var crawler = game.Enemies.Single();
    double initialX = crawler.X;
    Tick(game, 120);
    Assert.IsTrue(crawler.IsBuried);
    Assert.AreEqual(initialX, crawler.X);
    Assert.AreEqual(Enemy.CrawlerBodyHeight, crawler.BodyHeight);
    Assert.AreEqual(5, crawler.Y + crawler.BodyHeight, 0.00001);
    crawler.X = game.X + 3;
    game.Update(0, false);
    Assert.IsTrue(crawler.IsEmerging);
    Assert.AreEqual(0, crawler.EmergenceFrameIndex);
    double emergenceX = crawler.X;
    Tick(game, 20);
    Assert.AreEqual(1, crawler.EmergenceFrameIndex);
    game.Paused = true;
    Tick(game, 60);
    Assert.AreEqual(40, crawler.EmergenceTicksRemaining);
    Assert.AreEqual(emergenceX, crawler.X);
    game.Paused = false;
    Tick(game, 20);
    Assert.AreEqual(2, crawler.EmergenceFrameIndex);
    Tick(game, 20);
    Assert.IsFalse(crawler.IsEmerging);
    Assert.IsTrue(crawler.CanInteract);
    game.Update(0, false);
    Assert.AreEqual(emergenceX - (1.5 * Game.Step), crawler.X, 0.00001);
    Assert.AreEqual(5, crawler.Y + crawler.BodyHeight, 0.00001);
    Assert.AreEqual(0, crawler.VelocityY);
    game.Restart();
    Assert.IsTrue(game.Enemies.Single().IsBuried);
  }

  [TestMethod]
  public void DormantCrawlerIsCompletelyInvisibleUntilPlayerApproaches()
  {
    var game = CreateLevel();
    var crawler = game.Enemies.Single();
    crawler.X = 10; // Visible camera area, outside the four-tile trigger.
    Tick(game, 60);
    Assert.IsTrue(crawler.IsBuried);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    uint[] hidden = (uint[])renderer.Frame.Pixels.Clone();
    game.Enemies.Remove(crawler);
    renderer.Draw(game, 0, 0);
    CollectionAssert.AreEqual(hidden, renderer.Frame.Pixels, "No mound, body or other cue should reveal the buried enemy.");
    game.Enemies.Add(crawler);
    crawler.X = game.X + Enemy.CrawlerWakeDistance;
    game.Update(0, false);
    Assert.IsTrue(crawler.IsEmerging);
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(hidden.SequenceEqual(renderer.Frame.Pixels));
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(20)]
  [DataRow(40)]
  public void EmergingBodyNeverDrawsOverTheTerrainBelowTheSurface(int ticks)
  {
    var game = CreateLevel();
    var crawler = game.Enemies.Single();
    crawler.X = 12;
    crawler.AdvanceEmergence(11, crawler.Y);
    for (int tick = 0; tick < ticks; tick++)
      crawler.AdvanceEmergence(11, crawler.Y);

    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    int groundRow = 28 + 5 * PixelRenderer.TileSize;
    uint[] ground = renderer.Frame.Pixels.Skip(groundRow * PixelRenderer.Width).ToArray();
    game.Enemies.Remove(crawler);
    renderer.Draw(game, 0, 0);
    CollectionAssert.AreEqual(ground, renderer.Frame.Pixels.Skip(groundRow * PixelRenderer.Width).ToArray());
  }

  [TestMethod]
  public void EmergingCrawlerCannotDamagePlayerOrReceiveHitsUntilItIsExposed()
  {
    var game = CreateLevel();
    Tick(game, 10);
    var crawler = game.Enemies.Single();
    Assert.IsFalse(crawler.Hit(12));
    crawler.X = game.X;
    game.Update(0, false);
    Assert.IsFalse(game.IsDying);
    Assert.IsFalse(crawler.Hit(12));
    Assert.AreEqual(2, crawler.Health);
    Tick(game, Enemy.CrawlerEmergenceDurationTicks);
    Assert.IsFalse(game.IsDying);
    game.Update(0, false);
    Assert.IsTrue(game.IsDying, "Body contact becomes dangerous after emergence.");
  }

  [TestMethod]
  public void LowCrawlerFitsUnderOverhangAndNeverHopsEvenWithCustomHopSettings()
  {
    CreateLevel();
    File.WriteAllLines(NextPath, ["[Creepy-Crawler]", "Speed=2", "PowerLevel=3", "HopVelocity=9"]);
    var config = EnemyConfiguration.Load(NextPath);
    Assert.AreEqual(EnemyConfiguration.DefaultZombie, config.Zombie);
    var game = new Game([LevelPath], enemySettingsPath: NextPath);
    var crawler = game.Enemies.Single();
    Reveal(crawler);
    game.Level.UseBlock(29, 3);
    Tick(game, 60);
    Assert.AreEqual(28, crawler.X, 0.00001);
    Assert.AreEqual(3, crawler.Health);
    Assert.AreEqual(4.5, crawler.Y, 0.00001);
    Assert.AreEqual(0, crawler.VelocityY);
  }

  [TestMethod]
  public void SmallPlayerCanStompTwiceForPointsAndSplatter()
  {
    var game = CreateLevel(airborne: true);
    Tick(game, 10);
    var crawler = game.Enemies.Single();
    Reveal(crawler);
    crawler.X = game.X;
    crawler.Y = game.Y + Game.BodyHeight + 0.01;
    crawler.Direction = 0;
    game.Update(0, false);
    Assert.AreEqual(1, crawler.Health);
    Assert.IsLessThan(0, game.VelocityY);
    for (int tick = 0; tick < 180 && crawler.Alive; tick++)
      game.Update(0, false);

    Assert.IsFalse(crawler.Alive);
    Assert.IsFalse(game.IsDying);
    Assert.AreEqual(400, game.Score);
    Assert.AreEqual(Enemy.SquashDurationTicks, crawler.SquashTicksRemaining);
  }

  [TestMethod]
  public void DestroyingBuriedCrawlersSupportMakesItFallInsteadOfFloating()
  {
    var game = CreateLevel();
    var crawler = game.Enemies.Single();
    crawler.Direction = 0;
    game.Level.Clear(30, 5);
    Tick(game, 120);
    Assert.IsFalse(crawler.IsBuried);
    Assert.IsFalse(crawler.IsEmerging);
    Assert.IsFalse(crawler.Alive);
    Assert.AreEqual(0, crawler.Health);
  }

  [TestMethod]
  public void ExposedCrawlerCanBeKilledByWeaponDamage()
  {
    var crawler = new Enemy(0, 0, EnemyKind.CreepyCrawler);
    Assert.IsFalse(crawler.Hit(3));
    Reveal(crawler);
    Assert.IsTrue(crawler.Hit(3));
    Assert.IsFalse(crawler.Alive);
  }

  [TestMethod]
  [DataRow("creepy-crawler-emerge-1", 0, 0.0)]
  [DataRow("creepy-crawler-emerge-2", 20, 0.0)]
  [DataRow("creepy-crawler-emerge-3", 40, 0.0)]
  [DataRow("creepy-crawler-crawl-1", 60, 0.0)]
  [DataRow("creepy-crawler-crawl-2", 60, 0.3)]
  [DataRow("creepy-crawler-squashed", 61, 0.0)]
  public void EveryCrawlerStateRendersItsOwnSpriteAtGroundLevel(string name, int ticks, double elapsed)
  {
    var game = CreateLevel();
    var crawler = game.Enemies.Single();
    crawler.X = 12;
    if (ticks >= 0)
    {
      crawler.AdvanceEmergence(crawler.X - 1, crawler.Y);
      for (int tick = 0; tick < ticks; tick++)
        crawler.AdvanceEmergence(crawler.X - 1, crawler.Y);
    }

    if (name.EndsWith("squashed", StringComparison.Ordinal))
      crawler.Squash();

    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var sprite = Sprite.Load(Path.Combine(assets, name + ".sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, elapsed);
    int left = (int)Math.Round((crawler.X + (Game.BodyWidth / 2)) * 16) - (sprite.Width / 2);
    int top = 28 + (int)Math.Round((crawler.Y + crawler.BodyHeight) * 16) - sprite.Height;
    int groundY = top + sprite.Height;
    if (crawler.IsEmerging)
      top += (crawler.EmergenceTicksRemaining * 8) / Enemy.CrawlerEmergenceDurationTicks;

    for (int y = 0; y < sprite.Height; y++)
    {
      for (int x = 0; x < sprite.Width; x++)
      {
        if (sprite.Pixel(x, y) != 0 && (!crawler.IsEmerging || top + y < groundY))
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
      }
    }
  }

  private Game CreateLevel(bool airborne = false) => Map(
    "Background=Night", "",
    new string('.', 48),
    airborne ? ".S" + new string('.', 46) : new string('.', 48),
    new string('.', 48),
    new string('.', 48),
    (airborne ? ".." : ".S") + new string('.', 28) + "R" + new string('.', 15) + "G.",
    new string('#', 48));

  private static void Reveal(Enemy crawler)
  {
    crawler.AdvanceEmergence(crawler.X - 1, crawler.Y);
    for (int tick = 0; tick < Enemy.CrawlerEmergenceDurationTicks; tick++)
      crawler.AdvanceEmergence(crawler.X - 1, crawler.Y);
  }
}
