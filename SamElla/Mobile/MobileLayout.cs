// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Mobile;

internal readonly record struct GameViewport(double X, double Y, double Width, double Height)
{
  public static GameViewport Fit(double width, double height)
  {
    double scale = Math.Min(width / PixelRenderer.Width, height / PixelRenderer.Height);

    // Fractional scaling fills the available display without stretching or cropping.
    double w = PixelRenderer.Width * scale, h = PixelRenderer.Height * scale;
    return new((width - w) / 2, (height - h) / 2, w, h);
  }
}

internal readonly record struct TouchRegion(int? Key, string Label, double X, double Y, double Width, double Height, string? Heading = null)
{
  public bool Contains(double x, double y) => x >= X && y >= Y && x < X + Width && y < Y + Height;
}

internal sealed class MobileLayout
{
  public MobileLayout(double width, double height, double density, bool menu, bool? canPause = null, bool largeControls = false)
  {
    double unit = Math.Min(Math.Max(1, density), Math.Min(width / 600, height / 300));
    if (largeControls)
      unit *= 1.5;

    double size = 48 * unit, gap = 6 * unit, bottom = height - 8 * unit;

    Viewport = GameViewport.Fit(width, height);

    double x = 12 * unit;
    double middleY = bottom - size * 2 - gap;
    double actionSize = 64 * unit;
    double middleWidth = 64 * unit, middleHeight = 30 * unit;
    double attackLeft = width - 12 * unit - actionSize * 2 - gap;
    double middleX = Math.Min(width / 2, attackLeft - middleWidth - gap);

    Buttons = [
      new(0x26, "↑", x + size + gap, middleY - size - gap, size, size),
      new(0x25, "←", x, middleY, size, size),
      new(0x27, "→", x + 2 * (size + gap), middleY, size, size),
      new(0x28, "↓", x + size + gap, bottom - size, size, size),
      new(menu ? 0x28 : null, "SELECT", middleX - gap / 2 - middleWidth, bottom - middleHeight, middleWidth, middleHeight),
      new((canPause ?? !menu) ? GameKeyboard.PauseKey : null, "START", middleX + gap / 2, bottom - middleHeight, middleWidth, middleHeight),
      new(menu ? 0x1B : 0x58, "ATTACK", attackLeft, bottom - actionSize, actionSize, actionSize, "I"),
      new(menu ? 0x0D : 0x20, "JUMP", width - 12 * unit - actionSize, bottom - actionSize, actionSize, actionSize, "○"),
    ];
  }

  public IReadOnlyList<TouchRegion> Buttons { get; }
  public GameViewport Viewport { get; }

  public int? KeyAt(double x, double y)
  {
    foreach (var button in Buttons)
      if (button.Contains(x, y))
        return button.Key;

    return null;
  }
}
