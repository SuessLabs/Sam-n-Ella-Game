// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class SludgeRunGateTests : GameTestBase
{
  [TestMethod]
  public void SludgeRunStartsGrownWithShotgunAndRetryRestoresEquipment()
  {
    var game = new Game([SourcePath]);
    Assert.IsTrue(game.Level.StartsWithShotgun);
    Assert.IsTrue(game.IsLarge);
    Assert.IsTrue(game.HasBoomStick);
    Assert.IsFalse(game.HasFlamethrower);
    RemoveShotgun(game);
    Assert.IsFalse(game.HasBoomStick);
    game.Restart();
    Assert.IsTrue(game.IsLarge);
    Assert.IsTrue(game.HasBoomStick);
  }

  [TestMethod]
  public void WallCoversEntireMapWithDeathPitOnApproach()
  {
    var level = Level.Load(SourcePath);
    for (int y = 0; y < level.Height; y++)
      Assert.AreEqual('B', level.Tile(164, y));

    for (int x = 162; x < 164; x++)
    {
      Assert.AreEqual('.', level.Tile(x, 20));
      Assert.AreEqual('.', level.Tile(x, 21));
    }

    Assert.AreEqual('G', level.Tile(171, 19));
  }

  [TestMethod]
  public void ShotgunOpensTwoTilePassageAndPlayerCanJumpThroughToGoal()
  {
    var game = AtLedge();
    Tick(game, 6);
    game.Update(0, false, true);
    Assert.AreEqual('.', game.Level.Tile(164, 19));
    Tick(game, Game.ShotgunReloadTicks);
    game.QueueJump();
    Tick(game, 4, jump: true);
    game.Update(0, true, true);
    Assert.AreEqual('.', game.Level.Tile(164, 18));
    Assert.IsTrue(game.HasBoomStick);
    for (int tick = 0; tick < 120 && !game.Won && !game.IsDying; tick++)
      game.Update(1, true);

    Assert.IsTrue(game.Won, $"Stopped at ({game.X:F2}, {game.Y:F2}).");
    Assert.AreEqual(3, game.Lives);
  }

  [TestMethod]
  public void LosingShotgunLeavesWallIntactAndJumpingIntoItFallsIntoDeathPit()
  {
    var game = AtLedge();
    Tick(game, 6);
    RemoveShotgun(game);
    Assert.IsTrue(game.IsLarge);
    game.QueueJump();
    for (int tick = 0; tick < 180 && !game.IsDying; tick++)
      game.Update(1, true, true);

    Assert.IsTrue(game.IsDying);
    Assert.IsFalse(game.Won);
    Assert.AreEqual(2, game.Lives);
    Assert.IsTrue(game.Y > game.Level.Height + 2, "The unarmed player must die by falling into the pit.");
    for (int y = 0; y < game.Level.Height; y++)
      Assert.AreEqual('B', game.Level.Tile(164, y));

    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.IsTrue(game.HasBoomStick);
    Assert.IsTrue(game.IsLarge);
  }

  [TestMethod]
  public void OtherLevelsDoNotGrantStartingShotgun()
  {
    string directory = Path.Combine(AppContext.BaseDirectory, "Levels");
    foreach (string path in Directory.GetFiles(directory, "*.txt").Where(path => !path.EndsWith("03-02-sludge-run.txt", StringComparison.OrdinalIgnoreCase)))
      Assert.IsFalse(Level.Load(path).StartsWithShotgun);
  }

  [TestMethod]
  [DataRow("StartingPower=Shotgun", true)]
  [DataRow(" startingpower = shotgun ", true)]
  [DataRow("StartingPower=None", false)]
  public void StartingEquipmentHeaderParsesWithoutChangingSpawn(string header, bool equipped)
  {
    var level = Level.Parse("test", [header, "..........", "..........", "..........", ".S......G.", "##########"]);
    Assert.AreEqual(equipped, level.StartsWithShotgun);
    Assert.AreEqual((1, 3), level.Spawn);
  }

  [TestMethod]
  public void InvalidStartingEquipmentIsRejected()
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("bad-power",
      ["StartingPower=Banana", "..........", ".S......G.", "##########"]));
  }

  private static string SourcePath => Path.Combine(AppContext.BaseDirectory, "Levels", "03-02-sludge-run.txt");

  private Game AtLedge()
  {
    string[] rows = File.ReadAllLines(SourcePath);
    int start = rows.Length - Level.Load(SourcePath).Height;
    for (int row = start; row < rows.Length; row++)
      rows[row] = rows[row].Replace('S', '.');

    char[] spawn = rows[start + 19].ToCharArray();
    spawn[161] = 'S';
    rows[start + 19] = new string(spawn);
    File.WriteAllLines(LevelPath, rows);
    var game = new Game([LevelPath]);
    game.Enemies.Clear();
    return game;
  }

  private static void RemoveShotgun(Game game)
  {
    var enemy = new Enemy(game.X, game.Y, settings: new EnemySettings(0, 10, 0)) { Direction = 0 };
    game.Enemies.Add(enemy);
    game.Update(0, false);
    game.Enemies.Clear();
  }
}
