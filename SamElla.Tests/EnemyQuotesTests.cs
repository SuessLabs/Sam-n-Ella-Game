using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class EnemyQuotesTests : GameTestBase
{
  [TestMethod]
  public void SharedFileHasOriginalSixAndFifteenAdditionalDistinctQuotes()
  {
    string[] quotes = ReadQuotes();
    Assert.HasCount(21, quotes);
    Assert.HasCount(21, quotes.Distinct().ToArray());
    CollectionAssert.AreEqual(new[] { "use toxic straws", "i'm confused", "i want free stuff", "i hate working", "responsibility is dumb", "Ok boomer" }, quotes.Take(6).ToArray());
    Assert.IsTrue(quotes.Skip(6).All(quote => quote.Length is > 0 and <= 80));
  }

  [TestMethod]
  [DataRow(EnemyKind.PunkBlue)]
  [DataRow(EnemyKind.PunkPurple)]
  [DataRow(EnemyKind.EmoBoss)]
  public void PunksAndBossCanSpeakAllTwentyOneSharedQuotes(EnemyKind kind)
  {
    var game = Map("................................", "................................", "................................", "................................",
      "................................", "................................", "................................", "...S..........................G.", "################################");
    var enemy = new Enemy(10, 8 - (kind == EnemyKind.EmoBoss ? Enemy.EmoBossBodyHeight : Game.LargeBodyHeight), kind)
      { Direction = 0, ProjectileAttackTicks = 100000 };
    game.Enemies.Add(enemy);
    foreach (string quote in ReadQuotes())
    {
      enemy.SpeechTicks = 1;
      Tick(game, 1);
      Assert.AreEqual(quote, game.EnemyQuote);
      Tick(game, 150);
    }
  }

  [TestMethod]
  public void LevelThreeOnePunksStartWithVariedSpeechWhenApproached()
  {
    string path = Path.Combine(AppContext.BaseDirectory, "Levels", "03-01-drainpipe-descent.txt");
    var game = new Game([path]);
    var punks = game.Enemies.Where(enemy => enemy.Kind is EnemyKind.PunkBlue or EnemyKind.PunkPurple).ToArray();
    Assert.IsTrue(punks.Length >= 2);
    Assert.IsTrue(punks.All(enemy => enemy.SpeechTicks == 1));
    Assert.IsTrue(punks.Select(enemy => enemy.SpeechIndex % 21).Distinct().Count() > 1);
  }

  private static string[] ReadQuotes() => File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Assets", "enemy-quotes.txt"))
    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith(';')).ToArray();
}
