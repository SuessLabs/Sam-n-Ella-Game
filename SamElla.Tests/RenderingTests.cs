// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;
using SamElla.Effects;

namespace SamElla.Tests;

[TestClass]
public sealed class RenderingTests
{
  [TestMethod]
  public void BothSplatterSpritesRenderAndExpireIndependently()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    var combinedLevelPath = Path.Combine(Path.GetTempPath(), $"samella-both-splatters-{Guid.NewGuid():N}.txt");

    try
    {
      File.WriteAllLines(combinedLevelPath, [
        "....................",
        "....................",
        ".S..................",
        "...^^^^^^^^.........",
        ".E................G.",
        "####################"]);

      var combinedGame = new Game([combinedLevelPath]);
      combinedGame.Enemies[0].Direction = 0;

      for (int i = 0; i < 60 && combinedGame.Enemies[0].Alive; i++)
        combinedGame.Update(0, false);

      renderer.Draw(combinedGame, 0);
      Assert.IsTrue(
        renderer.Frame.Pixels[(107 * PixelRenderer.Width) + 13] == gamePalette['F'],
        "Enemy splatter sprite renders at the stomp location");

      for (int i = 0; i < 60 && !combinedGame.IsDying; i++)
        combinedGame.Update(1, true);

      renderer.Draw(combinedGame, 0);
      Assert.IsTrue(
        combinedGame.IsDying &&
        combinedGame.Enemies[0].SquashTicksRemaining > 0 &&
        renderer.Frame.Pixels[(107 * PixelRenderer.Width) + 13] == gamePalette['F'] &&
        renderer.Frame.Pixels.Contains(gamePalette['T']) &&
        renderer.Frame.Pixels.Contains(gamePalette['B']),
        "Enemy and player splatter sprites render together");

      int enemyTicks = combinedGame.Enemies[0].SquashTicksRemaining;

      for (int i = 0; i < enemyTicks; i++)
        combinedGame.Update(0, false);

      renderer.Draw(combinedGame, 0);
      Assert.IsTrue(
        combinedGame.IsDying &&
        renderer.Frame.Pixels[(107 * PixelRenderer.Width) + 13] != gamePalette['F'] &&
        renderer.Frame.Pixels.Contains(gamePalette['T']),
        "Expired enemy splatter is removed without removing player splatter");
    }

    finally
    {
      File.Delete(combinedLevelPath);
    }
  }

  [TestMethod]
  public void BrickSpritesRenderRubbleThenDustThenDisappear()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    var brickLevelPath = Path.Combine(Path.GetTempPath(), $"samella-brick-{Guid.NewGuid():N}.txt");

    try
    {
      File.WriteAllLines(brickLevelPath, [
        "....................",
        "....................",
        ".B..................",
        "....................",
        ".S................G.",
        "####################"]);

      var brickGame = new Game([brickLevelPath]);

      for (int i = 0; i < 10; i++)
        brickGame.Update(0, false);

      brickGame.QueueJump();

      for (int i = 0; i < 30 && brickGame.BlockBreakEffects.Count == 0; i++)
        brickGame.Update(0, true);

      renderer.Draw(brickGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels[(61 * PixelRenderer.Width) + 17] == gamePalette['K'],
        "Broken brick renders the editable rubble sprite");

      for (int i = 0; i < BlockBreakEffect.FrameDurationTicks; i++)
        brickGame.Update(0, false);

      renderer.Draw(brickGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels[(61 * PixelRenderer.Width) + 17] != gamePalette['K']
        && renderer.Frame.Pixels[(61 * PixelRenderer.Width) + 18] == gamePalette['O'],
        "Brick animation replaces rubble with the editable dust sprite");

      for (int i = 0; i < BlockBreakEffect.FrameDurationTicks; i++)
        brickGame.Update(0, false);

      renderer.Draw(brickGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels[(61 * PixelRenderer.Width) + 18] != gamePalette['O'],
        "Finished brick animation leaves no rubble or dust on screen");
    }

    finally
    {
      File.Delete(brickLevelPath);
    }
  }

  [TestMethod]
  public void BundledSpritesHaveExpectedDimensions()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));

    Assert.IsNotEmpty(Directory.GetFiles(assets, "*.sprite"));

    foreach (string file in Directory.GetFiles(assets, "*.sprite"))
    {
      var asset = Sprite.Load(file, gamePalette);
      bool validSize = Path.GetFileName(file).Replace("ella-", "player-", StringComparison.Ordinal) switch
      {
        "dung-bag-walk-1.sprite" or "dung-bag-walk-2.sprite" or "dung-bag-squashed.sprite"
          => asset.Width == 20 && asset.Height == 32,
        string name when name.StartsWith("old-man-", StringComparison.Ordinal)
          => asset.Width == 32 && asset.Height == 32,
        string name when name.StartsWith("emo-boss-", StringComparison.Ordinal)
          => asset.Width == 32 && asset.Height == 48,
        string name when name.StartsWith("punk-", StringComparison.Ordinal)
          => asset.Width == 24 && asset.Height == 32,
        string name when name.StartsWith("worm-", StringComparison.Ordinal)
          => asset.Width == 24 && asset.Height == 16,
        "toilet.sprite" or "player-toilet-sit.sprite" or "ella-toilet-sit.sprite"
          => asset.Width == 32 && asset.Height == 32,
        "moon.sprite" => asset.Width == 32 && asset.Height == 32,
        "tombstone.sprite" or "tombstone-cross.sprite" => asset.Width == 16 && asset.Height == 24,
        "player-burning-1.sprite" or "player-burning-2.sprite" or "player-burning-3.sprite" or "player-burning-4.sprite"
          => asset.Width == 24 && asset.Height == 40,
        "player-splatter.sprite" => asset.Width == 24 && asset.Height == 16,
        "enemy-squashed.sprite" => asset.Width == 24 && asset.Height == 16,
        "toaster-cat-run-1.sprite" or "toaster-cat-run-2.sprite" or "toaster-cat-jump.sprite"
          => asset.Width == 24 && asset.Height == 16,
        "toaster-cat-squashed.sprite" => asset.Width == 32 && asset.Height == 16,
        "zombie-walk-1.sprite" or "zombie-walk-2.sprite" => asset.Width == 32 && asset.Height == 32,
        "zombie-squashed.sprite" => asset.Width == 32 && asset.Height == 16,
        string name when name.StartsWith("creepy-crawler-", StringComparison.Ordinal)
          => asset.Width == 32 && asset.Height == 16,
        "dog-walk-1.sprite" or "dog-walk-2.sprite" or "dog-squashed.sprite" => asset.Width == 24 && asset.Height == 16,
        "poop-screen-splatter.sprite" => asset.Width == 128 && asset.Height == 96,
        "reward-plus-one.sprite" => true,
        "player-explosion-1.sprite" or "player-explosion-2.sprite" or "player-explosion-3.sprite" or "player-explosion-4.sprite"
          => asset.Width == 64 && asset.Height == 64,
        "player-large-idle.sprite" or "player-large-run-1.sprite" or "player-large-run-2.sprite" or "player-large-jump.sprite"
          => asset.Width == 16 && asset.Height == 32,
        _ => asset.Width == 16 && asset.Height == 16,
      };

      Assert.IsTrue(validSize, $"Bundled pixel art validates: {Path.GetFileName(file)}");
    }
  }

  [TestMethod]
  public void EnemyBloodRendersForOneSecond()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    var stompLevelPath = Path.Combine(Path.GetTempPath(), $"samella-stomp-{Guid.NewGuid():N}.txt");

    try
    {
      File.WriteAllLines(stompLevelPath, [
        "....................",
        "....................",
        ".S..................",
        "....................",
        ".E................G.",
        "####################"]);

      var stompGame = new Game([stompLevelPath]);
      stompGame.Enemies[0].Direction = 0;

      for (int i = 0; i < 60 && stompGame.Enemies[0].Alive; i++)
        stompGame.Update(0, false);

      renderer.Draw(stompGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels[(107 * PixelRenderer.Width) + 13] == gamePalette['F'],
        "Stomp renders the blood splatter at the enemy's feet");

      for (int i = 0; i < Enemy.SquashDurationTicks; i++)
        stompGame.Update(0, false);

      renderer.Draw(stompGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels[(107 * PixelRenderer.Width) + 13] != gamePalette['F'],
        "Expired blood splatter disappears from the rendered scene");
    }

    finally
    {
      File.Delete(stompLevelPath);
    }
  }

  [TestMethod]
  public void InvalidSpritesAreRejected()
  {
    var palette = new Dictionary<char, uint> { ['.'] = 0, ['A'] = 0xFFFF0000, ['B'] = 0xFF00FF00 };
    bool Rejects(string[] lines)
    {
      try
      {
        Sprite.Parse("bad", lines, palette);
        return false;
      }

      catch (InvalidDataException)
      {
        return true;
      }
    }

    Assert.IsTrue(Rejects(["2 2", "A."]), "Missing sprite rows rejected");
    Assert.IsTrue(Rejects(["2 2", "A", ".B"]), "Unequal sprite row widths rejected");
    Assert.IsTrue(Rejects(["2 2", "AZ", ".B"]), "Undefined sprite colors rejected");
    Assert.IsTrue(Rejects(["99999 99999"]), "Oversized sprite dimensions rejected");
  }

  [TestMethod]
  public void PlayerBloodAndClothingRenderOnSpikeAndPitDeaths()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    var deathLevelPath = Path.Combine(Path.GetTempPath(), $"samella-death-{Guid.NewGuid():N}.txt");

    try
    {
      File.WriteAllLines(deathLevelPath, [
        "....................",
        "...S................",
        "....................",
        "....................",
        "...^..............G.",
        "####################"]);

      var deathGame = new Game([deathLevelPath]);

      for (int i = 0; i < 60 && !deathGame.IsDying; i++)
        deathGame.Update(0, false);

      renderer.Draw(deathGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels.Contains(gamePalette['E']) && renderer.Frame.Pixels.Contains(gamePalette['F']),
        "Dead player renders bright and dark blood from the editable splatter sprite");

      Assert.IsTrue(renderer.Frame.Pixels.Contains(gamePalette['T']) && renderer.Frame.Pixels.Contains(gamePalette['B']),
        "Player splatter retains teal and blue clothing colors");

      for (int i = 0; i < Game.DeathDurationTicks; i++)
        deathGame.Update(0, false);

      Assert.IsTrue(deathGame.ContinueAfterDeath());
      renderer.Draw(deathGame, 0);

      Assert.IsTrue(!renderer.Frame.Pixels.Contains(gamePalette['E']) && !renderer.Frame.Pixels.Contains(gamePalette['F']),
        "Player splatter disappears when the level respawns");

      File.WriteAllLines(deathLevelPath, [
        "....................",
        "....................",
        "....................",
        ".S................G.",
        "...................."]);

      deathGame = new Game([deathLevelPath]);

      for (int life = 0; life < 3; life++)
      {
        for (int i = 0; i < 120 && !deathGame.IsDying; i++)
          deathGame.Update(0, false);

        renderer.Draw(deathGame, 0);
        Assert.IsTrue(renderer.Frame.Pixels.Contains(gamePalette['E']),
          "Pit death splatter stays visible, including on the last life");

        for (int i = 0; i < Game.DeathDurationTicks; i++)
          deathGame.Update(0, false);
        Assert.IsTrue(deathGame.ContinueAfterDeath());
      }
    }

    finally
    {
      File.Delete(deathLevelPath);
    }
  }

  [TestMethod]
  public void RewardSpriteRendersForOneSecond()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    var rewardLevelPath = Path.Combine(Path.GetTempPath(), $"samella-reward-{Guid.NewGuid():N}.txt");

    try
    {
      File.WriteAllLines(rewardLevelPath, [
        "....................",
        "....................",
        ".?..................",
        "....................",
        ".S................G.",
        "####################"]);

      var rewardGame = new Game([rewardLevelPath]);

      for (int i = 0; i < 10; i++)
        rewardGame.Update(0, false);

      rewardGame.QueueJump();

      for (int i = 0; i < 30 && rewardGame.RewardPopups.Count == 0; i++)
        rewardGame.Update(0, true);

      renderer.Draw(rewardGame, 0);
      Assert.IsTrue(renderer.Frame.Pixels[(53 * PixelRenderer.Width) + 21] == gamePalette['Y']
        && renderer.Frame.Pixels[(52 * PixelRenderer.Width) + 25] == gamePalette['Y'],
        "Coin reward renders the editable sprite above the rewarded block");

      for (int i = 0; i < 60; i++)
        rewardGame.Update(0, false);

      renderer.Draw(rewardGame, 0);
      Assert.IsTrue(
        renderer.Frame.Pixels[(53 * PixelRenderer.Width) + 21] != gamePalette['Y'] &&
        renderer.Frame.Pixels[(52 * PixelRenderer.Width) + 25] != gamePalette['Y'],
        "Expired reward text is removed from the rendered scene");
    }

    finally
    {
      File.Delete(rewardLevelPath);
    }
  }

  [TestMethod]
  public void SceneAndPauseOverlayRenderAndAnimationFreezes()
  {
    var assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var gamePalette = Sprite.LoadPalette(Path.Combine(assets, "palette.txt"));
    var renderer = new PixelRenderer(assets);
    var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Levels"), "*.txt").Order().ToArray();
    var game = new Game(files);

    renderer.Draw(game, 0);
    Assert.IsTrue(renderer.Frame.Pixels.Distinct().Count() > 16, "Complete scene renders with sprites and background");

    uint[] beforePause = (uint[])renderer.Frame.Pixels.Clone();

    game.Paused = true;
    renderer.Draw(game, 0);
    Assert.IsTrue(!renderer.Frame.Pixels.SequenceEqual(beforePause), "Pause overlay renders over the game");

    uint[] paused = (uint[])renderer.Frame.Pixels.Clone();

    renderer.Draw(game, 0, 1);
    Assert.IsTrue(renderer.Frame.Pixels.SequenceEqual(paused), "Animation freezes while paused");
  }

  [TestMethod]
  public void SpritesDrawFlipAndClipWithTransparency()
  {
    var palette = new Dictionary<char, uint> { ['.'] = 0, ['A'] = 0xFFFF0000, ['B'] = 0xFF00FF00 };
    var sprite = Sprite.Parse("test", ["2 2", "A.", ".B"], palette);
    var frame = new PixelBuffer(3, 3);

    frame.Clear(0xFF0000FF);
    frame.Draw(sprite, 0, 0);
    Assert.IsTrue(frame.Pixels[0] == palette['A'] && frame.Pixels[4] == palette['B'], "Sprite colors reach correct pixels");
    Assert.IsTrue(frame.Pixels[1] == 0xFF0000FF, "Transparent pixels preserve the background");

    frame.Clear(0);
    frame.Draw(sprite, 0, 0, true);
    Assert.IsTrue(frame.Pixels[1] == palette['A'] && frame.Pixels[3] == palette['B'], "Sprite can face the opposite direction");

    frame.Clear(0);
    frame.Draw(sprite, -1, -1);
    Assert.IsTrue(frame.Pixels[0] == palette['B'] && frame.Pixels.Count(pixel => pixel != 0) == 1, "Sprites clip at top and left edges");

    frame.Clear(0);
    frame.Draw(sprite, 2, 2);
    Assert.IsTrue(frame.Pixels[8] == palette['A'], "Sprites clip at bottom and right edges");

    frame.Fill(-3, -3, 4, 4, 0xFF112233);
    Assert.IsTrue(frame.Pixels[0] == 0xFF112233, "Filled rectangles clip to the framebuffer");
    frame.Fill(100, 100, 5, 5, 0);
  }
}
