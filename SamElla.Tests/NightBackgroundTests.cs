// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SamElla.Tests;

[TestClass]
public sealed class NightBackgroundTests : GameTestBase
{
  [TestMethod]
  [DataRow(1, false)]
  [DataRow(2, false)]
  [DataRow(3, false)]
  [DataRow(4, true)]
  [DataRow(5, true)]
  [DataRow(6, false)]
  [DataRow(7, false)]
  [DataRow(8, true)]
  [DataRow(9, true)]
  public void NightLevelsHaveNightSkyMoonAndTombstones(int levelNumber, bool night)
  {
    var game = ReachLevel(levelNumber);
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    Assert.AreEqual(night ? 0xFF101B35 : 0xFF608CC8, renderer.Frame.Pixels[30 * PixelRenderer.Width]);
    var palette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var moon = Sprite.Load(Path.Combine(assets, "moon.sprite"), palette);
    uint moonPixel = renderer.Frame.Pixels[(43 * PixelRenderer.Width) + 311];
    if (night)
    {
      Assert.AreEqual(moon.Pixel(16, 5), moonPixel);
      Assert.AreEqual(palette['C'], renderer.Frame.Pixels[(89 * PixelRenderer.Width) + 22]);
    }
    else
    {
      Assert.AreNotEqual(moon.Pixel(16, 5), moonPixel);
      Assert.AreNotEqual(palette['C'], renderer.Frame.Pixels[(89 * PixelRenderer.Width) + 22]);
    }
  }

  [TestMethod]
  public void TombstonesScrollPastWithoutCollidingWithPlayer()
  {
    var game = ReachLevel(4);
    Tick(game, 37);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    uint[] before = renderer.Frame.Pixels.Skip(84 * PixelRenderer.Width).Take(8 * PixelRenderer.Width).ToArray();
    double x = game.X;

    Tick(game, 70, 1);
    Assert.AreEqual(x + 14, game.X, 0.00001);
    Assert.AreEqual(3, game.Lives);
    Assert.IsFalse(game.IsDying);

    renderer.Draw(game, 1, 0);
    Assert.IsFalse(before.SequenceEqual(renderer.Frame.Pixels.Skip(84 * PixelRenderer.Width).Take(8 * PixelRenderer.Width)));
  }

  [TestMethod]
  [DataRow(1, "Night", 0xFF101B35u)]
  [DataRow(4, "Day", 0xFF608CC8u)]
  public void BackgroundFollowsHeaderInsteadOfCampaignPosition(int target, string background, uint expectedSky)
  {
    Map($"Background={background}", "", new string('.', 64), new string('.', 64), new string('.', 64), new string('.', 64),
      ".S" + new string('.', 60) + "G.", new string('#', 64));

    var game = new Game(Enumerable.Repeat(LevelPath, 7).ToArray());

    for (int tick = 0; tick < 4000 && game.LevelNumber < target; tick++)
      game.Update(1, false);

    Assert.AreEqual(target, game.LevelNumber);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.Draw(game, 0, 0);
    Assert.AreEqual(expectedSky, renderer.Frame.Pixels[30 * PixelRenderer.Width]);
  }

  [TestMethod]
  public void TitleScreenKeepsDaytimeScenery()
  {
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawTitle(new TitleScreen());
    Assert.AreNotEqual(0xFF101B35, renderer.Frame.Pixels[30 * PixelRenderer.Width]);
  }

  private Game ReachLevel(int target)
  {
    Map(new string('.', 64), new string('.', 64), new string('.', 64), new string('.', 64),
      ".S" + new string('.', 60) + "G.", new string('#', 64));

    File.WriteAllLines(NextPath, new[] { "Background=Night", "" }.Concat(File.ReadAllLines(LevelPath)));
    var game = new Game(Enumerable.Range(1, 9).Select(number => number is 4 or 5 or 8 or 9 ? NextPath : LevelPath).ToArray());
    for (int tick = 0; tick < 4000 && game.LevelNumber < target; tick++)
      game.Update(1, false);

    Assert.AreEqual(target, game.LevelNumber);
    return game;
  }
}
