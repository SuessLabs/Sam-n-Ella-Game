// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Audio;

internal interface IGameAudio : IDisposable
{
  bool IsDeathSoundPlaying { get; }

  void Play(GameSound sound);

  void SetEnabled(bool musicEnabled, bool effectsEnabled);

  void SetFlamethrowerActive(bool active);

  void SetMusic(GameMusic music);

  void SetMusicPaused(bool paused);

  void SetVolume(int musicVolume, int effectsVolume);

  void Stop();
}
