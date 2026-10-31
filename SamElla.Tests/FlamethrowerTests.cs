using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class FlamethrowerTests : GameTestBase
{
  [TestMethod]
  [DataRow(1)]
  [DataRow(10)]
  public void FlamePlaysEnemyDeathSoundOnlyForDefeatingHit(int health)
  {
    var game = Arm();
    var enemy = AddEnemy(game, 3, health);
    int deathsHeard = 0;

    game.SoundRequested += sound => deathsHeard += sound == GameSound.EnemyDied ? 1 : 0;
    game.Update(0, false, true);
    Assert.AreEqual(health > game.FlameDamage, enemy.Alive);
    Assert.AreEqual(enemy.Alive ? 0 : 1, deathsHeard);

    if (enemy.Alive)
      return;

    for (int tick = 0; tick < 60; tick++)
      game.Update(0, false, true);

    Assert.AreEqual(1, deathsHeard, "Continued fire must not replay a dead enemy's sound.");
  }

  [TestMethod]
  public void SmallPlayerCannotCollectFlamethrowerOrGrowFromIt()
  {
    var game = CreateGame();
    Tick(game, 10);
    game.QueueJump();
    Tick(game, 30, jump: true);
    Assert.AreEqual('X', game.Level.Tile(1, 5));
    Assert.HasCount(1, game.GrowthHearts);
    Assert.IsTrue(game.GrowthHearts[0].IsFlamethrower);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.AreEqual(0, game.Score);
    TouchHeart(game);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.IsFalse(game.IsLarge);
    Assert.HasCount(1, game.GrowthHearts);
    Assert.AreEqual(string.Empty, game.FlamethrowerQuote);
    Assert.AreEqual(0, game.FlamethrowerQuoteTicksRemaining);
    Assert.AreEqual(0, game.Score);
    game.Update(0, false, true);
    Assert.IsNull(game.Flame);
  }

  [TestMethod]
  public void LargePlayerCanCollectFlamethrowerAsSecondTier()
  {
    var game = Arm();
    Assert.AreEqual(2, game.PowerUpLevel);
    Assert.AreEqual(game.PlayerPowerLevel + 1, game.FlameDamage);
    Assert.AreEqual(200, game.Score);
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam)]
  [DataRow(PlayerCharacter.Ella)]
  public void FireHurtsEnemiesInRangeAndAnimatesUntilReleased(PlayerCharacter character)
  {
    var game = Arm(character);
    var near = AddEnemy(game, 3, 10);
    var far = AddEnemy(game, 5, 10);
    game.Update(0, false, true);
    Assert.IsNotNull(game.Flame);
    Assert.AreEqual(3, game.Flame.Length);
    Assert.AreEqual(7, near.Health);
    Assert.AreEqual(10, far.Health);
    Assert.AreEqual(0, game.Flame.FrameIndex);
    for (int tick = 0; tick < 6; tick++)
      game.Update(0, false, true);
    Assert.AreEqual(1, game.Flame!.FrameIndex);
    Assert.AreEqual(7, near.Health, "Flames must not inflict damage every simulation tick.");
    for (int tick = 0; tick < 11; tick++)
      game.Update(0, false, true);
    Assert.AreEqual(7, near.Health);
    game.Update(0, false, true);
    Assert.AreEqual(4, near.Health);
    game.Update(0, false);
    Assert.IsNull(game.Flame);
  }

  [TestMethod]
  public void FireKillsWithNormalScoreAndSplatterAndSupportsMaximumPower()
  {
    CreateGame();
    File.WriteAllLines(NextPath, ["[Player.PowerLevel2]", "PowerLevel=10"]);
    var game = ArmGame(new Game([LevelPath], enemySettingsPath: NextPath));
    Assert.AreEqual(11, game.FlameDamage);
    var enemy = AddEnemy(game, 3, 10);
    game.Update(0, false, true);
    Assert.IsFalse(enemy.Alive);
    Assert.AreEqual(Enemy.SquashDurationTicks, enemy.SquashTicksRemaining);
    Assert.AreEqual(2200, game.Score);
  }

  [TestMethod]
  public void WallsStopFireBeforeEnemiesAndFacingChangesItsDirection()
  {
    var game = Arm();
    var enemy = AddEnemy(game, 5, 10);
    game.Level.UseBlock(4, 7);
    game.Update(0, false, true);
    Assert.IsNotNull(game.Flame);
    Assert.AreEqual(4 - (game.X + Game.BodyWidth), game.Flame.Length, 0.00001);
    Assert.AreEqual(10, enemy.Health);
    game.Update(-1, false, true);
    Assert.AreEqual(-1, game.Flame!.Direction);
    Assert.AreEqual(10, enemy.Health);
    Assert.IsTrue(game.Flame.Length <= game.X, "Fire must stop at the map's left edge.");
  }

  [TestMethod]
  public void DamageRemovesWeaponThenGrowthAndRestartClearsIt()
  {
    var game = Arm();
    game.Enemies.Add(new Enemy(game.X, game.Y, settings: new EnemySettings(0, 1, 0)) { Direction = 0 });
    game.Update(0, false, true);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(1, game.PowerUpLevel);
    Assert.IsNull(game.Flame);
    Assert.AreEqual(3, game.Lives);
    Tick(game, Game.DamageProtectionTicks + 1);
    Assert.IsFalse(game.IsLarge);
    Assert.AreEqual(0, game.PowerUpLevel);
    game.Restart();
    Assert.IsFalse(game.HasFlamethrower);
    game.Update(0, false, true);
    Assert.IsNull(game.Flame);
  }

  [TestMethod]
  public void PauseFreezesStreamAndDamageAndLevelTransitionKeepsWeapon()
  {
    var game = Arm();
    var enemy = AddEnemy(game, 3, 10);
    game.Update(0, false, true);
    var flame = game.Flame;
    game.Paused = true;
    for (int tick = 0; tick < 120; tick++)
      game.Update(0, false, true);
    Assert.AreEqual(flame, game.Flame);
    Assert.AreEqual(7, enemy.Health);
    game.Paused = false;
    game.Enemies.Clear();

    File.Copy(LevelPath, NextPath, true);
    game = ArmGame(new Game([LevelPath, NextPath]));
    Tick(game, 120, 1);
    Assert.AreEqual(2, game.LevelNumber);
    Assert.IsTrue(game.HasFlamethrower);
    Assert.AreEqual(2, game.PowerUpLevel);
    game.Restart();
    Assert.IsFalse(game.HasFlamethrower);
  }

  [TestMethod]
  public void FireRendersAllFourEditableFramesAcrossItsEntireReach()
  {
    var game = Arm();
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    game.Update(0, false, true);
    for (int frameIndex = 0; frameIndex < 4; frameIndex++)
    {
      var flame = game.Flame!;
      Assert.AreEqual(frameIndex, flame.FrameIndex);
      renderer.Draw(game, 0, 0);
      var sprite = Sprite.Load(Path.Combine(assets, $"flame-{frameIndex + 1}.sprite"), palette);
      int left = (int)Math.Round(flame.X * 16);
      int top = 28 + (int)Math.Round(flame.Y * 16) - 8;
      for (int distance = 0; distance < 48; distance++)
        for (int y = 0; y < 16; y++)
          if (sprite.Pixel(distance % 16, y) != 0)
            Assert.AreEqual(sprite.Pixel(distance % 16, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + distance]);
      for (int tick = 0; tick < 6; tick++)
        game.Update(0, false, true);
    }
  }

  [TestMethod]
  public void ExtraWeaponPickupBurnsForThreeSecondsAndLosesWeaponWithoutDying()
  {
    Map("........................", "........................", "........................",
      "........................", "........................", ".FF.M...................",
      "........................", ".S....................G.", "########################");
    var game = ArmGame(new Game([LevelPath]));
    Tick(game, 5, 1);
    game.QueueJump();
    Tick(game, 30, jump: true);
    Assert.HasCount(1, game.GrowthHearts);
    TouchHeart(game);
    Assert.IsFalse(game.IsExploding);
    Assert.IsFalse(game.IsDying);
    Assert.IsFalse(game.HasFlamethrower);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(3, game.Lives);
    Assert.AreEqual(180, game.BurnTicksRemaining);
    Assert.AreEqual(string.Empty, game.FlamethrowerQuote);
    Assert.IsNull(game.Flame);
    game.Paused = true;
    Tick(game, 240);
    Assert.AreEqual(180, game.BurnTicksRemaining);
    game.Paused = false;
    for (int tick = 0; tick < 6; tick++)
      game.Update(0, false, true);
    Assert.AreEqual(1, game.BurnFrameIndex);
    Assert.IsNull(game.Flame);
    Tick(game, 173);
    Assert.AreEqual(1, game.BurnTicksRemaining);
    Tick(game, 1);
    Assert.IsFalse(game.IsBurning);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(3, game.Lives);
    Assert.IsFalse(game.HasFlamethrower);
  }

  [TestMethod]
  public void BurstStopsAfterOneSecondAndNeedsReleaseBeforeRestarting()
  {
    var game = Arm();
    var enemy = AddEnemy(game, 3, 10);
    for (int tick = 0; tick < 60; tick++)
      game.Update(0, false, true);
    Assert.IsNotNull(game.Flame);
    Assert.IsFalse(enemy.Alive);
    var nextEnemy = AddEnemy(game, 3, 10);
    game.Update(0, false, true);
    Assert.IsNull(game.Flame);
    for (int tick = 0; tick < 60; tick++)
      game.Update(0, false, true);
    Assert.IsNull(game.Flame);
    Assert.AreEqual(10, nextEnemy.Health);
    Assert.IsTrue(game.HasFlamethrower);
    game.Update(0, false);
    game.Update(0, false, true);
    Assert.IsNotNull(game.Flame);
    Assert.AreEqual(0, game.Flame.FrameIndex);
    Assert.AreEqual(7, nextEnemy.Health);
  }

  [TestMethod]
  public void TappingFireDoesNotBypassDamageInterval()
  {
    var game = Arm();
    var enemy = AddEnemy(game, 3, 10);
    game.Update(0, false, true);
    Assert.AreEqual(7, enemy.Health);
    for (int tick = 0; tick < 8; tick++)
    {
      game.Update(0, false);
      game.Update(0, false, true);
    }

    Assert.AreEqual(7, enemy.Health);
    game.Update(0, false);
    game.Update(0, false, true);
    Assert.AreEqual(4, enemy.Health);
  }

  [TestMethod]
  public void PickupQuoteIsChosenOnceShownForThreeSecondsAndPausesWithGame()
  {
    CreateGame();
    string[] quotes = ["Light Em' If you Got Em!", "Make Flamethrowers Great Again"];
    File.WriteAllLines(NextPath, new[] { "; comment", "", "   " }.Concat(quotes));
    var game = ArmGame(new Game([LevelPath], flamethrowerQuotesPath: NextPath), 0);
    string selected = game.FlamethrowerQuote;
    CollectionAssert.Contains(quotes, selected);
    Assert.AreEqual(180, game.FlamethrowerQuoteTicksRemaining);

    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    Assert.AreEqual(0xFFFFD56B, renderer.Frame.Pixels[(42 * PixelRenderer.Width) + 12]);

    game.Paused = true;
    Tick(game, 240);
    Assert.AreEqual(180, game.FlamethrowerQuoteTicksRemaining);
    Assert.AreEqual(selected, game.FlamethrowerQuote);

    game.Paused = false;
    Tick(game, 179);
    Assert.AreEqual(1, game.FlamethrowerQuoteTicksRemaining);
    Assert.AreEqual(selected, game.FlamethrowerQuote);

    Tick(game, 1);
    Assert.AreEqual(0, game.FlamethrowerQuoteTicksRemaining);
    Assert.AreEqual(string.Empty, game.FlamethrowerQuote);

    renderer.Draw(game, 0, 0);
    Assert.AreNotEqual(0xFFFFD56B, renderer.Frame.Pixels[(42 * PixelRenderer.Width) + 12]);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void MissingOrEmptyWeaponQuotesHaveFallback(bool createFile)
  {
    CreateGame();
    if (createFile)
      File.WriteAllLines(NextPath, ["", " ; comment only"]);

    var game = ArmGame(new Game([LevelPath], flamethrowerQuotesPath: NextPath), 0);
    Assert.AreEqual("Light Em' If you Got Em!", game.FlamethrowerQuote);
  }

  [TestMethod]
  public void RestartClearsPickupQuote()
  {
    var game = Arm();
    Assert.IsGreaterThan(0, game.FlamethrowerQuoteTicksRemaining);
    game.Restart();
    Assert.AreEqual(string.Empty, game.FlamethrowerQuote);
    Assert.AreEqual(0, game.FlamethrowerQuoteTicksRemaining);
  }

  [TestMethod]
  public void BundledWeaponQuotesIncludeTheTwoRequestedAndEightMore()
  {
    string[] quotes = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Assets", "flamethrower-quotes.txt"))
      .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray();

    Assert.HasCount(10, quotes);
    Assert.HasCount(10, quotes.Distinct().ToArray());
    CollectionAssert.Contains(quotes, "Light Em' If you Got Em!");
    CollectionAssert.Contains(quotes, "Make Flamethrowers Great Again!");
  }

  [TestMethod]
  public void FireControlsUseXAndControl()
  {
    foreach (int code in new[] { 0x58, 0x11 })
    {
      var keyboard = new GameKeyboard();
      keyboard.KeyDown(code);
      Assert.IsTrue(keyboard.Down(GameKey.Fire));
      keyboard.KeyUp(code);
      Assert.IsFalse(keyboard.Down(GameKey.Fire));
    }
  }

  [TestMethod]
  public void BundledFlamethrowerIsIntroducedInLevelTwo()
  {
    string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    Assert.IsTrue(paths.Length >= 2);
    Assert.IsFalse(File.ReadAllText(paths[0]).Contains('F'), "Level 1 must not offer a flamethrower.");
    Assert.IsTrue(File.ReadAllText(paths[1]).Contains('F'), "Level 2 introduces the flamethrower; later levels may choose other weapons.");
  }

  [TestMethod]
  public void AllFireFramesAreDistinct()
  {
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var frames = Enumerable.Range(1, 4).Select(index => Sprite.Load(Path.Combine(assets, $"flame-{index}.sprite"), palette)).ToArray();
    for (int index = 1; index < frames.Length; index++)
    {
      bool different = false;
      for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
          different |= frames[index - 1].Pixel(x, y) != frames[index].Pixel(x, y);
      Assert.IsTrue(different);
    }
  }

  private Game CreateGame() => Map(
    "........................", "........................", "........................",
    "........................", "........................", ".F..M...................",
    "........................", ".S....................G.", "########################");

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
    Assert.IsFalse(game.HasFlamethrower);
    Tick(game, 15, -1);
    game.QueueJump();
    Tick(game, 30, jump: true);
    TouchHeart(game);
    Tick(game, ticksAfterPickup);
    Assert.IsTrue(game.HasFlamethrower);
    if (ticksAfterPickup > 0)
      game.Update(1, false);
    return game;
  }

  private static Enemy AddEnemy(Game game, double x, int health)
  {
    var enemy = new Enemy(x, game.Y + Game.LargeBodyHeight - Game.BodyHeight,
      settings: new EnemySettings(0, health, 0));
    game.Enemies.Add(enemy);
    return enemy;
  }
}
