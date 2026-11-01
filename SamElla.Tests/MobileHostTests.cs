// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;
using SamElla.Mobile;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class MobileHostTests : GameTestBase
{
  [TestMethod]
  public void TouchSupportsMoveJumpAndAttackTogetherAndReleasesEachFingerIndependently()
  {
    using var input = new TouchInput();
    input.SetPointer(10, 0x27);
    input.SetPointer(20, 0x20);
    input.SetPointer(30, 0x58);
    Assert.IsTrue(input.Down(GameKey.Right) && input.Down(GameKey.Jump) && input.Down(GameKey.Fire));
    Assert.IsTrue(input.Pressed(GameKey.Jump));
    input.PumpEvents();
    Assert.IsFalse(input.Pressed(GameKey.Jump));
    Assert.IsTrue(input.Down(GameKey.Jump));
    input.SetPointer(20, null);
    Assert.IsFalse(input.Down(GameKey.Jump));
    Assert.IsTrue(input.Down(GameKey.Right) && input.Down(GameKey.Fire));
    input.Clear();
    Assert.IsFalse(input.Down(GameKey.Right) || input.Down(GameKey.Fire) || input.AnyKeyPressed);
  }

  [TestMethod]
  public void HeldStartCannotRepeatOrBecomeAMenuConfirmationAfterChangingScreens()
  {
    using var input = new TouchInput();
    input.SetPointer(1, GameKeyboard.PauseKey);
    Assert.IsTrue(input.Pressed(GameKey.Start));
    Assert.IsFalse(input.KeyPresses.Contains(0x0D));
    input.PumpEvents();
    input.SuppressUntilRelease();
    for (int tick = 0; tick < 120; tick++)
    {
      input.MovePointer(1, tick % 3 == 0 ? null : GameKeyboard.PauseKey);
      Assert.IsFalse(input.Pressed(GameKey.Start));
      Assert.IsFalse(input.AnyKeyPressed);
      input.PumpEvents();
    }

    input.MovePointer(1, 0x0D);
    Assert.IsFalse(input.AnyKeyPressed, "The held finger must not accept a selection on the new screen.");
    input.SetPointer(1, null);
    input.SetPointer(1, GameKeyboard.PauseKey);
    Assert.IsTrue(input.Pressed(GameKey.Start));
    Assert.IsFalse(input.KeyPresses.Contains(0x0D));
  }

  [TestMethod]
  public void ContextChangeRequiresJumpReleaseBeforeAcceptAndIgnoresOldMovesAfterSuspension()
  {
    using var input = new TouchInput();
    input.SetPointer(1, 0x20);
    input.SuppressUntilRelease();
    input.MovePointer(1, 0x0D);
    Assert.IsFalse(input.AnyKeyPressed);
    input.SetPointer(1, null);
    input.SetPointer(1, 0x0D);
    Assert.IsTrue(input.KeyPresses.Contains(0x0D));
    input.Clear();
    input.MovePointer(1, 0x0D);
    Assert.IsFalse(input.AnyKeyPressed);
  }

  [TestMethod]
  public void SlidingBetweenButtonsAndTwoFingersOnSameButtonCannotLeaveStuckInput()
  {
    using var input = new TouchInput();
    input.SetPointer(1, 0x25);
    input.SetPointer(2, 0x25);
    input.SetPointer(1, null);
    Assert.IsTrue(input.Down(GameKey.Left));
    input.SetPointer(2, 0x27);
    Assert.IsFalse(input.Down(GameKey.Left));
    Assert.IsTrue(input.Down(GameKey.Right));
    input.SetPointer(2, null);
    Assert.IsFalse(input.Down(GameKey.Right));
  }

  [TestMethod]
  [DataRow(1920, 1080, 3.0)]
  [DataRow(2400, 1080, 3.0)]
  [DataRow(2340, 1080, 3.0)]
  [DataRow(2560, 1440, 4.0)]
  [DataRow(1280, 800, 2.0)]
  [DataRow(640, 360, 1.0)]
  public void PhoneAndTabletLayoutsMaximizeGameAreaAndOverlaySeparateTouchTargets(int width, int height, double density)
  {
    foreach (bool menu in new[] { false, true })
    {
      var layout = new MobileLayout(width, height, density, menu);
      var view = layout.Viewport;
      Assert.AreEqual(1.6, view.Width / view.Height, 0.00001);
      Assert.IsTrue(Math.Abs(view.Width - width) < 0.00001 || Math.Abs(view.Height - height) < 0.00001,
        "The game must reach a pair of screen edges instead of reserving space for controls or rounding the scale down.");
      Assert.IsTrue(view.X >= 0 && view.Y >= 0 && view.X + view.Width <= width && view.Y + view.Height <= height);
      foreach (var button in layout.Buttons)
      {
        Assert.IsTrue(button.X >= 0 && button.Y >= 0);
        Assert.IsTrue(button.X + button.Width <= width && button.Y + button.Height <= height);
        Assert.AreEqual(button.Key, layout.KeyAt(button.X + button.Width / 2, button.Y + button.Height / 2));
        Assert.AreEqual(1, layout.Buttons.Count(other => other.Contains(button.X + button.Width / 2, button.Y + button.Height / 2)));
      }
    }
  }

  [TestMethod]
  public void FullHdPhoneUsesFullHeightWithFractionalScaling()
  {
    var layout = new MobileLayout(1920, 1080, 3, menu: false);
    Assert.AreEqual(new GameViewport(96, 0, 1728, 1080), layout.Viewport);
    Assert.IsTrue(layout.Buttons.Any(button => button.Contains(button.X + button.Width / 2, 1000)),
      "Controls must remain accessible over the enlarged game view.");
    Assert.IsTrue(layout.Buttons.Any(button => button.X < layout.Viewport.X + layout.Viewport.Width
      && button.X + button.Width > layout.Viewport.X && button.Y < layout.Viewport.Height));
  }

  [TestMethod]
  [DataRow(false)]
  [DataRow(true)]
  public void ControllerAlwaysShowsArrowCrossCenterButtonsAndNumberedActions(bool menu)
  {
    var layout = new MobileLayout(1920, 1080, 3, menu);
    Assert.HasCount(8, layout.Buttons);
    var up = layout.Buttons.Single(button => button.Label == "↑");
    var down = layout.Buttons.Single(button => button.Label == "↓");
    var left = layout.Buttons.Single(button => button.Label == "←");
    var right = layout.Buttons.Single(button => button.Label == "→");
    Assert.IsTrue(up.Y + up.Height < left.Y && down.Y > left.Y + left.Height);
    Assert.AreEqual(left.Y, right.Y);
    Assert.AreEqual(up.X, down.X);
    Assert.IsTrue(left.X < up.X && right.X > up.X);
    Assert.AreEqual((int?)0x26, up.Key);
    Assert.AreEqual((int?)0x28, down.Key);
    var start = layout.Buttons.Single(button => button.Label == "START");
    var select = layout.Buttons.Single(button => button.Label == "SELECT");
    Assert.AreEqual(menu ? null : (int?)GameKeyboard.PauseKey, start.Key);
    Assert.AreEqual(menu ? (int?)0x28 : null, select.Key);
    Assert.AreEqual(start.Y, select.Y);
    Assert.IsTrue(select.X < 960 && start.X > 960);
    var attack = layout.Buttons.Single(button => button.Label == "ATTACK");
    var jump = layout.Buttons.Single(button => button.Label == "JUMP");
    Assert.AreEqual("I", attack.Heading);
    Assert.AreEqual("○", jump.Heading);
    Assert.AreEqual(menu ? (int?)0x1B : 0x58, attack.Key);
    Assert.AreEqual(menu ? (int?)0x0D : 0x20, jump.Key);
    Assert.AreEqual(attack.Y, jump.Y);
    Assert.IsTrue(attack.X > start.X + start.Width && jump.X > attack.X + attack.Width);
    Assert.AreEqual(down.Y + down.Height, jump.Y + jump.Height);
  }

  [TestMethod]
  public void StartPausesAndSelectCyclesMenusWithoutTriggeringAPipeInGameplay()
  {
    using var input = new TouchInput();
    var gameplay = new MobileLayout(1920, 1080, 3, menu: false);
    var select = gameplay.Buttons.Single(button => button.Label == "SELECT");
    input.SetPointer(1, gameplay.KeyAt(select.X + 1, select.Y + 1));
    Assert.IsFalse(input.AnyKeyPressed);
    Assert.IsFalse(input.Down(GameKey.Down));
    var start = gameplay.Buttons.Single(button => button.Label == "START");
    input.SetPointer(2, start.Key);
    Assert.IsTrue(input.Pressed(GameKey.Start));
    input.Clear();
    var menu = new MobileLayout(1920, 1080, 3, menu: true);
    var title = new TitleScreen();
    select = menu.Buttons.Single(button => button.Label == "SELECT");
    input.SetPointer(1, select.Key);
    title.HandleInput(0, input.KeyPresses, true);
    Assert.AreEqual(1, title.SelectedMenuIndex);
    input.Clear();
    input.SetPointer(1, select.Key);
    title.HandleInput(0, input.KeyPresses, true);
    Assert.AreEqual(2, title.SelectedMenuIndex);
  }

  [TestMethod]
  public void MobileSettingsHaveAudioAndBackWithoutDesktopUpdateControls()
  {
    var settings = new SettingsScreen(new AudioSettings(), updatesSupported: false);
    settings.Open();
    settings.HandleInput([0x26], true);
    Assert.AreEqual(4, settings.SelectedIndex);
    settings.HandleInput([0x0D], true);
    Assert.IsFalse(settings.IsActive);
    Assert.IsFalse(settings.Updates.IncludePrereleases);
  }

  [TestMethod]
  public void SharedSessionStartsMovesJumpsAndPausesWithoutRunningDuringSuspension()
  {
    Map("................................", "................................", "................................", "................................",
      "................................", "................................", "................................", "...S..........................G.", "################################");
    string directory = Path.Combine(Path.GetTempPath(), "samella-mobile-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    string level = Path.Combine(directory, "01-01-test.txt");
    File.Copy(LevelPath, level);
    try
    {
      using var audio = new FakeAudio();
      using var input = new TouchInput();
      using var session = new GameSession(Path.Combine(AppContext.BaseDirectory, "Assets"), directory, NextPath, audio, desktopUpdates: false);
      Press(session, input, 0x1B);
      Assert.IsFalse(session.ExitRequested, "Cancel at the root mobile menu must not quit the application.");
      Press(session, input, 0x0D);
      Press(session, input, 0x0D);
      session.Tick(input, Game.Step);
      Assert.IsFalse(session.MenuControls);
      double x = session.Game.X;
      input.SetPointer(1, 0x27);
      for (int tick = 0; tick < 6; tick++)
        session.Tick(input, Game.Step);

      Assert.IsTrue(session.Game.X > x);
      input.Clear();
      input.SetPointer(1, 0x20);
      session.Tick(input, Game.Step);
      Assert.IsTrue(session.Game.VelocityY < 0);
      input.Clear();
      session.Suspend();
      input.Active = false;
      int clock = session.Game.LevelTicksRemaining;
      session.Tick(input, 30);
      Assert.AreEqual(clock, session.Game.LevelTicksRemaining);
      Assert.IsTrue(session.Game.Paused);
      Assert.IsTrue(audio.Paused);
      input.Active = true;
      Press(session, input, 0x0D);
      Assert.IsFalse(session.Game.Paused);
      Assert.AreEqual(clock - 1, session.Game.LevelTicksRemaining);
      Press(session, input, GameKeyboard.PauseKey);
      Assert.IsTrue(session.Game.Paused);
      input.SetPointer(100, GameKeyboard.PauseKey);
      input.SuppressUntilRelease();
      int pausedClock = session.Game.LevelTicksRemaining;
      for (int tick = 0; tick < 60; tick++)
      {
        input.MovePointer(100, tick % 3 == 0 ? null : GameKeyboard.PauseKey);
        session.Tick(input, Game.Step);
        input.PumpEvents();
        Assert.IsTrue(session.Game.Paused, "Holding Start must not flicker between paused and unpaused.");
      }

      Assert.AreEqual(pausedClock, session.Game.LevelTicksRemaining);
      input.SetPointer(100, null);
      Press(session, input, 0x28);
      Press(session, input, GameKeyboard.PauseKey);
      Assert.IsFalse(session.Game.Paused, "Start toggles pause instead of accepting the selected Settings item.");
      Press(session, input, GameKeyboard.PauseKey);
      Press(session, input, 0x28);
      Press(session, input, 0x0D);
      Assert.IsFalse(session.CanPause, "Jump accepts the Settings selection; Start is inactive inside Settings.");
      Press(session, input, 0x1B);
      Assert.IsTrue(session.Game.Paused, "Attack cancels Settings back to the pause menu.");
      Press(session, input, 0x1B);
      Assert.IsFalse(session.Game.Paused);
      Assert.IsFalse(session.ExitRequested, "Mobile Back resumes the pause menu rather than exiting the game.");
      Assert.AreEqual(30, audio.MusicVolume);
      Assert.AreEqual(50, audio.EffectsVolume);
    }

    finally
    {
      File.Delete(level);
      Directory.Delete(directory);
    }
  }

  [TestMethod]
  public void StartIsAvailableWhilePausedAndMenuActionsKeepTheirGameplayMappingsSeparate()
  {
    var menu = new MobileLayout(1920, 1080, 3, menu: true, canPause: true);
    var game = new MobileLayout(1920, 1080, 3, menu: false);
    Assert.AreEqual((int?)GameKeyboard.PauseKey, menu.Buttons.Single(button => button.Label == "START").Key);
    using var input = new TouchInput();
    input.SetPointer(1, menu.Buttons.Single(button => button.Label == "JUMP").Key);
    Assert.IsTrue(input.KeyPresses.Contains(0x0D));
    Assert.IsFalse(input.Down(GameKey.Jump));
    input.Clear();
    input.SetPointer(1, menu.Buttons.Single(button => button.Label == "ATTACK").Key);
    Assert.IsTrue(input.Down(GameKey.Escape));
    Assert.IsFalse(input.Down(GameKey.Fire));
    input.Clear();
    input.SetPointer(1, game.Buttons.Single(button => button.Label == "JUMP").Key);
    input.SetPointer(2, game.Buttons.Single(button => button.Label == "ATTACK").Key);
    Assert.IsTrue(input.Down(GameKey.Jump) && input.Down(GameKey.Fire));
  }

  [TestMethod]
  public void CoreAssemblyHasNoWindowsOrVelopackDependencies()
  {
    string[] names = typeof(Game).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!).ToArray();
    Assert.IsFalse(names.Any(name => name.Contains("Velopack") || name.Contains("Windows")));
    Assert.AreEqual("SamElla.Core", typeof(Game).Assembly.GetName().Name);
  }

  private static void Press(GameSession session, TouchInput input, int key)
  {
    input.SetPointer(99, key);
    session.Tick(input, Game.Step);
    input.Clear();
  }

  private sealed class FakeAudio : IGameAudio
  {
    public bool Paused { get; private set; }
    public int MusicVolume { get; private set; }
    public int EffectsVolume { get; private set; }

    public void Play(GameSound sound) { }

    public void SetMusic(GameMusic music) { }

    public void SetMusicPaused(bool paused) => Paused = paused;

    public void SetFlamethrowerActive(bool active) { }

    public void SetEnabled(bool musicEnabled, bool effectsEnabled) { }

    public void SetVolume(int musicVolume, int effectsVolume)
    {
      MusicVolume = musicVolume;
      EffectsVolume = effectsVolume;
    }

    public void Stop() { }

    public void Dispose() { }
  }
}
