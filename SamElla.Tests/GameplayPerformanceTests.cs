using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class GameplayPerformanceTests : GameTestBase
{
  [TestMethod]
  public void CrowdedLevelDoesNotAllocateCollisionArraysForEveryEnemy()
  {
    var game = Map(
      new string('.', 400),
      new string('.', 400),
      ".S" + new string('.', 396) + "G.",
      new string('#', 400));

    game.CompleteLevelStart();
    for (int i = 0; i < 256; i++)
      game.Enemies.Add(new Enemy(60 + i, 2));

    Tick(game, 120);
    long before = GC.GetAllocatedBytesForCurrentThread();
    Tick(game, 120);
    long bytesPerTick = (GC.GetAllocatedBytesForCurrentThread() - before) / 120;

    Assert.IsFalse(game.IsDying);
    Assert.IsTrue(bytesPerTick < 8192,
      $"Crowded-level simulation allocated {bytesPerTick} bytes per tick; movement should use stack contacts.");
  }

}
