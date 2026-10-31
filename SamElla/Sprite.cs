using System.Globalization;

namespace SamElla;

internal sealed class Sprite
{
  private readonly uint[] _pixels;

  private Sprite(int width, int height, uint[] pixels)
  {
    Width = width;
    Height = height;
    _pixels = pixels;
  }

  public int Height { get; }

  public int Width { get; }

  public uint Pixel(int x, int y) => _pixels[(y * Width) + x];

  public static Sprite Load(string path, IReadOnlyDictionary<char, uint> palette) =>
    Parse(Path.GetFileName(path), File.ReadAllLines(path), palette);

  public static IReadOnlyDictionary<char, uint> LoadPalette(string path)
  {
    var palette = new Dictionary<char, uint> { ['.'] = 0 };

    foreach (var line in DataLines(File.ReadAllLines(path)))
    {
      var parts = line.Split('=', StringSplitOptions.TrimEntries);

      if (parts.Length != 2 || parts[0].Length != 1 || parts[0] == "." || parts[1].Length != 6
        || !uint.TryParse(parts[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint color)
        || !palette.TryAdd(parts[0][0], 0xFF000000 | color))
        throw new InvalidDataException($"{path}: invalid or duplicate palette entry '{line}'. Use A=RRGGBB; '.' is reserved for transparency.");
    }

    return palette;
  }

  public static Sprite Parse(string name, string[] lines, IReadOnlyDictionary<char, uint> palette)
  {
    var data = DataLines(lines).ToArray();
    var size = data.FirstOrDefault()?.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    if (size is not { Length: 2 } || !int.TryParse(size[0], out int width)
      || !int.TryParse(size[1], out int height) || width is < 1 or > 128 || height is < 1 or > 128
      || data.Length != height + 1)
      throw new InvalidDataException($"{name}: expected 'width height' followed by that many pixel rows (maximum 128 x 128).");

    var pixels = new uint[width * height];

    for (int y = 0; y < height; y++)
    {
      if (data[y + 1].Length != width)
        throw new InvalidDataException($"{name}: pixel row {y + 1} must contain {width} symbols.");

      for (int x = 0; x < width; x++)
      {
        if (!palette.TryGetValue(data[y + 1][x], out uint pixel))
          throw new InvalidDataException($"{name}: unknown palette symbol at ({x + 1}, {y + 1}).");

        pixels[(y * width) + x] = pixel;
      }
    }

    return new Sprite(width, height, pixels);
  }

  private static IEnumerable<string> DataLines(string[] lines) =>
    lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith(';'));
}
