// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Effects;

public readonly record struct BlockBreakEffect(int X, int Y, int AgeTicks = 0)
{
  public const int FrameDurationTicks = 15;

  public const int DurationTicks = FrameDurationTicks * 2;

  public int FrameIndex => AgeTicks / FrameDurationTicks;
}
