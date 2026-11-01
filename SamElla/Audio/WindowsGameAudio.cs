// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SamElla.Audio;

// Native audio resources belong to a dedicated worker. Device operations may
// block, so they must never run on the gameplay and input thread.
[SupportedOSPlatform("windows")]
internal sealed unsafe class WindowsGameAudio : IGameAudio
{
  private readonly MusicPauseState _musicPause = new();

  private readonly Dictionary<GameMusic, short[]> _musicSamples = Enum.GetValues<GameMusic>()
    .Where(music => music != GameMusic.None).ToDictionary(music => music, GameMusicTracks.Create);

  private readonly Dictionary<GameSound, short[]> _samples = Enum.GetValues<GameSound>()
    .ToDictionary(sound => sound, GameSounds.Create);

  private readonly List<Voice> _voices = [];

  private readonly AudioWorker _worker;

  private volatile bool _deathSoundPending;

  private volatile Voice? _deathVoice;

  private bool _disposed;

  private bool _effectsEnabled = true;

  private int _effectsVolume = AudioSettings.DefaultSoundEffectsVolume;

  private Voice? _flamethrower;

  private bool _flamethrowerRequested;

  private Voice? _music;

  private int _musicVolume = AudioSettings.DefaultMusicVolume;

  private bool _queuedEffectsEnabled = true;

  private int _queuedEffectsVolume = AudioSettings.DefaultSoundEffectsVolume;

  private bool _queuedFlamethrower;

  private GameMusic _queuedMusic;

  private bool _queuedMusicEnabled = true;

  private bool _queuedMusicPaused;

  private int _queuedMusicVolume = AudioSettings.DefaultMusicVolume;

  private GameMusic _requestedMusic;

  public WindowsGameAudio()
  {
    _worker = new AudioWorker(UpdateCore, StopCore);
  }

  public bool IsDeathSoundPlaying => _deathSoundPending || _deathVoice is not null;

  public void Dispose()
  {
    _worker.Dispose();
    _disposed = true;
  }

  public void Play(GameSound sound)
  {
    if (sound == GameSound.Died)
    {
      _deathSoundPending = true;
      ResetQueuedState();
    }

    _worker.Enqueue(() =>
    {
      try
      {
        PlayCore(sound);
      }
      finally
      {
        if (sound == GameSound.Died)
          _deathSoundPending = false;
      }
    });
  }

  public void SetEnabled(bool musicEnabled, bool effectsEnabled)
  {
    if (_queuedMusicEnabled == musicEnabled && _queuedEffectsEnabled == effectsEnabled)
      return;

    _queuedMusicEnabled = musicEnabled;
    _queuedEffectsEnabled = effectsEnabled;
    _worker.Enqueue(() => SetEnabledCore(musicEnabled, effectsEnabled));
  }

  public void SetFlamethrowerActive(bool active)
  {
    if (_queuedFlamethrower == active)
      return;

    _queuedFlamethrower = active;
    _worker.Enqueue(() => SetFlamethrowerActiveCore(active));
  }

  public void SetMusic(GameMusic music)
  {
    if (_queuedMusic == music)
      return;

    _queuedMusic = music;
    _worker.Enqueue(() => SetMusicCore(music));
  }

  public void SetMusicPaused(bool paused)
  {
    if (_queuedMusicPaused == paused)
      return;

    _queuedMusicPaused = paused;
    _worker.Enqueue(() => SetMusicPausedCore(paused));
  }

  public void SetVolume(int musicVolume, int effectsVolume)
  {
    musicVolume = Math.Clamp(musicVolume, 0, 100);
    effectsVolume = Math.Clamp(effectsVolume, 0, 100);
    if (_queuedMusicVolume == musicVolume && _queuedEffectsVolume == effectsVolume)
      return;

    _queuedMusicVolume = musicVolume;
    _queuedEffectsVolume = effectsVolume;
    _worker.Enqueue(() => SetVolumeCore(musicVolume, effectsVolume));
  }

  public void Stop()
  {
    ResetQueuedState();
    _worker.Enqueue(StopCore);
  }

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutClose(nint device);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutOpen(out nint device, uint deviceId, ref WaveFormat format,
    nuint callback, nuint instance, uint flags);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutPause(nint device);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutPrepareHeader(nint device, WaveHeader* header, uint size);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutReset(nint device);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutRestart(nint device);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutUnprepareHeader(nint device, WaveHeader* header, uint size);

  [DllImport("winmm.dll", ExactSpelling = true)]
  private static extern uint waveOutWrite(nint device, WaveHeader* header, uint size);

  private void ApplyMusicPause()
  {
    _music?.SetPaused(_musicPause.ShouldPause);
  }

  private Voice? CreateVoice(GameSound sound, bool loop = false) => CreateVoice(_samples[sound], loop, volume: _effectsVolume);

  private Voice? CreateVoice(short[] samples, bool loop = false, bool paused = false, int volume = 100)
  {
    var format = new WaveFormat
    {
      FormatTag = 1,
      Channels = 1,
      SamplesPerSecond = GameSounds.SampleRate,
      AverageBytesPerSecond = GameSounds.SampleRate * 2,
      BlockAlign = 2,
      BitsPerSample = 16,
    };

    // Audio is optional: missing/disabled devices must not prevent playing the game.
    if (waveOutOpen(out nint device, uint.MaxValue, ref format, 0, 0, 0) != 0)
      return null;

    var voice = new Voice(device, samples, loop);
    voice.SetVolume(volume);
    if (paused)
      voice.SetPaused(true);

    if (!voice.Start())
    {
      voice.Dispose();
      return null;
    }

    return voice;
  }

  private void PlayCore(GameSound sound)
  {
    if (_disposed)
      return;

    UpdateCore();
    if (sound == GameSound.Died)
      StopCore();

    if (!_effectsEnabled || _voices.Count >= 8)
      return;

    bool notification = sound == GameSound.PowerUp;
    if (notification)
      _music?.SetPaused(true);

    var voice = CreateVoice(sound);
    if (voice is not null)
    {
      _voices.Add(voice);
      if (sound == GameSound.Died)
        _deathVoice = voice;
      if (notification)
        _musicPause.StartNotification(voice);
    }
    else if (notification)
      ApplyMusicPause();
  }

  private void ResetQueuedState()
  {
    _queuedMusic = GameMusic.None;
    _queuedFlamethrower = false;
  }

  private void SetEnabledCore(bool musicEnabled, bool effectsEnabled)
  {
    _musicPause.Enabled = musicEnabled;
    _effectsEnabled = effectsEnabled;
    if (!_effectsEnabled)
    {
      foreach (var voice in _voices)
        voice.Dispose();

      _voices.Clear();
      _deathVoice = null;
      _musicPause.ClearNotifications();
      _flamethrower?.Dispose();
      _flamethrower = null;
    }
    else if (_flamethrowerRequested && _flamethrower is null)
      _flamethrower = CreateVoice(GameSound.Flamethrower, loop: true);

    ApplyMusicPause();
  }

  private void SetFlamethrowerActiveCore(bool active)
  {
    if (_disposed || active == _flamethrowerRequested)
      return;

    _flamethrowerRequested = active;
    if (active && _effectsEnabled)
      _flamethrower = CreateVoice(GameSound.Flamethrower, loop: true);
    else
    {
      _flamethrower?.Dispose();
      _flamethrower = null;
    }
  }

  private void SetMusicCore(GameMusic music)
  {
    if (_disposed || music == _requestedMusic)
      return;

    _music?.Dispose();
    _music = null;
    _requestedMusic = music;
    if (music != GameMusic.None)
    {
      _music = CreateVoice(_musicSamples[music], GameMusicTracks.Loops(music),
        paused: _musicPause.ShouldPause, volume: _musicVolume);
    }
  }

  private void SetMusicPausedCore(bool paused)
  {
    if (_disposed || paused == _musicPause.GamePaused)
      return;

    _musicPause.GamePaused = paused;
    ApplyMusicPause();
  }

  private void SetVolumeCore(int musicVolume, int effectsVolume)
  {
    _musicVolume = musicVolume;
    _effectsVolume = effectsVolume;
    _music?.SetVolume(musicVolume);
    _flamethrower?.SetVolume(effectsVolume);
    foreach (var voice in _voices)
      voice.SetVolume(effectsVolume);
  }

  private void StopCore()
  {
    SetFlamethrowerActiveCore(false);
    SetMusicCore(GameMusic.None);
    foreach (var voice in _voices)
      voice.Dispose();
    _voices.Clear();
    _deathVoice = null;
    _musicPause.ClearNotifications();
  }

  private void UpdateCore()
  {
    _music?.Update();
    _flamethrower?.Update();
    if (_music is not null && _music.Completed)
    {
      _music.Dispose();
      _music = null;
      // Keep the requested track so Game Over cannot restart after completion.
    }

    for (int i = _voices.Count - 1; i >= 0; i--)
    {
      _voices[i].Update();
      if (!_voices[i].Completed)
        continue;

      if (ReferenceEquals(_deathVoice, _voices[i]))
        _deathVoice = null;

      bool notificationFinished = _musicPause.CompleteNotification(_voices[i]);
      _voices[i].Dispose();
      _voices.RemoveAt(i);
      if (notificationFinished)
        ApplyMusicPause();
    }
  }

  [StructLayout(LayoutKind.Sequential, Pack = 2)]
  private struct WaveFormat
  {
    public ushort FormatTag, Channels;
    public uint SamplesPerSecond, AverageBytesPerSecond;
    public ushort BlockAlign, BitsPerSample, ExtraSize;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct WaveHeader
  {
    public nint Data;
    public uint Length, BytesRecorded;
    public nuint User;
    public uint Flags, Loops;
    public nint Next;
    public nuint Reserved;
  }

  private sealed class Voice : IDisposable
  {
    // Three 20 ms buffers keep playback continuous and volume changes responsive.
    private const int BufferCount = 3;

    private const int SamplesPerBuffer = GameSounds.SampleRate / 50;

    private readonly nint _device;
    private readonly WaveHeader* _headers;
    private readonly SamplePlayback _playback;
    private readonly bool[] _queued = new bool[BufferCount];
    private bool _failed;

    public Voice(nint device, short[] samples, bool loop)
    {
      _device = device;
      _playback = new SamplePlayback(samples, loop);
      _headers = (WaveHeader*)NativeMemory.AllocZeroed((nuint)(BufferCount * sizeof(WaveHeader)));
      for (int i = 0; i < BufferCount; i++)
      {
        _headers[i].Length = SamplesPerBuffer * sizeof(short);
        _headers[i].Data = (nint)NativeMemory.Alloc(_headers[i].Length);
      }
    }

    public bool Completed => _failed || (!_playback.HasRemaining && !_queued.Any(queued => queued));

    public void Dispose()
    {
      waveOutReset(_device);
      for (int i = 0; i < BufferCount; i++)
      {
        if ((_headers[i].Flags & 2) != 0) // WHDR_PREPARED
          waveOutUnprepareHeader(_device, &_headers[i], (uint)sizeof(WaveHeader));

        NativeMemory.Free((void*)_headers[i].Data);
      }

      waveOutClose(_device);
      NativeMemory.Free(_headers);
    }

    public void SetPaused(bool paused)
    {
      if (paused)
        waveOutPause(_device);
      else
        waveOutRestart(_device);
    }

    public void SetVolume(int volume)
    {
      _playback.Volume = volume;
    }

    public bool Start()
    {
      for (int i = 0; i < BufferCount; i++)
      {
        if (waveOutPrepareHeader(_device, &_headers[i], (uint)sizeof(WaveHeader)) != 0)
          return false;
      }

      Update();
      return !_failed && !Completed;
    }

    public void Update()
    {
      if (_failed)
        return;

      for (int i = 0; i < BufferCount; i++)
      {
        WaveHeader* header = &_headers[i];
        if (_queued[i] && (header->Flags & 1) == 0) // WHDR_DONE
          continue;

        _queued[i] = false;
        // Never alter a buffer while the audio device is still playing it.
        int count = _playback.Read(new Span<short>((void*)header->Data, SamplesPerBuffer));
        if (count == 0)
          continue;

        header->Length = (uint)(count * sizeof(short));
        if (waveOutWrite(_device, header, (uint)sizeof(WaveHeader)) != 0)
        {
          _failed = true;
          return;
        }

        _queued[i] = true;
      }
    }
  }
}
