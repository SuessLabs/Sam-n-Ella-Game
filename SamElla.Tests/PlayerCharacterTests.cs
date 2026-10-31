using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class PlayerCharacterTests : GameTestBase
{
  [TestMethod]
  public void EllaRemainsSelectedAfterDeathAndRestart()
  {
    var game = CreateElla();
    game.Enemies.Add(new Enemy(game.X, game.Y) { Direction = 0 });
    game.Update(0, false);
    Assert.IsTrue(game.IsDying);
    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(PlayerCharacter.Ella, game.Character);
    game.Restart();
    Assert.AreEqual(PlayerCharacter.Ella, game.Character);
  }

  [TestMethod]
  public void EllaUsesPurpleSpritesForSmallLargeAndDeathStates()
  {
    var game = CreateElla();
    AssertFrame(game, "ella-idle");
    game.QueueJump();
    Tick(game, 30, jump: true);
    Assert.HasCount(1, game.GrowthHearts);
    TouchHeart(game);
    Tick(game, 60);
    Assert.IsTrue(game.IsLarge);
    AssertFrame(game, "ella-large-idle");

    // One hit shrinks her; the next hit after protection expires causes death.
    game.Enemies.Add(new Enemy(game.X, game.Y) { Direction = 0 });
    game.Update(0, false);
    game.Enemies.Clear();
    Tick(game, Game.DamageProtectionTicks);
    game.Enemies.Add(new Enemy(game.X, game.Y) { Direction = 0 });
    game.Update(0, false);
    Assert.IsTrue(game.IsDying);
    AssertFrame(game, "ella-splatter");
  }

  private Game CreateElla()
  {
    Map(
      "........................", "........................", "........................",
      "........................", "........................", "........................",
      "........................", ".M......................", "........................",
      ".S....................G.", "########################");
    var game = new Game([LevelPath]) { Character = PlayerCharacter.Ella };
    Tick(game, 10);
    return game;
  }

  private static void AssertFrame(Game game, string name)
  {
    string assets = Path.Combine(AppContext.BaseDirectory, "Assets");
    var sprite = Sprite.Load(Path.Combine(assets, name + ".sprite"), Sprite.LoadPalette(Path.Combine(assets, "palette.txt")));
    var renderer = new PixelRenderer(assets);
    renderer.Draw(game, 0, 0);
    int left = (int)Math.Round((game.X + (Game.BodyWidth / 2)) * 16) - (sprite.Width / 2);
    int top = 28 + (int)Math.Round((game.Y + game.PlayerHeight) * 16) - sprite.Height;
    for (int y = 0; y < sprite.Height; y++)
      for (int x = 0; x < sprite.Width; x++)
        if (sprite.Pixel(x, y) != 0 && left + x >= 0)
          Assert.AreEqual(sprite.Pixel(x, y), renderer.Frame.Pixels[((top + y) * PixelRenderer.Width) + left + x], name);
  }
}
