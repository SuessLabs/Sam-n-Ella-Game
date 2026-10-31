using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Audio;
using SamElla.Effects;

namespace SamElla.Tests;

[TestClass]
public sealed class ChainWhipTests : GameTestBase
{
  private static string SourcePath => Path.Combine(AppContext.BaseDirectory, "Levels", "03-03-rusted-mains.txt");

  [TestMethod]
  public void WhipIsNearStartAfterGrowthAndCanBeCollectedFromBundledBlocks()
  {
    var level = Level.Load(SourcePath);
    int heartX = Enumerable.Range(0, 25).Single(x => level.Tile(x, 16) == 'M');
    int whipX = Enumerable.Range(0, 25).Single(x => level.Tile(x, 16) == 'W');
    Assert.IsTrue(heartX < whipX && whipX < 20);
    var game = Arm();
    Assert.IsTrue(game.IsLarge);
    Assert.IsTrue(game.HasChainWhip);
    Assert.AreEqual(4, game.PowerUpLevel);
    Assert.IsFalse(game.HasBoomStick);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.AreEqual('X', game.Level.Tile(heartX, 16));
    Assert.AreEqual('X', game.Level.Tile(whipX, 16));
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam)]
  [DataRow(PlayerCharacter.Ella)]
  public void EmoBossLevelOffersWhipAfterCollectingGrowth(PlayerCharacter character)
  {
    var game = new Game([Path.Combine(AppContext.BaseDirectory, "Levels", "03-06-emo-kid-boss.txt")]) { Character = character };
    game.Enemies.Clear();
    MoveTo(game, 18);
    Bump(game);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.IsLarge);
    Assert.IsFalse(game.HasChainWhip);
    MoveTo(game, 24);
    Bump(game);
    TouchHeart(game);
    Assert.IsTrue(game.HasChainWhip);
    Assert.AreEqual('X', game.Level.Tile(18, 16));
    Assert.AreEqual('X', game.Level.Tile(24, 16));
  }

  [TestMethod]
  public void SmallPlayerCannotCollectWhipUntilGrown()
  {
    var game = CreateGame();
    MoveTo(game, 15);
    Bump(game);
    Assert.IsTrue(game.GrowthHearts.Any(heart => heart.IsChainWhip));
    TouchHeart(game);
    Assert.IsFalse(game.HasChainWhip);
    Assert.IsFalse(game.IsLarge);
    Assert.IsTrue(game.GrowthHearts.Any(heart => heart.IsChainWhip));
    game.Update(0, false, true);
    Assert.IsNull(game.WhipSwing);
  }

  [TestMethod]
  public void WhipReplacesShotgunCarriedIntoLevelThreeThree()
  {
    string[] lines = File.ReadAllLines(SourcePath);
    File.WriteAllLines(LevelPath, new[] { "StartingPower=Shotgun" }.Concat(lines));
    var game = new Game([LevelPath]);
    game.Enemies.Clear();
    Assert.IsTrue(game.HasBoomStick);
    MoveTo(game, 15);
    Bump(game);
    TouchHeart(game);
    Assert.IsTrue(game.HasChainWhip);
    Assert.IsFalse(game.HasBoomStick);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.AreEqual(0, game.BoomStickQuoteTicksRemaining);
    Assert.IsNull(game.ShotgunBlast);
  }

  [TestMethod]
  public void WhipDamagesEachEnemyOncePerLashAndNeedsAnotherPress()
  {
    var game = Arm();
    var near = AddEnemy(game, game.X + 2, 10);
    var far = AddEnemy(game, game.X + 5, 10);
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    TickFire(game, 6);
    Assert.AreEqual(10, near.Health, "Wind-up must not damage enemies.");
    TickFire(game, 9);
    Assert.AreEqual(7, near.Health);
    Assert.AreEqual(10, far.Health);
    Assert.AreEqual(ChainWhipSwing.Range, game.WhipSwing!.Length, 0.00001);
    TickFire(game, 60);
    Assert.AreEqual(7, near.Health);
    Assert.IsNull(game.WhipSwing);
    Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.ChainWhip));
    game.Update(0, false);
    TickFire(game, 15);
    Assert.AreEqual(4, near.Health);
    Assert.AreEqual(2, sounds.Count(sound => sound == GameSound.ChainWhip));
  }

  [TestMethod]
  public void WhipClipsAtTerrainAndCanLashLeft()
  {
    var game = Arm();
    int wall = (int)Math.Ceiling(game.X + Game.BodyWidth + 1);
    game.Level.UseBlock(wall, 19);
    var behind = AddEnemy(game, wall + 0.2, 10);
    TickFire(game, 15);
    Assert.IsTrue(game.WhipSwing!.Length < 3);
    Assert.AreEqual(10, behind.Health);
    Assert.AreEqual('X', game.Level.Tile(wall, 19));
    TickFire(game, 30);
    game.Update(-1, false);
    var left = AddEnemy(game, game.X - 2, 10);
    TickFire(game, 15);
    Assert.AreEqual(-1, game.WhipSwing!.Direction);
    Assert.AreEqual(7, left.Health);
  }

  [TestMethod]
  [DataRow(EnemyKind.Walker)]
  [DataRow(EnemyKind.PunkBlue)]
  public void FatalLashAwardsScoreAndPlaysTheEnemyDeathEffectOnce(EnemyKind kind)
  {
    var game = Arm();
    var enemy = new Enemy(game.X + 2, 0, kind, new EnemySettings(0, 3, 0)) { Direction = 0 };
    enemy.Y = game.Y + game.PlayerHeight - enemy.BodyHeight;
    game.Enemies.Add(enemy);
    int score = game.Score;
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    TickFire(game, 15);
    Assert.IsFalse(enemy.Alive);
    Assert.IsTrue(enemy.SquashTicksRemaining > 0);
    Assert.AreEqual(score + 600, game.Score);
    GameSound death = enemy.IsTeen ? GameSound.TeenGroan : GameSound.EnemyDied;
    Assert.AreEqual(1, sounds.Count(sound => sound == death));
    TickFire(game, 60);
    Assert.AreEqual(1, sounds.Count(sound => sound == death));
  }

  [TestMethod]
  public void PauseFreezesSwingAndDamageWhileLosingWeaponKeepsGrowth()
  {
    var game = Arm();
    var enemy = AddEnemy(game, game.X + 2, 10);
    TickFire(game, 10);
    var swing = game.WhipSwing;
    int health = enemy.Health;
    game.Paused = true;
    TickFire(game, 120);
    Assert.AreEqual(swing, game.WhipSwing);
    Assert.AreEqual(health, enemy.Health);
    game.Paused = false;
    game.Enemies.Clear();
    game.Enemies.Add(new Enemy(game.X, game.Y, settings: new EnemySettings(0, 10, 0)) { Direction = 0 });
    game.Update(0, false);
    Assert.IsFalse(game.HasChainWhip);
    Assert.IsNull(game.WhipSwing);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(3, game.Lives);
    game.Enemies.Clear();
    game.Restart();
    Assert.IsFalse(game.HasChainWhip);
    Assert.IsFalse(game.IsLarge);
  }

  [TestMethod]
  public void WhipPersistsThroughNextLevelAndDeathClearsIt()
  {
    CreateGame();
    File.Copy(LevelPath, NextPath);
    string[] rows = File.ReadAllLines(LevelPath);
    int start = rows.Length - Level.Load(LevelPath).Height;
    char[] goalRow = rows[start + 19].Replace('G', '.').ToCharArray();
    goalRow[22] = 'G';
    rows[start + 19] = new string(goalRow);
    File.WriteAllLines(LevelPath, rows);
    var game = Arm(new Game([LevelPath, NextPath]));
    for (int tick = 0; tick < 120 && game.LevelNumber == 1 && !game.IsDying; tick++)
    {
      if (game.Grounded)
        game.QueueJump();

      game.Update(1, true);
    }

    Assert.AreEqual(2, game.LevelNumber);
    Assert.IsTrue(game.HasChainWhip);
    Assert.IsNull(game.WhipSwing);
    game.CompleteLevelStart();
    game.Enemies.Clear();
    for (int x = 0; x < game.Level.Width; x++)
      for (int y = 0; y < game.Level.Height; y++)
        game.Level.Clear(x, y);

    for (int tick = 0; tick < 240 && !game.IsDying; tick++)
      game.Update(0, false);

    Assert.IsTrue(game.IsDying);
    Assert.IsFalse(game.HasChainWhip);
    Assert.IsNull(game.WhipSwing);
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam)]
  [DataRow(PlayerCharacter.Ella)]
  public void ChainLinksRenderForBothCharacters(PlayerCharacter character)
  {
    var game = Arm(new Game([PrepareSource()]) { Character = character });
    TickFire(game, 14);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    string? preview = Environment.GetEnvironmentVariable("SAMELLA_WHIP_PREVIEW");
    if (preview is not null)
      renderer.Frame.SavePng(Path.Combine(preview, $"chain-whip-{character}.png"));

    Assert.IsNotNull(game.WhipSwing);
    Assert.AreEqual(ChainWhipSwing.Range, game.WhipSwing.Length, 0.00001);
  }

  private string PrepareSource()
  {
    File.Copy(SourcePath, LevelPath, true);
    return LevelPath;
  }

  private Game CreateGame()
  {
    PrepareSource();
    var game = new Game([LevelPath]);
    game.Enemies.Clear();
    return game;
  }

  private Game Arm(Game? game = null)
  {
    game ??= CreateGame();
    game.Enemies.Clear();
    MoveTo(game, 9);
    Bump(game);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.IsLarge);
    MoveTo(game, 15);
    Bump(game);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.HasChainWhip);
    game.Update(1, false);
    return game;
  }

  private static void MoveTo(Game game, double target)
  {
    Tick(game, 6);
    for (int tick = 0; tick < 200 && Math.Abs(game.X - target) > 0.15; tick++)
      game.Update(game.X < target ? 1 : -1, false);
  }

  private static void Bump(Game game)
  {
    game.QueueJump();
    Tick(game, 30, jump: true);
  }

  private static void TickFire(Game game, int ticks)
  {
    for (int tick = 0; tick < ticks; tick++)
      game.Update(0, false, true);
  }

  private static Enemy AddEnemy(Game game, double x, int health)
  {
    var enemy = new Enemy(x, game.Y + game.PlayerHeight - Game.BodyHeight,
      settings: new EnemySettings(0, health, 0)) { Direction = 0 };
    game.Enemies.Add(enemy);
    return enemy;
  }
}
