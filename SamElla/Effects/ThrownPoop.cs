namespace SamElla.Effects;

public sealed class ThrownPoop(double x, double y, double velocityX)
{
  public const double Height = 0.7;

  public double X { get; internal set; } = x;
  public double Y { get; internal set; } = y;
  public double VelocityX { get; } = velocityX;
  public double VelocityY { get; internal set; } = -9;
  internal int AgeTicks { get; set; }
}
