// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class MusicPauseStateTests
{
  [TestMethod]
  public void MusicResumesOnlyAfterEveryOverlappingPickupSoundFinishes()
  {
    var state = new MusicPauseState();
    var growth = new object();
    var weapon = new object();
    Assert.IsFalse(state.ShouldPause);
    state.StartNotification(growth);
    state.StartNotification(weapon);
    Assert.IsTrue(state.ShouldPause);
    Assert.IsTrue(state.CompleteNotification(growth));
    Assert.IsTrue(state.ShouldPause, "The weapon notification is still playing.");
    Assert.IsFalse(state.CompleteNotification(new object()), "Other effects must not release the music pause.");
    Assert.IsTrue(state.ShouldPause);
    Assert.IsTrue(state.CompleteNotification(weapon));
    Assert.IsFalse(state.ShouldPause);
  }

  [TestMethod]
  public void PickupFinishingWhilePausedDoesNotResumeMusicUntilGameplayResumes()
  {
    var state = new MusicPauseState();
    var pickup = new object();
    state.StartNotification(pickup);
    state.GamePaused = true;
    state.CompleteNotification(pickup);
    Assert.IsTrue(state.ShouldPause);
    state.GamePaused = false;
    Assert.IsFalse(state.ShouldPause);
  }

  [TestMethod]
  public void ResumingGameplayOrTogglingMusicOnCannotOverrideAnActiveNotification()
  {
    var state = new MusicPauseState();
    var pickup = new object();
    state.StartNotification(pickup);
    state.GamePaused = true;
    state.Enabled = false;
    state.GamePaused = false;
    state.Enabled = true;
    Assert.IsTrue(state.ShouldPause);
    state.CompleteNotification(pickup);
    Assert.IsFalse(state.ShouldPause);
  }

  [TestMethod]
  public void MutingEffectsOrStoppingClearsNotificationPausesButPreservesMuteAndFocus()
  {
    var state = new MusicPauseState { Enabled = false };
    state.StartNotification(new object());
    state.ClearNotifications();
    Assert.IsTrue(state.ShouldPause, "Music is still disabled.");
    state.Enabled = true;
    Assert.IsFalse(state.ShouldPause);
    state.StartNotification(new object());
    state.GamePaused = true;
    state.ClearNotifications();
    Assert.IsTrue(state.ShouldPause, "Gameplay or window focus is still paused.");
    state.GamePaused = false;
    Assert.IsFalse(state.ShouldPause);
  }
}
