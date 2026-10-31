using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Updater;

namespace SamElla.Tests;

[TestClass]
public sealed class AppUpdatesTests
{
  [TestMethod]
  public async Task UnpackagedDevelopmentBuildDoesNotContactGitHub()
  {
    var backend = new FakeBackend { IsInstalled = false };
    using var updates = new AppUpdates(backend);
    await updates.CheckAsync();
    Assert.AreEqual(0, backend.Checks);
    Assert.AreEqual(string.Empty, updates.Status);
    Assert.IsFalse(updates.TryScheduleApply(true, []));
  }

  [TestMethod]
  public async Task UpdateIsAppliedOnlyAfterDownloadAndOnlyOnce()
  {
    var backend = new FakeBackend();
    using var updates = new AppUpdates(backend);
    Task check = updates.CheckAsync();
    Assert.IsFalse(updates.TryScheduleApply(true, []));
    backend.Progress!(40);
    Assert.AreEqual("DOWNLOADING UPDATE 40%", updates.Status);
    backend.Completion.SetResult(true);
    await check;
    Assert.AreEqual("UPDATE READY", updates.Status);
    string[] args = ["--levels", "C:\\Custom Levels"];
    Assert.IsTrue(updates.TryScheduleApply(true, args));
    Assert.IsTrue(backend.Restart);
    CollectionAssert.AreEqual(args, backend.RestartArgs);
    Assert.IsFalse(updates.TryScheduleApply(false, []));
    await updates.CheckAsync();
    Assert.AreEqual(1, backend.Checks);
    Assert.AreEqual(1, backend.Applies);
  }

  [TestMethod]
  public async Task InGameUpdateCanBeAppliedOnExitWithoutRestart()
  {
    var backend = new FakeBackend();
    backend.Completion.SetResult(true);
    using var updates = new AppUpdates(backend);
    await updates.CheckAsync();
    Assert.AreEqual(0, backend.Applies, "Download completion must not interrupt gameplay.");
    Assert.IsTrue(updates.TryScheduleApply(false, []));
    Assert.IsFalse(backend.Restart);
  }

  [TestMethod]
  public async Task NoUpdateAndOfflineFailuresDoNotPreventPlay()
  {
    var backend = new FakeBackend();
    backend.Completion.SetResult(false);
    using var current = new AppUpdates(backend);
    await current.CheckAsync();
    Assert.IsFalse(current.TryScheduleApply(true, []));

    backend = new FakeBackend();
    backend.Completion.SetException(new InvalidOperationException("GitHub unavailable"));
    Exception? logged = null;
    using var offline = new AppUpdates(backend, exception => logged = exception);
    await offline.CheckAsync();
    Assert.IsNotNull(logged);
    Assert.AreEqual(string.Empty, offline.Status);
    Assert.IsFalse(offline.TryScheduleApply(true, []));
  }

  [TestMethod]
  public async Task ApplyFailureIsLoggedWithoutCrashingOrRetryLoop()
  {
    var backend = new FakeBackend { FailApply = true };
    backend.Completion.SetResult(true);
    Exception? logged = null;
    using var updates = new AppUpdates(backend, exception => logged = exception);
    await updates.CheckAsync();
    Assert.IsFalse(updates.TryScheduleApply(true, []));
    Assert.IsNotNull(logged);
    Assert.IsFalse(updates.TryScheduleApply(true, []));
    Assert.AreEqual(1, backend.Applies);
  }

  private sealed class FakeBackend : IAppUpdateBackend
  {
    public bool IsInstalled { get; set; } = true;
    public bool Restart { get; private set; }
    public bool FailApply { get; init; }
    public string[] RestartArgs { get; private set; } = [];
    public int Checks { get; private set; }
    public int Applies { get; private set; }
    public Action<int>? Progress { get; private set; }
    public TaskCompletionSource<bool> Completion { get; } = new();

    public Task<bool> DownloadLatestAsync(Action<int> progress, CancellationToken cancellationToken)
    {
      Checks++;
      Progress = progress;
      return Completion.Task.WaitAsync(cancellationToken);
    }

    public void ScheduleApply(bool restart, string[] restartArgs)
    {
      Applies++;
      if (FailApply)
        throw new InvalidOperationException("Update helper unavailable");
      Restart = restart;
      RestartArgs = restartArgs;
    }
  }
}
