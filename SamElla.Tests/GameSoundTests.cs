using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class GameSoundTests : GameTestBase
{
  [TestMethod]
  [DataRow('B')]
  [DataRow('?')]
  public void BreakingBrickAndRewardingCoinUseDistinctSoundsOnce(char tile)
  {
    var game = Map(
      "....................",
      "....................",
      $".{tile}..................",
      "....................",
      ".S................G.",
      "####################");

    var sounds = Listen(game);

    Tick(game, 10);
    game.QueueJump();
    Tick(game, 60, jump: true);
    GameSound expected = tile == 'B' ? GameSound.BlockBreak : GameSound.Coin;

    Assert.AreEqual(1, sounds.Count(sound => sound == expected));
    Assert.IsFalse(sounds.Contains(tile == 'B' ? GameSound.Coin : GameSound.BlockBreak));

    game.QueueJump();
    Tick(game, 60, jump: true);
    Assert.AreEqual(1, sounds.Count(sound => sound == expected), "Used or destroyed blocks must not replay rewards.");
  }

  [TestMethod]
  public void WalkingOverCoinPlaysCollectionSoundOnce()
  {
    var game = Map(
      "....................",
      "....................",
      "....................",
      "....................",
      ".So...............G.",
      "####################");

    var sounds = Listen(game);

    Tick(game, 10);
    Tick(game, 10, 1);
    Assert.AreEqual(1, game.Coins);

    CollectionAssert.AreEqual(new[] { GameSound.Coin }, sounds);
    Tick(game, 10, -1);
    Tick(game, 10, 1);
    CollectionAssert.AreEqual(new[] { GameSound.Coin }, sounds);
  }

  [TestMethod]
  public void EffectsHaveDistinctNonSilentPcmAndDeathLastsExactlyTwoSeconds()
  {
    var effects = Enum.GetValues<GameSound>().Select(GameSounds.Create).ToArray();
    foreach (var samples in effects)
    {
      Assert.IsTrue(samples.Length > 0);
      Assert.IsTrue(samples.Any(sample => Math.Abs((int)sample) > 1000));
      Assert.IsTrue(samples.All(sample => Math.Abs((int)sample) < short.MaxValue));
      Assert.AreEqual(0, samples[0]);
      Assert.IsTrue(Math.Abs((int)samples[^1]) < 100, "Release must end near silence.");
    }

    Assert.AreEqual(GameSounds.SampleRate * 2, GameSounds.Create(GameSound.Died).Length);
    for (int i = 0; i < effects.Length; i++)
      for (int j = i + 1; j < effects.Length; j++)
        Assert.IsFalse(effects[i].Take(4000).SequenceEqual(effects[j].Take(4000)));
  }

  [TestMethod]
  [DataRow('F')]
  [DataRow('Q')]
  public void GrowthAndWeaponPickupPlayPowerUpAndDamagePlaysPowerDown(char weapon)
  {
    var game = Map(
      "........................",
      "........................",
      "........................",
      "........................",
      "........................",
      $".{weapon}..M...................",
      "........................",
      ".S....................G.",
      "########################");

    var sounds = Listen(game);
    Tick(game, 10);
    Tick(game, 15, 1);
    game.QueueJump();
    Tick(game, 30, jump: true);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.PowerUp));

    Tick(game, 15, -1);
    game.QueueJump();
    Tick(game, 30, jump: true);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.HasFlamethrower || game.HasBoomStick);
    Assert.AreEqual(2, sounds.Count(sound => sound == GameSound.PowerUp));

    sounds.Clear();
    Hit(game);
    CollectionAssert.AreEqual(new[] { GameSound.PowerDown }, sounds);
    Assert.IsTrue(game.IsLarge);

    game.Update(0, false);
    Assert.HasCount(1, sounds, "Invulnerability must not repeat the loss sound.");

    game.Enemies.Clear();
    Tick(game, Game.DamageProtectionTicks);
    Hit(game);
    Assert.IsFalse(game.IsLarge);

    CollectionAssert.AreEqual(new[] { GameSound.PowerDown, GameSound.PowerDown }, sounds);
    game.Enemies.Clear();
    Tick(game, Game.DamageProtectionTicks);
    Hit(game);

    Assert.IsTrue(game.IsDying);
    Tick(game, Game.DeathDurationTicks + 10);
    CollectionAssert.AreEqual(new[] { GameSound.PowerDown, GameSound.PowerDown, GameSound.Died }, sounds);
  }

  [TestMethod]
  public void JumpPlaysOnTakeoffWithoutRepeatingInAirOrWhilePaused()
  {
    var game = Map(
      "....................",
      "....................",
      "....................",
      "....................",
      ".S................G.",
      "####################");

    Tick(game, 10);
    var sounds = Listen(game);
    game.Paused = true;
    game.QueueJump();
    Tick(game, 2, jump: true);
    Assert.IsEmpty(sounds);

    game.Paused = false;
    game.QueueJump();
    game.Update(0, true);
    CollectionAssert.AreEqual(new[] { GameSound.Jump }, sounds);

    game.QueueJump();
    Tick(game, 8, jump: true);
    CollectionAssert.AreEqual(new[] { GameSound.Jump }, sounds);
  }

  [TestMethod]
  public void OverheatingFlamethrowerPlaysPowerDown()
  {
    Map(
      "....................",
      "....................",
      ".F..................",
      "....................",
      ".S................G.",
      "####################");

    var game = new Game([LevelPath], konamiCheat: true);
    var sounds = Listen(game);
    Tick(game, 10);
    game.QueueJump();
    Tick(game, 30, jump: true);
    TouchHeart(game);

    Assert.IsTrue(game.IsBurning);
    Assert.AreEqual(1, sounds.Count(sound => sound == GameSound.PowerDown));
    Assert.IsFalse(sounds.Contains(GameSound.PowerUp));
  }

  [TestMethod]
  [DataRow(EnemyKind.Walker, 1)]
  [DataRow(EnemyKind.ToasterCat, 1)]
  [DataRow(EnemyKind.Zombie, 1)]
  [DataRow(EnemyKind.Dog, 1)]
  [DataRow(EnemyKind.Walker, 2)]
  [DataRow(EnemyKind.ToasterCat, 2)]
  [DataRow(EnemyKind.Zombie, 2)]
  [DataRow(EnemyKind.Dog, 2)]
  public void StompsPlaySmashForSurvivingEnemyAndDistinctDeathSoundForDefeat(EnemyKind kind, int health)
  {
    var game = Map(
      "....................",
      "....................",
      ".S..................",
      "....................",
      "..................G.",
      "####################");

    var enemy = new Enemy(game.X, 5 - (kind == EnemyKind.Zombie ? Game.LargeBodyHeight : Game.BodyHeight),
      kind, new EnemySettings(0, health, 0)) { Direction = 0 };
    game.Enemies.Add(enemy);
    var sounds = Listen(game);
    for (int tick = 0; tick < 60; tick++)
    {
      game.Update(0, false);
      if (game.VelocityY < 0)
        break;
    }

    Assert.IsTrue(game.VelocityY < 0, "The player should bounce after stomping.");
    Assert.AreEqual(health > 1, enemy.Alive);
    if (health > 1)
    {
      CollectionAssert.AreEqual(new[] { GameSound.EnemySmash }, sounds);
      for (int tick = 0; tick < 180 && enemy.Alive; tick++)
        game.Update(0, false);

      Assert.IsFalse(enemy.Alive, "A second stomp should defeat the injured enemy.");
    }

    GameSound[] expected = health > 1
      ? [GameSound.EnemySmash, GameSound.EnemyDied]
      : [GameSound.EnemyDied];
    CollectionAssert.AreEqual(expected, sounds);
    Tick(game, Enemy.SquashDurationTicks + 30);
    CollectionAssert.AreEqual(expected, sounds,
      "The enemy's splatter should not repeat the impact sound.");
  }

  private static void Hit(Game game)
  {
    game.Enemies.Add(new Enemy(game.X + 0.4, game.Y) { Direction = 0 });
    game.Update(0, false);
  }

  private static List<GameSound> Listen(Game game)
  {
    var sounds = new List<GameSound>();
    game.SoundRequested += sounds.Add;
    return sounds;
  }
}
