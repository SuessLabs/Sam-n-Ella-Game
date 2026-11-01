// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;
using SamElla.Audio;
using SamElla.Effects;
using SamElla.Items;
using SamElla.Screens;

namespace SamElla;

public sealed class Game
{
  public const double BodyWidth = 0.75, BodyHeight = 0.9;
  public const string BoomStickPickupQuote = "This here is my BOOM STICK!";
  public const int BoomStickQuoteDurationTicks = 180;
  public const int BurnDurationTicks = 180;
  public const int ChainWhipCooldownTicks = 30;
  public const int DamageProtectionTicks = 90;
  public const int DeathDurationTicks = 120;
  public const int ExplosionDurationTicks = ExplosionFrameTicks * ExplosionFrameCount;
  public const int ExplosionFrameCount = 4;

  /// <remarks>100 ms is exactly six simulation ticks at 60 Hz.</remarks>
  public const int ExplosionFrameTicks = 6;

  public const int FlameDamageIntervalTicks = 18;
  public const int FlameMaxDurationTicks = 60;
  public const double FlameRange = 3;
  public const int FlamethrowerQuoteDurationTicks = 180;
  public const double LargeBodyHeight = 1.8;
  public const double OverloadedBodyHeight = 2.7;
  public const int PipeTravelDurationTicks = 48;
  public const int PeptoResetTicks = Level.SpeedRunTimeLimitSeconds * 60;
  public const int PipeReliefBonusTicks = 5 * 60;
  public const int PipeReliefDurationTicks = 36;
  public const int ToiletFinishDurationTicks = 210;
  public const int ToiletPaperCooldownTicks = 24;
  public const int PoopSlowDurationTicks = 300;
  public const double PoopSpeedMultiplier = 0.5;
  public const int PoopSplatterDurationTicks = 120;
  public const double ShotgunRange = 3;
  public const int ShotgunReloadTicks = 30;
  public const double Step = 1.0 / 60;
  public const string TeenDeathQuote = "it's everyone else's fault not mine";

  /// <remarks>The spike artwork's tip begins three pixels below the top of its 16-pixel tile.</remarks>
  private const double SpikeTipOffset = 3.0 / 16;

  private readonly List<BlockBreakEffect> _blockBreakEffects = [];
  private readonly HashSet<(int Level, int X, int Y)> _collectedItems = [];
  private readonly string _creditsAssetsPath;
  private readonly string[] _deathQuotes;
  private readonly string[] _enemyQuotes;
  private readonly EnemyConfiguration _enemyConfiguration;
  private readonly List<EnemyProjectile> _enemyProjectiles = [];
  private readonly string[] _files;
  private readonly string[] _flamethrowerQuotes;
  private readonly string[] _gameOverQuotes;
  private readonly List<GrowthHeart> _growthHearts = [];
  private readonly HashSet<(int X, int Y)> _levelItems = [];
  private readonly List<PoopPile> _poopPiles = [];
  private readonly List<CoinRewardPopup> _rewardPopups = [];
  private readonly List<ThrownPoop> _thrownPoop = [];
  private readonly HashSet<Enemy> _whipHits = [];
  private readonly List<ToiletPaperRoll> _toiletPaperRolls = [];
  private int _toiletPaperCooldownTicks;
  private readonly HashSet<(int X, int Y)> _usedReliefPipes = [];
  private double _toiletStartX, _toiletStartY;

  private int _facingDirection = 1;
  private bool _fireWasHeld;
  private int _flameAnimationTicks;
  private int _flameDamageTicks;
  private bool _growthPending;
  private double _jumpBuffer, _coyoteTime, _transition;
  private int _levelIndex;
  private int _shotgunReloadTicks;
  private PipePassage? _travelPipe;
  private int _whipAgeTicks = -1;
  private int _whipCooldownTicks;
  private int _whipDirection = 1;

  public Game(string[] files, string? deathQuotesPath = null, string? creditsAssetsPath = null, string? enemySettingsPath = null,
    string? flamethrowerQuotesPath = null, bool konamiCheat = false, string? gameOverQuotesPath = null,
    int startingLevelIndex = 0)
  {
    if ((uint)startingLevelIndex >= (uint)files.Length)
      throw new ArgumentOutOfRangeException(nameof(startingLevelIndex));

    this._files = files;

    _levelIndex = startingLevelIndex;
    KonamiCheatEnabled = konamiCheat;

    _enemyConfiguration = EnemyConfiguration.Load(enemySettingsPath ?? Path.Combine(AppContext.BaseDirectory, "Assets", "enemies.txt"));
    _creditsAssetsPath = creditsAssetsPath ?? Path.Combine(AppContext.BaseDirectory, "Assets");
    deathQuotesPath ??= Path.Combine(AppContext.BaseDirectory, "Assets", "death-quotes.txt");
    _deathQuotes = File.Exists(deathQuotesPath)
      ? File.ReadAllLines(deathQuotesPath).Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray()
      : [];
    if (_deathQuotes.Length == 0)
      _deathQuotes = ["Gravity wins again."];

    flamethrowerQuotesPath ??= Path.Combine(AppContext.BaseDirectory, "Assets", "flamethrower-quotes.txt");

    _flamethrowerQuotes = File.Exists(flamethrowerQuotesPath)
      ? File.ReadAllLines(flamethrowerQuotesPath).Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray()
      : [];

    if (_flamethrowerQuotes.Length == 0)
      _flamethrowerQuotes = ["Light Em' If you Got Em!"];

    gameOverQuotesPath ??= Path.Combine(AppContext.BaseDirectory, "Assets", "game-over-quotes.txt");

    _gameOverQuotes = File.Exists(gameOverQuotesPath)
      ? File.ReadAllLines(gameOverQuotesPath).Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray()
      : [];

    if (_gameOverQuotes.Length == 0)
      _gameOverQuotes = ["Bet you wish you could rewind? Nope."];

    string quotesPath = Path.Combine(_creditsAssetsPath, "enemy-quotes.txt");
    _enemyQuotes = File.Exists(quotesPath)
      ? File.ReadAllLines(quotesPath).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith(';')).ToArray()
      : [];
    if (_enemyQuotes.Length == 0)
      _enemyQuotes = ["use toxic straws", "i'm confused", "i want free stuff", "i hate working", "responsibility is dumb", "Ok boomer"];

    LoadLevel();
    ApplyCheatStart();
  }

  internal event Action<GameSound>? SoundRequested;

  public PipePassage? AvailablePipe => !HasEnteredUnderworld ? Level.EntryPipe
    : !ReturnedToDaylight ? Level.ExitPipe : null;

  public bool AwaitingRestart { get; private set; }
  public IReadOnlyList<BlockBreakEffect> BlockBreakEffects => _blockBreakEffects;
  public string BoomStickQuote => BoomStickQuoteTicksRemaining > 0 ? BoomStickPickupQuote : string.Empty;
  public int BoomStickQuoteTicksRemaining { get; private set; }
  public Enemy? Boss => Enemies.FirstOrDefault(enemy => enemy.IsBoss && enemy.Alive);
  public int BurnFrameIndex => (BurnDurationTicks - BurnTicksRemaining) / 6 % 4;
  public int BurnTicksRemaining { get; private set; }
  public int ChainWhipDamage => PlayerPowerLevel + 1;
  public PlayerCharacter Character { get; init; } = PlayerCharacter.Sam;
  public int Coins { get; private set; }
  public TimeSpan CompletionTime => TimeSpan.FromTicks(RunTicks * TimeSpan.TicksPerSecond / 60);
  public long CreditsTicks { get; private set; }

  public BackgroundTheme CurrentBackground => Level.Background == BackgroundTheme.Underworld
    && (!HasEnteredUnderworld || ReturnedToDaylight) ? BackgroundTheme.Day : Level.Background;

  public int DamageTicksRemaining { get; private set; }
  public string DeathQuote { get; private set; } = string.Empty;
  public int DeathTicksRemaining { get; private set; }
  public List<Enemy> Enemies { get; private set; } = [];
  public IReadOnlyList<EnemyProjectile> EnemyProjectiles => _enemyProjectiles;
  public string EnemyQuote { get; private set; } = string.Empty;
  public int EnemyQuoteTicksRemaining { get; private set; }

  public int ExplosionFrameIndex => IsExploding
    ? Math.Min(ExplosionFrameCount - 1, (DeathDurationTicks - DeathTicksRemaining) / ExplosionFrameTicks) : 0;

  public int FacingDirection => _facingDirection;
  public FlameStream? Flame { get; private set; }
  public int FlameDamage => PlayerPowerLevel + 1;
  public string FlamethrowerQuote { get; private set; } = string.Empty;
  public int FlamethrowerQuoteTicksRemaining { get; private set; }
  public string GameOverQuote { get; private set; } = string.Empty;
  public bool Grounded { get; private set; }
  public IReadOnlyList<GrowthHeart> GrowthHearts => _growthHearts;
  public bool HasBoomStick { get; private set; }
  public bool HasChainWhip { get; private set; }
  public bool HasToiletPaper { get; private set; }
  public int ToiletPaperDamage => PlayerPowerLevel + 2;
  public IReadOnlyList<ToiletPaperRoll> ToiletPaperRolls => _toiletPaperRolls;
  public bool HasEnteredUnderworld { get; private set; }
  public bool HasFlamethrower { get; private set; }
  public bool IsBurning => BurnTicksRemaining > 0;
  public bool IsDying => DeathTicksRemaining > 0 || AwaitingRestart;
  public bool IsExploding { get; private set; }
  public bool IsExplosionVisible => IsExploding && DeathDurationTicks - DeathTicksRemaining < ExplosionDurationTicks;
  public bool IsInSludge => Grounded && Level.Tile((int)Math.Floor(X + BodyWidth / 2), (int)Math.Round(Y + PlayerHeight)) == '~';
  public bool IsLarge { get; private set; }
  public int ItemsMissed { get; private set; }
  public bool KonamiCheatEnabled { get; }
  public Level Level { get; private set; } = null!;
  public int LevelCount => _files.Length;
  public string LevelLabel => Level.SectionLevelNumber ?? LevelNumber.ToString();
  public int LevelNumber => _levelIndex + 1;
  public int LevelSecondsRemaining => (LevelTicksRemaining + 59) / 60;
  public int LevelTicksRemaining { get; private set; }
  public int Lives { get; private set; } = 3;
  public string Message { get; private set; } = "Let's effn' go!";
  public bool Paused { get; set; }
  public int PipeTravelTicksRemaining { get; private set; }
  public double PlayerHeight => IsExploding ? OverloadedBodyHeight : IsLarge ? LargeBodyHeight : BodyHeight;
  public int PlayerPowerLevel => PlayerSettings.PowerLevel;
  public PlayerSettings PlayerSettings => IsLarge ? _enemyConfiguration.PlayerPowerLevel2 : _enemyConfiguration.PlayerPowerLevel1;
  public double PlayerSpeed => PlayerSettings.Speed * (SlowTicksRemaining > 0 ? PoopSpeedMultiplier : 1) * (IsInSludge ? 0.7 : 1);
  public IReadOnlyList<PoopPile> PoopPiles => _poopPiles;
  public int PoopSplatterTicksRemaining { get; private set; }
  public int PowerUpLevel => HasToiletPaper ? 5 : HasChainWhip ? 4 : HasBoomStick ? 3 : HasFlamethrower ? 2 : IsLarge || _growthPending ? 1 : 0;
  public bool ReturnedToDaylight { get; private set; }
  public IReadOnlyList<CoinRewardPopup> RewardPopups => _rewardPopups;
  public long RunTicks { get; private set; }
  public int Score { get; private set; }
  public ShotgunBlast? ShotgunBlast { get; private set; }
  public int ShotgunDamage => FlameDamage + 1;
  public int SlowTicksRemaining { get; private set; }
  public IReadOnlyList<ThrownPoop> ThrownPoop => _thrownPoop;
  public int PipeReliefTicksRemaining { get; private set; }
  public int ToiletFinishTicksRemaining { get; private set; }
  public int ToiletFinishAgeTicks => ToiletFinishDurationTicks - ToiletFinishTicksRemaining;
  public bool IsFinishingToilet => ToiletFinishTicksRemaining > 0;
  public bool IsSeatedOnToilet => IsFinishingToilet && ToiletFinishAgeTicks >= 30;
  public int ToiletX { get; private set; }
  public int ToiletY { get; private set; }
  public int TotalDeaths { get; private set; }
  public double VelocityY { get; private set; }
  public ChainWhipSwing? WhipSwing { get; private set; }
  public bool Won { get; private set; }
  public double X { get; private set; }
  public double Y { get; private set; }
  internal EndCreditsScreen? Credits { get; private set; }
  internal int ThemeLevelNumber { get; private set; }

  public bool ContinueAfterDeath()
  {
    if (!IsDying)
      return false;

    if (Lives > 0)
    {
      Paused = false;
      LoadLevel();
      Message = "Try again!";
    }
    else
      Restart();

    return true;
  }

  public void QueueJump()
  {
    if (!Paused && !Won && !IsDying && !IsFinishingToilet && PipeReliefTicksRemaining == 0 && Lives > 0)
      _jumpBuffer = 0.12;
  }

  public void Restart()
  {
    _levelIndex = 0;
    Lives = 3;
    Coins = Score = 0;
    RunTicks = CreditsTicks = 0;
    TotalDeaths = ItemsMissed = 0;
    Credits = null;
    _collectedItems.Clear();
    Won = Paused = false;
    _transition = 0;

    Message = "Let's go!";
    LoadLevel();
    ApplyCheatStart();
  }

  public void Update(int direction, bool jumpHeld, bool fireHeld = false, bool downHeld = false)
  {
    if (Paused)
      return;

    Flame = null;

    if (Won)
    {
      ShotgunBlast = null;
      CreditsTicks++;
      return;
    }

    if (!AwaitingRestart)
      RunTicks++;

    if (PoopSplatterTicksRemaining > 0)
      PoopSplatterTicksRemaining--;

    for (int i = _blockBreakEffects.Count - 1; i >= 0; i--)
    {
      var effect = _blockBreakEffects[i] with { AgeTicks = _blockBreakEffects[i].AgeTicks + 1 };

      if (effect.AgeTicks >= BlockBreakEffect.DurationTicks)
        _blockBreakEffects.RemoveAt(i);
      else
        _blockBreakEffects[i] = effect;
    }

    foreach (var enemy in Enemies)
      enemy.AdvanceSquash();

    for (int i = _rewardPopups.Count - 1; i >= 0; i--)
    {
      var popup = _rewardPopups[i] with { AgeTicks = _rewardPopups[i].AgeTicks + 1 };

      if (popup.AgeTicks >= CoinRewardPopup.DurationTicks)
        _rewardPopups.RemoveAt(i);
      else
        _rewardPopups[i] = popup;
    }

    if (IsDying)
    {
      if (DeathTicksRemaining > 0)
      {
        DeathTicksRemaining--;
        AwaitingRestart = DeathTicksRemaining == 0;
      }

      return;
    }

    if (Lives == 0)
      return;

    if (_transition > 0)
    {
      _transition -= Step;
      return;
    }

    if (IsFinishingToilet)
    {
      AdvanceToiletFinish();
      return;
    }

    if (PipeReliefTicksRemaining > 0)
    {
      PipeReliefTicksRemaining--;
      AdvanceLevelClock();
      return;
    }

    if (PipeTravelTicksRemaining > 0)
    {
      AdvancePipeTravel();
      return;
    }

    if (EnemyQuoteTicksRemaining > 0 && --EnemyQuoteTicksRemaining == 0)
      EnemyQuote = string.Empty;

    if (downHeld && (TryEnterPipe() || TryRelieveInPipe()))
      return;

    if (!AdvanceLevelClock())
      return;

    if (ShotgunBlast is { } blast)
      ShotgunBlast = blast.AgeTicks + 1 >= ShotgunBlast.DurationTicks ? null : blast with { AgeTicks = blast.AgeTicks + 1 };

    if (_toiletPaperCooldownTicks > 0)
      _toiletPaperCooldownTicks--;

    if (_shotgunReloadTicks > 0)
      _shotgunReloadTicks--;

    _jumpBuffer -= Step;
    if (BoomStickQuoteTicksRemaining > 0)
      BoomStickQuoteTicksRemaining--;

    if (FlamethrowerQuoteTicksRemaining > 0 && --FlamethrowerQuoteTicksRemaining == 0)
      FlamethrowerQuote = string.Empty;

    if (BurnTicksRemaining > 0)
      BurnTicksRemaining--;

    if (direction != 0)
      _facingDirection = Math.Sign(direction);

    AdvanceGrowthHearts();
    if (DamageTicksRemaining > 0)
      DamageTicksRemaining--;

    _coyoteTime = Grounded ? 0.1 : _coyoteTime - Step;

    if (_jumpBuffer > 0 && _coyoteTime > 0)
    {
      VelocityY = -PlayerSettings.HopVelocity;
      Grounded = false;
      _jumpBuffer = _coyoteTime = 0;
      SoundRequested?.Invoke(GameSound.Jump);
    }

    // Releasing jump early produces a shorter hop.
    VelocityY = Math.Min(VelocityY + (VelocityY < 0 && !jumpHeld ? 75 : 40) * Step, 28);

    double x = X, y = Y;
    double previousFeet = Y + PlayerHeight;
    Move(ref x, ref y, Math.Clamp(direction, -1, 1) * PlayerSpeed * Step, true, false, out _, PlayerHeight);
    if (SlowTicksRemaining > 0)
    {
      SlowTicksRemaining--;
      if (SlowTicksRemaining == 0)
        Message = "Back to full speed!";
    }

    var vertical = VelocityY * Step;
    Move(ref x, ref y, vertical, false, true, out bool hitY, PlayerHeight);

    Grounded = hitY && vertical > 0;

    if (hitY)
      VelocityY = 0;

    X = x;
    Y = y;

    if (vertical > 0 && TryFindSpikeLanding(previousFeet, out double spikeTop))
    {
      Y = spikeTop - PlayerHeight;
      TakeDamage();
      if (IsDying)
        return;

      Grounded = true;
      VelocityY = 0;
    }

    if (CollectGrowthHearts())
      return;

    TryGrow();
    if (HasToiletPaper)
    {
      if (IsLarge && fireHeld && !_fireWasHeld && _toiletPaperCooldownTicks == 0)
        ThrowToiletPaper();
    }
    else if (HasChainWhip)
      UpdateChainWhip(fireHeld);
    else if (HasBoomStick)
    {
      if (IsLarge && fireHeld && !_fireWasHeld && _shotgunReloadTicks == 0)
        FireShotgun();
    }
    else
      UpdateFlame(fireHeld);
    _fireWasHeld = fireHeld;

    foreach (var (tx, ty) in Cells(X, Y, PlayerHeight))
    {
      switch (Level.Tile(tx, ty))
      {
        case 'o':
          Level.Clear(tx, ty);
          _collectedItems.Add((_levelIndex, tx, ty));
          Coins++;
          Score += 100;
          SoundRequested?.Invoke(GameSound.Coin);
          break;

        case 'k':
          Level.Clear(tx, ty);
          _collectedItems.Add((_levelIndex, tx, ty));
          Score += 100;
          if (Level.IsSpeedRun)
          {
            LevelTicksRemaining = PeptoResetTicks;
            Message = "Pepto! Back to 10 seconds!";
          }

          SoundRequested?.Invoke(GameSound.PowerUp);
          break;

        case 'T':
        case 'G':
          if (!HasEnteredUnderworld || (Level.ExitPipe is not null && !ReturnedToDaylight))
          {
            Message = "Use the pipe to continue!";
            break;
          }

          if (Boss is not null)
          {
            Message = $"Defeat {Boss.Name} to open the exit!";
            break;
          }

          if (Level.Tile(tx, ty) == 'T')
            BeginToiletFinish(tx, ty);
          else
            CompleteLevel();
          return;
      }
    }

    if (Y > Level.Height + 2)
    {
      Die();
      return;
    }

    foreach (var enemy in Enemies.Where(enemy => enemy.Alive))
    {
      if (enemy.IsBoss && X < enemy.PatrolOriginX - 14
        || Level.Background == BackgroundTheme.Underworld && CurrentBackground != BackgroundTheme.Underworld)
        continue;

      bool supported = enemy.Kind != EnemyKind.CreepyCrawler || enemy.CanInteract
        || Level.Solid((int)Math.Floor(enemy.X + (BodyWidth / 2)), (int)Math.Round(enemy.Y + enemy.BodyHeight));
      if (enemy.AdvanceEmergence(X, Y, supported))
        continue;

      double ex = enemy.X, ey = enemy.Y;
      bool dodging = enemy.IsBoss && enemy.BossDodgeTicksRemaining > 0;
      bool stunned = enemy.StunTicksRemaining > 0 && !dodging;
      if (enemy.StunTicksRemaining > 0)
        enemy.StunTicksRemaining--;
      if (enemy.ThrowPoseTicksRemaining > 0)
        enemy.ThrowPoseTicksRemaining--;

      bool floor = false;
      double enemyVertical = 0;
      if (enemy.Kind == EnemyKind.Fly)
      {
        if (!stunned)
        {
          enemy.FlightTicks++;
          double angle = enemy.FlightTicks / 24.0;
          double desiredX = enemy.PatrolOriginX + Math.Sin(angle) * 1.25;
          double desiredY = enemy.PatrolOriginY + (Math.Cos(angle) - 1) * 1.25;
          enemy.Direction = desiredX < ex ? -1 : 1;
          Move(ref ex, ref ey, desiredX - ex, true, false, out _, enemy.BodyHeight);
          Move(ref ex, ref ey, desiredY - ey, false, false, out _, enemy.BodyHeight);
        }
      }
      else
      {
        double patrolSpeed = dodging ? Enemy.BossDodgeSpeed : enemy.Settings.Speed;
        double patrolDistance = stunned || enemy.CaneSwingTicksRemaining > 0 ? 0 : enemy.Direction * patrolSpeed * Step;
        if (enemy.IsBoss && patrolDistance != 0
          && (ex + patrolDistance < enemy.PatrolOriginX - 5 || ex + patrolDistance > enemy.PatrolOriginX + 4))
        {
          enemy.Direction *= -1;
          patrolDistance = enemy.Direction * patrolSpeed * Step;
        }

        if ((Level.EnemiesAvoidLedges || enemy.IsBoss) && patrolDistance != 0 && HasPatrolSupport(ex, enemy.PatrolFloorRow)
          && !HasPatrolSupport(ex + patrolDistance, enemy.PatrolFloorRow))
        {
          enemy.Direction *= -1;
          patrolDistance = enemy.Direction * patrolSpeed * Step;
          if (!HasPatrolSupport(ex + patrolDistance, enemy.PatrolFloorRow))
            patrolDistance = 0;
        }

        Move(ref ex, ref ey, patrolDistance, true, false, out bool wall, enemy.BodyHeight);

        if (wall)
          enemy.Direction *= -1;

        enemy.VelocityY = Math.Min(enemy.VelocityY + 40 * Step, 28);
        enemyVertical = enemy.VelocityY * Step;
        Move(ref ex, ref ey, enemyVertical, false, false, out floor, enemy.BodyHeight);

        if (floor)
        {
          enemy.PatrolFloorRow = (int)Math.Round(ey + enemy.BodyHeight);
          enemy.VelocityY = 0;
          if (!stunned && enemy.Kind != EnemyKind.CreepyCrawler && enemy.Settings.HopVelocity > 0 && enemyVertical > 0)
          {
            if (enemy.HopTicksRemaining > 0)
              enemy.HopTicksRemaining--;
            else
            {
              enemy.VelocityY = -enemy.Settings.HopVelocity;
              enemy.HopTicksRemaining = Enemy.HopIntervalTicks;
            }
          }
        }
      }

      enemy.X = ex; enemy.Y = ey;
      if (dodging)
        enemy.BossDodgeTicksRemaining--;
      if (enemy.Kind == EnemyKind.Dog && floor && enemyVertical > 0 && !stunned)
        LeavePoop(enemy);

      if (enemy.Kind == EnemyKind.DungBag && !stunned && !dodging && --enemy.ThrowTicksRemaining <= 0)
      {
        enemy.ThrowTicksRemaining = Enemy.BossThrowIntervalTicks;
        int aim = X < enemy.X ? -1 : 1;
        _thrownPoop.Add(new ThrownPoop(enemy.X + aim * 0.85, enemy.Y + 0.2, aim * 6));
      }

      if (enemy.IsTeen && !stunned && !dodging)
        AdvanceTeenAttack(enemy);

      if (enemy.Kind is EnemyKind.OldMan or EnemyKind.Grandpa)
      {
        AdvanceCaneAttack(enemy, stunned || dodging || enemy.Kind == EnemyKind.Grandpa && Math.Abs(X - enemy.X) > 8);
        if (IsDying)
          return;
      }

      if (ey > Level.Height + 2)
      {
        enemy.Y = Level.Height - enemy.BodyHeight;
        enemy.Squash();
      }

      if (!enemy.Alive || !Overlaps(X, Y, PlayerHeight, ex, ey, enemy.BodyHeight))
        continue;

      if (vertical > 0 && Y + PlayerHeight - vertical <= ey + 0.25)
      {
        bool defeated = enemy.Stomp(enemy.IsBoss && !IsLarge ? 1 : PlayerPowerLevel);
        Y = ey - PlayerHeight;
        VelocityY = -10;
        if (!defeated && enemy.IsBoss && enemy.BossHeadStomps % 2 == 0)
          BeginBossDodge(enemy);

        if (defeated)
          EnemyDefeated(enemy);
        else
          SoundRequested?.Invoke(GameSound.EnemySmash);
        Message = enemy.IsBoss
          ? defeated ? $"{enemy.Name} defeated! Exit open!" : $"{enemy.Name}: {enemy.Health}/{enemy.Settings.PowerLevel}"
          : defeated ? "Stomp!" : $"Still kicking! {enemy.Health} hits left";
      }
      else
      {
        TakeDamage();
        if (IsDying)
          return;
      }
    }

    AdvanceToiletPaperRolls();
    AdvanceEnemyProjectiles();
    if (IsDying)
      return;

    AdvanceThrownPoop();
    StepInPoop();
  }

  // The level-start page replaces the short in-game transition delay.
  internal void CompleteLevelStart() => _transition = 0;

  private static IEnumerable<(int X, int Y)> Cells(double x, double y, double height = BodyHeight)
  {
    for (int ty = (int)Math.Floor(y + 0.00001);
      ty <= (int)Math.Floor(y + height - 0.00001); ty++)
    {
      for (int tx = (int)Math.Floor(x + 0.00001); tx <= (int)Math.Floor(x + BodyWidth - 0.00001); tx++)
        yield return (tx, ty);
    }
  }

  private static bool Overlaps(double ax, double ay, double height, double bx, double by, double otherHeight = BodyHeight) =>
    ax < bx + BodyWidth && ax + BodyWidth > bx && ay < by + otherHeight && ay + height > by;

  private void BeginBossDodge(Enemy enemy)
  {
    int direction = X + BodyWidth / 2 <= enemy.X + BodyWidth / 2 ? 1 : -1;
    double leftSpace = enemy.X - (enemy.PatrolOriginX - 5);
    double rightSpace = enemy.PatrolOriginX + 4 - enemy.X;
    if (direction > 0 && rightSpace < 2 && leftSpace > rightSpace)
      direction = -1;
    else if (direction < 0 && leftSpace < 2 && rightSpace > leftSpace)
      direction = 1;

    enemy.Direction = direction;
  }

  private void AdvanceCaneAttack(Enemy enemy, bool stunned)
  {
    enemy.AirWaveLength = 0;
    if (stunned)
    {
      enemy.CaneSwingTicksRemaining = 0;
      return;
    }

    if (enemy.CaneSwingTicksRemaining == 0)
    {
      if (--enemy.CaneAttackTicksRemaining > 0)
        return;

      enemy.CaneAttackTicksRemaining = Enemy.CaneAttackIntervalTicks;
      enemy.CaneSwingTicksRemaining = Enemy.CaneSwingDurationTicks;
      enemy.AttackDirection = X < enemy.X ? -1 : 1;
      enemy.Direction = enemy.AttackDirection;
    }
    else
      enemy.CaneSwingTicksRemaining--;

    int ticks = enemy.CaneSwingTicksRemaining;
    if (ticks > Enemy.CaneWaveDurationTicks || ticks == 0)
      return;

    if (ticks == Enemy.CaneWaveDurationTicks)
    {
      enemy.AirWaveX = enemy.X + (enemy.AttackDirection > 0 ? BodyWidth : 0);
      enemy.AirWaveY = enemy.Y + enemy.BodyHeight - 1;
    }

    double length = Enemy.CaneWaveRange * (Enemy.CaneWaveDurationTicks + 1 - ticks) / Enemy.CaneWaveDurationTicks;
    // Clip the gust against terrain throughout its one-block height.
    for (double distance = 0; distance <= length; distance += 0.05)
    {
      int column = (int)Math.Floor(enemy.AirWaveX + enemy.AttackDirection * distance);
      if (Level.Solid(column, (int)Math.Floor(enemy.AirWaveY))
        || Level.Solid(column, (int)Math.Floor(enemy.AirWaveY + 0.99)))
      {
        length = Math.Max(0, distance - 0.05);
        break;
      }
    }

    enemy.AirWaveLength = length;
    double left = enemy.AttackDirection < 0 ? enemy.AirWaveX - length : enemy.AirWaveX;
    if (length > 0 && X < left + length && X + BodyWidth > left
      && Y < enemy.AirWaveY + 1 && Y + PlayerHeight > enemy.AirWaveY)
      TakeDamage();
  }

  private void AdvanceEnemyProjectiles()
  {
    for (int index = _enemyProjectiles.Count - 1; index >= 0; index--)
    {
      var projectile = _enemyProjectiles[index];
      if (!projectile.Owner.Alive || ++projectile.AgeTicks > 240)
      {
        _enemyProjectiles.RemoveAt(index);
        continue;
      }

      if (projectile.Kind == EnemyProjectileKind.BeerBottle)
        projectile.VelocityY += 18 * Step;

      double dx = projectile.VelocityX * Step, dy = projectile.VelocityY * Step;
      int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 0.1));
      for (int step = 0; step < steps; step++)
      {
        projectile.X += dx / steps;
        projectile.Y += dy / steps;
        bool blocked = projectile.Y > Level.Height || projectile.X < 0 || projectile.X >= Level.Width
          || Level.Solid((int)Math.Floor(projectile.X), (int)Math.Floor(projectile.Y))
          || Level.Solid((int)Math.Floor(projectile.X + EnemyProjectile.Width), (int)Math.Floor(projectile.Y))
          || Level.Solid((int)Math.Floor(projectile.X), (int)Math.Floor(projectile.Y + EnemyProjectile.Height))
          || Level.Solid((int)Math.Floor(projectile.X + EnemyProjectile.Width), (int)Math.Floor(projectile.Y + EnemyProjectile.Height));
        bool hit = !blocked && X < projectile.X + EnemyProjectile.Width && X + BodyWidth > projectile.X
          && Y < projectile.Y + EnemyProjectile.Height && Y + PlayerHeight > projectile.Y;
        if (!blocked && !hit)
          continue;

        _enemyProjectiles.RemoveAt(index);
        if (hit)
        {
          TakeDamage();
          if (IsDying)
            return;
        }

        break;
      }
    }
  }

  private void AdvanceGrowthHearts()
  {
    for (int i = _growthHearts.Count - 1; i >= 0; i--)
    {
      var heart = _growthHearts[i];
      heart.AgeTicks++;
      double x = heart.X, y = heart.Y;
      Move(ref x, ref y, heart.Direction * GrowthHeart.Speed * Step, true, false, out bool wall);
      if (wall)
        heart.Direction *= -1;

      heart.VelocityY = Math.Min(heart.VelocityY + 40 * Step, 28);
      Move(ref x, ref y, heart.VelocityY * Step, false, false, out bool floor);
      if (floor)
        heart.VelocityY = heart.VelocityY > 0 ? -5 : 0;

      heart.X = x;
      heart.Y = y;
      if (heart.Y > Level.Height + 2)
        _growthHearts.RemoveAt(i);
    }
  }

  private void AdvancePipeTravel()
  {
    PipeTravelTicksRemaining--;
    if (PipeTravelTicksRemaining != PipeTravelDurationTicks / 2 || _travelPipe is not { } pipe)
      return;

    X = pipe.TargetX;
    Y = pipe.TargetY + BodyHeight - PlayerHeight;
    Grounded = false;
    VelocityY = 0;
    _jumpBuffer = _coyoteTime = 0;
    _enemyProjectiles.Clear();
    if (!HasEnteredUnderworld)
      HasEnteredUnderworld = true;
    else
      ReturnedToDaylight = true;
  }

  private void AdvanceTeenAttack(Enemy enemy)
  {
    if (Math.Abs(enemy.X - X) > 12)
      return;

    if (--enemy.SpeechTicks <= 0)
    {
      enemy.SpeechTicks = 210;
      if (EnemyQuoteTicksRemaining == 0)
      {
        EnemyQuote = _enemyQuotes[enemy.SpeechIndex++ % _enemyQuotes.Length];
        EnemyQuoteTicksRemaining = 150;
      }
    }

    if (--enemy.ProjectileAttackTicks > 0)
      return;

    enemy.ProjectileAttackTicks = enemy.IsBoss ? 85 : 150;
    enemy.ThrowPoseTicksRemaining = 20;
    int aim = X < enemy.X ? -1 : 1;
    enemy.Direction = aim;
    bool needle = enemy.IsBoss && enemy.ProjectileCount++ % 2 == 1;
    double x = enemy.X + (aim > 0 ? BodyWidth : -EnemyProjectile.Width);
    double y = enemy.Y + enemy.BodyHeight * 0.45;
    double velocityY = needle ? Math.Clamp((Y + PlayerHeight / 2 - y) * 1.5, -3, 3) : -6;
    _enemyProjectiles.Add(new EnemyProjectile(enemy, needle ? EnemyProjectileKind.Needle : EnemyProjectileKind.BeerBottle,
      x, y, aim * (needle ? 8 : 6), velocityY));
  }

  private void AdvanceThrownPoop()
  {
    for (int index = _thrownPoop.Count - 1; index >= 0; index--)
    {
      var poop = _thrownPoop[index];
      double x = poop.X, y = poop.Y;
      Move(ref x, ref y, poop.VelocityX * Step, true, false, out bool wall, Effects.ThrownPoop.Height);
      poop.VelocityY = Math.Min(28, poop.VelocityY + 18 * Step);
      Move(ref x, ref y, poop.VelocityY * Step, false, false, out bool floor, Effects.ThrownPoop.Height);
      poop.X = x;
      poop.Y = y;
      poop.AgeTicks++;
      if (Overlaps(X, Y, PlayerHeight, x, y, Effects.ThrownPoop.Height))
      {
        ApplyPoopSlowdown();
        _thrownPoop.RemoveAt(index);
        continue;
      }

      if (floor && poop.VelocityY > 0)
      {
        int tileX = (int)Math.Floor(x + BodyWidth / 2);
        int tileY = (int)Math.Round(y + Effects.ThrownPoop.Height);
        var pile = new PoopPile(tileX, tileY);
        if (Level.Solid(tileX, tileY) && !_poopPiles.Contains(pile))
          _poopPiles.Add(pile);
      }

      if (floor || wall || poop.AgeTicks > 300 || x < 0 || x >= Level.Width || y > Level.Height + 2)
        _thrownPoop.RemoveAt(index);
    }
  }

  private void ApplyCheatStart()
  {
    if (!KonamiCheatEnabled)
      return;

    Lives = 30;
    HasFlamethrower = true;
    _growthPending = true;
    TryGrow();
    Message = "Konami code! 30 lives and fire!";
  }

  private void ApplyPoopSlowdown()
  {
    SlowTicksRemaining = PoopSlowDurationTicks;
    PoopSplatterTicksRemaining = PoopSplatterDurationTicks;
    Message = "Oh crap! Slowed for 5 seconds";
  }

  private void BreakBrick(int x, int y)
  {
    if (Level.Tile(x, y) != 'B')
      return;
    Level.Clear(x, y);
    Score += 50;
    _blockBreakEffects.Add(new BlockBreakEffect(x, y));
    SoundRequested?.Invoke(GameSound.BlockBreak);
  }

  private void ClearFlamethrowerQuote()
  {
    FlamethrowerQuote = string.Empty;
    FlamethrowerQuoteTicksRemaining = 0;
  }

  private void ClearWhipSwing()
  {
    WhipSwing = null;
    _whipAgeTicks = -1;
    _whipCooldownTicks = 0;
    _whipHits.Clear();
  }

  private bool CollectGrowthHearts()
  {
    for (int i = _growthHearts.Count - 1; i >= 0; i--)
    {
      var heart = _growthHearts[i];
      if (!Overlaps(X, Y, PlayerHeight, heart.X, heart.Y))
        continue;

      if (heart.IsToiletPaper && (!IsLarge || IsBurning || HasToiletPaper))
        continue;

      if (heart.IsChainWhip && (!IsLarge || IsBurning || HasChainWhip))
        continue;

      if ((heart.IsFlamethrower || heart.IsBoomStick) && (!IsLarge || IsBurning || HasBoomStick || HasChainWhip || HasToiletPaper))
        continue;

      _growthHearts.RemoveAt(i);
      if (heart.SourceX >= 0 && heart.SourceY >= 0)
        _collectedItems.Add((_levelIndex, heart.SourceX, heart.SourceY));

      Score += 100;
      if (heart.IsToiletPaper)
      {
        HasToiletPaper = true;
        HasChainWhip = HasBoomStick = HasFlamethrower = false;
        BoomStickQuoteTicksRemaining = 0;
        ShotgunBlast = null;
        Flame = null;
        ClearFlamethrowerQuote();
        ClearWhipSwing();
        _toiletPaperRolls.Clear();
        _toiletPaperCooldownTicks = 0;
        Message = "Toilet paper! X or CTRL to throw!";
        SoundRequested?.Invoke(GameSound.PowerUp);
        continue;
      }

      if (heart.IsChainWhip)
      {
        HasToiletPaper = false;
        _toiletPaperRolls.Clear();
        HasChainWhip = true;
        HasBoomStick = HasFlamethrower = false;
        BoomStickQuoteTicksRemaining = 0;
        ShotgunBlast = null;
        Flame = null;
        ClearFlamethrowerQuote();
        ClearWhipSwing();
        Message = "Chain whip! X or CTRL to lash!";
        SoundRequested?.Invoke(GameSound.PowerUp);
        continue;
      }

      if (heart.IsBoomStick)
      {
        HasBoomStick = true;
        BoomStickQuoteTicksRemaining = BoomStickQuoteDurationTicks;
        HasFlamethrower = false;
        Flame = null;
        ClearFlamethrowerQuote();
        Message = "Boom-Stick! X or CTRL to blast!";
        SoundRequested?.Invoke(GameSound.PowerUp);
        continue;
      }

      if (heart.IsFlamethrower)
      {
        if (HasFlamethrower)
        {
          HasFlamethrower = false;
          Flame = null;
          BurnTicksRemaining = BurnDurationTicks;
          ClearFlamethrowerQuote();
          Message = "On fire! Lost flamethrower!";
          SoundRequested?.Invoke(GameSound.PowerDown);
          continue;
        }

        HasFlamethrower = true;
        FlamethrowerQuote = _flamethrowerQuotes[Random.Shared.Next(_flamethrowerQuotes.Length)];
        FlamethrowerQuoteTicksRemaining = FlamethrowerQuoteDurationTicks;
        Message = "Flamethrower! Hold X or CTRL";
        SoundRequested?.Invoke(GameSound.PowerUp);
        continue;
      }

      if (IsLarge || _growthPending)
      {
        Explode();
        return true;
      }

      _growthPending = true;
      Message = "Heart collected!";
      SoundRequested?.Invoke(GameSound.PowerUp);
    }

    return false;
  }

  private void Die()
  {
    if (IsDying || Lives == 0)
      return;

    Lives--;
    if (Lives == 0)
      GameOverQuote = _gameOverQuotes[Random.Shared.Next(_gameOverQuotes.Length)];
    HasFlamethrower = false;
    HasBoomStick = false;
    HasChainWhip = false;
    HasToiletPaper = false;
    _toiletPaperRolls.Clear();
    ClearWhipSwing();
    BoomStickQuoteTicksRemaining = 0;
    ShotgunBlast = null;
    Flame = null;
    BurnTicksRemaining = 0;
    ClearFlamethrowerQuote();
    TotalDeaths++;
    AwaitingRestart = false;
    DeathQuote = _deathQuotes[Random.Shared.Next(_deathQuotes.Length)];
    Y += PlayerHeight - BodyHeight;
    IsExploding = false;
    IsLarge = false;
    _growthPending = false;
    DamageTicksRemaining = 0;
    DeathTicksRemaining = DeathDurationTicks;
    VelocityY = 0;
    Grounded = false;
    _jumpBuffer = _coyoteTime = 0;

    Message = Lives == 0 ? "Ouch! Restarting game..." : "Ouch!";
    SoundRequested?.Invoke(GameSound.Died);
  }

  private void EnemyDefeated(Enemy enemy)
  {
    Score += 200 * enemy.Settings.PowerLevel;
    SoundRequested?.Invoke(enemy.IsTeen ? GameSound.TeenGroan : GameSound.EnemyDied);
    if (enemy.IsTeen)
    {
      EnemyQuote = TeenDeathQuote;
      EnemyQuoteTicksRemaining = 180;
    }
  }

  private void Explode()
  {
    double feet = Y + PlayerHeight;
    Die();
    IsExploding = true;
    Y = feet - OverloadedBodyHeight;
    Message = "Power overload!";
  }

  private void FireShotgun()
  {
    double originX = _facingDirection > 0 ? X + BodyWidth : X;
    double originY = Y + PlayerHeight - 0.65;
    double length = 0;
    for (int pixel = 0; pixel < (int)(ShotgunRange * 16); pixel++)
    {
      int tileX = (int)Math.Floor(originX + (_facingDirection * ((pixel + 0.5) / 16)));
      int top = (int)Math.Floor(originY - 0.25);
      int bottom = (int)Math.Floor(originY + 0.25);
      // A brick breaks and lets the rest of this shot continue through it.
      for (int tileY = top; tileY <= bottom; tileY++)
        BreakBrick(tileX, tileY);
      if (Level.Solid(tileX, top) || Level.Solid(tileX, bottom))
      {
        double distance = _facingDirection > 0 ? tileX - originX : originX - (tileX + 1);
        length = Math.Clamp(distance, 0, length);
        break;
      }

      length = (pixel + 1) / 16.0;
    }

    ShotgunBlast = new ShotgunBlast(originX, originY, _facingDirection, length);
    _shotgunReloadTicks = ShotgunReloadTicks;
    double left = _facingDirection > 0 ? originX : originX - length;
    double right = _facingDirection > 0 ? originX + length : originX;
    foreach (var enemy in Enemies.Where(enemy => enemy.Alive))
    {
      if (enemy.X >= right || enemy.X + BodyWidth <= left
        || enemy.Y >= originY + 0.25 || enemy.Y + enemy.BodyHeight <= originY - 0.25)
        continue;

      if (enemy.Hit(ShotgunDamage))
      {
        EnemyDefeated(enemy);
      }
    }

    Message = "BOOM-STICK!";
    SoundRequested?.Invoke(GameSound.Shotgun);
  }

  private bool HasPatrolSupport(double x, int floorRow)
  {
    int left = (int)Math.Floor(x + 0.00001);
    int right = (int)Math.Floor(x + BodyWidth - 0.00001);
    return left >= 0 && right < Level.Width && floorRow >= 0 && floorRow < Level.Height
      && Level.Solid(left, floorRow) && Level.Solid(right, floorRow);
  }

  private void LeavePoop(Enemy dog)
  {
    if (--dog.PoopTicksRemaining > 0)
      return;

    dog.PoopTicksRemaining = Enemy.PoopIntervalTicks;
    int x = (int)Math.Floor(dog.X + (BodyWidth / 2) - (dog.Direction * 0.65));
    int y = (int)Math.Round(dog.Y + dog.BodyHeight);
    if (x >= 0 && x < Level.Width && Level.Solid(x, y) && !Level.Solid(x, y - 1))
    {
      var pile = new PoopPile(x, y);
      if (!_poopPiles.Contains(pile))
        _poopPiles.Add(pile);
    }
  }

  private void LoadLevel(bool preserveGrowth = false)
  {
    bool keepGrowth = preserveGrowth && (IsLarge || _growthPending);
    HasFlamethrower = preserveGrowth && HasFlamethrower;
    HasBoomStick = preserveGrowth && HasBoomStick;
    HasChainWhip = preserveGrowth && HasChainWhip;
    HasToiletPaper = preserveGrowth && HasToiletPaper;
    _toiletPaperCooldownTicks = 0;
    _toiletPaperRolls.Clear();
    ClearWhipSwing();
    BoomStickQuoteTicksRemaining = 0;
    ShotgunBlast = null;
    _shotgunReloadTicks = 0;
    _fireWasHeld = false;
    ClearFlamethrowerQuote();
    BurnTicksRemaining = 0;
    Flame = null;
    _flameDamageTicks = _flameAnimationTicks = 0;
    IsExploding = false;
    IsLarge = false;
    _growthPending = keepGrowth;
    _growthHearts.Clear();
    _poopPiles.Clear();
    _thrownPoop.Clear();
    _enemyProjectiles.Clear();
    _usedReliefPipes.Clear();
    PipeReliefTicksRemaining = ToiletFinishTicksRemaining = 0;
    ToiletX = ToiletY = 0;
    _travelPipe = null;
    PipeTravelTicksRemaining = 0;
    EnemyQuote = string.Empty;
    EnemyQuoteTicksRemaining = 0;
    SlowTicksRemaining = PoopSplatterTicksRemaining = 0;
    _facingDirection = 1;
    DamageTicksRemaining = 0;
    Level = Level.Load(_files[_levelIndex]);
    if (Level.StartsGrown)
    {
      _growthPending = true;
      HasBoomStick = Level.StartsWithShotgun;
      HasChainWhip = Level.StartsWithChainWhip;
      HasToiletPaper = Level.StartsWithToiletPaper;
      HasFlamethrower = false;
    }

    HasEnteredUnderworld = Level.EntryPipe is null;
    ReturnedToDaylight = false;
    LevelTicksRemaining = Level.TimeLimitSeconds * 60;
    ThemeLevelNumber = 1;

    for (int index = 0; index < _levelIndex; index++)
    {
      if (Level.Load(_files[index]).Background == Level.Background)
        ThemeLevelNumber++;
    }

    _levelItems.Clear();
    for (int y = 0; y < Level.Height; y++)
    {
      for (int x = 0; x < Level.Width; x++)
      {
        if (Level.Tile(x, y) is 'o' or '?' or 'M' or 'F' or 'Q' or 'W' or 'L' or 'k')
          _levelItems.Add((x, y));
      }
    }

    _rewardPopups.Clear();
    _blockBreakEffects.Clear();

    X = Level.Spawn.X;
    Y = Level.Spawn.Y;
    VelocityY = 0;
    DeathTicksRemaining = 0;
    AwaitingRestart = false;
    DeathQuote = string.Empty;
    GameOverQuote = string.Empty;

    Grounded = false;
    _jumpBuffer = _coyoteTime = 0;

    Enemies = Level.EnemySpawns.Select(spawn => new Enemy(spawn.X, spawn.Y, EnemyKind.Walker, _enemyConfiguration.Walker))
      .Concat(Level.ToasterCatSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y, EnemyKind.ToasterCat, _enemyConfiguration.ToasterCat)))
      .Concat(Level.ZombieSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y - (LargeBodyHeight - BodyHeight), EnemyKind.Zombie, _enemyConfiguration.Zombie)))
      .Concat(Level.DogSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y, EnemyKind.Dog, _enemyConfiguration.Dog)))
      .Concat(Level.CreepyCrawlerSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + 1 - Enemy.CrawlerBodyHeight,
        EnemyKind.CreepyCrawler, _enemyConfiguration.CreepyCrawler)))
      .Concat(Level.FlySpawns.Select(spawn => new Enemy(spawn.X, spawn.Y, EnemyKind.Fly)))
      .Concat(Level.WormSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - 0.45, EnemyKind.Worm)))
      .Concat(Level.BluePunkSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - LargeBodyHeight, EnemyKind.PunkBlue)))
      .Concat(Level.PurplePunkSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - LargeBodyHeight, EnemyKind.PunkPurple)))
      .Concat(Level.EmoBossSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - Enemy.EmoBossBodyHeight, EnemyKind.EmoBoss)))
      .Concat(Level.GrandpaSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - LargeBodyHeight, EnemyKind.Grandpa)))
      .Concat(Level.OldManSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - LargeBodyHeight, EnemyKind.OldMan)))
      .Concat(Level.BossSpawns.Select(spawn => new Enemy(spawn.X, spawn.Y + BodyHeight - LargeBodyHeight, EnemyKind.DungBag)))
      .ToList();
    foreach (var enemy in Enemies.Where(enemy => enemy.Kind is EnemyKind.PunkBlue or EnemyKind.PunkPurple))
    {
      enemy.SpeechIndex = (int)enemy.PatrolOriginX;
      enemy.SpeechTicks = 1;
    }

    TryGrow();
    if (Level.StartsWithShotgun)
      Message = "Shotgun ready! Blast the exit wall.";
    else if (Level.StartsWithToiletPaper)
      Message = "Toilet paper ready! X or CTRL to throw!";
    else if (Level.StartsWithChainWhip)
      Message = "Chain whip ready! X or CTRL to lash!";
  }

  private void Move(ref double x, ref double y, double distance, bool horizontal, bool bump, out bool hit, double height = BodyHeight)
  {
    hit = false;
    // Snapshot contacts before bumping tiles, without allocating per body or substep.
    Span<(int X, int Y)> collisions = stackalloc (int, int)[2 * ((int)Math.Ceiling(height) + 1)];
    int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(distance) / 0.15));

    for (int i = 0; i < steps; i++)
    {
      double nx = x + (horizontal ? distance / steps : 0);
      double ny = y + (horizontal ? 0 : distance / steps);
      double previousX = x;
      double bodyBottom = y + height;

      // Spike sides block entry like a pipe. Vertical contact is handled separately at the tip.
      int count = 0;
      for (int ty = (int)Math.Floor(ny + 0.00001); ty <= (int)Math.Floor(ny + height - 0.00001); ty++)
      {
        for (int tx = (int)Math.Floor(nx + 0.00001); tx <= (int)Math.Floor(nx + BodyWidth - 0.00001); tx++)
        {
          if (Level.Solid(tx, ty) || (horizontal && Level.Tile(tx, ty) == '^'
            && bodyBottom > ty + SpikeTipOffset + 0.00001
            && ((distance > 0 && previousX + BodyWidth <= tx + 0.00001)
              || (distance < 0 && previousX >= tx + 1 - 0.00001))))
            collisions[count++] = (tx, ty);
        }
      }

      if (count == 0)
      {
        x = nx;
        y = ny;
        continue;
      }

      hit = true;

      foreach (var cell in collisions[..count])
      {
        if (bump && distance < 0)
        {
          if (Level.Tile(cell.X, cell.Y) is 'M' or 'F' or 'Q' or 'W' or 'L')
          {
            bool flamethrower = Level.Tile(cell.X, cell.Y) == 'F';
            bool shotgun = Level.Tile(cell.X, cell.Y) == 'Q';
            bool whip = Level.Tile(cell.X, cell.Y) == 'W';
            bool toiletPaper = Level.Tile(cell.X, cell.Y) == 'L';
            Level.UseBlock(cell.X, cell.Y);
            _growthHearts.Add(new GrowthHeart(cell.X + 0.125, cell.Y - BodyHeight, _facingDirection, cell.X, cell.Y, flamethrower, shotgun, whip, toiletPaper));
            Message = toiletPaper ? "Catch the toilet paper!" : whip ? "Catch the chain whip!" : shotgun ? "Catch the Boom-Stick!" : flamethrower ? "Catch the flamethrower!" : "Catch the heart!";
          }
          else if (Level.Tile(cell.X, cell.Y) == '?')
          {
            Level.UseBlock(cell.X, cell.Y);
            _collectedItems.Add((_levelIndex, cell.X, cell.Y));
            Coins++;
            Score += 100;
            Message = "Bonus coin!";
            SoundRequested?.Invoke(GameSound.Coin);

            _rewardPopups.Add(new CoinRewardPopup(cell.X, cell.Y));
          }
          else if (Level.Tile(cell.X, cell.Y) == 'B')
          {
            BreakBrick(cell.X, cell.Y);
          }
        }
      }

      int boundary = horizontal ? collisions[0].X : collisions[0].Y;
      for (int contact = 1; contact < count; contact++)
      {
        int coordinate = horizontal ? collisions[contact].X : collisions[contact].Y;
        boundary = distance > 0 ? Math.Min(boundary, coordinate) : Math.Max(boundary, coordinate);
      }

      if (horizontal)
        x = distance > 0 ? boundary - BodyWidth : boundary + 1;
      else
        y = distance > 0 ? boundary - height : boundary + 1;

      break;
    }
  }

  private void StepInPoop()
  {
    for (int i = _poopPiles.Count - 1; i >= 0; i--)
    {
      var pile = _poopPiles[i];
      if (!Level.Solid(pile.X, pile.Y))
      {
        _poopPiles.RemoveAt(i);
        continue;
      }

      if (!Grounded || Math.Abs(Y + PlayerHeight - pile.Y) > 0.05
        || X + BodyWidth <= pile.X + 0.15 || X >= pile.X + 0.85)
        continue;

      _poopPiles.RemoveAt(i);
      ApplyPoopSlowdown();
    }
  }

  private void ThrowToiletPaper()
  {
    double x = _facingDirection > 0 ? X + BodyWidth : X - ToiletPaperRoll.Size;
    _toiletPaperRolls.Add(new ToiletPaperRoll(x, Y + PlayerHeight - 0.95, _facingDirection, ToiletPaperDamage));
    _toiletPaperCooldownTicks = ToiletPaperCooldownTicks;
    SoundRequested?.Invoke(GameSound.ToiletPaperThrow);
  }

  private void AdvanceToiletPaperRolls()
  {
    for (int index = _toiletPaperRolls.Count - 1; index >= 0; index--)
    {
      var roll = _toiletPaperRolls[index];
      if (++roll.AgeTicks >= ToiletPaperRoll.LifetimeTicks)
      {
        _toiletPaperRolls.RemoveAt(index);
        continue;
      }

      roll.VelocityY += 14 * Step;
      double dx = roll.Direction * ToiletPaperRoll.Speed * Step, dy = roll.VelocityY * Step;
      int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)) / 0.1));
      for (int step = 0; step < steps; step++)
      {
        roll.X += dx / steps;
        roll.Y += dy / steps;
        bool wall = roll.X < 0 || roll.X + ToiletPaperRoll.Size > Level.Width || roll.Y > Level.Height
          || Level.Solid((int)Math.Floor(roll.X), (int)Math.Floor(roll.Y))
          || Level.Solid((int)Math.Floor(roll.X + ToiletPaperRoll.Size), (int)Math.Floor(roll.Y))
          || Level.Solid((int)Math.Floor(roll.X), (int)Math.Floor(roll.Y + ToiletPaperRoll.Size))
          || Level.Solid((int)Math.Floor(roll.X + ToiletPaperRoll.Size), (int)Math.Floor(roll.Y + ToiletPaperRoll.Size));
        Enemy? target = wall ? null : Enemies.FirstOrDefault(enemy => enemy.CanInteract
          && roll.X < enemy.X + BodyWidth && roll.X + ToiletPaperRoll.Size > enemy.X
          && roll.Y < enemy.Y + enemy.BodyHeight && roll.Y + ToiletPaperRoll.Size > enemy.Y);
        if (!wall && target is null)
          continue;

        _toiletPaperRolls.RemoveAt(index);
        if (target is not null)
        {
          if (target.Hit(roll.Damage))
            EnemyDefeated(target);
          else
            SoundRequested?.Invoke(GameSound.EnemySmash);
        }

        break;
      }
    }
  }

  private void TakeDamage()
  {
    if (DamageTicksRemaining > 0)
      return;

    if (HasToiletPaper)
    {
      HasToiletPaper = false;
      _toiletPaperRolls.Clear();
      DamageTicksRemaining = DamageProtectionTicks;
      Message = "Lost toilet paper! Keep moving!";
      SoundRequested?.Invoke(GameSound.PowerDown);
      return;
    }

    if (HasChainWhip)
    {
      HasChainWhip = false;
      ClearWhipSwing();
      DamageTicksRemaining = DamageProtectionTicks;
      Message = "Lost chain whip! Keep moving!";
      SoundRequested?.Invoke(GameSound.PowerDown);
      return;
    }

    if (HasBoomStick)
    {
      HasBoomStick = false;
      BoomStickQuoteTicksRemaining = 0;
      HasFlamethrower = false;
      ShotgunBlast = null;
      DamageTicksRemaining = DamageProtectionTicks;
      Message = "Lost Boom-Stick! Keep moving!";
      SoundRequested?.Invoke(GameSound.PowerDown);
      return;
    }

    if (HasFlamethrower)
    {
      HasFlamethrower = false;
      Flame = null;
      DamageTicksRemaining = DamageProtectionTicks;
      Message = "Lost flamethrower!";
      SoundRequested?.Invoke(GameSound.PowerDown);
      ClearFlamethrowerQuote();
      return;
    }

    if (!IsLarge)
    {
      Die();
      return;
    }

    Y += LargeBodyHeight - BodyHeight;
    IsLarge = false;
    _growthPending = false;
    DamageTicksRemaining = DamageProtectionTicks;
    Message = "Shrunk! Keep moving!";
    SoundRequested?.Invoke(GameSound.PowerDown);
  }

  private void CompleteLevel()
  {
    Score += 1000;
    ItemsMissed += _levelItems.Count(item => !_collectedItems.Contains((_levelIndex, item.X, item.Y)));
    if (_levelIndex + 1 == _files.Length)
    {
      Won = true;
      CreditsTicks = 0;
      Credits = new EndCreditsScreen(_creditsAssetsPath, CompletionTime, TotalDeaths, Score, ItemsMissed);
      Message = "YOU WIN! R to play again";
    }
    else
    {
      _levelIndex++;
      LoadLevel(preserveGrowth: true);
      _transition = 0.6;
      Message = "Next level!";
    }
  }

  private bool AdvanceLevelClock()
  {
    if (--LevelTicksRemaining > 0)
      return true;

    LevelTicksRemaining = 0;
    Die();
    Message = Level.IsSpeedRun ? "You shit your pants!" : "TIME'S UP!";
    DeathQuote = Level.IsSpeedRun ? "You shit your pants!" : "Time's up!";
    return false;
  }

  public bool CanUseReliefPipe(int x, int y)
  {
    char tile = Level.Tile(x, y);
    if (!Level.IsSpeedRun || tile is not ('P' or 'p') || Level.Tile(x, y - 1) == tile)
      return false;

    if (tile == 'P')
    {
      while (Level.Tile(x - 1, y) == 'P')
        x--;
    }

    return !_usedReliefPipes.Contains((x, y));
  }

  private bool TryRelieveInPipe()
  {
    int x = (int)Math.Floor(X + BodyWidth / 2);
    int y = (int)Math.Round(Y + PlayerHeight);
    if (!Grounded || Math.Abs(Y + PlayerHeight - y) > 0.05 || !CanUseReliefPipe(x, y))
      return false;

    if (Level.Tile(x, y) == 'P')
    {
      while (Level.Tile(x - 1, y) == 'P')
        x--;
    }

    _usedReliefPipes.Add((x, y));
    LevelTicksRemaining += PipeReliefBonusTicks;
    PipeReliefTicksRemaining = PipeReliefDurationTicks;
    _toiletPaperRolls.Clear();
    VelocityY = 0;
    _jumpBuffer = _coyoteTime = 0;
    ClearWhipSwing();
    Flame = null;
    ShotgunBlast = null;
    Message = "Pipe relief! +5 seconds!";
    SoundRequested?.Invoke(GameSound.FartShort);
    return true;
  }

  private void BeginToiletFinish(int x, int y)
  {
    ToiletX = x;
    ToiletY = y;
    ToiletFinishTicksRemaining = ToiletFinishDurationTicks;
    _toiletPaperRolls.Clear();
    _toiletStartX = X;
    _toiletStartY = Y;
    VelocityY = 0;
    _jumpBuffer = _coyoteTime = 0;
    Flame = null;
    ShotgunBlast = null;

    ClearWhipSwing();
    Message = "Made it! Privacy, please...";
  }

  private void AdvanceToiletFinish()
  {
    ToiletFinishTicksRemaining--;
    int age = ToiletFinishAgeTicks;
    double approach = Math.Min(1, age / 30.0);
    X = _toiletStartX + (ToiletX + 0.5 - _toiletStartX) * approach;
    Y = _toiletStartY + (ToiletY + 1 - PlayerHeight - _toiletStartY) * approach;

    if (age == 105)
      SoundRequested?.Invoke(GameSound.FartShort);

    if (age == 150)
      SoundRequested?.Invoke(GameSound.FartLong);

    if (ToiletFinishTicksRemaining == 0)
      CompleteLevel();
  }

  private bool TryEnterPipe()
  {
    if (!Grounded || AvailablePipe is not { } pipe
      || X + BodyWidth / 2 < pipe.X || X + BodyWidth / 2 >= pipe.X + 2
      || Math.Abs(Y + PlayerHeight - pipe.Y) > 0.05)
      return false;

    if (HasEnteredUnderworld && Boss is not null)
    {
      Message = $"Defeat {Boss.Name} to use the exit pipe!";
      return false;
    }

    _travelPipe = pipe;
    PipeTravelTicksRemaining = PipeTravelDurationTicks;
    Flame = null;
    ShotgunBlast = null;
    _toiletPaperRolls.Clear();
    ClearWhipSwing();
    VelocityY = 0;
    Message = HasEnteredUnderworld ? "Back to daylight!" : "Descending to the Underworld...";
    return true;
  }

  private bool TryFindSpikeLanding(double previousFeet, out double spikeTop)
  {
    double currentFeet = Y + PlayerHeight;
    spikeTop = 0;

    if (currentFeet <= previousFeet + 0.00001)
      return false;

    // Sweep the feet across the whole downward movement so fast falls cannot skip a tip.
    for (int ty = (int)Math.Floor(previousFeet);
         ty <= (int)Math.Floor(currentFeet);
         ty++)
    {
      double tip = ty + SpikeTipOffset;

      if (previousFeet > tip + 0.00001 || currentFeet < tip)
        continue;

      for (int tx = (int)Math.Floor(X + 0.00001);
           tx <= (int)Math.Floor(X + BodyWidth - 0.00001);
           tx++)
      {
        if (Level.Tile(tx, ty) != '^')
          continue;

        spikeTop = tip;
        return true;
      }
    }

    return false;
  }

  private void TryGrow()
  {
    if (!_growthPending || IsLarge)
      return;

    double grownY = Y + BodyHeight - LargeBodyHeight;

    // Wait for headroom instead of growing into the block that awarded the power.
    if (grownY < 0 || Cells(X, grownY, LargeBodyHeight).Any(cell => Level.Solid(cell.X, cell.Y)))
      return;

    Y = grownY;
    IsLarge = true;
    _growthPending = false;

    Message = HasFlamethrower ? "Flamethrower! Hold X or CTRL" : "Powered up!";
  }

  private void UpdateChainWhip(bool fireHeld)
  {
    if (_whipCooldownTicks > 0)
      _whipCooldownTicks--;

    if (_whipAgeTicks < 0)
    {
      if (!fireHeld || _fireWasHeld || _whipCooldownTicks > 0 || !IsLarge)
        return;

      _whipAgeTicks = 0;
      _whipDirection = _facingDirection;
      _whipCooldownTicks = ChainWhipCooldownTicks;
      _whipHits.Clear();
      SoundRequested?.Invoke(GameSound.ChainWhip);
    }

    if (_whipAgeTicks >= ChainWhipSwing.DurationTicks)
    {
      WhipSwing = null;
      _whipAgeTicks = -1;
      return;
    }

    int age = _whipAgeTicks++;
    double extension = age < 6 ? 0 : age < 12 ? (age - 5) / 6.0 : age < 15 ? 1 : (24 - age) / 9.0;
    double requestedLength = ChainWhipSwing.Range * extension;
    double originX = _whipDirection > 0 ? X + BodyWidth : X;
    double originY = Y + PlayerHeight - 0.65;
    double length = 0;

    for (int pixel = 0; pixel < (int)(requestedLength * 16); pixel++)
    {
      int tileX = (int)Math.Floor(originX + _whipDirection * (pixel + 0.5) / 16);
      if (Level.Solid(tileX, (int)Math.Floor(originY - 0.4))
        || Level.Solid(tileX, (int)Math.Floor(originY + 0.4)))
      {
        double boundary = _whipDirection > 0 ? tileX - originX : originX - (tileX + 1);
        length = Math.Clamp(boundary, 0, length);
        break;
      }

      length = (pixel + 1) / 16.0;
    }

    WhipSwing = new ChainWhipSwing(originX, originY, _whipDirection, length, age);
    if (length <= 0)
      return;

    double left = _whipDirection > 0 ? originX : originX - length;
    double right = left + length;

    foreach (var enemy in Enemies.Where(enemy => enemy.CanInteract))
    {
      if (enemy.X >= right || enemy.X + BodyWidth <= left || enemy.Y >= originY + 0.4
        || enemy.Y + enemy.BodyHeight <= originY - 0.4 || !_whipHits.Add(enemy))
        continue;

      if (enemy.Hit(ChainWhipDamage))
        EnemyDefeated(enemy);
      else
        SoundRequested?.Invoke(GameSound.EnemySmash);
    }
  }

  private void UpdateFlame(bool fireHeld)
  {
    if (_flameDamageTicks > 0)
      _flameDamageTicks--;
    if (!HasFlamethrower || !IsLarge || !fireHeld)
    {
      _flameAnimationTicks = 0;
      return;
    }

    if (_flameAnimationTicks >= FlameMaxDurationTicks)
      return;

    double originX = _facingDirection > 0 ? X + BodyWidth : X;
    double originY = Y + PlayerHeight - 0.65;
    double length = 0;

    // Trace the full flame thickness so it cannot pass through a wall or a pipe.
    for (int pixel = 0; pixel < (int)(FlameRange * 16); pixel++)
    {
      double checkX = originX + (_facingDirection * ((pixel + 0.5) / 16));
      int tileX = (int)Math.Floor(checkX);

      if (Level.Solid(tileX, (int)Math.Floor(originY - 0.25))
        || Level.Solid(tileX, (int)Math.Floor(originY + 0.25)))
      {
        double boundaryDistance = _facingDirection > 0 ? tileX - originX : originX - (tileX + 1);
        length = Math.Clamp(boundaryDistance, 0, length);
        break;
      }

      length = (pixel + 1) / 16.0;
    }

    Flame = new FlameStream(originX, originY, _facingDirection, length, _flameAnimationTicks / 6 % 4);
    _flameAnimationTicks++;

    if (_flameDamageTicks > 0 || length == 0)
      return;

    _flameDamageTicks = FlameDamageIntervalTicks;

    double left = _facingDirection > 0 ? originX : originX - length;
    double right = _facingDirection > 0 ? originX + length : originX;

    foreach (var enemy in Enemies.Where(enemy => enemy.Alive))
    {
      if (enemy.X >= right || enemy.X + BodyWidth <= left
        || enemy.Y >= originY + 0.25 || enemy.Y + enemy.BodyHeight <= originY - 0.25)
        continue;

      if (enemy.Hit(FlameDamage))
      {
        EnemyDefeated(enemy);
      }
    }
  }
}
