// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Effects;

public sealed class GrowthHeart(double x, double y, int direction, int sourceX = -1, int sourceY = -1,
  bool isFlamethrower = false, bool isBoomStick = false, bool isChainWhip = false, bool isToiletPaper = false)
{
  public const int BeatCycleTicks = 36;
  public const double Speed = 4;
  public int AgeTicks { get; internal set; }
  public int Direction { get; internal set; } = direction;

  // A quick double beat followed by a longer rest.
  public int FrameIndex => AgeTicks % BeatCycleTicks is < 6 or >= 12 and < 18 ? 1 : 0;

  public bool IsBoomStick { get; } = isBoomStick;
  public bool IsChainWhip { get; } = isChainWhip;
  public bool IsFlamethrower { get; } = isFlamethrower;
  public bool IsToiletPaper { get; } = isToiletPaper;
  public double VelocityY { get; internal set; } = -6;
  public double X { get; internal set; } = x;
  public double Y { get; internal set; } = y;
  internal int SourceX { get; } = sourceX;
  internal int SourceY { get; } = sourceY;
}
