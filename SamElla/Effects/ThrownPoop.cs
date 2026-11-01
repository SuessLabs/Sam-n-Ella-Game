// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Effects;

public sealed class ThrownPoop(double x, double y, double velocityX)
{
  public const double Height = 0.7;

  public double VelocityX { get; } = velocityX;
  public double VelocityY { get; internal set; } = -9;
  public double X { get; internal set; } = x;
  public double Y { get; internal set; } = y;
  internal int AgeTicks { get; set; }
}
