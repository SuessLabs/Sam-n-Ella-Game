// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace SamElla.Audio;

internal sealed class AudioSettings
{
  public const int DefaultMusicVolume = 30;
  public const int DefaultSoundEffectsVolume = 50;

  private int _musicVolume = DefaultMusicVolume;
  private int _soundEffectsVolume = DefaultSoundEffectsVolume;

  public bool MusicEnabled { get; set; } = true;

  public int MusicVolume
  {
    get => _musicVolume;
    set => _musicVolume = Math.Clamp(value, 0, 100);
  }

  public bool SoundEffectsEnabled { get; set; } = true;

  public int SoundEffectsVolume
  {
    get => _soundEffectsVolume;
    set => _soundEffectsVolume = Math.Clamp(value, 0, 100);
  }

  public static AudioSettings Load(string path)
  {
    var settings = new AudioSettings();

    try
    {
      foreach (string line in File.ReadAllLines(path))
      {
        string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
          continue;

        switch (parts[0])
        {
          case nameof(MusicEnabled) when bool.TryParse(parts[1], out bool musicEnabled):
            settings.MusicEnabled = musicEnabled;
            break;

          case nameof(SoundEffectsEnabled) when bool.TryParse(parts[1], out bool effectsEnabled):
            settings.SoundEffectsEnabled = effectsEnabled;
            break;

          case nameof(MusicVolume) when int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int musicVolume):
            settings.MusicVolume = musicVolume;
            break;

          case nameof(SoundEffectsVolume) when int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int effectsVolume):
            settings.SoundEffectsVolume = effectsVolume;
            break;
        }
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Missing or unavailable preferences fall back to audible defaults.
    }

    return settings;
  }

  public void Save(string path)
  {
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
      File.WriteAllLines(path, ToLines());
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Preferences still apply to this session if the file cannot be written.
    }
  }

  public IEnumerable<string> ToLines() =>
  [
    $"{nameof(MusicEnabled)}={MusicEnabled}",
    $"{nameof(MusicVolume)}={MusicVolume.ToString(CultureInfo.InvariantCulture)}",
    $"{nameof(SoundEffectsEnabled)}={SoundEffectsEnabled}",
    $"{nameof(SoundEffectsVolume)}={SoundEffectsVolume.ToString(CultureInfo.InvariantCulture)}",
  ];
}
