// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla;

// A tiny 3 x 5 bitmap font keeps HUD text sharp at the same scale as the sprites.
internal static class PixelFont
{
  private static readonly Dictionary<char, string> Glyphs = new()
  {
    ['A'] = "010101111101101",
    ['B'] = "110101110101110",
    ['C'] = "011100100100011",
    ['D'] = "110101101101110",
    ['E'] = "111100110100111",
    ['F'] = "111100110100100",
    ['G'] = "011100101101011",
    ['H'] = "101101111101101",
    ['I'] = "111010010010111",
    ['J'] = "001001001101010",
    ['K'] = "101101110101101",
    ['L'] = "100100100100111",
    ['M'] = "101111111101101",
    ['N'] = "101111111111101",
    ['O'] = "010101101101010",
    ['P'] = "110101110100100",
    ['Q'] = "010101101111011",
    ['R'] = "110101110101101",
    ['S'] = "011100010001110",
    ['T'] = "111010010010010",
    ['U'] = "101101101101111",
    ['V'] = "101101101101010",
    ['v'] = "000000101101010",
    ['a'] = "000000011101111",
    ['d'] = "001001011101011",
    ['e'] = "000010101110011",
    ['i'] = "010000010010010",
    ['m'] = "000000110111101",
    ['s'] = "000000011010110",
    ['t'] = "010110010010011",
    ['W'] = "101101111111101",
    ['X'] = "101101010101101",
    ['Y'] = "101101010010010",
    ['Z'] = "111001010100111",
    ['0'] = "111101101101111",
    ['1'] = "010110010010111",
    ['2'] = "110001010100111",
    ['3'] = "110001010001110",
    ['4'] = "101101111001001",
    ['5'] = "111100110001110",
    ['6'] = "011100111101111",
    ['7'] = "111001010010010",
    ['8'] = "111101111101111",
    ['9'] = "111101111001110",
    [':'] = "000010000010000",
    ['-'] = "000000111000000",
    ['/'] = "001001010100100",
    ['!'] = "010010010000010",
    ['.'] = "000000000000010",
    [','] = "000000000010100",
    ['©'] = "111101101101111",
    ['>'] = "100010001010100",
    ['?'] = "110001010000010",
    ['+'] = "000010111010000",
    ['%'] = "101001010100101",
    ['\''] = "010010000000000",
  };

  public static void Draw(PixelBuffer frame, string text, int x, int y, uint color, int scale = 1,
    int clipTop = 0, int clipBottom = int.MaxValue, bool preserveCase = false)
  {
    foreach (char character in preserveCase ? text : text.ToUpperInvariant())
    {
      if (Glyphs.TryGetValue(character, out string? bits))
      {
        for (int row = 0; row < 5; row++)
        {
          for (int col = 0; col < 3; col++)
          {
            if (bits[(row * 3) + col] == '1')
            {
              int top = Math.Max(y + (row * scale), clipTop);
              int bottom = Math.Min(y + ((row + 1) * scale), clipBottom);
              if (bottom > top)
                frame.Fill(x + (col * scale), top, scale, bottom - top, color);
            }
          }
        }
      }

      x += 4 * scale;
    }
  }
}
