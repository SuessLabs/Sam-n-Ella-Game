using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Effects;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class SectionFourTests : GameTestBase
{
  private static readonly string[] Names = ["04-02-paper-chase.txt", "04-03-roll-with-it.txt", "04-04-whip-it-quick.txt", "04-05-panic-parade.txt"];

  [TestMethod]
  public void SectionFourHasFiveTimedRunsAndClearlyNamedBossPlaceholder()
  {
    string[] files = LevelSelectScreen.Discover(Path.Combine(AppContext.BaseDirectory, "Levels"));
    var levels = files.Select(Level.Load).Where(level => level.Section == 4).ToArray();
    Assert.HasCount(6, levels);
    foreach (var level in levels.Take(5))
      Assert.IsTrue(level.IsSpeedRun);

    var placeholder = new Game([files[^1]]);
    Assert.AreEqual("04-06", placeholder.LevelLabel);
    StringAssert.Contains(placeholder.Level.DisplayName, "Placeholder");
    Assert.IsNull(placeholder.Boss);
    Assert.AreEqual(100, placeholder.LevelSecondsRemaining);
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(1)]
  [DataRow(2)]
  [DataRow(3)]
  public void NewRunsHaveCorrectEquipmentRefillsAndMixedNonBossEnemies(int index)
  {
    var game = NewRun(index);
    var level = game.Level;
    Assert.AreEqual(600, game.LevelTicksRemaining);
    Assert.IsTrue(game.IsLarge);
    Assert.AreEqual(index < 2, game.HasToiletPaper);
    Assert.AreEqual(index >= 2, game.HasChainWhip);
    Assert.AreEqual(index < 2 ? 'L' : 'W', level.Tile(14, 16));
    Assert.IsNotEmpty(level.BluePunkSpawns);
    Assert.IsNotEmpty(level.PurplePunkSpawns);
    Assert.IsNotEmpty(level.GrandpaSpawns);
    Assert.IsNull(game.Boss);
    int[] bottles = Enumerable.Range(0, level.Width).Where(x => level.Tile(x, 19) == 'k').ToArray();
    Assert.IsTrue(bottles.Length >= 3);
    Assert.IsTrue(bottles.Zip(bottles.Skip(1), (left, right) => right - left).All(distance => distance == 96));
    int pipes = Enumerable.Range(0, level.Width).Count(x => game.CanUseReliefPipe(x, 19) && level.Tile(x - 1, 19) != 'P');
    Assert.AreEqual(2, pipes);
    game.Restart();
    Assert.AreEqual(index < 2, game.HasToiletPaper);
    Assert.AreEqual(index >= 2, game.HasChainWhip);
  }

  [TestMethod]
  public void GrandpaPatrolsAndSwingsCaneWithoutLockingExitOrDodgingLikeBoss()
  {
    var game = Arena();
    var grandpa = new Enemy(8, 6.2, EnemyKind.Grandpa) { Direction = 0, CaneAttackTicksRemaining = 1 };
    game.Enemies.Add(grandpa);
    Assert.AreEqual(3, grandpa.Health);
    Assert.IsFalse(grandpa.IsBoss);
    Assert.IsNull(game.Boss);
    Tick(game, 36);
    Assert.IsTrue(grandpa.AirWaveLength > 0);
    grandpa.Stomp();
    grandpa.Stomp();
    Assert.AreEqual(0, grandpa.BossDodgeTicksRemaining);
  }

  [TestMethod]
  public void PaperRollHasArcAndHitsOnlyFirstEnemy()
  {
    var game = Arena("ToiletPaper");
    var first = new Enemy(8, 6.2, EnemyKind.PunkBlue, new EnemySettings(0, 10, 0)) { Direction = 0, ProjectileAttackTicks = 10000 };
    var second = new Enemy(10, 6.2, EnemyKind.PunkPurple, new EnemySettings(0, 10, 0)) { Direction = 0, ProjectileAttackTicks = 10000 };
    game.Enemies.AddRange([first, second]);
    Tick(game, 6);
    game.Update(0, false, true);
    var roll = game.ToiletPaperRolls.Single();
    double x = roll.X, y = roll.Y;
    Tick(game, 4);
    Assert.IsTrue(roll.X > x);
    Assert.IsTrue(roll.Y < y);
    Assert.IsTrue(roll.VelocityY > -3);
    Tick(game, 20);
    Assert.AreEqual(6, first.Health);
    Assert.AreEqual(10, second.Health);
    Assert.IsEmpty(game.ToiletPaperRolls);
  }

  [TestMethod]
  public void ThrowNeedsReleaseAndCooldownAndTerrainStopsPaper()
  {
    var game = Arena("ToiletPaper");
    Tick(game, 6);
    game.Level.UseBlock(6, 6);
    game.Level.UseBlock(6, 7);
    game.Update(0, false, true);
    var roll = game.ToiletPaperRolls.Single();
    game.Paused = true;
    Tick(game, 100);
    Assert.AreEqual(1, roll.AgeTicks);
    game.Paused = false;
    for (int tick = 0; tick < 60; tick++)
      game.Update(0, false, true);

    Assert.IsEmpty(game.ToiletPaperRolls);
    Assert.AreEqual('X', game.Level.Tile(6, 7));
    game.Update(0, false);
    game.Update(-1, false, true);
    Assert.AreEqual(-1, game.ToiletPaperRolls.Single().Direction);
  }

  [TestMethod]
  public void LosingPaperKeepsGrowthAndRetryRestoresStartingWeapon()
  {
    var game = Arena("ToiletPaper");
    game.Update(0, false, true);
    game.Enemies.Add(new Enemy(game.X, game.Y, settings: new EnemySettings(0, 10, 0)) { Direction = 0 });
    game.Update(0, false);
    Assert.IsFalse(game.HasToiletPaper);
    Assert.IsTrue(game.IsLarge);
    Assert.IsEmpty(game.ToiletPaperRolls);
    Assert.AreEqual(3, game.Lives);
    game.Restart();
    Assert.IsTrue(game.HasToiletPaper);
  }

  [TestMethod]
  [DataRow("None", false)]
  [DataRow("ChainWhip", true)]
  public void PaperCarriesBetweenLevelsAndWhipStartingEquipmentReplacesIt(string nextPower, bool whip)
  {
    Arena("ToiletPaper");
    string[] rows = File.ReadAllLines(LevelPath);
    rows = rows.Select(row => row.Replace('G', '.')).ToArray();
    int mapStart = rows.Length - Level.Load(LevelPath).Height;
    char[] goal = rows[mapStart + 7].ToCharArray();
    goal[4] = 'G';
    rows[mapStart + 7] = new string(goal);
    File.WriteAllLines(LevelPath, rows);
    File.WriteAllLines(NextPath, rows.Select(row => row.StartsWith("StartingPower=") ? $"StartingPower={nextPower}" : row));
    var game = new Game([LevelPath, NextPath]);
    for (int tick = 0; tick < 30 && game.LevelNumber == 1; tick++)
      game.Update(1, false);

    Assert.AreEqual(2, game.LevelNumber);
    Assert.AreEqual(whip, game.HasChainWhip);
    Assert.AreEqual(!whip, game.HasToiletPaper);
    Assert.IsEmpty(game.ToiletPaperRolls);
  }

  [TestMethod]
  [DataRow("None", false)]
  [DataRow("ChainWhip", true)]
  public void PaperPickupRequiresGrowthAndCanReplaceWhip(string power, bool canCollect)
  {
    Map($"StartingPower={power}", "........................", "........................", "........................", "........................",
      "........................", ".L......................", "........................", ".S....................G.", "########################");
    var game = new Game([LevelPath]);
    Tick(game, 6);
    game.QueueJump();
    Tick(game, 30, jump: true);
    Assert.IsTrue(game.GrowthHearts.Any(heart => heart.IsToiletPaper));
    TouchHeart(game);
    Assert.AreEqual(canCollect, game.HasToiletPaper);
    if (canCollect)
      Assert.IsFalse(game.HasChainWhip);
  }

  [TestMethod]
  [DataRow(0)]
  [DataRow(1)]
  [DataRow(2)]
  [DataRow(3)]
  public void TimedRoutesCanReachToiletWithRefillsAndStartingWeapons(int index)
  {
    var game = NewRun(index);
    // Check terrain and refill timing independently of combat decisions.
    game.Enemies.Clear();
    for (int delay = 0; delay <= 60 && !game.Won; delay++)
    {
      if (delay > 0)
      {
        game.Restart();
        game.Enemies.Clear();
        Tick(game, delay);
      }

      int grounded = 0;
      for (int tick = 0; tick < 6000 && !game.Won && !game.IsDying; tick++)
      {
        grounded = game.Grounded ? grounded + 1 : 0;
        bool obstacle = Enumerable.Range(1, 3).Any(ahead => game.Level.Solid((int)Math.Floor(game.X) + ahead, (int)Math.Floor(game.Y + game.PlayerHeight - 0.2)));
        bool enemy = game.Enemies.Any(enemy => enemy.CanInteract && enemy.X + Game.BodyWidth > game.X && enemy.X - game.X < 3);
        bool approachingBottle = Enumerable.Range(0, 9).Any(ahead => game.Level.Tile((int)Math.Floor(game.X) + ahead, 19) == 'k');
        if (game.Grounded && grounded >= 2 && (obstacle || enemy && !approachingBottle))
          game.QueueJump();

        game.Update(1, true, tick % 2 == 0);
      }
    }

    Assert.IsTrue(game.Won, $"{Names[index]} stopped at ({game.X:F2},{game.Y:F2}), timer={game.LevelTicksRemaining}, dying={game.IsDying}.");
    Assert.AreEqual(3, game.Lives);
  }

  [TestMethod]
  public void PaperAndGrandpaRenderWithBothPunkVariants()
  {
    var game = NewRun(0);
    game.Enemies.Clear();
    game.Enemies.Add(new Enemy(10, 18.2, EnemyKind.PunkBlue) { Direction = 0, ProjectileAttackTicks = 10000 });
    game.Enemies.Add(new Enemy(14, 18.2, EnemyKind.PunkPurple) { Direction = 0, ProjectileAttackTicks = 10000 });
    game.Enemies.Add(new Enemy(18, 18.2, EnemyKind.Grandpa) { Direction = 0 });
    game.Update(0, false, true);
    Tick(game, 5);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    string? preview = Environment.GetEnvironmentVariable("SAMELLA_PAPER_PREVIEW");
    if (preview is not null)
      renderer.Frame.SavePng(preview);

    Assert.IsTrue(renderer.Frame.Pixels.Contains(0xFFF8F0D9));
    Assert.IsTrue(game.ToiletPaperRolls.Count > 0);
  }

  private Game NewRun(int index) => new([Path.Combine(AppContext.BaseDirectory, "Levels", Names[index])]);

  private Game Arena(string power = "None") => Map($"StartingPower={power}", "................................", "................................", "................................",
    "................................", "................................", "................................", "................................", "...S..........................G.", "################################");
}
