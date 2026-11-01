// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;

namespace SamElla.Effects;

public enum EnemyProjectileKind
{
  BeerBottle,
  Needle,
}

public sealed class EnemyProjectile(Enemy owner, EnemyProjectileKind kind, double x, double y, double velocityX, double velocityY)
{
  public const double Height = 0.25;
  public const double Width = 0.6;
  public int AgeTicks { get; internal set; }
  public EnemyProjectileKind Kind { get; } = kind;
  public Enemy Owner { get; } = owner;
  public double VelocityX { get; } = velocityX;
  public double VelocityY { get; internal set; } = velocityY;
  public double X { get; internal set; } = x;
  public double Y { get; internal set; } = y;
}
