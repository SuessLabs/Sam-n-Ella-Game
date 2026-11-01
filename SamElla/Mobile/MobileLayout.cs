// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Mobile;

internal readonly record struct GameViewport(double X, double Y, double Width, double Height)
{
  public static GameViewport Fit(double width, double height)
  {
    double scale = Math.Min(width / PixelRenderer.Width, height / PixelRenderer.Height);
    // Whole physical-pixel scaling keeps pixel art sharp; tiny windows can scale down.
    if (scale >= 1)
      scale = Math.Floor(scale);

    double w = PixelRenderer.Width * scale, h = PixelRenderer.Height * scale;
    return new((width - w) / 2, (height - h) / 2, w, h);
  }
}

internal readonly record struct TouchRegion(int Key, string Label, double X, double Y, double Width, double Height)
{
  public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

internal sealed class MobileLayout
{
  public GameViewport Viewport { get; }
  public IReadOnlyList<TouchRegion> Buttons { get; }

  public MobileLayout(double width, double height, double density, bool menu, bool canRestart = false)
  {
    double unit = Math.Min(Math.Max(1, density), Math.Min(width / 600, height / 300));
    double size = 48 * unit, gap = 6 * unit, bottom = height - 8 * unit;
    double controlsHeight = size * 2 + gap + 16 * unit;
    Viewport = GameViewport.Fit(width, Math.Max(1, height - controlsHeight));
    double x = 12 * unit, y = bottom - size * 2 - gap;
    var buttons = new List<TouchRegion>
    {
      new(0x25, "LEFT", x, y + size + gap, size, size),
      new(0x27, "RIGHT", x + 2 * (size + gap), y + size + gap, size, size),
      new(0x28, "DOWN", x + size + gap, y + size + gap, size, size),
    };
    if (menu)
    {
      buttons.Add(new(0x26, "UP", x + size + gap, y, size, size));
      buttons.Add(new(0x0D, "OK", width - 12 * unit - size * 2 - gap, y + size + gap, size * 2 + gap, size));
      buttons.Add(new(0x1B, "BACK", width - 12 * unit - size * 2 - gap, y, size * 2 + gap, size));
      if (canRestart)
        buttons.Add(new(0x52, "RESTART", width / 2 - size, y + size + gap, size * 2, size));
    }
    else
    {
      buttons.Add(new(0x20, "JUMP", width - 12 * unit - size, y, size, size * 2 + gap));
      buttons.Add(new(0x58, "ATTACK", width - 12 * unit - size * 2 - gap, y + size + gap, size, size));
      buttons.Add(new(0x0D, "PAUSE", width - 12 * unit - size * 2 - gap, y, size, size));
    }

    Buttons = buttons;
  }

  public int? KeyAt(double x, double y)
  {
    foreach (var button in Buttons)
      if (button.Contains(x, y))
        return button.Key;

    return null;
  }
}
