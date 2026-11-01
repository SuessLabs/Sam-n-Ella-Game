// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Actors;

public sealed record EnemySettings
{
  public EnemySettings(double speed, int powerLevel, double hopVelocity)
  {
    if (!double.IsFinite(speed) || speed is < 0 or > 30)
      throw new ArgumentOutOfRangeException(nameof(speed), "Enemy speed must be between 0 and 30 tiles per second.");

    if (powerLevel is < 1 or > 10)
      throw new ArgumentOutOfRangeException(nameof(powerLevel), "Enemy power level must be between 1 and 10.");

    if (!double.IsFinite(hopVelocity) || hopVelocity is < 0 or > 20)
      throw new ArgumentOutOfRangeException(nameof(hopVelocity), "Hop velocity must be between 0 and 20.");

    Speed = speed;
    PowerLevel = powerLevel;
    HopVelocity = hopVelocity;
  }

  public double Speed { get; }

  public int PowerLevel { get; }

  public double HopVelocity { get; }
}
