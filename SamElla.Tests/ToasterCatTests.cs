using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class ToasterCatTests : GameTestBase
{
  [TestMethod]
  public void BundledCombatLevelsIntroduceToasterCatsFromLevelTwo()
  {
    string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    Assert.IsEmpty(Level.Load(paths[0]).ToasterCatSpawns);
    foreach (string path in paths.Skip(1).Where(path => Level.Load(path).Section is 1 or 2 && Level.Load(path).BossSpawns.Count == 0 && Level.Load(path).OldManSpawns.Count == 0))
    {
      var level = Level.Load(path);
      Assert.IsNotEmpty(level.ToasterCatSpawns, Path.GetFileName(path));
      Assert.IsNotEmpty(level.EnemySpawns, "Existing enemies should remain in the level.");

      foreach (var spawn in level.ToasterCatSpawns)
      {
        Assert.AreEqual('.', level.Tile(spawn.X, spawn.Y));
        Assert.IsTrue(level.Solid(spawn.X, spawn.Y + 1));
      }
    }
  }

  [TestMethod]
  public void EnemyTypesLoadIndependentSpeedAndPowerSettings()
  {
    CreateMixedLevel();
    File.WriteAllLines(NextPath, [
      "; custom balance",
      "[Walker]",
      "Speed=2.5",
      "PowerLevel=3",
      "HopVelocity=0",
       "[ToasterCat]",
      "Speed=4.5",
      "PowerLevel=7",
      "HopVelocity=8"]);

    var game = new Game([LevelPath], enemySettingsPath: NextPath);
    var walker = game.Enemies.Single(enemy => enemy.Kind == EnemyKind.Walker);
    var cat = game.Enemies.Single(enemy => enemy.Kind == EnemyKind.ToasterCat);
    Assert.AreEqual(2.5, walker.Settings.Speed);
    Assert.AreEqual(3, walker.Health);
    Assert.AreEqual(4.5, cat.Settings.Speed);
    Assert.AreEqual(7, cat.Health);
    Assert.AreEqual(8, cat.Settings.HopVelocity);

    double walkerX = walker.X, catX = cat.X;
    game.Update(0, false);
    Assert.AreEqual(walkerX - (2.5 * Game.Step), walker.X, 0.00001);
    Assert.AreEqual(catX - (4.5 * Game.Step), cat.X, 0.00001);
  }

  [TestMethod]
  [DataRow("PowerLevel=0")]
  [DataRow("PowerLevel=11")]
  [DataRow("PowerLevel=1.5")]
  [DataRow("Speed=-1")]
  [DataRow("Speed=31")]
  [DataRow("Speed=NaN")]
  [DataRow("Speed=Infinity")]
  [DataRow("HopVelocity=-1")]
  [DataRow("HopVelocity=21")]
  [DataRow("HopVelocity=oops")]
  [DataRow("Unknown=3")]
  public void InvalidSettingsReportTheFileAndLine(string setting)
  {
    File.WriteAllLines(NextPath, ["[ToasterCat]", setting]);
    var exception = Assert.ThrowsExactly<InvalidDataException>(() => EnemyConfiguration.Load(NextPath));
    Assert.IsTrue(exception.Message.Contains(NextPath));
    Assert.IsTrue(exception.Message.Contains(":2:"));
  }

  [TestMethod]
  public void MissingConfigKeepsDefaultEnemyBehavior()
  {
    var config = EnemyConfiguration.Load(NextPath);
    Assert.AreEqual(3, config.Walker.Speed);
    Assert.AreEqual(1, config.Walker.PowerLevel);
    Assert.AreEqual(0, config.Walker.HopVelocity);
    Assert.IsGreaterThan(config.Walker.Speed, config.ToasterCat.Speed);
    Assert.AreEqual(2, config.ToasterCat.PowerLevel);
    Assert.IsGreaterThan(0, config.ToasterCat.HopVelocity);
  }

  [TestMethod]
  [DataRow(1)]
  [DataRow(2)]
  [DataRow(10)]
  public void PowerLevelDeterminesStompsRequired(int power)
  {
    var enemy = new Enemy(0, 0, EnemyKind.ToasterCat, new EnemySettings(4, power, 7));

    for (int hit = 1; hit <= power; hit++)
    {
      Assert.AreEqual(hit == power, enemy.Stomp());
      Assert.AreEqual(power - hit, enemy.Health);
      Assert.AreEqual(hit < power, enemy.Alive);
    }

    Assert.AreEqual(Enemy.SquashDurationTicks, enemy.SquashTicksRemaining);
    Assert.IsFalse(enemy.Stomp());
    Assert.AreEqual(0, enemy.Health);
  }

  [TestMethod]
  public void SurvivingCatStompBouncesPlayerSafelyAndFinalHitCreatesSplatter()
  {
    Map(
      "....................",
      "....................",
      ".S..................",
      "....................",
      ".C................G.",
      "####################");

    File.WriteAllLines(NextPath, ["[ToasterCat]", "Speed=0", "PowerLevel=2", "HopVelocity=0"]);
    var game = new Game([LevelPath], enemySettingsPath: NextPath);
    var cat = game.Enemies[0];
    for (int tick = 0; tick < 90 && cat.Health == 2; tick++)
      game.Update(0, true);

    Assert.AreEqual(1, cat.Health);
    Assert.IsTrue(cat.Alive);
    Assert.AreEqual(0, game.Score);
    Assert.AreEqual(3, game.Lives);
    game.Update(0, true);
    Assert.IsFalse(game.IsDying, "A surviving enemy must not immediately damage the bouncing player.");
    for (int tick = 0; tick < 120 && cat.Alive; tick++)
      game.Update(0, true);

    Assert.IsFalse(cat.Alive);
    Assert.AreEqual(400, game.Score);
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(Enemy.SquashDurationTicks, cat.SquashTicksRemaining);

    game.Paused = true;
    Tick(game, 120);
    Assert.AreEqual(Enemy.SquashDurationTicks, cat.SquashTicksRemaining);

    game.Paused = false;
    Tick(game, 59);
    Assert.AreEqual(1, cat.SquashTicksRemaining);

    Tick(game, 1);
    Assert.AreEqual(0, cat.SquashTicksRemaining);

    game.Restart();
    Assert.AreEqual(2, game.Enemies[0].Health);
    Assert.IsTrue(game.Enemies[0].Alive);
  }

  [TestMethod]
  [DataRow("toaster-cat-run-1", 0.0, 0.0, false)]
  [DataRow("toaster-cat-run-2", 0.2, 0.0, false)]
  [DataRow("toaster-cat-jump", 0.0, -7.0, false)]
  [DataRow("toaster-cat-squashed", 0.0, 0.0, true)]
  public void ToasterCatRendersItsOwnEditableSprites(string name, double elapsed, double velocity, bool dead)
  {
    var game = CreateMixedLevel();
    var cat = game.Enemies.Single(enemy => enemy.Kind == EnemyKind.ToasterCat);
    cat.VelocityY = velocity;

    if (dead)
      cat.Squash();

    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var sprite = Sprite.Load(Path.Combine(assets, name + ".sprite"), palette);
    var renderer = new PixelRenderer(assets);

    renderer.Draw(game, 0, elapsed);

    // The map is 32 tiles wide, but the player is still at the left edge: camera X/Y remain zero.
    int left = (int)Math.Round((cat.X + (Game.BodyWidth / 2)) * 16) - (sprite.Width / 2);
    int top = 28 + (int)Math.Round((cat.Y + Game.BodyHeight) * 16) - sprite.Height;

    for (int y = 0; y < sprite.Height; y++)
    {
      for (int x = 0; x < sprite.Width; x++)
      {
        if (sprite.Pixel(x, y) != 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x]);
      }
    }
  }

  [TestMethod]
  public void ToasterCatRunsFasterAndHopsBetweenGroundRuns()
  {
    var game = CreateMixedLevel();
    var cat = game.Enemies.Single(enemy => enemy.Kind == EnemyKind.ToasterCat);
    var walker = game.Enemies.Single(enemy => enemy.Kind == EnemyKind.Walker);

    double catX = cat.X, walkerX = walker.X;
    bool hopped = false, ranOnGround = false;
    double highestY = cat.Y;

    for (int tick = 0; tick < 60; tick++)
    {
      game.Update(0, false);
      hopped |= cat.VelocityY < 0;
      ranOnGround |= cat.VelocityY == 0;
      highestY = Math.Min(highestY, cat.Y);
    }

    Assert.AreEqual(cat.Settings.Speed, catX - cat.X, 0.00001);
    Assert.AreEqual(walker.Settings.Speed, walkerX - walker.X, 0.00001);
    Assert.IsTrue(hopped);
    Assert.IsTrue(ranOnGround);
    Assert.IsLessThan(3.9, highestY);

    double x = cat.X, y = cat.Y, velocity = cat.VelocityY;
    game.Paused = true;

    Tick(game, 120);
    Assert.AreEqual(x, cat.X);
    Assert.AreEqual(y, cat.Y);
    Assert.AreEqual(velocity, cat.VelocityY);
  }

  private Game CreateMixedLevel() => Map(
    "................................",
    "................................",
    "................................",
    "................................",
    ".S..........E.......C.........G.",
    "################################");
}
