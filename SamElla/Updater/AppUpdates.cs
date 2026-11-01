// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using Velopack;
using Velopack.Sources;

namespace SamElla.Updater;

internal interface IAppUpdateBackend
{
  bool IsInstalled { get; }

  Task<bool> DownloadLatestAsync(Action<int> progress, CancellationToken cancellationToken);

  void ScheduleApply(bool restart, string[] restartArgs);
}

internal sealed class AppUpdates(IAppUpdateBackend backend, Action<Exception>? log = null) : IDisposable
{
  public const string RepositoryUrl = "https://github.com/SuessLabs/Sam-n-Ella-Game";

  private readonly CancellationTokenSource _cancellation = new(TimeSpan.FromMinutes(2));

  private volatile bool _ready;

  private int _scheduled;
  private int _started;
  private volatile string _status = string.Empty;

  public bool IsInstalled => backend.IsInstalled;
  public string Status => _status;

  public async Task CheckAsync()
  {
    if (Interlocked.Exchange(ref _started, 1) != 0 || !backend.IsInstalled)
      return;

    try
    {
      _status = "CHECKING FOR UPDATES...";
      _ready = await backend.DownloadLatestAsync(
        progress => _status = $"DOWNLOADING UPDATE {progress}%", _cancellation.Token);
      _status = _ready ? "UPDATE READY" : string.Empty;
    }
    catch (Exception exception)
    {
      log?.Invoke(exception);
      _status = string.Empty;
    }
  }

  public void Dispose()
  {
    _cancellation.Cancel();
    _cancellation.Dispose();
  }

  // Called on the UI thread at the title screen, or once when the game exits.
  public bool TryScheduleApply(bool restart, string[] restartArgs)
  {
    if (!_ready || Interlocked.Exchange(ref _scheduled, 1) != 0)
      return false;

    try
    {
      backend.ScheduleApply(restart, restartArgs);
      return true;
    }
    catch (Exception exception)
    {
      log?.Invoke(exception);
      _status = string.Empty;
      return false;
    }
  }
}

internal sealed class VelopackUpdateBackend(bool includePrereleases = false) : IAppUpdateBackend
{
  private readonly UpdateManager _manager = new(new GithubSource(AppUpdates.RepositoryUrl, null, includePrereleases));
  public bool IsInstalled => _manager.IsInstalled;

  public async Task<bool> DownloadLatestAsync(Action<int> progress, CancellationToken cancellationToken)
  {
    var update = await _manager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
    if (update is null)
      return false;

    await _manager.DownloadUpdatesAsync(update, progress, cancellationToken);
    return true;
  }

  public void ScheduleApply(bool restart, string[] restartArgs) =>
    _manager.WaitExitThenApplyUpdates(null, silent: true, restart: restart, restartArgs: restartArgs);
}
