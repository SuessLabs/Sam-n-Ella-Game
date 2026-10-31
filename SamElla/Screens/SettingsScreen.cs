using SamElla.Audio;
using SamElla.Updater;

namespace SamElla.Screens;

internal sealed class SettingsScreen(AudioSettings audio, UpdateSettings? updates = null)
{
  private const int RowCount = 6;
  private const int VolumeStep = 5;

  public AudioSettings Audio { get; } = audio;

  public UpdateSettings Updates { get; } = updates ?? new();

  public bool IsActive { get; private set; }

  public int SelectedIndex { get; private set; }

  public void Open()
  {
    SelectedIndex = 0;
    IsActive = true;
  }

  public void HandleInput(IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!IsActive || !windowActive)
      return;

    foreach (int code in keyPresses)
    {
      switch (code)
      {
        case 0x1B: // Escape
        case 0x70: // F1
        case 0x53: // S
          IsActive = false;
          return;

        case 0x26: // Up
          SelectedIndex = (SelectedIndex + RowCount - 1) % RowCount;
          break;

        case 0x28: // Down
          SelectedIndex = (SelectedIndex + 1) % RowCount;
          break;

        case 0x25: // Left: off / decrease volume
        case 0x27: // Right: on / increase volume
        case 0x0D: // Enter
        case 0x20: // Space
          if (SelectedIndex == 5)
          {
            if (code is 0x0D or 0x20)
            {
              IsActive = false;
              return;
            }

            break;
          }

          if (SelectedIndex is 1 or 3)
          {
            if (code is 0x25 or 0x27)
            {
              int change = code == 0x25 ? -VolumeStep : VolumeStep;
              if (SelectedIndex == 1)
                Audio.MusicVolume += change;
              else
                Audio.SoundEffectsVolume += change;
            }

            break;
          }

          bool enabled = SelectedIndex == 4 ? Updates.IncludePrereleases
            : SelectedIndex == 0 ? Audio.MusicEnabled : Audio.SoundEffectsEnabled;
          enabled = code == 0x25 ? false : code == 0x27 ? true : !enabled;
          if (SelectedIndex == 4)
            Updates.IncludePrereleases = enabled;
          else if (SelectedIndex == 0)
            Audio.MusicEnabled = enabled;
          else
            Audio.SoundEffectsEnabled = enabled;

          break;
      }
    }
  }
}
