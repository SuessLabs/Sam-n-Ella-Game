// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Effects;

public sealed record ShotgunBlast(double X, double Y, int Direction, double Length, int AgeTicks = 0)
{
  public const int DurationTicks = 12;
  public int FrameIndex => AgeTicks / 6;
}
