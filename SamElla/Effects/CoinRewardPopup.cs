// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Items;

public readonly record struct CoinRewardPopup(int X, int Y, int AgeTicks = 0)
{
  // Game.Step advances at 60 Hz, giving the popup one second of game time.
  public const int DurationTicks = 60;
}
