// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Actors;

namespace SamElla.Tests;

[TestClass]
public sealed class TitleScreenTests
{
  [TestMethod]
  [DataRow(false)]
  [DataRow(true)]
  public void DoubleKonamiOpensLevelSelectOnlyAfterBothAInputs(bool mobileControls)
  {
    var cheat = new LevelSelectCheat();
    int b = mobileControls ? 0x1B : 0x42;
    int a = mobileControls ? 0x0D : 0x41;
    int[] keys = [0x26, 0x26, 0x26, 0x26, 0x28, 0x28, 0x28, 0x28,
      0x25, 0x25, 0x27, 0x27, 0x25, 0x25, 0x27, 0x27, b, b, a, a];
    for (int index = 0; index < keys.Length; index++)
    {
      Assert.AreEqual(index == keys.Length - 1, cheat.HandleInput([keys[index]], true, mobileControls));
      Assert.AreEqual(index == keys.Length - 2, cheat.ConsumesTitleConfirmation);
    }

    Assert.IsFalse(cheat.HandleInput([], true, mobileControls));
    Assert.IsFalse(cheat.ConsumesTitleConfirmation);
    Assert.IsTrue(cheat.HandleInput(keys, true, mobileControls), "The full sequence can reopen level select.");
  }

  [TestMethod]
  public void DoubleKonamiRejectsInterruptedInputAndSingleKonamiAndClearsOnFocusLoss()
  {
    var cheat = new LevelSelectCheat();
    int[] directions = [0x26, 0x26, 0x26, 0x26, 0x28, 0x28, 0x28, 0x28,
      0x25, 0x25, 0x27, 0x27, 0x25, 0x25, 0x27, 0x27];
    Assert.IsFalse(cheat.HandleInput([0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x42, 0x41], true));
    Assert.IsFalse(cheat.HandleInput(directions, true));
    Assert.IsFalse(cheat.HandleInput([], false));
    Assert.IsFalse(cheat.HandleInput([0x42, 0x42, 0x41, 0x41], true));
    Assert.IsFalse(cheat.HandleInput(directions, true));
    Assert.IsFalse(cheat.HandleInput([0x42, 0x43, 0x42, 0x41, 0x41], true));
    Assert.IsTrue(cheat.HandleInput(directions.Concat(new[] { 0x58, 0x58, 0x20, 0x20 }).ToArray(), true, mobileControls: true),
      "Gameplay Attack and Jump must also be recognized on mobile.");
  }

  [TestMethod]
  public void DeadbeefRequiresCorrectSequenceAndFocusAndRequestsOnlyOnce()
  {
    var cheat = new LevelSelectCheat();
    int[] code = "DEADBEEF".Select(letter => (int)letter).ToArray();
    Assert.IsFalse(cheat.HandleInput(code, false));

    Assert.IsFalse(cheat.HandleInput("DEADBEAF".Select(letter => (int)letter).ToArray(), true));

    Assert.IsTrue(cheat.HandleInput(code.Concat(new[] { 0x0D }).ToArray(), true));
    Assert.IsFalse(cheat.HandleInput([], true));

    var title = new TitleScreen();
    title.Update(0, true, true);
    Assert.IsFalse(title.IsActive);
    Assert.IsTrue(cheat.HandleInput(code, true), "The cheat must also work after leaving the title.");
    Assert.IsTrue(cheat.HandleInput(code, true), "The selector must be reopenable.");
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void LevelSelectAndKonamiCodesCanBothBeUsed(bool konamiFirst)
  {
    var title = new TitleScreen();
    var cheat = new LevelSelectCheat();
    int[] debug = "DEADBEEF".Select(letter => (int)letter).ToArray();
    int[] konami = [0x26, 0x26, 0x28, 0x28, 0x25, 0x27, 0x25, 0x27, 0x42, 0x41];
    title.HandleInput(0, konamiFirst ? konami : debug, true);
    bool requested = cheat.HandleInput(konamiFirst ? konami : debug, true);
    title.HandleInput(0, konamiFirst ? debug : konami, true);
    requested |= cheat.HandleInput(konamiFirst ? debug : konami, true);
    Assert.IsTrue(title.KonamiCodeEnabled);
    Assert.IsTrue(requested);
    Assert.IsTrue(title.IsActive);
  }

  [TestMethod]
  public void GlobalDebugCheatWorksAcrossFramesAndClearsPartialCodeOnLostFocus()
  {
    var cheat = new LevelSelectCheat();
    foreach (char letter in "DEAD")
      Assert.IsFalse(cheat.HandleInput([(int)letter], true));

    Assert.IsFalse(cheat.HandleInput([], false));
    foreach (char letter in "BEEF")
      Assert.IsFalse(cheat.HandleInput([(int)letter], true));

    string code = "DEADBEEF";
    for (int index = 0; index < code.Length; index++)
      Assert.AreEqual(index == code.Length - 1, cheat.HandleInput([(int)code[index]], true));

  }

  [TestMethod]
  public void PlayerSelectionRespondsToDirectionAndLocksAfterStarting()
  {
    var title = new TitleScreen();
    Assert.AreEqual(PlayerCharacter.Sam, title.SelectedCharacter);
    title.Update(0, false, true, 1);
    Assert.AreEqual(PlayerCharacter.Ella, title.SelectedCharacter);
    title.Update(0, false, false, -1);
    Assert.AreEqual(PlayerCharacter.Ella, title.SelectedCharacter);
    title.Update(0, false, true, -1);
    Assert.AreEqual(PlayerCharacter.Sam, title.SelectedCharacter);
    title.Update(0, true, true, 1);
    Assert.AreEqual(PlayerCharacter.Ella, title.SelectedCharacter);
    title.Update(0, false, true, -1);
    Assert.AreEqual(PlayerCharacter.Ella, title.SelectedCharacter);
  }

  [TestMethod]
  public void TitleWaitsForStartAndFreezesWhenUnfocused()
  {
    var title = new TitleScreen();
    Assert.IsTrue(title.IsActive);
    title.Update(1, false, true);
    Assert.AreEqual(1, title.Elapsed);
    title.Update(1, true, false);
    Assert.IsTrue(title.IsActive);
    Assert.AreEqual(1, title.Elapsed);
    title.Update(0, true, true);
    Assert.IsFalse(title.IsActive);
    title.Update(1, false, true);
    Assert.AreEqual(1, title.Elapsed);
  }

  [TestMethod]
  public void EnterStartsOnlyOnFreshKeypress()
  {
    var keyboard = new GameKeyboard();
    keyboard.KeyDown(0x0D);
    Assert.IsTrue(keyboard.Pressed(GameKey.Start));
    keyboard.BeginFrame();
    keyboard.KeyDown(0x0D, isRepeat: true);
    Assert.IsFalse(keyboard.Pressed(GameKey.Start));
    keyboard.KeyUp(0x0D);
    keyboard.KeyDown(0x0D);
    Assert.IsTrue(keyboard.Pressed(GameKey.Start));
  }

  [TestMethod]
  public void TitleAnimatesCharactersWhileKeepingCopyrightFixed()
  {
    var title = new TitleScreen();
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawTitle(title);
    uint[] first = (uint[])renderer.Frame.Pixels.Clone();
    title.Update(1, false, true);
    renderer.DrawTitle(title);
    int laneStart = 192 * PixelRenderer.Width;
    int footerStart = 226 * PixelRenderer.Width;
    Assert.IsFalse(first.Skip(laneStart).Take(footerStart - laneStart)
      .SequenceEqual(renderer.Frame.Pixels.Skip(laneStart).Take(footerStart - laneStart)));
    Assert.IsTrue(first.Skip(footerStart).SequenceEqual(renderer.Frame.Pixels.Skip(footerStart)));
    Assert.IsTrue(renderer.Frame.Pixels.Skip(footerStart).Any(pixel => pixel == 0xFFF6F4DF));
  }
}
