using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class UpdateSettingsTests : GameTestBase
{
  [TestMethod]
  [DataRow("")]
  [DataRow("MusicEnabled=false")]
  [DataRow("IncludePrereleases=invalid")]
  [DataRow("IncludePrereleases=1")]
  public void MissingOrInvalidOptInUsesStableUpdates(string contents)
  {
    Assert.IsFalse(GameSettings.Load(NextPath).Updates.IncludePrereleases);
    File.WriteAllText(NextPath, contents);
    Assert.IsFalse(GameSettings.Load(NextPath).Updates.IncludePrereleases);
  }

  [TestMethod]
  public void OptInPersistsThroughSettingsAndProgressSavesAndCanBeDisabled()
  {
    var settings = new GameSettings();
    settings.Audio.MusicVolume = 15;
    settings.Leaderboard.TryAdd("ABC", 1234, DateTimeOffset.Now);
    var screen = new SettingsScreen(settings.Audio, settings.Updates);
    screen.Open();
    screen.HandleInput([0x28, 0x28, 0x28, 0x28, 0x0D, 0x1B], true);
    Assert.IsTrue(settings.Updates.IncludePrereleases);
    settings.Save(NextPath);
    var loaded = GameSettings.Load(NextPath);
    Assert.IsTrue(loaded.Updates.IncludePrereleases);
    Assert.AreEqual(15, loaded.Audio.MusicVolume);
    Assert.HasCount(1, loaded.Leaderboard.Entries);
    loaded.Save(NextPath);
    Assert.IsTrue(GameSettings.Load(NextPath).Updates.IncludePrereleases);

    screen.Open();
    screen.HandleInput([0x28, 0x28, 0x28, 0x28, 0x25, 0x1B], true);
    settings.Save(NextPath);
    Assert.IsFalse(GameSettings.Load(NextPath).Updates.IncludePrereleases);
  }

  [TestMethod]
  public void OptInSupportsDirectionalAndToggleKeysWithoutChangingAudio()
  {
    var settings = new GameSettings();
    var screen = new SettingsScreen(settings.Audio, settings.Updates);
    screen.Open();
    screen.HandleInput([0x28, 0x28, 0x28, 0x28], true);
    screen.HandleInput([0x27], false);
    Assert.IsFalse(settings.Updates.IncludePrereleases);
    screen.HandleInput([0x27, 0x27], true);
    Assert.IsTrue(settings.Updates.IncludePrereleases);
    screen.HandleInput([0x20], true);
    Assert.IsFalse(settings.Updates.IncludePrereleases);
    screen.HandleInput([0x0D], true);
    Assert.IsTrue(settings.Updates.IncludePrereleases);
    Assert.IsTrue(screen.IsActive);
    Assert.IsTrue(settings.Audio.MusicEnabled && settings.Audio.SoundEffectsEnabled);
    Assert.AreEqual(30, settings.Audio.MusicVolume);
    Assert.AreEqual(50, settings.Audio.SoundEffectsVolume);
  }

  [TestMethod]
  public void ExperimentalToggleRendersBothStatesAndBackRemainsReachable()
  {
    var screen = new SettingsScreen(new GameSettings().Audio);
    screen.Open();
    screen.HandleInput([0x28, 0x28, 0x28, 0x28], true);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawSettings(screen);
    uint[] disabled = (uint[])renderer.Frame.Pixels.Clone();
    screen.HandleInput([0x27], true);
    renderer.Frame.Clear(0);
    renderer.DrawSettings(screen);
    Assert.AreNotEqual(disabled[170 * PixelRenderer.Width + 320], renderer.Frame.Pixels[170 * PixelRenderer.Width + 320]);
    string? preview = Environment.GetEnvironmentVariable("SAMELLA_UPDATE_SETTINGS_PREVIEW");
    if (preview is not null)
      renderer.Frame.SavePng(preview);

    screen.HandleInput([0x28, 0x0D], true);
    Assert.IsFalse(screen.IsActive);
  }
}
