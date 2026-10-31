using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class BoomStickTests : GameTestBase
{
  [TestMethod]
  [DataRow(1)]
  [DataRow(10)]
  public void ShotgunPlaysEnemyDeathSoundOnlyForDefeatingHit(int health)
  {
    var game = Arm();
    var enemy = AddEnemy(game, 3, health);
    int deathsHeard = 0;

    game.SoundRequested += sound => deathsHeard += sound == GameSound.EnemyDied ? 1 : 0;
    game.Update(0, false, true);
    Assert.AreEqual(health > game.ShotgunDamage, enemy.Alive);
    Assert.AreEqual(enemy.Alive ? 0 : 1, deathsHeard);

    if (enemy.Alive)
      return;

    Tick(game, Game.ShotgunReloadTicks);
    game.Update(0, false, true);
    Assert.AreEqual(1, deathsHeard, "Shooting again must not replay a dead enemy's sound.");
  }

  [TestMethod]
  public void ShotgunSoundPlaysOnlyForShotsThatFire()
  {
    var game = Arm();
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    game.Paused = true;
    game.Update(0, false, true);
    Assert.IsEmpty(sounds);

    game.Paused = false;
    game.Update(0, false, true);
    CollectionAssert.AreEqual(new[] { GameSound.Shotgun }, sounds);

    for (int tick = 0; tick < Game.ShotgunReloadTicks + 10; tick++)
      game.Update(0, false, true);

    Assert.HasCount(1, sounds, "Holding fire must not repeat the blast sound.");

    game.Update(0, false, false);
    game.Update(0, false, true);
    CollectionAssert.AreEqual(new[] { GameSound.Shotgun, GameSound.Shotgun }, sounds);

    game.Update(0, false, false);
    game.Update(0, false, true);
    Assert.HasCount(2, sounds, "Reloading must not produce another blast sound.");
  }

  [TestMethod]
  public void BundledBoomSticksStartAtLevelFour()
  {
    string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt")
      .Where(path => Level.Load(path).Section is 1 or 2).Order().ToArray();
    for (int index = 0; index < paths.Length; index++)
    {
      Assert.AreEqual(index is 3 or 4 or 6 or 7, File.ReadAllLines(paths[index]).SkipWhile(line => line.Contains('=') || string.IsNullOrWhiteSpace(line)).Any(line => line.Contains('Q')), paths[index]);
      Level.Load(paths[index]);
    }
  }

  [TestMethod]
  [DataRow("02-01-ruins-crossing.txt")]
  [DataRow("02-02-brick-summit.txt")]
  public void LaterLevelShotgunBlocksCanBeHitFromUnderneath(string filename)
  {
    string source = Path.Combine(AppContext.BaseDirectory, "Levels", filename);
    var level = Level.Load(source);
    var blocks = Enumerable.Range(0, level.Height).SelectMany(y => Enumerable.Range(0, level.Width)
      .Where(x => level.Tile(x, y) == 'Q').Select(x => (X: x, Y: y))).ToArray();
    Assert.HasCount(1, blocks);
    var block = blocks[0];
    Assert.AreEqual('.', level.Tile(block.X, block.Y + 1));
    Assert.AreEqual('.', level.Tile(block.X, block.Y + 2));
    Assert.AreEqual('.', level.Tile(block.X, block.Y + 3));
    Assert.IsTrue(level.Solid(block.X, block.Y + 4));

    // Keep the actual level geometry, but start underneath the block to isolate jump reachability.
    string[] rows = File.ReadAllLines(source);
    int mapStart = rows.Length - level.Height;
    for (int y = mapStart; y < rows.Length; y++)
      rows[y] = rows[y].Replace('S', '.');
    char[] spawnRow = rows[mapStart + block.Y + 3].ToCharArray();
    spawnRow[block.X] = 'S';
    rows[mapStart + block.Y + 3] = new string(spawnRow);
    File.WriteAllLines(LevelPath, rows);
    var game = new Game([LevelPath], konamiCheat: true);
    game.Enemies.Clear();
    Tick(game, 10);
    game.QueueJump();
    for (int tick = 0; tick < 60 && game.Level.Tile(block.X, block.Y) == 'Q'; tick++)
      game.Update(0, true);
    Assert.AreEqual('X', game.Level.Tile(block.X, block.Y));
    Assert.IsTrue(game.GrowthHearts.Any(item => item.IsBoomStick));
    TouchHeart(game);
    Assert.IsTrue(game.HasBoomStick);
  }

  [TestMethod]
  public void PickupShowsBoomStickQuoteForThreeSecondsAndPauseFreezesIt()
  {
    CreateGame();
    var game = ArmGame(new Game([LevelPath]), 0);
    Assert.AreEqual("This here is my BOOM STICK!", game.BoomStickQuote);
    Assert.AreEqual(180, game.BoomStickQuoteTicksRemaining);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    var expected = new PixelBuffer(PixelRenderer.Width, PixelRenderer.Height);
    int quoteX = (PixelRenderer.Width - (((game.BoomStickQuote.Length * 4) - 1) * 2)) / 2;
    PixelFont.Draw(expected, game.BoomStickQuote, quoteX, 60, 0xFFF6F4DF, 2);
    for (int index = 0; index < expected.Pixels.Length; index++)
      if (expected.Pixels[index] != 0)
        Assert.AreEqual(expected.Pixels[index], renderer.Frame.Pixels[index]);
    game.Paused = true;
    Tick(game, 300);
    Assert.AreEqual(180, game.BoomStickQuoteTicksRemaining);
    game.Paused = false;
    Tick(game, 179);
    Assert.AreEqual(1, game.BoomStickQuoteTicksRemaining);
    Tick(game, 1);
    Assert.AreEqual(0, game.BoomStickQuoteTicksRemaining);
    Assert.AreEqual(string.Empty, game.BoomStickQuote);
    renderer.Draw(game, 0, 0);
    Assert.AreNotEqual(0xFFFFD56B, renderer.Frame.Pixels[(42 * PixelRenderer.Width) + 12]);
    Assert.IsTrue(game.HasBoomStick);
  }

  [TestMethod]
  public void RestartClearsBoomStickQuote()
  {
    var game = Arm();
    Assert.IsGreaterThan(0, game.BoomStickQuoteTicksRemaining);
    game.Restart();
    Assert.AreEqual(0, game.BoomStickQuoteTicksRemaining);
    Assert.AreEqual(string.Empty, game.BoomStickQuote);
  }

  [TestMethod]
  public void SmallPlayerCannotCollectBoomStick()
  {
    var game = CreateGame();
    Tick(game, 10);
    game.QueueJump();
    Tick(game, 30, jump: true);
    Assert.HasCount(1, game.GrowthHearts);
    Assert.IsTrue(game.GrowthHearts[0].IsBoomStick);
    TouchHeart(game);
    Assert.IsFalse(game.HasBoomStick);
    Assert.AreEqual(0, game.BoomStickQuoteTicksRemaining);
    Assert.IsFalse(game.IsLarge);
    Assert.HasCount(1, game.GrowthHearts);
    Assert.AreEqual(0, game.Score);
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam)]
  [DataRow(PlayerCharacter.Ella)]
  public void LargePlayersCollectShotgunAndHitOnlyWithinThreeTiles(PlayerCharacter character)
  {
    var game = Arm(character);
    Assert.IsTrue(game.HasBoomStick);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.AreEqual(3, game.PowerUpLevel);
    Assert.AreEqual(game.FlameDamage + 1, game.ShotgunDamage);
    var near = AddEnemy(game, 3, 10);
    var far = AddEnemy(game, 5.2, 10);
    game.Update(0, false, true);
    Assert.IsNotNull(game.ShotgunBlast);
    Assert.AreEqual(3, game.ShotgunBlast.Length);
    Assert.AreEqual(6, near.Health);
    Assert.AreEqual(10, far.Health);
    Assert.IsNull(game.Flame);
  }

  [TestMethod]
  public void ShotPassesThroughBricksAndLeavesExistingRubbleAnimation()
  {
    var game = Arm();
    Tick(game, 11, 1);
    var enemy = AddEnemy(game, 7, 10);
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    game.Update(0, false, true);
    Assert.AreEqual('.', game.Level.Tile(6, 7));
    Assert.AreEqual('.', game.Level.Tile(7, 7));
    Assert.AreEqual('B', game.Level.Tile(9, 7));
    Assert.HasCount(2, game.BlockBreakEffects);
    Assert.AreEqual(2, sounds.Count(sound => sound == GameSound.BlockBreak));
    Assert.AreEqual(300, game.Score);
    Assert.AreEqual(6, enemy.Health);
    Assert.IsTrue(game.BlockBreakEffects.All(effect => effect.FrameIndex == 0));
    Tick(game, 15);
    Assert.IsTrue(game.BlockBreakEffects.All(effect => effect.FrameIndex == 1));
    Tick(game, 15);
    Assert.IsEmpty(game.BlockBreakEffects);
  }

  [TestMethod]
  public void SolidWallsStopTheShotAndProtectBricksAndEnemiesBehindThem()
  {
    var game = Arm();
    Tick(game, 11, 1);
    game.Level.UseBlock(7, 7);
    var enemy = AddEnemy(game, 7.3, 10);
    game.Update(0, false, true);
    Assert.AreEqual('.', game.Level.Tile(6, 7));
    Assert.AreEqual('X', game.Level.Tile(7, 7));
    Assert.AreEqual('B', game.Level.Tile(9, 7));
    Assert.AreEqual(10, enemy.Health);
    Assert.AreEqual(7 - (game.X + Game.BodyWidth), game.ShotgunBlast!.Length, 0.00001);
  }

  [TestMethod]
  public void HoldingFireShootsOnceAndRapidTappingCannotSkipReload()
  {
    var game = Arm();
    var enemy = AddEnemy(game, 3, 10);
    for (int tick = 0; tick < 60; tick++)
      game.Update(0, false, true);
    Assert.AreEqual(6, enemy.Health);
    game.Update(0, false);
    game.Update(0, false, true);
    Assert.AreEqual(2, enemy.Health);
    game.Update(0, false);
    game.Update(0, false, true);
    Assert.AreEqual(2, enemy.Health);
  }

  [TestMethod]
  public void BlastAnimationAndReloadFreezeWhenPaused()
  {
    var game = Arm();
    game.Update(0, false, true);
    var blast = game.ShotgunBlast;
    game.Paused = true;
    Tick(game, 100);
    Assert.AreEqual(blast, game.ShotgunBlast);
    game.Paused = false;
    Tick(game, 6);
    Assert.AreEqual(1, game.ShotgunBlast!.FrameIndex);
    Tick(game, 6);
    Assert.IsNull(game.ShotgunBlast);
  }

  [TestMethod]
  public void HighPowerShotKillsWithNormalScoreAndSplatter()
  {
    CreateGame();
    File.WriteAllLines(NextPath, ["[Player.PowerLevel2]", "PowerLevel=10"]);
    var game = ArmGame(new Game([LevelPath], enemySettingsPath: NextPath));
    var enemy = AddEnemy(game, 3, 10);
    Assert.AreEqual(12, game.ShotgunDamage);
    game.Update(0, false, true);
    Assert.IsFalse(enemy.Alive);
    Assert.AreEqual(60, enemy.SquashTicksRemaining);
    Assert.AreEqual(2200, game.Score);
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(6)]
  public void ShotgunRendersItsOwnWeaponAndBothBlastFrames(int elapsed)
  {
    var game = Arm();
    game.Update(0, false, true);
    Tick(game, elapsed);
    var blast = game.ShotgunBlast!;
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    var sprite = Sprite.Load(Path.Combine(assets, $"shotgun-blast-{blast.FrameIndex + 1}.sprite"), palette);
    int left = (int)Math.Round(blast.X * 16);
    int top = 28 + (int)Math.Round(blast.Y * 16) - 8;
    for (int distance = 0; distance < 48; distance++)
      for (int y = 0; y < 16; y++)
        if (sprite.Pixel(distance % 16, y) != 0)
          Assert.AreEqual(sprite.Pixel(distance % 16, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + distance]);
    var weapon = Sprite.Load(Path.Combine(assets, "boom-stick.sprite"), palette);
    int weaponX = (int)Math.Round((game.X + (Game.BodyWidth / 2)) * 16) - 8;
    Assert.AreEqual(weapon.Pixel(3, 8), renderer.Frame.Pixels[((top + 8) * PixelRenderer.Width) + weaponX + 3]);
  }

  [TestMethod]
  public void DamageRemovesShotgunWithoutGrantingFlamethrowerAndKeepsPlayerLarge()
  {
    var game = Arm();
    game.Enemies.Add(new Enemy(game.X, game.Y, settings: new EnemySettings(0, 1, 0)) { Direction = 0 });
    game.Update(0, false);
    Assert.IsFalse(game.HasBoomStick);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(1, game.PowerUpLevel);
    Assert.AreEqual(3, game.Lives);
    game.Restart();
    Assert.IsFalse(game.HasBoomStick);
    Assert.IsFalse(game.HasFlamethrower);
  }

  [TestMethod]
  public void ShotgunSurvivesLevelTransitionAndCanShootLeft()
  {
    CreateGame();
    File.Copy(LevelPath, NextPath);
    var game = ArmGame(new Game([LevelPath, NextPath]));
    // Remove test obstacles so this fixture can reach its goal on foot.
    foreach (int x in new[] { 6, 7, 9 })
      game.Level.Clear(x, 7);
    Tick(game, 120, 1);
    Assert.AreEqual(2, game.LevelNumber);
    Assert.IsTrue(game.HasBoomStick);
    game.CompleteLevelStart();
    Tick(game, 15, 1);
    game.Update(-1, false);
    var enemy = AddEnemy(game, game.X - 2, 10);
    game.Update(0, false, true);
    Assert.AreEqual(-1, game.ShotgunBlast!.Direction);
    Assert.AreEqual(6, enemy.Health);
  }

  private Game CreateGame() => Map(
    "........................", "........................", "........................",
    "........................", "........................", ".Q..M...................",
    "........................", ".S....BB.B............G.", "########################");

  private Game Arm(PlayerCharacter character = PlayerCharacter.Sam)
  {
    CreateGame();
    return ArmGame(new Game([LevelPath]) { Character = character });
  }

  private static Game ArmGame(Game game, int ticksAfterPickup = 60)
  {
    Tick(game, 10);
    Tick(game, 15, 1);
    game.QueueJump();
    Tick(game, 30, jump: true);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.IsLarge);
    Tick(game, 15, -1);
    game.QueueJump();
    Tick(game, 30, jump: true);
    TouchHeart(game);
    Tick(game, ticksAfterPickup);
    Assert.IsTrue(game.HasBoomStick);
    if (ticksAfterPickup > 0)
      game.Update(1, false);
    return game;
  }

  private static Enemy AddEnemy(Game game, double x, int health)
  {
    var enemy = new Enemy(x, 7.1, settings: new EnemySettings(0, health, 0));
    game.Enemies.Add(enemy);
    return enemy;
  }
}
