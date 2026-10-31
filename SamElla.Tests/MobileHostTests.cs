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
  [DataRow(2560, 1440, 4.0)]
  [DataRow(1280, 800, 2.0)]
  [DataRow(640, 360, 1.0)]
  public void PhoneAndTabletLayoutsKeepPixelAspectRatioAndSeparateTouchTargets(int width, int height, double density)
  {
    foreach (bool menu in new[] { false, true })
    {
      var layout = new MobileLayout(width, height, density, menu);
      var view = layout.Viewport;
      Assert.AreEqual(1.6, view.Width / view.Height, 0.00001);
      Assert.AreEqual(Math.Floor(view.Width / 384), view.Width / 384);
      Assert.IsTrue(view.X >= 0 && view.Y >= 0 && view.X + view.Width <= width && view.Y + view.Height <= height);
      foreach (var button in layout.Buttons)
      {
        Assert.IsTrue(button.X >= 0 && button.Y >= view.Y + view.Height);
        Assert.IsTrue(button.X + button.Width <= width && button.Y + button.Height <= height);
        Assert.AreEqual(button.Key, layout.KeyAt(button.X + button.Width / 2, button.Y + button.Height / 2));
        Assert.AreEqual(1, layout.Buttons.Count(other => other.Contains(button.X + button.Width / 2, button.Y + button.Height / 2)));
      }
    }
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
      Press(session, input, 0x0D);
      Assert.IsTrue(session.Game.Paused);
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
