// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class PipeRenderingTests : GameTestBase
{
  [TestMethod]
  [DataRow("PP/PP", 5, 2, "top-left,top-right/bottom-left,bottom-right")]
  [DataRow("PP/PP", 0, 0, "top-left,top-right/bottom-left,bottom-right")]
  [DataRow("PP/PP/PP", 5, 2, "top-left,top-right/body-left,body-right/bottom-left,bottom-right")]
  [DataRow("PP/PP/PP/PP", 5, 1, "top-left,top-right/body-left,body-right/body-left,body-right/bottom-left,bottom-right")]
  [DataRow("PP/PP/PP", 18, 0, "top-left,top-right/body-left,body-right/bottom-left,bottom-right")]
  [DataRow("P/P", 5, 2, "small-top/small-bottom")]
  [DataRow("P/P/P", 5, 2, "small-top/small-body/small-bottom")]
  [DataRow("P/P/P/P/P", 0, 0, "small-top/small-body/small-body/small-body/small-bottom")]
  [DataRow("P", 5, 2, "standard")]
  [DataRow("PP", 5, 2, "standard")]
  [DataRow("PPP/PPP", 5, 2, "standard")]
  [DataRow("PP/P.", 5, 2, "standard")]
  [DataRow("P./PP", 5, 2, "standard")]
  [DataRow("PP/PPP/PP", 5, 2, "standard")]
  [DataRow("PP/../PP", 5, 2, "standard")]
  [DataRow("p", 5, 2, "standard")]
  [DataRow("p/p/p", 5, 2, "small-top/small-body/small-bottom")]
  [DataRow(".PP/.PP/pPP", 5, 2, ".,top-left,top-right/.,body-left,body-right/standard,bottom-left,bottom-right")]
  [DataRow("PP./PP./PPp", 5, 2, "top-left,top-right,./body-left,body-right,./bottom-left,bottom-right,standard")]
  [DataRow(".PP/.PP/pPP/pPP", 5, 1, ".,top-left,top-right/.,body-left,body-right/small-top,body-left,body-right/small-bottom,bottom-left,bottom-right")]
  [DataRow("pPPp/pPPp", 5, 2, "small-top,top-left,top-right,small-top/small-bottom,bottom-left,bottom-right,small-bottom")]
  public void PipeGroupsRenderMatchingArtwork(string layout, int left, int top, string expectedLayout)
  {
    string[] rows =
    [
      "....................",
      "....................",
      "....................",
      "....................",
      "....................",
      ".S................G.",
      "####################",
    ];
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var expected = new PixelRenderer(assets);
    expected.Draw(Map(rows), 0, 0);

    string[] pieces = layout.Split('/');
    string[][] expectedPieces = expectedLayout.Split('/').Select(row => row.Split(',')).ToArray();

    for (int y = 0; y < pieces.Length; y++)
    {
      char[] row = rows[top + y].ToCharArray();

      for (int x = 0; x < pieces[y].Length; x++)
      {
        if (pieces[y][x] is not ('P' or 'p'))
          continue;

        row[left + x] = pieces[y][x];
        string piece = expectedLayout == "standard" ? "standard" : expectedPieces[y][x];
        string name = piece == "standard" ? "pipe.sprite" : $"pipe-{piece}.sprite";
        var sprite = Sprite.Load(Path.Combine(assets, name), palette);
        expected.Frame.Draw(sprite, (left + x) * PixelRenderer.TileSize, 28 + ((top + y) * PixelRenderer.TileSize));
      }

      rows[top + y] = new string(row);
    }

    var actual = new PixelRenderer(assets);
    actual.Draw(Map(rows), 0, 0);

    CollectionAssert.AreEqual(expected.Frame.Pixels, actual.Frame.Pixels,
      "Rectangular stacks should have one top rim, continuous body pieces, and a single bottom; irregular groups retain the standard sprite.");
  }

  [TestMethod]
  public void SkinnyPipeProvidesStepToJumpOverThreeTileHighWidePipe()
  {
    var game = Map(
      "....................",
      "....................",
      "....................",
      ".....PP.............",
      ".....PP.............",
      ".S..pPP...........G.",
      "####################");

    Tick(game, 10);
    game.QueueJump();
    Tick(game, 70, 1, true);

    Assert.IsTrue(game.Grounded, "The first jump should land on the skinny step.");
    Assert.AreEqual(5, game.Y + Game.BodyHeight, 0.00001);
    Assert.AreEqual(5, game.X + Game.BodyWidth, 0.00001,
      "The wide pipe should block forward movement until the player jumps again.");
    Assert.IsTrue(game.Level.Solid(4, 5), "The skinny pipe must be solid.");

    game.QueueJump();
    Tick(game, 60, 1, true);

    Assert.IsGreaterThan(7, game.X, "The second jump should cross the wide pipe.");
    Assert.AreEqual(3, game.Lives);
  }
}
