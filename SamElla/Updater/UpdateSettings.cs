// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Updater;

internal sealed class UpdateSettings
{
  public bool IncludePrereleases { get; set; }

  public static UpdateSettings Load(string path)
  {
    var settings = new UpdateSettings();
    try
    {
      foreach (string line in File.ReadAllLines(path))
      {
        string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && parts[0] == nameof(IncludePrereleases)
          && bool.TryParse(parts[1], out bool enabled))
          settings.IncludePrereleases = enabled;
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      // Missing or unreadable preferences keep stable updates enabled.
    }

    return settings;
  }

  public IEnumerable<string> ToLines() => [$"{nameof(IncludePrereleases)}={IncludePrereleases}"];
}
