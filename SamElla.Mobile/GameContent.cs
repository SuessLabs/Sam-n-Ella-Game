namespace SamElla.Mobile;

internal static class GameContent
{
  public static async Task<string> PrepareAsync()
  {
    string root = Path.Combine(FileSystem.AppDataDirectory, "Content");
    using var manifest = new StreamReader(await FileSystem.OpenAppPackageFileAsync("game-content.txt"));
    while (await manifest.ReadLineAsync() is string line)
    {
      string relative = line.Trim().Replace('\\', '/');
      if (relative.Length == 0)
        continue;

      string target = Path.GetFullPath(Path.Combine(root, relative));
      if (!target.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        throw new InvalidDataException("Invalid bundled content path.");

      Directory.CreateDirectory(Path.GetDirectoryName(target)!);
      using var source = await FileSystem.OpenAppPackageFileAsync("Game/" + relative);
      using var destination = File.Create(target);
      await source.CopyToAsync(destination);
    }

    return root;
  }
}
