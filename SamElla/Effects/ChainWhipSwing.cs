// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Effects;

public sealed record ChainWhipSwing(double X, double Y, int Direction, double Length, int AgeTicks)
{
  public const int DurationTicks = 24;
  public const double Range = 3;
  public int FrameIndex => AgeTicks / 6;
}
