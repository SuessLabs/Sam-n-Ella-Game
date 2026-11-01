// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class LevelTests
{
  [TestMethod]
  [DataRow("Background=Day", BackgroundTheme.Day)]
  [DataRow("Background=Night", BackgroundTheme.Night)]
  [DataRow(" background = night ", BackgroundTheme.Night)]
  public void BackgroundHeaderDoesNotChangeMapCoordinates(string header, BackgroundTheme expected)
  {
    var level = Level.Parse("header", [header, "", ".S........", ".........G", "##########"]);
    Assert.AreEqual(expected, level.Background);
    Assert.AreEqual(3, level.Height);
    Assert.AreEqual(10, level.Width);
    Assert.AreEqual((1, 0), level.Spawn);
    Assert.AreEqual('G', level.Tile(9, 1));
    Assert.IsTrue(level.Solid(0, 2));
  }

  [TestMethod]
  public void HeaderSeparatorIsOptionalAndOldMapsDefaultToDay()
  {
    var night = Level.Parse("night", ["Background=Night", ".S........", ".........G", "##########"]);
    Assert.AreEqual(BackgroundTheme.Night, night.Background);
    Assert.AreEqual(3, night.Height);
    var old = Level.Parse("old", [".S........", ".........G", "##########"]);
    Assert.AreEqual(BackgroundTheme.Day, old.Background);
  }

  [TestMethod]
  [DataRow("Background=Dusk")]
  [DataRow("Background=")]
  [DataRow("Background=1")]
  [DataRow("Theme=Night")]
  public void InvalidBackgroundHeadersExplainAllowedValues(string header)
  {
    var error = Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("bad-setting",
      [header, ".S........", ".........G", "##########"]));
    StringAssert.Contains(error.Message, "bad-setting");
    StringAssert.Contains(error.Message, "Background=Day or Background=Night");
  }

  [TestMethod]
  public void BundledLevelsExplicitlySelectTheirDayOrNightTheme()
  {
    string[] paths = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    for (int index = 0; index < paths.Length; index++)
    {
      bool night = index is 3 or 4 or 5 or 8 or 9 or 10 or 11;
      BackgroundTheme theme = index is >= 12 and <= 17 ? BackgroundTheme.Underworld : night ? BackgroundTheme.Night : BackgroundTheme.Day;
      Assert.AreEqual($"Background={theme}", File.ReadLines(paths[index]).First());
      Assert.AreEqual(theme, Level.Load(paths[index]).Background);
    }
  }

  [TestMethod]
  public void MissingSpawnIsRejected()
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("test", ["..........", ".........G", "##########"]));
  }

  [TestMethod]
  public void UnknownTileIsRejected()
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("test", ["S.........", "........@G", "##########"]));
  }

  [TestMethod]
  public void DuplicateSpawnIsRejected()
  {
    Assert.ThrowsExactly<InvalidDataException>(() => Level.Parse("test", ["SS........", ".........G", "##########"]));
  }

  [TestMethod]
  public void ShortRowsArePadded()
  {
    Assert.AreEqual('.', Level.Parse("ragged", ["S.........G", ".", "###########"]).Tile(5, 1));
  }

  [TestMethod]
  public void BundledLevelsAreValid()
  {
    var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt");
    Assert.IsNotEmpty(files);
    foreach (var file in files)
    {
      Assert.IsGreaterThan(0, Level.Load(file).Width, Path.GetFileName(file));
    }
  }
}
