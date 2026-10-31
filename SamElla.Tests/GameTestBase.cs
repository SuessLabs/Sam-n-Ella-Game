using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

public abstract class GameTestBase
{
  protected string LevelPath { get; private set; } = string.Empty;
  protected string NextPath { get; private set; } = string.Empty;

  [TestInitialize]
  public void CreateLevelPaths()
  {
    LevelPath = Path.Combine(Path.GetTempPath(), $"samella-test-{Guid.NewGuid():N}.txt");
    NextPath = Path.Combine(Path.GetTempPath(), $"samella-test-{Guid.NewGuid():N}.txt");
  }

  [TestCleanup]
  public void DeleteLevels()
  {
    File.Delete(LevelPath);
    File.Delete(NextPath);
  }

  protected Game Map(params string[] rows)
  {
    File.WriteAllLines(LevelPath, rows);
    return new Game([LevelPath]);
  }

  protected static void Tick(Game game, int count, int direction = 0, bool jump = false)
  {
    for (int i = 0; i < count; i++)
    {
      game.Update(direction, jump);
    }
  }

  protected static void TouchHeart(Game game)
  {
    // Arrange physical contact for size/damage tests; chase physics is tested separately.
    foreach (var heart in game.GrowthHearts)
    {
      heart.X = game.X;
      heart.Y = game.Y;
      heart.Direction = 0;
      heart.VelocityY = 0;
    }
    game.Update(0, false);
  }
}
