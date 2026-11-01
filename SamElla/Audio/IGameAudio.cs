// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Audio;

internal interface IGameAudio : IDisposable
{
  void Play(GameSound sound);

  void SetMusic(GameMusic music);

  void SetMusicPaused(bool paused);

  void SetFlamethrowerActive(bool active);

  void SetEnabled(bool musicEnabled, bool effectsEnabled);

  void SetVolume(int musicVolume, int effectsVolume);

  void Stop();
}
