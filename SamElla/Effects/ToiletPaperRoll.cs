// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Effects;

public sealed class ToiletPaperRoll(double x, double y, int direction, int damage)
{
  public const int LifetimeTicks = 42;
  public const double Size = 0.6;
  public const double Speed = 20;
  public int AgeTicks { get; internal set; }
  public int Damage { get; } = damage;
  public int Direction { get; } = direction;
  public int FrameIndex => AgeTicks / 4 % 2;
  public double VelocityY { get; internal set; } = -3;
  public double X { get; internal set; } = x;
  public double Y { get; internal set; } = y;
}
