namespace SamElla.Effects;

public sealed class ToiletPaperRoll(double x, double y, int direction, int damage)
{
  public const double Size = 0.6;
  public const double Speed = 20;
  public const int LifetimeTicks = 42;

  public double X { get; internal set; } = x;
  public double Y { get; internal set; } = y;
  public double VelocityY { get; internal set; } = -3;
  public int Direction { get; } = direction;
  public int Damage { get; } = damage;
  public int AgeTicks { get; internal set; }
  public int FrameIndex => AgeTicks / 4 % 2;
}
