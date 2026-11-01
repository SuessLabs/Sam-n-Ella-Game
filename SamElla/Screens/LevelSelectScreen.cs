// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Screens;

internal sealed record LevelSelectEntry(int Number, string Name, BackgroundTheme Theme)
{
  public string DisplayNumber { get; init; } = Number.ToString();
}

internal sealed class LevelSelectScreen
{
  public const int PageSize = 8;

  public LevelSelectScreen(IReadOnlyList<string> files)
  {
    if (files.Count == 0)
      throw new ArgumentException("Level selection requires at least one level.", nameof(files));

    Levels = files.Select((file, index) =>
    {
      var level = Level.Load(file);
      return new LevelSelectEntry(index + 1, level.DisplayName, level.Background)
      {
        DisplayNumber = level.SectionLevelNumber ?? (index + 1).ToString(),
      };

    }).ToArray();
  }

  public IReadOnlyList<LevelSelectEntry> Levels { get; }
  public bool IsActive { get; private set; } = true;
  public bool Confirmed { get; private set; }
  public int SelectedIndex { get; private set; }
  public int Page => SelectedIndex / PageSize;
  public int PageCount => (Levels.Count + PageSize - 1) / PageSize;

  public static string[] Discover(string directory) => Directory.GetFiles(directory, "*.txt")
    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray();

  public void HandleInput(IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!IsActive || !windowActive)
      return;

    foreach (int code in keyPresses)
    {
      switch (code)
      {
        case 0x1B:
          IsActive = false;
          return;

        case 0x0D:
          Confirmed = true;
          IsActive = false;
          return;

        case 0x26:
        case 0x57:
          SelectedIndex = Math.Max(0, SelectedIndex - 1);
          break;

        case 0x28:
        case 0x53:
          SelectedIndex = Math.Min(Levels.Count - 1, SelectedIndex + 1);
          break;

        case 0x25:
        case 0x41:
        case 0x21:
          ChangePage(-1);
          break;

        case 0x27:
        case 0x44:
        case 0x22:
          ChangePage(1);
          break;
      }
    }
  }

  private void ChangePage(int direction)
  {
    int page = Math.Clamp(Page + direction, 0, PageCount - 1);
    SelectedIndex = Math.Min(Levels.Count - 1, page * PageSize + SelectedIndex % PageSize);
  }
}
