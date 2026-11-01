// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Mobile;

internal sealed class MobileSettings
{
  public bool LargeControls { get; set; }

  public static MobileSettings Load(string path)
  {
    var settings = new MobileSettings();
    try
    {
      foreach (string line in File.ReadAllLines(path))
      {
        string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0] == "Mobile.LargeControls" && bool.TryParse(parts[1], out bool enabled))
          settings.LargeControls = enabled;
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Existing installs and unavailable preferences retain standard-sized controls.
    }

    return settings;
  }

  public IEnumerable<string> ToLines() => [$"Mobile.LargeControls={LargeControls}"];
}

