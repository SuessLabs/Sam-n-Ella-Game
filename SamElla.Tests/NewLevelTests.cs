using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class NewLevelTests
{
  [TestMethod]
  [DataRow("01-03-pipe-gardens.txt", 0)]
  [DataRow("01-04-zombie-ate-my.txt", 0)]
  [DataRow("01-05-pipe-terraces.txt", 2)]
  [DataRow("02-01-ruins-crossing.txt", 0)]
  [DataRow("02-02-brick-summit.txt", 0)]
  [DataRow("02-05-canes-end-crossing.txt", 0)]
  public void RebalancedLevelsCanBeCompletedWithoutLosingALife(string name, int groundedWait)
  {
    var path = Path.Combine(AppContext.BaseDirectory, "Levels", name);
    var game = new Game([path]);
    int groundRow = game.Level.Height - 2;
    int goalX = Enumerable.Range(0, game.Level.Width).Single(x => game.Level.Tile(x, groundRow - 1) == 'G');

    int[] takeoffs = Enumerable.Range(1, game.Level.Width - 1)
      .Where(x => (game.Level.Tile(x, groundRow) == '.' && game.Level.Tile(x - 1, groundRow) == '#')
        || (game.Level.Tile(x, groundRow - 1) == '^' && game.Level.Tile(x - 1, groundRow - 1) != '^'))
      .Select(x => x - 2).ToArray();

    // Patrol speed is configurable. Try a few launch timings instead of requiring
    // every level to match one particular enemy phase at the start of the run.
    for (int startDelay = 0; startDelay <= 60 && !game.Won; startDelay++)
    {
      if (startDelay > 0)
        game.Restart();

      for (int tick = 0; tick < startDelay; tick++)
        game.Update(0, false);

      int nextTakeoff = 0;
      int groundedFrames = 0;
      for (int tick = 0; tick < 6000 && !game.Won && !game.IsDying; tick++)
      {
        int direction = game.X >= goalX - 0.2 ? 0 : 1;
        bool atTakeoff = nextTakeoff < takeoffs.Length && game.X >= takeoffs[nextTakeoff];

        // Land before each pit or spike cluster, then jump from a safe takeoff point.
        if (atTakeoff && !game.Grounded)
          direction = 0;

        // Rise above tall zombies before moving across their bodies.
        bool tallEnemyAhead = game.Enemies.Any(enemy => enemy.Alive && enemy.Kind == EnemyKind.Zombie
          && enemy.X >= game.X + Game.BodyWidth && enemy.X - game.X < 2.5
          && game.Y + game.PlayerHeight > enemy.Y && enemy.Y >= game.Y - 1.2);
        if (tallEnemyAhead)
          direction = 0;

        groundedFrames = game.Grounded ? groundedFrames + 1 : 0;
        if (game.Grounded && (direction != 0 || tallEnemyAhead)
          && (groundedFrames > groundedWait || tallEnemyAhead))
        {
          game.QueueJump();

          if (atTakeoff)
            nextTakeoff++;
        }

        game.Update(direction, true, game.HasFlamethrower || tick % 32 == 0);
      }
    }

    Assert.IsTrue(game.Won, $"{name}: the route must reach the goal; stopped at ({game.X:F2}, {game.Y:F2}), dying={game.IsDying}.");
    Assert.AreEqual(3, game.Lives, "The route should be completable without losing a life.");
  }
}
