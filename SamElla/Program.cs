// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using SamElla;
using SamElla.Audio;
using SamElla.Updater;
using Velopack;

try
{
  VelopackApp.Build().Run();

#if !SAMELLA_DISTRIBUTION
  int exportOption = Array.IndexOf(args, "--export-art");

  if (exportOption >= 0)
  {
    if (exportOption + 1 >= args.Length)
      throw new ArgumentException("Usage: --export-art <output-directory>");

    ArtExporter.Export(Path.Combine(AppContext.BaseDirectory, "Assets"), Path.GetFullPath(args[exportOption + 1]));
    return;
  }

#endif

  if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("Sam n' Ella currently has a Windows backend. Use SamElla.Mobile for Android and iOS.");

  string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SamElla", "settings.txt");
  var savedSettings = GameSettings.Load(settingsPath);

  using var updates = new AppUpdates(new VelopackUpdateBackend(savedSettings.Updates.IncludePrereleases), exception =>
  {
    try
    {
      string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SamElla", "Logs");
      Directory.CreateDirectory(logs);
      File.AppendAllText(Path.Combine(logs, "updates.log"), $"{DateTimeOffset.UtcNow:u} {exception}\n");
    }
    catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
    {
      // An unavailable log folder must not prevent offline play.
    }
  });

  string contentRoot = AppContext.BaseDirectory;
  if (updates.IsInstalled)
  {
    contentRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SamElla", "Content");
    UserContent.Synchronize(AppContext.BaseDirectory, contentRoot);
  }

  string assetsPath = Path.Combine(contentRoot, "Assets");
  var levelsPath = Path.Combine(contentRoot, "Levels");
  var option = Array.IndexOf(args, "--levels");

  if (option >= 0)
  {
    if (option + 1 >= args.Length)
      throw new ArgumentException("Usage: --levels <directory>");

    levelsPath = Path.GetFullPath(args[option + 1]);
  }

  using var audio = new WindowsGameAudio();
  using var session = new GameSession(assetsPath, levelsPath, settingsPath, audio, savedSettings)
  {
    TryApplyUpdate = () => updates.TryScheduleApply(restart: true, args),
  };
  using IGameWindow window = new WindowsGameWindow(PixelRenderer.Width, PixelRenderer.Height);
  _ = Task.Run(updates.CheckAsync);
  var clock = Stopwatch.StartNew();
  double previous = clock.Elapsed.TotalSeconds;
  while (!window.Closed && !session.ExitRequested)
  {
    double started = clock.Elapsed.TotalSeconds;
    window.PumpEvents();
    if (window.Closed)
      break;

    double now = clock.Elapsed.TotalSeconds;
    session.UpdateStatus = updates.Status;
    session.Tick(window, now - previous);
    previous = now;
    window.Present(session.Frame);
    int wait = (int)((Game.Step - (clock.Elapsed.TotalSeconds - started)) * 1000);
    if (wait > 0)
      Thread.Sleep(wait);
  }

  updates.TryScheduleApply(restart: false, args);
}
catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException
  or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
{
#if !SAMELLA_DISTRIBUTION
  Console.Error.WriteLine($"Sam n' Ella: {exception.Message}");
#endif

  if (OperatingSystem.IsWindows() && !args.Contains("--export-art"))
    WindowsGameWindow.ShowError(exception.Message);

  Environment.ExitCode = 1;
}
