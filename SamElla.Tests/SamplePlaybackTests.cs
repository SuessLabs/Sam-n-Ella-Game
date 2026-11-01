// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class SamplePlaybackTests
{
  [TestMethod]
  public void MusicAndEffectsScaleIndependentlyWithoutChangingSourceSamples()
  {
    short[] source = [1000, -2000, 3000, -4000];
    var music = new SamplePlayback(source) { Volume = 25 };
    var effect = new SamplePlayback(source) { Volume = 75 };
    short[] musicBuffer = new short[2], effectBuffer = new short[2];
    Assert.AreEqual(2, music.Read(musicBuffer));
    Assert.AreEqual(2, effect.Read(effectBuffer));
    CollectionAssert.AreEqual(new short[] { 250, -500 }, musicBuffer);
    CollectionAssert.AreEqual(new short[] { 750, -1500 }, effectBuffer);
    music.Volume = 50;
    music.Read(musicBuffer);
    effect.Read(effectBuffer);
    CollectionAssert.AreEqual(new short[] { 1500, -2000 }, musicBuffer);
    CollectionAssert.AreEqual(new short[] { 2250, -3000 }, effectBuffer);
    CollectionAssert.AreEqual(new short[] { 1000, -2000, 3000, -4000 }, source);
  }

  [TestMethod]
  public void LoopingVoiceAppliesLiveVolumeWithoutRestartingPlayback()
  {
    var playback = new SamplePlayback([1000, 2000, 3000], loop: true);
    short[] buffer = new short[2];
    playback.Read(buffer);
    playback.Volume = 50;
    playback.Read(buffer);
    CollectionAssert.AreEqual(new short[] { 1500, 500 }, buffer);
    playback.Volume = 0;
    playback.Read(buffer);
    CollectionAssert.AreEqual(new short[] { 0, 0 }, buffer);
    playback.Volume = 100;
    playback.Read(buffer);
    CollectionAssert.AreEqual(new short[] { 1000, 2000 }, buffer);
    Assert.IsTrue(playback.HasRemaining);
  }

  [TestMethod]
  public void OneShotEndsAfterItsPartialFinalBufferAndEmptySamplesDoNotLoop()
  {
    var playback = new SamplePlayback([1000, -1000, 2000]);
    short[] buffer = new short[2];
    Assert.AreEqual(2, playback.Read(buffer));
    Assert.IsTrue(playback.HasRemaining);
    Assert.AreEqual(1, playback.Read(buffer));
    Assert.AreEqual((short)2000, buffer[0]);
    Assert.IsFalse(playback.HasRemaining);
    Assert.AreEqual(0, playback.Read(buffer));
    var empty = new SamplePlayback([], loop: true);
    Assert.IsFalse(empty.HasRemaining);
    Assert.AreEqual(0, empty.Read(buffer));
  }

  [TestMethod]
  public void VolumeClampsAndFullScaleSamplesNeverOverflow()
  {
    var playback = new SamplePlayback([short.MinValue, short.MaxValue], loop: true);
    short[] buffer = new short[2];
    playback.Volume = 150;
    playback.Read(buffer);
    CollectionAssert.AreEqual(new short[] { short.MinValue, short.MaxValue }, buffer);
    playback.Volume = -10;
    playback.Read(buffer);
    CollectionAssert.AreEqual(new short[] { 0, 0 }, buffer);
  }
}
