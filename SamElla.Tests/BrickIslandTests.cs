// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class BrickIslandTests
{
  [TestMethod]
  [DataRow("02-03-moonlit-stepping-stones.txt")]
  [DataRow("02-04-midnight-brickfall.txt")]
  public void MissingTheFirstJumpFallsIntoTheVoid(string name)
  {
    var game = new Game([Path.Combine(AppContext.BaseDirectory, "Levels", name)]);
    game.CompleteLevelStart();
    for (int tick = 0; tick < 180 && !game.IsDying; tick++)
      game.Update(1, false);

    Assert.IsTrue(game.IsDying);
    Assert.IsFalse(game.Won);
    Assert.AreEqual(2, game.Lives);
  }

  [TestMethod]
  [DataRow("02-03-moonlit-stepping-stones.txt", false)]
  [DataRow("02-03-moonlit-stepping-stones.txt", true)]
  [DataRow("02-04-midnight-brickfall.txt", false)]
  [DataRow("02-04-midnight-brickfall.txt", true)]
  public void GuardedBrickIslandsCanBeCompletedWithJumpsAndStomps(string name, bool large)
  {
    var game = new Game([Path.Combine(AppContext.BaseDirectory, "Levels", name)], konamiCheat: large);
    game.CompleteLevelStart();
    var islands = new List<(int Left, int Right, int Top)>();
    for (int y = 0; y < game.Level.Height; y++)
    {
      for (int x = 0; x < game.Level.Width; x++)
      {
        Assert.AreNotEqual('#', game.Level.Tile(x, y), "These levels must have no floor.");
        if (game.Level.Tile(x, y) != 'B')
          continue;

        int left = x;
        while (game.Level.Tile(x, y) == 'B')
          x++;

        islands.Add((left, x, y));
      }
    }

    islands = islands.OrderBy(island => island.Left).ToList();
    Assert.AreEqual(BackgroundTheme.Night, game.Level.Background);
    Assert.IsTrue(game.Level.EnemiesAvoidLedges);
    Assert.IsGreaterThanOrEqualTo(12, game.Enemies.Count);
    Assert.IsGreaterThanOrEqualTo(16, islands.Count);
    Assert.IsGreaterThanOrEqualTo(6, islands.Select(island => island.Top).Distinct().Count());
    for (int tick = 0; tick < 60 && !game.Grounded; tick++)
      game.Update(0, false);

    for (int index = 0; index < islands.Count && !game.Won; index++)
    {
      var island = islands[index];
      LandAndClearIsland(game, island, index == 0, name);
      if (game.Won)
        break;

      if (index == islands.Count - 1)
      {
        for (int tick = 0; tick < 120 && !game.Won && !game.IsDying; tick++)
          game.Update(1, false);

        break;
      }

      Assert.IsGreaterThan(island.Right, islands[index + 1].Left, "Every island must have a bottomless gap before it.");
      double takeoff = island.Right - 1;
      for (int tick = 0; tick < 180 && game.X < takeoff && !game.IsDying; tick++)
        game.Update(game.Grounded ? 1 : 0, false);

      // A dog can slow the player. Recover full speed before committing to a gap.
      for (int tick = 0; tick < 400 && (!game.Grounded || game.SlowTicksRemaining > 0) && !game.IsDying; tick++)
        game.Update(0, false);

      Assert.IsTrue(game.Grounded && !game.IsDying, $"{name}: safe takeoff required on island {index + 1}.");
      game.QueueJump();
    }

    Assert.IsTrue(game.Won, $"{name}: the last brick island must lead to the goal.");
    Assert.AreEqual(large ? 30 : 3, game.Lives);
    Assert.IsGreaterThanOrEqualTo(8, game.Enemies.Count(enemy => !enemy.Alive), "The route must handle the guarded landings.");
  }

  private static void LandAndClearIsland(Game game, (int Left, int Right, int Top) island, bool first, string name)
  {
    bool entered = first;
    for (int tick = 0; tick < 900 && !game.IsDying && !game.Won; tick++)
    {
      var enemy = game.Enemies.FirstOrDefault(enemy => enemy.Alive && enemy.PatrolFloorRow == island.Top
        && enemy.X >= island.Left - 0.01 && enemy.X < island.Right);
      bool landed = game.Grounded && Math.Abs(game.Y + game.PlayerHeight - island.Top) < 0.001;
      entered |= landed;
      if (enemy is null && landed)
        return;

      // Land on open space, or intercept a tall zombie from above for a stomp.
      double aim = enemy is null || (!entered && enemy.Kind != EnemyKind.Zombie) ? island.Left + 0.5
        : Math.Clamp(enemy.X, island.Left + 0.1, island.Right - Game.BodyWidth - 0.1);
      if (enemy is not null && entered && !enemy.CanInteract)
        aim = Math.Clamp(enemy.X - Game.BodyWidth - 0.4, island.Left + 0.1, island.Right - Game.BodyWidth - 0.1);

      int direction = Math.Abs(game.X - aim) < 0.15 ? 0 : game.X < aim ? 1 : -1;
      if (enemy is not null && enemy.CanInteract && entered && game.X >= island.Left - 0.3
        && game.Y + game.PlayerHeight > enemy.Y - 0.1)
        direction = 0;

      if (game.X < island.Left - 0.3)
        direction = 1;

      if (enemy is not null && enemy.CanInteract && landed)
        game.QueueJump();

      game.Update(direction, true);
    }

    Assert.IsTrue(game.Won, $"{name}: failed to clear island ({island.Left}, {island.Top}) at ({game.X:F2}, {game.Y:F2}).");
  }
}
