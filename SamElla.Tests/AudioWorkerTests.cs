using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Audio;

namespace SamElla.Tests;

[TestClass]
public sealed class AudioWorkerTests
{
  [TestMethod]
  public void SlowDeviceDoesNotBlockProducerAndCommandsKeepTheirOrder()
  {
    using var entered = new ManualResetEventSlim();
    using var release = new ManualResetEventSlim();
    var calls = new List<int>();
    int producerThread = Environment.CurrentManagedThreadId;
    int audioThread = producerThread;
    int cleanupThread = producerThread;
    var worker = new AudioWorker(() => { }, () => cleanupThread = Environment.CurrentManagedThreadId);

    try
    {
      worker.Enqueue(() =>
      {
        audioThread = Environment.CurrentManagedThreadId;
        entered.Set();
        release.Wait();
        calls.Add(1);
      });

      Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
      worker.Enqueue(() => calls.Add(2));
      Assert.AreNotEqual(producerThread, audioThread);
      Assert.IsFalse(release.IsSet, "Gameplay can enqueue another sound while the device is blocked.");
    }
    finally
    {
      release.Set();
      worker.Dispose();
    }

    CollectionAssert.AreEqual(new[] { 1, 2 }, calls);
    Assert.AreEqual(audioThread, cleanupThread, "Native cleanup must stay on the audio thread.");
    worker.Dispose();
    worker.Enqueue(() => Assert.Fail("Disposed workers must ignore new requests."));
  }

  [TestMethod]
  public void FinishedSoundsArePolledEvenWhenThereAreNoCommands()
  {
    using var updated = new ManualResetEventSlim();
    using var worker = new AudioWorker(() => updated.Set(), () => { });
    Assert.IsTrue(updated.Wait(TimeSpan.FromSeconds(5)));
  }

}
