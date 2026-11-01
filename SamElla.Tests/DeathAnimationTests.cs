// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class DeathAnimationTests : GameTestBase
{
  [TestMethod]
  [DataRow(PlayerCharacter.Sam, "spike")]
  [DataRow(PlayerCharacter.Ella, "spike")]
  [DataRow(PlayerCharacter.Sam, "pit")]
  [DataRow(PlayerCharacter.Ella, "pit")]
  [DataRow(PlayerCharacter.Sam, "enemy")]
  [DataRow(PlayerCharacter.Ella, "enemy")]
  public void NormalDeathsBurstSpillGutsAndSettleInsteadOfShowingOneStaticSprite(PlayerCharacter character, string cause)
  {
    var game = Die(character, cause);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    uint[]? previous = null;
    int last = 0;
    foreach (int age in new[] { 0, 12, 24, 36, 48, 72, 96, 120 })
    {
      Tick(game, age - last);
      last = age;
      renderer.Draw(game, 0, 0);
      if (previous is not null)
        Assert.IsFalse(previous.SequenceEqual(renderer.Frame.Pixels), $"The death must visibly animate at tick {age}.");

      previous = (uint[])renderer.Frame.Pixels.Clone();
      if (age >= 12)
        Assert.IsTrue(previous.Contains(0xFFD98290), "Spilled intestines must remain visible.");

      string? directory = Environment.GetEnvironmentVariable("SAMELLA_DEATH_PREVIEW");
      if (directory is not null && cause == "spike")
        renderer.Frame.SavePng(Path.Combine(directory, $"death-{character}-{age:000}.png"));
    }

    Assert.IsTrue(game.AwaitingRestart);
    Tick(game, 60);
    renderer.Draw(game, 0, 0);
    Assert.IsTrue(previous!.SequenceEqual(renderer.Frame.Pixels), "The final puddle stays settled while waiting for retry.");
    Assert.IsTrue(game.ContinueAfterDeath());
    renderer.Draw(game, 0, 0);
    Assert.IsFalse(renderer.Frame.Pixels.Contains(0xFFD98290), "Respawning removes the spilled guts.");
  }

  [TestMethod]
  [DataRow(PlayerCharacter.Sam)]
  [DataRow(PlayerCharacter.Ella)]
  public void PausingFreezesDeathSpillAndResumingContinuesIt(PlayerCharacter character)
  {
    var game = Die(character, "spike");
    Tick(game, 60);
    game.Paused = true;
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    uint[] paused = (uint[])renderer.Frame.Pixels.Clone();
    Tick(game, 30);
    renderer.Draw(game, 0, 0);
    Assert.AreEqual(60, game.DeathTicksRemaining);
    Assert.IsTrue(paused.SequenceEqual(renderer.Frame.Pixels));
    game.Paused = false;
    Tick(game, 12);
    Assert.AreEqual(48, game.DeathTicksRemaining);
  }

  private Game Die(PlayerCharacter character, string cause)
  {
    string[] rows = Enumerable.Repeat(new string('.', 32), 13).ToArray();
    char[] start = rows[cause == "enemy" ? 10 : 5].ToCharArray();
    start[8] = 'S';
    rows[cause == "enemy" ? 10 : 5] = new string(start);
    char[] floor = rows[10].ToCharArray();
    floor[30] = 'G';
    if (cause == "spike")
      floor[8] = '^';

    rows[10] = new string(floor);
    if (cause != "pit")
      rows[11] = new string('#', 32);

    Map(rows);
    var game = new Game([LevelPath]) { Character = character };
    if (cause == "enemy")
      game.Enemies.Add(new Enemy(game.X, game.Y) { Direction = 0 });

    for (int tick = 0; tick < 240 && !game.IsDying; tick++)
      game.Update(0, false);

    Assert.IsTrue(game.IsDying);
    Assert.IsFalse(game.IsExploding);
    return game;
  }
}
