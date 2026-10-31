using System.Security.Cryptography;

namespace SamElla;

internal static class UserContent
{
  // Copy defaults forward only when the user has not changed the previous version.
  // Custom files and edits stay outside Velopack's replaceable installation directory.
  public static void Synchronize(string bundledRoot, string userRoot)
  {
    Directory.CreateDirectory(userRoot);
    string manifestPath = Path.Combine(userRoot, "bundled-content.txt");
    var previous = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (File.Exists(manifestPath))
    {
      foreach (string line in File.ReadLines(manifestPath))
      {
        string[] parts = line.Split('\t', 2);
        if (parts.Length == 2)
          previous[parts[0]] = parts[1];
      }
    }

    var manifest = new List<string>();
    foreach (string folder in new[] { "Assets", "Levels" })
    {
      string sourceFolder = Path.Combine(bundledRoot, folder);
      foreach (string source in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
      {
        string relative = Path.GetRelativePath(bundledRoot, source);
        string destination = Path.Combine(userRoot, relative);
        string sourceHash = Hash(source);
        bool exists = File.Exists(destination);
        if (!exists && !previous.ContainsKey(relative)
          || exists && previous.TryGetValue(relative, out string? oldHash) && Hash(destination) == oldHash)
        {
          Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
          File.Copy(source, destination, true);
        }

        manifest.Add(relative + "\t" + sourceHash);
      }
    }

    // Only remove retired bundled files if they still match the previously shipped default.
    var current = manifest.Select(line => line.Split('\t', 2)[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var (relative, oldHash) in previous)
    {
      string destination = Path.GetFullPath(Path.Combine(userRoot, relative));
      string root = Path.GetFullPath(userRoot) + Path.DirectorySeparatorChar;
      if (!current.Contains(relative) && destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)
        && File.Exists(destination) && Hash(destination) == oldHash)
        File.Delete(destination);
    }

    string temporary = manifestPath + ".tmp";
    File.WriteAllLines(temporary, manifest);
    File.Move(temporary, manifestPath, true);
  }

  private static string Hash(string path)
  {
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
  }
}
