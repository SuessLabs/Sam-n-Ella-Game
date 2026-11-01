// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace SamElla;

// CPU-rendered ARGB pixels. No operating-system APIs or graphics packages are used here.
internal sealed class PixelBuffer(int width, int height)
{
  public int Height { get; } = height;

  public uint[] Pixels { get; } = new uint[width * height];

  public int Width { get; } = width;

  public void Clear(uint color) => Array.Fill(Pixels, color);

  public void OverlayBlack(byte opacity)
  {
    uint remaining = (uint)(255 - opacity);
    for (int i = 0; i < Pixels.Length; i++)
    {
      uint pixel = Pixels[i];
      uint red = ((pixel >> 16) & 255) * remaining / 255;
      uint green = ((pixel >> 8) & 255) * remaining / 255;
      uint blue = (pixel & 255) * remaining / 255;
      Pixels[i] = (pixel & 0xFF000000) | (red << 16) | (green << 8) | blue;
    }
  }

  public void Draw(Sprite sprite, int x, int y, bool flip = false, int clipBottom = int.MaxValue)
  {
    int bottom = Math.Min(Height, clipBottom);
    if (x >= Width || y >= bottom || x <= -sprite.Width || y <= -sprite.Height)
      return;

    for (int sy = Math.Max(0, -y); sy < Math.Min(sprite.Height, bottom - y); sy++)
    {
      for (int sx = Math.Max(0, -x); sx < Math.Min(sprite.Width, Width - x); sx++)
      {
        uint color = sprite.Pixel(flip ? sprite.Width - sx - 1 : sx, sy);

        if ((color >> 24) != 0)
          Pixels[((y + sy) * Width) + x + sx] = color;
      }
    }
  }

  public void Fill(int x, int y, int width, int height, uint color)
  {
    int left = Math.Clamp(x, 0, Width);
    int right = Math.Clamp(x + width, 0, Width);

    for (int row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
      Array.Fill(Pixels, color, (row * Width) + left, Math.Max(0, right - left));
  }

  public void DrawTranslucent(Sprite sprite, int x, int y, byte opacity)
  {
    if (x >= Width || y >= Height || x <= -sprite.Width || y <= -sprite.Height)
      return;

    for (int sy = Math.Max(0, -y); sy < Math.Min(sprite.Height, Height - y); sy++)
    {
      for (int sx = Math.Max(0, -x); sx < Math.Min(sprite.Width, Width - x); sx++)
      {
        uint source = sprite.Pixel(sx, sy);
        uint alpha = (source >> 24) * opacity / 255;
        if (alpha == 0)
          continue;

        int index = ((y + sy) * Width) + x + sx;
        uint target = Pixels[index];
        uint inverse = 255 - alpha;
        uint red = ((((source >> 16) & 255) * alpha) + (((target >> 16) & 255) * inverse)) / 255;
        uint green = ((((source >> 8) & 255) * alpha) + (((target >> 8) & 255) * inverse)) / 255;
        uint blue = (((source & 255) * alpha) + ((target & 255) * inverse)) / 255;
        Pixels[index] = 0xFF000000 | (red << 16) | (green << 8) | blue;
      }
    }
  }

  public void FillTranslucent(int x, int y, int width, int height, uint color, byte opacity)
  {
    int left = Math.Clamp(x, 0, Width);
    int right = Math.Clamp(x + width, 0, Width);
    uint inverse = (uint)(255 - opacity);
    for (int row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
    {
      for (int column = left; column < right; column++)
      {
        int index = (row * Width) + column;
        uint target = Pixels[index];
        uint red = ((((color >> 16) & 255) * opacity) + (((target >> 16) & 255) * inverse)) / 255;
        uint green = ((((color >> 8) & 255) * opacity) + (((target >> 8) & 255) * inverse)) / 255;
        uint blue = (((color & 255) * opacity) + ((target & 255) * inverse)) / 255;
        Pixels[index] = 0xFF000000 | (red << 16) | (green << 8) | blue;
      }
    }
  }

  // A minimal lossless PNG exporter, using only the .NET compression library.
  public void SavePng(string path)
  {
    using var output = File.Create(path);
    output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var header = new byte[13];
    BinaryPrimitives.WriteInt32BigEndian(header, Width);
    BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Height);
    header[8] = 8;
    header[9] = 6;
    WriteChunk(output, "IHDR", header);

    using var compressed = new MemoryStream();

    using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
    {
      var row = new byte[1 + (Width * 4)];

      for (int y = 0; y < Height; y++)
      {
        for (int x = 0; x < Width; x++)
        {
          uint pixel = Pixels[(y * Width) + x];
          int offset = 1 + (x * 4);
          row[offset] = (byte)(pixel >> 16);
          row[offset + 1] = (byte)(pixel >> 8);
          row[offset + 2] = (byte)pixel;
          row[offset + 3] = (byte)(pixel >> 24);
        }

        zlib.Write(row);
      }
    }

    WriteChunk(output, "IDAT", compressed.ToArray());
    WriteChunk(output, "IEND", []);
  }

  private static void WriteChunk(Stream output, string name, byte[] data)
  {
    byte[] type = Encoding.ASCII.GetBytes(name);
    Span<byte> number = stackalloc byte[4];
    BinaryPrimitives.WriteInt32BigEndian(number, data.Length);
    output.Write(number);
    output.Write(type);
    output.Write(data);
    uint crc = uint.MaxValue;

    foreach (byte value in type.Concat(data))
    {
      crc ^= value;

      for (int bit = 0; bit < 8; bit++)
        crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xEDB88320);
    }

    BinaryPrimitives.WriteUInt32BigEndian(number, ~crc);
    output.Write(number);
  }
}
