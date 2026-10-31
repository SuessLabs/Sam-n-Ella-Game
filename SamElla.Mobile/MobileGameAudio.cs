using Plugin.Maui.Audio;
using SamElla.Audio;

namespace SamElla.Mobile;

// Native audio runs on a worker so preparing playback never stalls game input.
internal sealed class MobileGameAudio : IGameAudio
{
  private readonly Dictionary<GameSound, byte[]> _effects = Enum.GetValues<GameSound>()
    .ToDictionary(sound => sound, sound => Wave(GameSounds.Create(sound)));
  private readonly Dictionary<GameMusic, byte[]> _musicSamples = Enum.GetValues<GameMusic>()
    .Where(music => music != GameMusic.None).ToDictionary(music => music, music => Wave(GameMusicTracks.Create(music)));
  private readonly List<(IAudioPlayer Player, bool Notification)> _voices = [];
  private readonly AudioWorker _worker;
  private readonly MusicPauseState _pause = new();
  private IAudioPlayer? _music, _flame;
  private GameMusic _track;
  private bool _effectsEnabled = true, _flameRequested, _suspended, _musicStarted, _musicWasPaused;
  private int _musicVolume = 30, _effectsVolume = 50;

  public MobileGameAudio() => _worker = new AudioWorker(() => RunAudio(Update), () => RunAudio(StopCore));

  public void Play(GameSound sound) => Queue(() =>
  {
    if (sound == GameSound.Died)
      StopCore();

    if (!_effectsEnabled || _suspended || _voices.Count >= 8)
      return;

    var player = Create(_effects[sound], false, _effectsVolume);
    if (player is null)
      return;

    bool notification = sound == GameSound.PowerUp;
    _voices.Add((player, notification));
    if (notification)
      _pause.StartNotification(player);

    UpdateMusicPause();
    player.Play();
  });

  public void SetMusic(GameMusic music) => Queue(() =>
  {
    if (music == _track)
      return;

    _music?.Dispose();
    _music = null;
    _track = music;
    _musicStarted = _musicWasPaused = false;
    if (music != GameMusic.None)
      _music = Create(_musicSamples[music], GameMusicTracks.Loops(music), _musicVolume);

    UpdateMusicPause();
  });

  public void SetMusicPaused(bool paused) => Queue(() =>
  {
    _pause.GamePaused = paused;
    UpdateMusicPause();
  });

  public void SetFlamethrowerActive(bool active) => Queue(() =>
  {
    _flameRequested = active;
    UpdateFlame();
  });

  public void SetEnabled(bool musicEnabled, bool effectsEnabled) => Queue(() =>
  {
    _pause.Enabled = musicEnabled;
    _effectsEnabled = effectsEnabled;
    if (!effectsEnabled)
      ClearVoices();

    UpdateFlame();
    UpdateMusicPause();
  });

  public void SetVolume(int musicVolume, int effectsVolume) => Queue(() =>
  {
    _musicVolume = Math.Clamp(musicVolume, 0, 100);
    _effectsVolume = Math.Clamp(effectsVolume, 0, 100);
    if (_music is not null)
      _music.Volume = _musicVolume / 100.0;
    if (_flame is not null)
      _flame.Volume = _effectsVolume / 100.0;

    foreach (var voice in _voices)
      voice.Player.Volume = _effectsVolume / 100.0;
  });

  public void SetSuspended(bool suspended) => Queue(() =>
  {
    _suspended = suspended;
    if (suspended)
      ClearVoices();

    UpdateFlame();
    UpdateMusicPause();
  });

  public void Stop() => Queue(StopCore);

  public void Dispose() => _worker.Dispose();

  private void Queue(Action action) => _worker.Enqueue(() => RunAudio(action));

  private static void RunAudio(Action action)
  {
    try
    {
      action();
    }

    catch (Exception exception)
    {
      System.Diagnostics.Debug.WriteLine(exception);
    }
  }

  private void Update()
  {
    for (int i = _voices.Count - 1; i >= 0; i--)
    {
      var voice = _voices[i];
      if (voice.Player.IsPlaying)
        continue;

      _pause.CompleteNotification(voice.Player);
      voice.Player.Dispose();
      _voices.RemoveAt(i);
      UpdateMusicPause();
    }
  }

  private void UpdateMusicPause()
  {
    if (_music is null)
      return;

    if (_suspended || _pause.ShouldPause)
    {
      if (_music.IsPlaying)
      {
        _music.Pause();
        _musicWasPaused = true;
      }
    }
    else if (!_musicStarted || _musicWasPaused)
    {
      _music.Play();
      _musicStarted = true;
      _musicWasPaused = false;
    }
  }

  private void UpdateFlame()
  {
    if (!_flameRequested || !_effectsEnabled || _suspended)
    {
      _flame?.Dispose();
      _flame = null;
    }
    else if (_flame is null)
    {
      _flame = Create(_effects[GameSound.Flamethrower], true, _effectsVolume);
      _flame?.Play();
    }
  }

  private void ClearVoices()
  {
    foreach (var voice in _voices)
      voice.Player.Dispose();

    _voices.Clear();
    _pause.ClearNotifications();
  }

  private void StopCore()
  {
    ClearVoices();
    _music?.Dispose();
    _flame?.Dispose();
    _music = _flame = null;
    _track = GameMusic.None;
    _flameRequested = false;
  }

  private static IAudioPlayer? Create(byte[] wave, bool loop, int volume)
  {
    try
    {
      var player = AudioManager.Current.CreatePlayer(new MemoryStream(wave, writable: false));
      player.Loop = loop;
      player.Volume = volume / 100.0;
      return player;
    }

    catch (Exception exception)
    {
      System.Diagnostics.Debug.WriteLine(exception);
      return null; // An unavailable audio device must not prevent offline play.
    }
  }

  private static byte[] Wave(short[] samples)
  {
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write("RIFF"u8);
    writer.Write(36 + samples.Length * 2);
    writer.Write("WAVEfmt "u8);
    writer.Write(16);
    writer.Write((short)1);
    writer.Write((short)1);
    writer.Write(GameSounds.SampleRate);
    writer.Write(GameSounds.SampleRate * 2);
    writer.Write((short)2);
    writer.Write((short)16);
    writer.Write("data"u8);
    writer.Write(samples.Length * 2);
    foreach (short sample in samples)
      writer.Write(sample);

    return stream.ToArray();
  }
}
