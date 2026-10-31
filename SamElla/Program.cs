using System.Diagnostics;
using SamElla;
using SamElla.Actors;
using SamElla.Audio;
using SamElla.Screens;
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
    throw new PlatformNotSupportedException("Sam n' Ella currently has a Windows backend. Its game and pixel renderer are portable; other window backends can be added later.");

  string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SamElla", "settings.txt");
  var savedSettings = GameSettings.Load(settingsPath);
  var settings = new SettingsScreen(savedSettings.Audio, savedSettings.Updates);

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

  var files = LevelSelectScreen.Discover(levelsPath);

  if (files.Length == 0)
    throw new InvalidDataException($"No .txt levels found in {levelsPath}");

  foreach (var file in files)
    Level.Load(file);

  // Restore saved audio preferences before starting any music or sound effects.
  using var audio = new WindowsGameAudio();
  audio.SetEnabled(settings.Audio.MusicEnabled, settings.Audio.SoundEffectsEnabled);
  audio.SetVolume(settings.Audio.MusicVolume, settings.Audio.SoundEffectsVolume);

  Game CreateGame(bool cheat = false, PlayerCharacter character = PlayerCharacter.Sam, int startingLevelIndex = 0)
  {
    var created = new Game(files,
      deathQuotesPath: Path.Combine(assetsPath, "death-quotes.txt"), creditsAssetsPath: assetsPath,
      enemySettingsPath: Path.Combine(assetsPath, "enemies.txt"),
      flamethrowerQuotesPath: Path.Combine(assetsPath, "flamethrower-quotes.txt"), konamiCheat: cheat,
      gameOverQuotesPath: Path.Combine(assetsPath, "game-over-quotes.txt"), startingLevelIndex: startingLevelIndex)
    { Character = character };
    created.SoundRequested += audio.Play;
    return created;
  }

  var renderer = new PixelRenderer(assetsPath);
  var game = CreateGame();
  var titleScreen = new TitleScreen();
  var pauseScreen = new PauseScreen();
  var leaderboard = new LeaderboardScreen(savedSettings.Leaderboard);
  LevelSelectScreen? levelSelect = null;
  var levelSelectCheat = new LevelSelectCheat();
  var levelStart = new LevelStartScreen(Path.Combine(assetsPath, "uncle-quotes.txt"));
  Level? presentedLevel = null;

  using IGameWindow window = new WindowsGameWindow(PixelRenderer.Width, PixelRenderer.Height);
  _ = Task.Run(updates.CheckAsync);

  var clock = Stopwatch.StartNew();
  double previous = clock.Elapsed.TotalSeconds;
  double accumulator = 0;

  // Include simulation, drawing and native presentation in the frame budget.
  void WaitForFrame(double started)
  {
    int milliseconds = (int)((Game.Step - (clock.Elapsed.TotalSeconds - started)) * 1000);
    if (milliseconds > 0)
      Thread.Sleep(milliseconds);
  }

  while (!window.Closed)
  {
    double frameStarted = clock.Elapsed.TotalSeconds;
    window.PumpEvents();
    audio.SetMusicPaused(leaderboard.IsActive || levelSelect is not null || !window.Active || (!titleScreen.IsActive && game.Paused));
    audio.SetMusic(GameMusicTracks.ForScreen(titleScreen.IsActive, game));
    audio.SetFlamethrowerActive(levelSelect is null && window.Active && !titleScreen.IsActive && !levelStart.IsActive
      && !game.Paused && !game.IsDying && !game.Won && game.Flame is not null);

    if (window.Closed)
      break;

    var now = clock.Elapsed.TotalSeconds;
    double elapsed = Math.Min(now - previous, 0.1);
    accumulator += elapsed;
    previous = now;

    bool openedLeaderboard = !titleScreen.IsActive && levelSelect is null && leaderboard.TryOpenFor(game);
    if (leaderboard.IsActive)
    {
      if (!openedLeaderboard && leaderboard.HandleInput(window.KeyPresses, window.Active, DateTimeOffset.Now))
        savedSettings.Save(settingsPath);

      accumulator = 0;
      if (!leaderboard.IsActive)
      {
        if (leaderboard.Context == LeaderboardContext.GameOver)
          game.ContinueAfterDeath();
      }
      else
      {
        renderer.DrawLeaderboard(leaderboard);
        window.Present(renderer.Frame);
      }

      WaitForFrame(frameStarted);
      continue; // Initials and confirmation keys never reach gameplay or cheats.
    }

    bool openedSettings = !settings.IsActive && levelSelect is null && window.Active
      && (titleScreen.IsActive || (game.Paused && !game.IsDying && !game.Won && game.Lives > 0))
      && window.KeyPresses.Any(code => code is 0x53 or 0x70);
    if (openedSettings)
      settings.Open();

    if (settings.IsActive)
    {
      // Opening and closing keys belong to the overlay, never the screen beneath it.
      if (!openedSettings)
        settings.HandleInput(window.KeyPresses, window.Active);

      audio.SetEnabled(settings.Audio.MusicEnabled, settings.Audio.SoundEffectsEnabled);
      audio.SetVolume(settings.Audio.MusicVolume, settings.Audio.SoundEffectsVolume);

      if (!settings.IsActive)
        savedSettings.Save(settingsPath);

      accumulator = 0;

      if (titleScreen.IsActive)
      {
        titleScreen.Update(elapsed, startPressed: false, window.Active);
        renderer.DrawTitle(titleScreen, updates.Status);
      }
      else
        renderer.Draw(game, 0, 0, pauseScreen.SelectedIndex);

      if (settings.IsActive)
        renderer.DrawSettings(settings);

      window.Present(renderer.Frame);
      WaitForFrame(frameStarted);
      continue;
    }

    bool openedLevelSelect = levelSelect is null && levelSelectCheat.HandleInput(window.KeyPresses, window.Active);
    if (openedLevelSelect)
    {
      files = LevelSelectScreen.Discover(levelsPath);
      levelSelect = new LevelSelectScreen(files);
      audio.SetFlamethrowerActive(false);
      audio.SetMusicPaused(true);
    }

    if (levelSelect is not null)
    {
      // The final cheat key (or Enter in the same frame) must not act on the menu.
      if (!openedLevelSelect)
        levelSelect.HandleInput(window.KeyPresses, window.Active);

      if (!levelSelect.IsActive)
      {
        if (levelSelect.Confirmed)
        {
          bool cheat = titleScreen.IsActive ? titleScreen.KonamiCodeEnabled : game.KonamiCheatEnabled;
          var character = titleScreen.IsActive ? titleScreen.SelectedCharacter : game.Character;
          game = CreateGame(cheat, character, levelSelect.SelectedIndex);
          titleScreen.Update(0, startPressed: true, windowActive: true);
          audio.Stop();
          audio.Play(GameSound.PowerUp);
        }

        levelSelect = null;
      }

      accumulator = 0;
      if (levelSelect is not null)
      {
        renderer.DrawLevelSelect(levelSelect);
        window.Present(renderer.Frame);
        WaitForFrame(frameStarted);
      }

      continue;
    }

    if (titleScreen.IsActive)
    {
      if (window.Active && updates.TryScheduleApply(restart: true, args))
        break;

      if (window.Active && window.Pressed(GameKey.Escape))
        break;

      var titleAction = titleScreen.HandleInput(elapsed, window.KeyPresses, window.Active);
      if (titleAction == MenuAction.Settings)
        settings.Open();
      else if (titleAction == MenuAction.Leaderboard)
        leaderboard.OpenViewer();

      if (!titleScreen.IsActive)
      {
        game = CreateGame(titleScreen.KonamiCodeEnabled, titleScreen.SelectedCharacter);
        audio.SetMusic(GameMusic.None);
        audio.Play(GameSound.PowerUp);
      }

      accumulator = 0;
      renderer.DrawTitle(titleScreen, updates.Status);
      if (leaderboard.IsActive)
        renderer.DrawLeaderboard(leaderboard);
      if (settings.IsActive)
        renderer.DrawSettings(settings);

      window.Present(renderer.Frame);
      WaitForFrame(frameStarted);
      continue;
    }

    if (!ReferenceEquals(presentedLevel, game.Level))
    {
      presentedLevel = game.Level;
      levelStart.Show(game);
    }

    if (levelStart.IsActive)
    {
      levelStart.Update(elapsed, window.AnyKeyPressed, window.Active);
      if (!levelStart.IsActive)
        game.CompleteLevelStart();

      accumulator = 0;
      renderer.DrawLevelStart(levelStart);
      window.Present(renderer.Frame);
      WaitForFrame(frameStarted);
      continue;
    }

    if (!window.Active)
    {
      pauseScreen.Open(game);
      accumulator = 0;
      renderer.Draw(game, 0, 0, pauseScreen.SelectedIndex);
      window.Present(renderer.Frame);
      WaitForFrame(frameStarted);
      continue;
    }

    bool wasDying = game.IsDying;
    if (wasDying)
    {
      game.Paused = false;
      // Qualifying runs must reach the initials prompt before Enter can restart.
      bool waitingForLeaderboard = game.Lives == 0 && savedSettings.Leaderboard.Qualifies(game.Score);
      if (!waitingForLeaderboard && DeathScreen.HandleInput(game, window.KeyPresses, window.Active))
        accumulator = 0;
    }
    else
    {
      if (window.Pressed(GameKey.Escape))
        break;

      bool wasPaused = game.Paused;
      if (window.Pressed(GameKey.Restart))
        game.Restart();

      if (pauseScreen.HandleInput(game, window.KeyPresses, window.Active) == MenuAction.Settings)
        settings.Open();

      if (game.Paused)
      {
        accumulator = 0;
        renderer.Draw(game, 0, 0, pauseScreen.SelectedIndex);
        if (settings.IsActive)
          renderer.DrawSettings(settings);

        window.Present(renderer.Frame);
        WaitForFrame(frameStarted);
        continue;
      }

      if (!wasPaused && window.Pressed(GameKey.Jump))
        game.QueueJump();
    }

    if (!ReferenceEquals(presentedLevel, game.Level))
    {
      accumulator = 0;
      continue;
    }

    int direction = wasDying ? 0 : (window.Down(GameKey.Right) ? 1 : 0) - (window.Down(GameKey.Left) ? 1 : 0);

    while (accumulator >= Game.Step)
    {
      game.Update(direction, !wasDying && window.Down(GameKey.Jump), !wasDying && window.Down(GameKey.Fire),
        !wasDying && window.Down(GameKey.Down));
      if (savedSettings.RecordProgress(game, DateTimeOffset.Now))
        savedSettings.Save(settingsPath);

      accumulator -= Game.Step;
      if (leaderboard.TryOpenFor(game))
      {
        accumulator = 0;
        break; // No credit-scrolling ticks before initials have been entered.
      }

      if (!ReferenceEquals(presentedLevel, game.Level))
      {
        accumulator = 0;
        break;
      }
    }

    if (leaderboard.IsActive || leaderboard.TryOpenFor(game))
    {
      accumulator = 0;
      renderer.DrawLeaderboard(leaderboard);
    }
    else
      renderer.Draw(game, direction, elapsed);

    window.Present(renderer.Frame);
    WaitForFrame(frameStarted);
  }

  savedSettings.Save(settingsPath);
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
