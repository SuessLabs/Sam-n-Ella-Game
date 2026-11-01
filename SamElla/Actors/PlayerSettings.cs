// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Actors;

public sealed record PlayerSettings
{
  public PlayerSettings(double speed, int powerLevel, double hopVelocity)
  {
    if (!double.IsFinite(speed) || speed is < 0 or > 30)
      throw new ArgumentOutOfRangeException(nameof(speed), "Player speed must be between 0 and 30 tiles per second.");

    if (powerLevel is < 1 or > 10)
      throw new ArgumentOutOfRangeException(nameof(powerLevel), "Player power level must be between 1 and 10.");

    if (!double.IsFinite(hopVelocity) || hopVelocity is < 0 or > 20)
      throw new ArgumentOutOfRangeException(nameof(hopVelocity), "Player hop velocity must be between 0 and 20.");

    Speed = speed;
    PowerLevel = powerLevel;
    HopVelocity = hopVelocity;
  }

  public double Speed { get; }

  public int PowerLevel { get; }

  public double HopVelocity { get; }
}
