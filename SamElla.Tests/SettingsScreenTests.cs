// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class SettingsScreenTests
{
  [TestMethod]
  public void TogglesAreIndependentAndAllSixRowsCanBeSelected()
  {
    var audio = new AudioSettings();
    var screen = new SettingsScreen(audio);
    screen.Open();
    screen.HandleInput([0x25], true);
    Assert.IsFalse(audio.MusicEnabled);
    Assert.IsTrue(audio.SoundEffectsEnabled);
    screen.HandleInput([0x28, 0x28, 0x20], true);
    Assert.AreEqual(2, screen.SelectedIndex);
    Assert.IsFalse(audio.SoundEffectsEnabled);
    screen.HandleInput([0x27], true);
    Assert.IsTrue(audio.SoundEffectsEnabled);
    Assert.IsFalse(audio.MusicEnabled);
    screen.HandleInput([0x28], true);
    Assert.AreEqual(3, screen.SelectedIndex);
    screen.HandleInput([0x28], true);
    Assert.AreEqual(4, screen.SelectedIndex);
    screen.HandleInput([0x28], true);
    Assert.AreEqual(5, screen.SelectedIndex);
    screen.HandleInput([0x25, 0x27], true);
    Assert.IsTrue(screen.IsActive);
    screen.HandleInput([0x0D], true);
    Assert.IsFalse(screen.IsActive);
  }

  [TestMethod]
  public void UnfocusedAndClosedSettingsIgnoreInputAndReopeningPreservesPreferences()
  {
    var screen = new SettingsScreen(new AudioSettings());
    screen.HandleInput([0x20], true);
    screen.Open();
    screen.HandleInput([0x25, 0x28, 0x1B], false);
    Assert.IsTrue(screen.IsActive);
    Assert.IsTrue(screen.Audio.MusicEnabled);
    Assert.AreEqual(0, screen.SelectedIndex);
    screen.HandleInput([0x20, 0x28, 0x1B], true);
    Assert.IsFalse(screen.Audio.MusicEnabled);
    Assert.IsFalse(screen.IsActive);
    screen.Open();
    Assert.IsFalse(screen.Audio.MusicEnabled);
    Assert.AreEqual(0, screen.SelectedIndex);
    screen.HandleInput([0x26], true);
    Assert.AreEqual(5, screen.SelectedIndex);
    screen.HandleInput([0x28], true);
    Assert.AreEqual(0, screen.SelectedIndex);
  }

  [TestMethod]
  public void SlidersAdjustIndependentlyClampAndKeepValuesWhenReopened()
  {
    var audio = new AudioSettings { MusicVolume = 100, SoundEffectsVolume = 100 };
    var screen = new SettingsScreen(audio);
    screen.Open();
    screen.HandleInput([0x28, 0x25, 0x25], true);
    Assert.AreEqual(90, audio.MusicVolume);
    Assert.AreEqual(100, audio.SoundEffectsVolume);

    screen.HandleInput([0x28, 0x28, 0x25], true);
    Assert.AreEqual(95, audio.SoundEffectsVolume);
    Assert.AreEqual(90, audio.MusicVolume);

    for (int i = 0; i < 30; i++)
      screen.HandleInput([0x25], true);

    Assert.AreEqual(0, audio.SoundEffectsVolume);
    Assert.IsTrue(audio.SoundEffectsEnabled, "Silent volume must not change the separate on/off switch.");

    screen.HandleInput([0x27], true);
    Assert.AreEqual(5, audio.SoundEffectsVolume);

    for (int i = 0; i < 30; i++)
      screen.HandleInput([0x27], true);

    Assert.AreEqual(100, audio.SoundEffectsVolume);

    screen.HandleInput([0x0D, 0x20], true);
    Assert.AreEqual(100, audio.SoundEffectsVolume, "Enter/Space do not toggle a slider.");
    Assert.IsTrue(screen.IsActive);

    screen.HandleInput([0x1B], true);
    screen.Open();
    Assert.AreEqual(90, audio.MusicVolume);
    Assert.AreEqual(100, audio.SoundEffectsVolume);
  }

  [TestMethod]
  public void VolumeChangesWhileMutedKeepTheSwitchOffAndIgnoreUnfocusedInput()
  {
    var audio = new AudioSettings { MusicVolume = 100, SoundEffectsVolume = 100 };
    var screen = new SettingsScreen(audio);

    screen.Open();
    screen.HandleInput([0x25, 0x28, 0x25], true);
    Assert.IsFalse(audio.MusicEnabled);
    Assert.AreEqual(95, audio.MusicVolume);

    screen.HandleInput([0x25, 0x28], false);
    Assert.AreEqual(95, audio.MusicVolume);
    Assert.AreEqual(1, screen.SelectedIndex);

    screen.HandleInput([0x1B], true);
    screen.HandleInput([0x25], true);
    Assert.AreEqual(95, audio.MusicVolume);

    audio.MusicVolume = -50;
    audio.SoundEffectsVolume = 150;
    Assert.AreEqual(0, audio.MusicVolume);
    Assert.AreEqual(100, audio.SoundEffectsVolume);
  }

  [TestMethod]
  [DataRow(0x1B)]
  [DataRow(0x53)]
  [DataRow(0x70)]
  public void ClosingConsumesRemainingKeys(int closeKey)
  {
    var screen = new SettingsScreen(new AudioSettings());
    screen.Open();
    screen.HandleInput([closeKey, 0x20], true);
    Assert.IsFalse(screen.IsActive);
    Assert.IsTrue(screen.Audio.MusicEnabled);
  }

  [TestMethod]
  public void PreferencesRoundTripAndInvalidValuesRetainDefaults()
  {
    string path = Path.Combine(Path.GetTempPath(), $"samella-audio-{Guid.NewGuid():N}.txt");
    try
    {
      var defaults = AudioSettings.Load(path);
      Assert.IsTrue(defaults.MusicEnabled && defaults.SoundEffectsEnabled);
      Assert.AreEqual(30, defaults.MusicVolume);
      Assert.AreEqual(50, defaults.SoundEffectsVolume);

      new AudioSettings { MusicEnabled = false, SoundEffectsEnabled = true, MusicVolume = 25, SoundEffectsVolume = 75 }.Save(path);
      var restored = AudioSettings.Load(path);
      Assert.IsFalse(restored.MusicEnabled);
      Assert.IsTrue(restored.SoundEffectsEnabled);
      Assert.AreEqual(25, restored.MusicVolume);
      Assert.AreEqual(75, restored.SoundEffectsVolume);

      File.WriteAllLines(path, ["MusicEnabled=invalid", "SoundEffectsEnabled=false", "MusicVolume=invalid", "SoundEffectsVolume=999999999999", "garbage"]);
      restored = AudioSettings.Load(path);
      Assert.IsTrue(restored.MusicEnabled);
      Assert.IsFalse(restored.SoundEffectsEnabled);
      Assert.AreEqual(30, restored.MusicVolume);
      Assert.AreEqual(50, restored.SoundEffectsVolume);
    }

    finally
    {
      File.Delete(path);
    }
  }

  [TestMethod]
  public void MissingVolumeValuesUseFreshInstallDefaults()
  {
    string path = Path.Combine(Path.GetTempPath(), $"samella-legacy-audio-{Guid.NewGuid():N}.txt");
    try
    {
      File.WriteAllLines(path, ["MusicEnabled=false", "SoundEffectsEnabled=false"]);
      var restored = AudioSettings.Load(path);
      Assert.IsFalse(restored.MusicEnabled);
      Assert.IsFalse(restored.SoundEffectsEnabled);
      Assert.AreEqual(30, restored.MusicVolume);
      Assert.AreEqual(50, restored.SoundEffectsVolume);
    }

    finally
    {
      File.Delete(path);
    }
  }

  [TestMethod]
  [DataRow(-20, 150, 0, 100)]
  [DataRow(0, 100, 0, 100)]
  [DataRow(65, 35, 65, 35)]
  public void LoadingVolumeLevelsClampsToSliderRange(int music, int effects, int expectedMusic, int expectedEffects)
  {
    string path = Path.Combine(Path.GetTempPath(), $"samella-volume-settings-{Guid.NewGuid():N}.txt");
    try
    {
      File.WriteAllLines(path, [$"MusicVolume = {music}", $"SoundEffectsVolume = {effects}"]);
      var restored = AudioSettings.Load(path);
      Assert.AreEqual(expectedMusic, restored.MusicVolume);
      Assert.AreEqual(expectedEffects, restored.SoundEffectsVolume);
      Assert.IsTrue(restored.MusicEnabled);
      Assert.IsTrue(restored.SoundEffectsEnabled);
    }

    finally
    {
      File.Delete(path);
    }
  }

  [TestMethod]
  public void OverlayBlendsWithUnderlyingScreenAndDrawsSelectedVolumeThumb()
  {
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    var screen = new SettingsScreen(new AudioSettings());
    screen.Open();
    renderer.Frame.Clear(0xFFFFFFFF);
    renderer.DrawSettings(screen);

    uint lightPanel = renderer.Frame.Pixels[(26 * PixelRenderer.Width) + 26];
    Assert.AreNotEqual(0xFF17263Fu, lightPanel, "The panel must allow the background to show through.");
    Assert.AreEqual(0xFFF6F4DFu, renderer.Frame.Pixels[(99 * PixelRenderer.Width) + 269]);

    screen.HandleInput([0x28, 0x25], true);
    renderer.Frame.Clear(0xFFFFFFFF);
    renderer.DrawSettings(screen);
    Assert.AreEqual(0xFFFFD56Bu, renderer.Frame.Pixels[(99 * PixelRenderer.Width) + 265]);

    renderer.Frame.Clear(0xFF000000);
    renderer.DrawSettings(screen);
    Assert.AreNotEqual(lightPanel, renderer.Frame.Pixels[(26 * PixelRenderer.Width) + 26]);
  }
}
