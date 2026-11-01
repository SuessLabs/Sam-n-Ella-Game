// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;
using SamElla.Audio;
using SamElla.Screens;

namespace SamElla;

// A single frame-driven session is shared by desktop and mobile hosts.
internal sealed class GameSession : IDisposable
{
  private readonly string assetsPath;
  private readonly string levelsPath;
  private readonly string settingsPath;
  private readonly IGameAudio audio;
  private readonly GameSettings savedSettings;
  private readonly SettingsScreen settings;
  private readonly PixelRenderer renderer;
  private readonly bool mobileHost;
  private readonly TitleScreen titleScreen = new();
  private readonly PauseScreen pauseScreen = new();
  private readonly LeaderboardScreen leaderboard;
  private readonly LevelSelectCheat levelSelectCheat = new();
  private readonly LevelStartScreen levelStart;
  private LevelSelectScreen? levelSelect;
  private Level? presentedLevel;
  private string[] files;
  private Game game;
  private double accumulator;

  public GameSession(string assetsPath, string levelsPath, string settingsPath, IGameAudio audio,
    GameSettings? preferences = null, bool desktopUpdates = true)
  {
    this.assetsPath = assetsPath;
    this.levelsPath = levelsPath;
    this.settingsPath = settingsPath;
    this.audio = audio;
    mobileHost = !desktopUpdates;
    savedSettings = preferences ?? GameSettings.Load(settingsPath);
    settings = new SettingsScreen(savedSettings.Audio, savedSettings.Updates, desktopUpdates);
    files = LevelSelectScreen.Discover(levelsPath);
    if (files.Length == 0)
      throw new InvalidDataException($"No .txt levels found in {levelsPath}");

    foreach (string path in files)
      Level.Load(path);

    audio.SetEnabled(settings.Audio.MusicEnabled, settings.Audio.SoundEffectsEnabled);
    audio.SetVolume(settings.Audio.MusicVolume, settings.Audio.SoundEffectsVolume);
    renderer = new PixelRenderer(assetsPath, touchControls: !desktopUpdates);
    game = CreateGame();
    leaderboard = new LeaderboardScreen(savedSettings.Leaderboard);
    levelStart = new LevelStartScreen(Path.Combine(assetsPath, "uncle-quotes.txt"));
    renderer.DrawTitle(titleScreen);
  }

  public PixelBuffer Frame => renderer.Frame;
  public Game Game => game;
  public bool ExitRequested { get; private set; }
  public string UpdateStatus { get; set; } = string.Empty;
  public Func<bool>? TryApplyUpdate { get; init; }
  public bool MenuControls => titleScreen.IsActive || settings.IsActive || leaderboard.IsActive
    || levelSelect is not null || game.Paused || game.IsDying || game.Won || levelStart.IsActive;
  public bool CanPause => !titleScreen.IsActive && !settings.IsActive && !leaderboard.IsActive
    && levelSelect is null && !levelStart.IsActive && ReferenceEquals(presentedLevel, game.Level)
    && !game.IsDying && !game.Won && game.Lives > 0;

  public void Suspend()
  {
    if (!titleScreen.IsActive)
      pauseScreen.Open(game);

    accumulator = 0;
    audio.SetMusicPaused(true);
    audio.SetFlamethrowerActive(false);
    savedSettings.Save(settingsPath);
  }

  public void Dispose()
  {
    savedSettings.Save(settingsPath);
    audio.Stop();
  }

  private Game CreateGame(bool cheat = false, PlayerCharacter character = PlayerCharacter.Sam, int startingLevelIndex = 0)
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

  public void Tick(IGameWindow window, double elapsed)
  {
    if (ExitRequested)
      return;

    elapsed = Math.Clamp(elapsed, 0, 0.1);
    accumulator += elapsed;
    audio.SetMusicPaused(leaderboard.IsActive || levelSelect is not null || !window.Active || (!titleScreen.IsActive && game.Paused));
    audio.SetMusic(GameMusicTracks.ForScreen(titleScreen.IsActive, game));
    audio.SetFlamethrowerActive(levelSelect is null && window.Active && !titleScreen.IsActive && !levelStart.IsActive
      && !game.Paused && !game.IsDying && !game.Won && game.Flame is not null);

    if (mobileHost && window.Active && CanPause && window.KeyPresses.Contains(GameKeyboard.PauseKey))
    {
      if (game.Paused)
        game.Paused = false;
      else
        pauseScreen.Open(game);

      accumulator = 0;
      renderer.Draw(game, 0, 0, pauseScreen.SelectedIndex);
      return;
    }

    bool openedLeaderboard = !titleScreen.IsActive && levelSelect is null && leaderboard.TryOpenFor(game);
    if (leaderboard.IsActive)
    {
      if (!openedLeaderboard && leaderboard.HandleInput(window.KeyPresses, window.Active, DateTimeOffset.Now, allowCancel: mobileHost))
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

      }

      return; // Initials and confirmation keys never reach gameplay or cheats.
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
        renderer.DrawTitle(titleScreen, UpdateStatus);
      }
      else
        renderer.Draw(game, 0, 0, pauseScreen.SelectedIndex);

      if (settings.IsActive)
        renderer.DrawSettings(settings);

      return;
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
      }

      return;
    }

    if (titleScreen.IsActive)
    {
      if (window.Active && (TryApplyUpdate?.Invoke() ?? false))
      {
        ExitRequested = true;
        return;
      }

      if (window.Active && window.Pressed(GameKey.Escape))
      {
        ExitRequested = !mobileHost;
        return;
      }

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
      renderer.DrawTitle(titleScreen, UpdateStatus);
      if (leaderboard.IsActive)
        renderer.DrawLeaderboard(leaderboard);
      if (settings.IsActive)
        renderer.DrawSettings(settings);

      return;
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

      return;
    }

    if (!window.Active)
    {
      pauseScreen.Open(game);
      accumulator = 0;
      renderer.Draw(game, 0, 0, pauseScreen.SelectedIndex);

      return;
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
      {
        if (mobileHost && game.Paused)
        {
          game.Paused = false;
          accumulator = 0;
          return;
        }

        ExitRequested = true;
        return;
      }

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

        return;
      }

      if (!wasPaused && window.Pressed(GameKey.Jump))
        game.QueueJump();
    }

    if (!ReferenceEquals(presentedLevel, game.Level))
    {
      accumulator = 0;
      return;
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

  }
}
