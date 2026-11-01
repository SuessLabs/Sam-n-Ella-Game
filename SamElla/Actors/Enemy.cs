// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Actors;

public sealed class Enemy
{
  public const int BossDodgeDurationTicks = 18;
  public const double BossDodgeSpeed = 14;
  public const int BossThrowIntervalTicks = 100;
  public const int CaneAttackIntervalTicks = 120;
  public const int CaneSwingDurationTicks = 36;
  public const int CaneWaveDurationTicks = 18;
  public const double CaneWaveRange = 2;
  public const double CrawlerBodyHeight = 0.5;
  public const int CrawlerEmergenceDurationTicks = 60;
  public const double CrawlerWakeDistance = 4;
  public const double EmoBossBodyHeight = 2.5;
  public const int HopIntervalTicks = 24;
  public const int PoopIntervalTicks = 180;
  public const int SquashDurationTicks = 60;
  public const int StompStunTicks = 20;
  public int Direction = -1;

  public double X, Y, VelocityY;

  public Enemy(double x, double y, EnemyKind kind = EnemyKind.Walker, EnemySettings? settings = null)
  {
    PatrolOriginX = x;
    PatrolOriginY = y;
    X = x;
    Y = y;
    Kind = kind;
    Settings = settings ?? (kind switch
    {
      EnemyKind.Fly => new EnemySettings(2, 1, 0),
      EnemyKind.Worm => new EnemySettings(1, 1, 0),
      EnemyKind.PunkBlue or EnemyKind.PunkPurple => new EnemySettings(2, 4, 0),
      EnemyKind.EmoBoss => new EnemySettings(2, 10, 0),
      EnemyKind.Grandpa => new EnemySettings(2, 3, 0),
      EnemyKind.OldMan => new EnemySettings(1.5, 10, 0),
      EnemyKind.DungBag => new EnemySettings(2.5, 10, 0),
      EnemyKind.ToasterCat => EnemyConfiguration.DefaultToasterCat,
      EnemyKind.Zombie => EnemyConfiguration.DefaultZombie,
      EnemyKind.Dog => EnemyConfiguration.DefaultDog,
      EnemyKind.CreepyCrawler => EnemyConfiguration.DefaultCreepyCrawler,
      _ => EnemyConfiguration.DefaultWalker,
    });

    Health = Settings.PowerLevel;
    IsBuried = kind == EnemyKind.CreepyCrawler;
    PatrolFloorRow = (int)Math.Round(y + BodyHeight);
  }

  public double AirWaveLength { get; internal set; }
  public double AirWaveX { get; internal set; }
  public double AirWaveY { get; internal set; }
  public bool Alive { get; private set; } = true;
  public int AttackDirection { get; internal set; } = -1;

  public double BodyHeight => Kind switch
  {
    EnemyKind.EmoBoss => EmoBossBodyHeight,
    EnemyKind.PunkBlue or EnemyKind.PunkPurple or EnemyKind.Zombie or EnemyKind.DungBag or EnemyKind.OldMan or EnemyKind.Grandpa => Game.LargeBodyHeight,
    EnemyKind.Worm => 0.45,
    EnemyKind.Fly => 0.6,
    EnemyKind.CreepyCrawler => CrawlerBodyHeight,
    _ => Game.BodyHeight,
  };

  public int BossDodgeTicksRemaining { get; internal set; }
  public int BossHeadStomps { get; private set; }
  public int CaneSwingTicksRemaining { get; internal set; }
  public bool CanInteract => Alive && !IsBuried && !IsEmerging;
  public int EmergenceFrameIndex => Math.Min(2, (CrawlerEmergenceDurationTicks - EmergenceTicksRemaining) / 20);
  public int EmergenceTicksRemaining { get; private set; }
  public int Health { get; private set; }
  public bool IsBoss => Kind is EnemyKind.DungBag or EnemyKind.OldMan or EnemyKind.EmoBoss;
  public bool IsBuried { get; private set; }
  public bool IsEmerging => EmergenceTicksRemaining > 0;
  public bool IsTeen => Kind is EnemyKind.PunkBlue or EnemyKind.PunkPurple or EnemyKind.EmoBoss;
  public EnemyKind Kind { get; }
  public string Name => Kind == EnemyKind.Grandpa ? "Grandpa" : Kind == EnemyKind.EmoBoss ? "Emo Kid" : Kind is EnemyKind.PunkBlue or EnemyKind.PunkPurple ? "Punk-tard" : Kind == EnemyKind.OldMan ? "Old Man" : Kind == EnemyKind.DungBag ? "Dung-Bag" : Kind.ToString();
  public EnemySettings Settings { get; }
  public int SquashTicksRemaining { get; private set; }
  public int ThrowPoseTicksRemaining { get; internal set; }
  internal int CaneAttackTicksRemaining { get; set; } = CaneAttackIntervalTicks;
  internal int FlightTicks { get; set; }
  internal int HopTicksRemaining { get; set; }
  internal int PatrolFloorRow { get; set; }
  internal double PatrolOriginX { get; }
  internal double PatrolOriginY { get; }
  internal int PoopTicksRemaining { get; set; } = PoopIntervalTicks;
  internal int ProjectileAttackTicks { get; set; } = 120;
  internal int ProjectileCount { get; set; }
  internal int SpeechIndex { get; set; }
  internal int SpeechTicks { get; set; } = 90;
  internal int StunTicksRemaining { get; set; }
  internal int ThrowTicksRemaining { get; set; } = BossThrowIntervalTicks;

  internal bool AdvanceEmergence(double playerX, double playerY, bool supported = true)
  {
    if (Kind != EnemyKind.CreepyCrawler)
      return false;

    if (!supported)
    {
      IsBuried = false;
      EmergenceTicksRemaining = 0;
      return false;
    }

    if (IsBuried)
    {
      if (Math.Abs(X - playerX) <= CrawlerWakeDistance && Math.Abs(Y - playerY) <= 4)
      {
        IsBuried = false;
        EmergenceTicksRemaining = CrawlerEmergenceDurationTicks;
        Direction = playerX < X ? -1 : 1;
      }

      return true;
    }

    if (IsEmerging)
    {
      EmergenceTicksRemaining--;
      return true;
    }

    return false;
  }

  internal void AdvanceSquash()
  {
    if (SquashTicksRemaining > 0)
      SquashTicksRemaining--;
  }

  internal bool Hit(int damage)
  {
    if (damage is < 1 or > 12)
      throw new ArgumentOutOfRangeException(nameof(damage));

    if (!CanInteract)
      return false;

    Health = Math.Max(0, Health - damage);
    if (Health == 0)
    {
      Squash();
      return true;
    }

    VelocityY = 0;
    StunTicksRemaining = StompStunTicks;
    return false;
  }

  internal void Squash()
  {
    if (!Alive)
      return;

    AirWaveLength = 0;
    CaneSwingTicksRemaining = 0;
    BossDodgeTicksRemaining = 0;
    Alive = false;
    Health = 0;
    VelocityY = 0;
    SquashTicksRemaining = SquashDurationTicks;
  }

  internal bool Stomp(int damage = 1)
  {
    if (damage is < 1 or > 10)
      throw new ArgumentOutOfRangeException(nameof(damage));

    bool defeated = Hit(damage);
    if (IsBoss && Alive)
    {
      BossHeadStomps++;
      if (BossHeadStomps % 2 == 0)
      {
        BossDodgeTicksRemaining = BossDodgeDurationTicks;
        StunTicksRemaining = 0;
        CaneSwingTicksRemaining = 0;
        AirWaveLength = 0;
      }
    }

    return defeated;
  }
}
