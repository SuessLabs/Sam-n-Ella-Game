// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using SamElla.Actors;
using SamElla.Items;
using SamElla.Screens;

namespace SamElla;

internal sealed class PixelRenderer
{
  public const int Height = 240;
  public const int TileSize = 16;
  public const int Width = 384;
  private const int FooterHeight = 14;
  private const uint Gold = 0xFFFFD56B;
  private const int HeaderHeight = 28;
  private const uint Ink = 0xFF17263F;
  private const uint White = 0xFFF6F4DF;

  private static readonly string VersionLabel = "v" + System.Reflection.CustomAttributeExtensions
    .GetCustomAttribute<System.Reflection.AssemblyFileVersionAttribute>(System.Reflection.Assembly.GetEntryAssembly() ?? typeof(PixelRenderer).Assembly)!.Version;

  private readonly Dictionary<string, Sprite> _sprites = [];
  private readonly bool _touchControls;
  private double _animationTime;
  private bool _facingLeft;

  public PixelRenderer(string assetsPath, bool touchControls = false)
  {
    _touchControls = touchControls;
    var palette = Sprite.LoadPalette(Path.Combine(assetsPath, "palette.txt"));

    foreach (string name in new[]
    {
      "player-idle",
      "player-run-1",
      "player-run-2",
      "player-jump",
      "player-splatter",
      "player-explosion-1",
      "player-explosion-2",
      "player-explosion-3",
      "player-explosion-4",
      "player-large-idle",
      "player-large-run-1",
      "player-large-run-2",
      "player-large-jump",
      "enemy-idle",
      "enemy-walk",
      "enemy-squashed",
      "toaster-cat-run-1",
      "toaster-cat-run-2",
      "toaster-cat-jump",
      "toaster-cat-squashed",
      "zombie-walk-1",
      "zombie-walk-2",
      "zombie-squashed",
      "creepy-crawler-buried",
      "creepy-crawler-emerge-1",
      "creepy-crawler-emerge-2",
      "creepy-crawler-emerge-3",
      "creepy-crawler-crawl-1",
      "creepy-crawler-crawl-2",
      "creepy-crawler-squashed",
      "dog-walk-1",
      "dog-walk-2",
      "dog-squashed",
      "pepto-bottle",
      "toilet",
      "player-toilet-sit",
      "poop",
      "sewer-brick",
      "sludge",
      "fly-1",
      "fly-2",
      "fly-squashed",
      "worm-1",
      "worm-2",
      "worm-squashed",
      "punk-blue-1",
      "punk-blue-2",
      "punk-blue-throw",
      "punk-blue-squashed",
      "punk-purple-1",
      "punk-purple-2",
      "punk-purple-throw",
      "punk-purple-squashed",
      "emo-boss-1",
      "emo-boss-2",
      "emo-boss-throw",
      "emo-boss-squashed",
      "beer-bottle",
      "needle",
      "old-man-walk-1",
      "old-man-walk-2",
      "old-man-windup",
      "old-man-swing",
      "old-man-squashed",
      "dung-bag-walk-1",
      "dung-bag-walk-2",
      "dung-bag-squashed",
      "poop-screen-splatter",
      "grass",
      "dirt",
      "brick",
      "brick-rubble",
      "brick-dust",
      "bonus",
      "heart-1",
      "heart-2",
      "flamethrower",
      "flamethrower-pickup",
      "toilet-paper",
      "toilet-paper-pickup",
      "toilet-paper-roll-1",
      "toilet-paper-roll-2",
      "chain-whip",
      "chain-whip-pickup",
      "chain-link-1",
      "chain-link-2",
      "boom-stick",
      "boom-stick-pickup",
      "shotgun-blast-1",
      "shotgun-blast-2",
      "flame-1",
      "flame-2",
      "flame-3",
      "flame-4",
      "player-burning-1",
      "player-burning-2",
      "player-burning-3",
      "player-burning-4",
      "used",
      "pipe",
      "pipe-small-top",
      "pipe-small-body",
      "pipe-small-bottom",
      "pipe-top-left",
      "pipe-top-right",
      "pipe-body-left",
      "pipe-body-right",
      "pipe-bottom-left",
      "pipe-bottom-right",
      "coin",
      "spike",
      "goal",
      "reward-plus-one",
      "moon",
      "tombstone",
      "tombstone-cross",
    })
      _sprites.Add(name, Sprite.Load(Path.Combine(assetsPath, name + ".sprite"), palette));

    foreach (string name in _sprites.Keys.Where(name => name.StartsWith("player-", StringComparison.Ordinal)
      && !name.StartsWith("player-burning-", StringComparison.Ordinal)).ToArray())
    {
      string ellaName = PlayerSprite(PlayerCharacter.Ella, name);
      _sprites.Add(ellaName, Sprite.Load(Path.Combine(assetsPath, ellaName + ".sprite"), palette));
    }
  }

  public PixelBuffer Frame { get; } = new(Width, Height);

  public void Draw(Game game, int direction, double elapsed = Game.Step, int pauseSelection = 0, bool deathContinueReady = true)
  {
    if (game.Lives == 0 && game.AwaitingRestart)
    {
      DrawGameOver(game, deathContinueReady);
      return;
    }

    bool moving = !game.Paused && !game.Won && !game.IsDying && game.Lives > 0;

    if (moving)
    {
      _animationTime += elapsed;

      if (direction != 0)
        _facingLeft = direction < 0;
    }

    int viewHeight = Height - HeaderHeight - FooterHeight;
    int cameraX = Math.Clamp((int)(game.X * TileSize) - (Width / 3), 0, Math.Max(0, (game.Level.Width * TileSize) - Width));
    int cameraY = Math.Clamp((int)(game.Y * TileSize) - ((viewHeight * 2) / 3), 0, Math.Max(0, (game.Level.Height * TileSize) - viewHeight));

    bool night = game.CurrentBackground == BackgroundTheme.Night;
    bool underworld = game.CurrentBackground == BackgroundTheme.Underworld;
    if (underworld)
      DrawUnderworld(cameraX);
    else
      DrawBackground(cameraX, night);

    if (night)
      DrawGraveyard(cameraX, cameraY, game.Level.Height);

    for (int y = cameraY / TileSize; y <= (cameraY + viewHeight) / TileSize; y++)
    {
      for (int x = cameraX / TileSize; x <= (cameraX + Width) / TileSize; x++)
      {
        string? name = game.Level.Tile(x, y) switch
        {
          '#' => underworld ? "sewer-brick" : game.Level.Tile(x, y - 1) == '#' ? "dirt" : "grass",
          '~' => "sludge",
          'B' => underworld ? "sewer-brick" : "brick",
          '?' or 'M' or 'F' or 'Q' or 'W' or 'L' => "bonus",
          'X' => "used",
          'P' => PipeSprite(game.Level, x, y),
          'p' => SkinnyPipeSprite(game.Level, x, y),
          'o' => "coin",
          '^' => "spike",
          'G' => "goal",
          'k' => "pepto-bottle",
          _ => null,
        };

        if (game.Level.Tile(x, y) == 'T')
        {
          int left = x * TileSize - cameraX;
          int top = HeaderHeight + (y - 3) * TileSize - cameraY;
          Frame.Fill(left - 9, top, 50, 2, Ink);
          Frame.Fill(left - 8, top + 2, 2, 63, 0xFFB85A47);
          Frame.Fill(left + 39, top + 2, 2, 63, 0xFFB85A47);
          Frame.Fill(left + 2, top - 12, 28, 9, Ink);
          PixelFont.Draw(Frame, "TOILET", left + 4, top - 10, Gold);
          Frame.Draw(_sprites["toilet"], left, HeaderHeight + (y - 1) * TileSize - cameraY);
        }

        if (game.CanUseReliefPipe(x, y) && game.Level.Tile(x - 1, y) != game.Level.Tile(x, y))
        {
          int signX = x * TileSize - cameraX - 8;
          int signY = HeaderHeight + y * TileSize - cameraY - 20;
          Frame.Fill(signX - 2, signY - 2, 51, 11, Ink);
          PixelFont.Draw(Frame, _touchControls ? "DOWN +5" : "DOWN/S +5", signX, signY, Gold);
        }

        if (name is not null)
          Frame.Draw(_sprites[name], (x * TileSize) - cameraX, HeaderHeight + (y * TileSize) - cameraY);
      }
    }

    if (underworld)
    {
      // Bubbles rise from the brown sludge; the terrain itself remains solid.
      for (int x = cameraX / TileSize; x <= (cameraX + Width) / TileSize; x++)
      {
        for (int y = cameraY / TileSize; y <= (cameraY + viewHeight) / TileSize; y++)
        {
          if (game.Level.Tile(x, y) != '~' || game.Level.Tile(x, y - 1) == '~')
            continue;

          int phase = ((int)(_animationTime * 12) + x * 7) % 24;
          if (phase < 12)
          {
            int bubbleX = x * TileSize - cameraX + 3 + x % 9;
            int bubbleY = HeaderHeight + y * TileSize - cameraY - phase / 3;
            Frame.Fill(bubbleX, bubbleY, 2, 2, 0xFFC97F60);
          }
        }
      }
    }

    if (game.AvailablePipe is { } pipe && (!game.HasEnteredUnderworld || game.Boss is null))
    {
      int signX = pipe.X * TileSize - cameraX - 13;
      int signY = HeaderHeight + pipe.Y * TileSize - cameraY - 31;
      Frame.Fill(signX - 3, signY - 3, 66, 21, Ink);
      PixelFont.Draw(Frame, game.HasEnteredUnderworld ? "TO DAYLIGHT" : "UNDERWORLD", signX, signY, Gold);
      PixelFont.Draw(Frame, "DOWN / S", signX + 6, signY + 10, White);
    }

    foreach (var roll in game.ToiletPaperRolls)
    {
      int x = (int)Math.Round(roll.X * TileSize) - cameraX - 3;
      int y = HeaderHeight + (int)Math.Round(roll.Y * TileSize) - cameraY - 3;
      Frame.Draw(_sprites[$"toilet-paper-roll-{roll.FrameIndex + 1}"], x, y, roll.Direction < 0);
    }

    foreach (var projectile in game.EnemyProjectiles)
    {
      string name = projectile.Kind == Effects.EnemyProjectileKind.BeerBottle ? "beer-bottle" : "needle";
      int px = (int)Math.Round(projectile.X * TileSize) - cameraX - 3;
      int py = HeaderHeight + (int)Math.Round(projectile.Y * TileSize) - cameraY - 6;
      Frame.Draw(_sprites[name], px, py, projectile.VelocityX > 0);
    }

    foreach (var pile in game.PoopPiles)
      Frame.Draw(_sprites["poop"], (pile.X * TileSize) - cameraX, HeaderHeight + (pile.Y * TileSize) - TileSize - cameraY);

    foreach (var poop in game.ThrownPoop)
      Frame.Draw(_sprites["poop"], (int)Math.Round(poop.X * TileSize) - cameraX,
        HeaderHeight + (int)Math.Round((poop.Y + Effects.ThrownPoop.Height) * TileSize) - TileSize - cameraY);

    foreach (var effect in game.BlockBreakEffects)
    {
      var sprite = _sprites[effect.FrameIndex == 0 ? "brick-rubble" : "brick-dust"];
      Frame.Draw(sprite, (effect.X * TileSize) - cameraX, HeaderHeight + (effect.Y * TileSize) - cameraY);
    }

    foreach (var heart in game.GrowthHearts)
      DrawActor(_sprites[heart.IsToiletPaper ? "toilet-paper-pickup" : heart.IsChainWhip ? "chain-whip-pickup" : heart.IsBoomStick ? "boom-stick-pickup" : heart.IsFlamethrower ? "flamethrower-pickup" : $"heart-{heart.FrameIndex + 1}"], heart.X, heart.Y, cameraX, cameraY, heart.Direction < 0);

    if (game.Boss is { } boss && game.X >= boss.PatrolOriginX - 14)
    {
      int labelX = Width / 2 - 42;
      PixelFont.Draw(Frame, $"{boss.Name.ToUpperInvariant()} {boss.Health}/{boss.Settings.PowerLevel}", labelX, HeaderHeight + 4, Gold);
      Frame.Fill(labelX, HeaderHeight + 12, 84, 4, Ink);
      Frame.Fill(labelX + 1, HeaderHeight + 13, 82 * boss.Health / boss.Settings.PowerLevel, 2, 0xFFE83B3B);
    }

    int animationFrame = (int)(_animationTime * 9) % 2;

    foreach (var enemy in game.Enemies.Where(enemy => enemy.Alive))
    {
      if (enemy.IsBuried)
        continue;

      if (enemy.IsEmerging)
      {
        var sprite = _sprites[$"creepy-crawler-emerge-{enemy.EmergenceFrameIndex + 1}"];
        int groundY = HeaderHeight + (int)Math.Round((enemy.Y + enemy.BodyHeight) * TileSize) - cameraY;
        int left = (int)Math.Round((enemy.X + (Game.BodyWidth / 2)) * TileSize) - (sprite.Width / 2) - cameraX;
        int undergroundOffset = (enemy.EmergenceTicksRemaining * (TileSize / 2)) / Enemy.CrawlerEmergenceDurationTicks;
        Frame.Draw(sprite, left, groundY - sprite.Height + undergroundOffset,
          enemy.Direction > 0, clipBottom: groundY);
        continue;
      }

      string name = enemy.Kind switch
      {
        EnemyKind.Fly => $"fly-{animationFrame + 1}",
        EnemyKind.Worm => $"worm-{animationFrame + 1}",
        EnemyKind.PunkBlue => enemy.ThrowPoseTicksRemaining > 0 ? "punk-blue-throw" : $"punk-blue-{animationFrame + 1}",
        EnemyKind.PunkPurple => enemy.ThrowPoseTicksRemaining > 0 ? "punk-purple-throw" : $"punk-purple-{animationFrame + 1}",
        EnemyKind.EmoBoss => enemy.ThrowPoseTicksRemaining > 0 ? "emo-boss-throw" : $"emo-boss-{animationFrame + 1}",
        EnemyKind.OldMan or EnemyKind.Grandpa => enemy.CaneSwingTicksRemaining > Enemy.CaneWaveDurationTicks ? "old-man-windup"
          : enemy.CaneSwingTicksRemaining > 0 ? "old-man-swing" : $"old-man-walk-{animationFrame + 1}",
        EnemyKind.DungBag => $"dung-bag-walk-{animationFrame + 1}",
        EnemyKind.CreepyCrawler => $"creepy-crawler-crawl-{(int)(_animationTime * 4) % 2 + 1}",
        EnemyKind.Zombie => $"zombie-walk-{(int)(_animationTime * 3) % 2 + 1}",
        EnemyKind.Dog => $"dog-walk-{(int)(_animationTime * 6) % 2 + 1}",
        EnemyKind.ToasterCat => Math.Abs(enemy.VelocityY) > 0.01 ? "toaster-cat-jump" : $"toaster-cat-run-{animationFrame + 1}",
        _ => animationFrame == 0 ? "enemy-idle" : "enemy-walk",
      };

      DrawActor(_sprites[name], enemy.X, enemy.Y, cameraX, cameraY, enemy.Direction > 0, bodyHeight: enemy.BodyHeight);
    }

    foreach (var enemy in game.Enemies.Where(enemy => enemy.Alive && enemy.AirWaveLength > 0))
    {
      int origin = (int)Math.Round(enemy.AirWaveX * TileSize) - cameraX;
      int top = HeaderHeight + (int)Math.Round(enemy.AirWaveY * TileSize) - cameraY;
      int reach = (int)Math.Round(enemy.AirWaveLength * TileSize);
      for (int row = 1; row < 15; row++)
      {
        int curve = Math.Abs(8 - row) / 3;
        int front = origin + enemy.AttackDirection * Math.Max(0, reach - curve);
        Frame.Fill(front - (enemy.AttackDirection > 0 ? 2 : 0), top + row, Math.Min(2, reach), 1, 0xFF6FBBD1);
        if (row % 4 == 0)
          Frame.Fill(Math.Min(origin, front), top + row, Math.Abs(front - origin), 1, White);
      }
    }

    foreach (var enemy in game.Enemies.Where(enemy => enemy.SquashTicksRemaining > 0))
    {
      string name = enemy.Kind switch
      {
        EnemyKind.Fly => "fly-squashed",
        EnemyKind.Worm => "worm-squashed",
        EnemyKind.PunkBlue => "punk-blue-squashed",
        EnemyKind.PunkPurple => "punk-purple-squashed",
        EnemyKind.EmoBoss => "emo-boss-squashed",
        EnemyKind.OldMan or EnemyKind.Grandpa => "old-man-squashed",
        EnemyKind.DungBag => "dung-bag-squashed",
        EnemyKind.CreepyCrawler => "creepy-crawler-squashed",
        EnemyKind.Zombie => "zombie-squashed",
        EnemyKind.Dog => "dog-squashed",
        EnemyKind.ToasterCat => "toaster-cat-squashed",
        _ => "enemy-squashed",
      };

      DrawActor(_sprites[name], enemy.X, enemy.Y, cameraX, cameraY, enemy.Direction > 0, bodyHeight: enemy.BodyHeight);
    }

    string playerFrame = !game.Grounded ? "player-jump"
      : moving && direction != 0 ? $"player-run-{animationFrame + 1}" : "player-idle";

    if (game.IsLarge)
      playerFrame = playerFrame.Replace("player-", "player-large-", StringComparison.Ordinal);

    if (game.IsSeatedOnToilet)
    {
      Frame.Draw(_sprites[game.Character == Actors.PlayerCharacter.Ella ? "ella-toilet-sit" : "player-toilet-sit"],
        game.ToiletX * TileSize - cameraX, HeaderHeight + (game.ToiletY - 1) * TileSize - cameraY);
    }
    else if (game.IsExploding)
    {
      if (game.IsExplosionVisible)
        DrawActor(_sprites[PlayerSprite(game.Character, $"player-explosion-{game.ExplosionFrameIndex + 1}")], game.X, game.Y,
          cameraX, cameraY, _facingLeft, true, game.PlayerHeight);
    }
    else if (game.IsDying)
      DrawPlayerDeath(game, cameraX, cameraY);
    else if (game.PipeReliefTicksRemaining > 0)
    {
      bool facingLeft = game.FacingDirection < 0;
      int puffX = (int)Math.Round((game.X + (facingLeft ? Game.BodyWidth : 0)) * TileSize) - cameraX;
      int puffY = HeaderHeight + (int)Math.Round((game.Y + game.PlayerHeight - 0.25) * TileSize) - cameraY;
      int drift = 3 + (Game.PipeReliefDurationTicks - game.PipeReliefTicksRemaining) % 5;
      Frame.Fill(puffX + (facingLeft ? drift : -drift - 5), puffY - 3, 5, 3, 0xFF754A44);
      Frame.Fill(puffX + (facingLeft ? 7 : -10), puffY - 6, 3, 3, 0xFF82BD58);
      DrawActor(_sprites[PlayerSprite(game.Character, game.IsLarge ? "player-large-idle" : "player-idle")], game.X, game.Y,
        cameraX, cameraY, facingLeft, bodyHeight: game.PlayerHeight);
    }
    else if ((game.DamageTicksRemaining / 5) % 2 == 0)
      DrawActor(_sprites[PlayerSprite(game.Character, playerFrame)], game.X, game.Y, cameraX, cameraY, _facingLeft, bodyHeight: game.PlayerHeight);

    if ((game.HasFlamethrower || game.HasBoomStick || game.HasChainWhip || game.HasToiletPaper) && !game.IsDying && !game.IsFinishingToilet && game.PipeReliefTicksRemaining == 0 && (game.DamageTicksRemaining / 5) % 2 == 0)
    {
      int weaponX = (int)Math.Round((game.X + (Game.BodyWidth / 2)) * TileSize) - 8 - cameraX;
      int weaponY = HeaderHeight + (int)Math.Round((game.Y + game.PlayerHeight - 0.65) * TileSize) - 8 - cameraY;
      Frame.Draw(_sprites[game.HasToiletPaper ? "toilet-paper" : game.HasChainWhip ? "chain-whip" : game.HasBoomStick ? "boom-stick" : "flamethrower"], weaponX, weaponY, game.FacingDirection < 0);
    }

    if (game.IsBurning && !game.IsDying)
    {
      var burning = _sprites[$"player-burning-{game.BurnFrameIndex + 1}"];
      int burnX = (int)Math.Round((game.X + (Game.BodyWidth / 2)) * TileSize) - (burning.Width / 2) - cameraX;
      int burnY = HeaderHeight + (int)Math.Round((game.Y + game.PlayerHeight) * TileSize) - burning.Height - cameraY;
      Frame.DrawTranslucent(burning, burnX, burnY, 200);
    }

    if (game.WhipSwing is { Length: > 0 } whip)
    {
      var sprite = _sprites[$"chain-link-{whip.FrameIndex % 2 + 1}"];
      int originX = (int)Math.Round(whip.X * TileSize) - cameraX;
      int originY = HeaderHeight + (int)Math.Round(whip.Y * TileSize) - 8 - cameraY;
      int reach = (int)(whip.Length * TileSize);

      for (int distance = 0; distance < reach; distance++)
      {
        int curve = (int)Math.Round(Math.Sin(Math.PI * distance / Math.Max(1, reach)) * (whip.FrameIndex == 1 ? -3 : 3));

        for (int y = 0; y < sprite.Height; y++)
        {
          uint color = sprite.Pixel(distance % sprite.Width, y);

          if (color != 0)
            Frame.Fill(originX + (whip.Direction > 0 ? distance : -distance - 1), originY + y + curve, 1, 1, color);
        }
      }
    }

    if (game.Flame is { } flame)
    {
      var sprite = _sprites[$"flame-{flame.FrameIndex + 1}"];
      int originX = (int)Math.Round(flame.X * TileSize) - cameraX;
      int originY = HeaderHeight + (int)Math.Round(flame.Y * TileSize) - 8 - cameraY;
      for (int distance = 0; distance < (int)(flame.Length * TileSize); distance++)
      {
        for (int y = 0; y < sprite.Height; y++)
        {
          uint color = sprite.Pixel(distance % TileSize, y);
          if (color != 0)
            Frame.Fill(originX + (flame.Direction > 0 ? distance : -distance - 1), originY + y, 1, 1, color);
        }
      }
    }

    if (game.ShotgunBlast is { } blast)
    {
      var sprite = _sprites[$"shotgun-blast-{blast.FrameIndex + 1}"];
      int originX = (int)Math.Round(blast.X * TileSize) - cameraX;
      int originY = HeaderHeight + (int)Math.Round(blast.Y * TileSize) - 8 - cameraY;
      for (int distance = 0; distance < (int)(blast.Length * TileSize); distance++)
      {
        for (int y = 0; y < sprite.Height; y++)
        {
          uint color = sprite.Pixel(distance % TileSize, y);
          if (color != 0)
            Frame.Fill(originX + (blast.Direction > 0 ? distance : -distance - 1), originY + y, 1, 1, color);
        }
      }
    }

    if (game.IsFinishingToilet)
    {
      int left = game.ToiletX * TileSize - cameraX - 8;
      int top = HeaderHeight + (game.ToiletY - 3) * TileSize - cameraY + 3;
      int width = 48 * Math.Clamp(game.ToiletFinishAgeTicks - 30, 0, 60) / 60;
      int curtainLeft = left + 48 - width;
      Frame.Fill(curtainLeft, top, width, 59, 0xFF8745B5);
      for (int fold = 0; fold < width; fold += 6)
      {
        Frame.Fill(curtainLeft + fold, top + 2, 2, 54, 0xFF61338B);
        Frame.Fill(curtainLeft + fold + 2, top + 2, Math.Clamp(width - fold - 2, 0, 1), 54, 0xFFC58AE8);
      }

      if (width == 48)
      {
        Frame.Fill(left + 4, top + 18, 40, 11, Ink);
        PixelFont.Draw(Frame, "OCCUPIED", left + 8, top + 21, Gold);
      }
    }

    foreach (var popup in game.RewardPopups)
    {
      var rewardSprite = _sprites["reward-plus-one"];
      int x = (popup.X * TileSize) - cameraX + ((TileSize - rewardSprite.Width) / 2);
      int y = HeaderHeight
        + (popup.Y * TileSize)
        - cameraY
        - rewardSprite.Height - 2
        - ((popup.AgeTicks * 8) / CoinRewardPopup.DurationTicks);

      Frame.Draw(rewardSprite, x, y);
    }

    if (game.Won && game.Credits is { } credits)
    {
      DrawCredits(game, credits);
      if (game.Paused)
        DrawPauseMenu(pauseSelection);

      return;
    }

    DrawHud(game);

    if (game.PoopSplatterTicksRemaining > 0)
      Frame.DrawTranslucent(_sprites["poop-screen-splatter"], (Width - 128) / 2, 64, 140);

    if (!game.IsDying && game.FlamethrowerQuoteTicksRemaining > 0)
      DrawWeaponQuote(game.FlamethrowerQuote, "FLAMETHROWER!");
    if (!game.IsDying && game.BoomStickQuoteTicksRemaining > 0)
      DrawWeaponQuote(game.BoomStickQuote, "BOOM-STICK!");

    if (!game.IsDying && game.EnemyQuoteTicksRemaining > 0)
      DrawWeaponQuote(game.EnemyQuote, "TRASH TALK");

    if (game.PipeTravelTicksRemaining > 0)
    {
      int half = Game.PipeTravelDurationTicks / 2;
      int distance = Math.Abs(game.PipeTravelTicksRemaining - half);
      Frame.OverlayBlack((byte)(255 * (half - distance) / half));
      DrawTitleText(game.CurrentBackground == BackgroundTheme.Underworld ? "THE UNDERWORLD" : "DAYLIGHT", 105, Gold, 2);
    }

    if (game.IsDying)
    {
      DrawDeathMessage(game.DeathQuote);
      string Prompt = !game.AwaitingRestart || !deathContinueReady ? "PLEASE WAIT..."
        : _touchControls ? "JUMP OR START TO CONTINUE"
        : game.Lives == 0 ? "PRESS ENTER TO START OVER" : "PRESS ANY KEY TO CONTINUE";
      int promptScale = game.AwaitingRestart ? 2 : 1;
      int promptY = game.AwaitingRestart ? 112 : Height - 9;
      if (!game.AwaitingRestart)
        Frame.Fill(0, Height - FooterHeight, Width, FooterHeight, Ink);
      int promptX = (Width - (((Prompt.Length * 4) - 1) * promptScale)) / 2;
      PixelFont.Draw(Frame, Prompt, promptX + 1, promptY + 1, Ink, promptScale);
      PixelFont.Draw(Frame, Prompt, promptX, promptY, Gold, promptScale);
    }
    else if (game.Paused && !game.Won && game.Lives > 0)
      DrawPauseMenu(pauseSelection);
    else if (game.Won || game.Lives == 0)
    {
      string title = game.Won ? "LEVELS COMPLETE!" : game.Lives == 0 ? "GAME OVER" : "PAUSED";
      const string hint = "R TO PLAY AGAIN";

      Frame.Fill(70, 90, 244, 60, Ink);
      Frame.Fill(72, 92, 240, 1, Gold);
      PixelFont.Draw(Frame, title, (Width - (title.Length * 8)) / 2, 105, Gold, 2);
      PixelFont.Draw(Frame, hint, (Width - (hint.Length * 4)) / 2, 130, White);
    }
  }

  public void DrawLeaderboard(LeaderboardScreen screen)
  {
    Frame.Clear(screen.Context == LeaderboardContext.GameOver ? 0xFF781C29 : Ink);
    Frame.Fill(12, 10, Width - 24, 1, Gold);
    if (screen.IsEnteringInitials)
    {
      DrawTitleText("TOP 10 SCORE!", 24, Gold, 3);
      DrawTitleText($"POINTS {screen.Score}  RANK {screen.Board.RankFor(screen.Score)}", 54, White, 2);
      DrawTitleText(screen.Context == LeaderboardContext.Victory ? "ALL LEVELS COMPLETE" : "GAME OVER", 78, White, 1);
      if (screen.Context == LeaderboardContext.GameOver)
        Frame.Draw(_sprites["tombstone"], (Width / 2) - 48, 66);

      for (int index = 0; index < 3; index++)
      {
        int left = 130 + (index * 44);
        bool selected = index == screen.SelectedInitialIndex;
        Frame.Fill(left, 101, 36, 42, selected ? Gold : 0xFF527787);
        Frame.Fill(left + 2, 103, 32, 38, Ink);
        string letter = index < screen.Initials.Length ? screen.Initials[index].ToString() : "-";
        PixelFont.Draw(Frame, letter, left + 12, 112, selected ? Gold : White, 4);
        if (selected && letter == " ")
          PixelFont.Draw(Frame, "SPACE", left + 8, 147, Gold);
      }

      DrawTitleText("A-Z / 0-9 / SPACE", 163, Gold, 2);
      DrawTitleText("UP/DOWN: CHARACTER", 184, White, 1);
      DrawTitleText("LEFT/RIGHT: POSITION", 195, White, 1);
      string accept = !_touchControls ? "ENTER: ACCEPT" : screen.SelectedInitialIndex < 2
        ? "JUMP: NEXT  START: ACCEPT" : "JUMP / START: ACCEPT";
      DrawTitleText(accept, 211, Gold, 1);
      if (_touchControls)
        DrawTitleText("ATTACK: CANCEL", 225, White, 1);

      return;
    }

    DrawTitleText("TOP 10", 18, Gold, 3);
    PixelFont.Draw(Frame, "RANK", 32, 45, White);
    PixelFont.Draw(Frame, "NAME", 80, 45, White);
    PixelFont.Draw(Frame, "POINTS", 152, 45, White);
    PixelFont.Draw(Frame, "DATE", 268, 45, White);
    for (int index = 0; index < Leaderboard.Capacity; index++)
    {
      int top = 61 + (index * 14);
      bool highlighted = index + 1 == screen.SubmittedRank;
      if (highlighted)
        Frame.FillTranslucent(24, top - 2, Width - 48, 13, Gold, 45);

      uint color = highlighted ? Gold : White;
      PixelFont.Draw(Frame, (index + 1).ToString("00"), 36, top + 2, color);
      if (index < screen.Board.Entries.Count)
      {
        var entry = screen.Board.Entries[index];
        PixelFont.Draw(Frame, entry.Initials, 80, top, color, 2);
        PixelFont.Draw(Frame, entry.Score.ToString(System.Globalization.CultureInfo.InvariantCulture), 152, top + 2, color);
        PixelFont.Draw(Frame, entry.RecordedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), 256, top + 2, color);
      }
      else
        PixelFont.Draw(Frame, "---", 80, top + 2, 0xFF838B98);
    }

    Frame.Fill(12, 210, Width - 24, 1, Gold);
    string prompt = screen.Context == LeaderboardContext.Victory ? (_touchControls ? "PRESS JUMP FOR CREDITS" : "PRESS ENTER FOR CREDITS") :
      screen.Context == LeaderboardContext.GameOver ? (_touchControls ? "PRESS JUMP TO START OVER" : "PRESS ENTER TO START OVER") : (_touchControls ? "JUMP ACCEPT  ATTACK CANCEL" : "ENTER / ESC BACK");
    DrawTitleText(prompt, 224, Gold, 1);
  }

  public void DrawLevelSelect(LevelSelectScreen screen)
  {
    Frame.Clear(Ink);
    DrawTitleText("LEVEL SELECT", 12, Gold, 3);
    PixelFont.Draw(Frame, "NO.", 24, 44, Gold);
    PixelFont.Draw(Frame, "NAME", 64, 44, Gold);
    PixelFont.Draw(Frame, "THEME", 300, 44, Gold);
    Frame.Fill(10, 54, Width - 20, 1, Gold);
    int first = screen.Page * LevelSelectScreen.PageSize;

    for (int row = 0; row < LevelSelectScreen.PageSize && first + row < screen.Levels.Count; row++)
    {
      int index = first + row;
      var level = screen.Levels[index];
      int y = 64 + row * 16;
      bool selected = index == screen.SelectedIndex;

      if (selected)
      {
        Frame.Fill(10, y - 4, Width - 20, 13, 0xFF355575);
        PixelFont.Draw(Frame, ">", 13, y, Gold);
      }

      uint color = selected ? Gold : White;
      PixelFont.Draw(Frame, level.DisplayNumber, 24, y, color);
      PixelFont.Draw(Frame, level.Name, 64, y, color);
      PixelFont.Draw(Frame, level.Theme == BackgroundTheme.Underworld ? "SEWAGE" : level.Theme == BackgroundTheme.Night ? "NIGHTTIME" : "DAYTIME", 300, y, color);
    }

    DrawTitleText($"PAGE {screen.Page + 1}/{screen.PageCount}  LEVELS {screen.Levels.Count}", 196, Gold, 1);
    DrawTitleText(_touchControls ? "UP/DOWN SELECT  LEFT/RIGHT PAGE" : "UP/DOWN SELECT  LEFT/RIGHT OR PGUP/PGDN PAGE", 213, White, 1);
    DrawTitleText(_touchControls ? "JUMP PLAY  ATTACK CANCEL" : "ENTER PLAY  ESC BACK", 227, White, 1);
  }

  public void DrawLevelStart(LevelStartScreen screen)
  {
    Frame.Clear(Ink);
    Frame.Fill(12, 18, Width - 24, 1, Gold);
    DrawTitleText($"LEVEL {screen.LevelLabel}", 32, Gold, 4);
    DrawWrappedCentered(screen.LevelName, 66, White, maxLines: 2);
    string pose = screen.IsLarge ? "player-large-idle" : "player-idle";
    var player = _sprites[PlayerSprite(screen.Character, pose)];
    Frame.Draw(player, 148, 142 - player.Height);
    PixelFont.Draw(Frame, $"LIVES {screen.Lives}", 180, 125, White, 2);
    DrawTitleText("UNCLE QUOTE", 155, Gold, 1);
    DrawWrappedCentered(screen.Quote, 170, White);
    Frame.Fill(12, 218, Width - 24, 1, 0xFF355575);
    DrawTitleText(_touchControls ? "PRESS JUMP TO BEGIN" : "PRESS ANY KEY TO BEGIN", 227, Gold, 1);
  }

  public void DrawSettings(SettingsScreen screen)
  {
    Frame.OverlayBlack(80);
    Frame.FillTranslucent(24, 24, Width - 48, 214, Ink, 225);
    Frame.FillTranslucent(26, 28, Width - 52, 32, Ink, 210);
    Frame.Fill(24, 24, Width - 48, 1, Gold);
    Frame.Fill(24, 237, Width - 48, 1, 0xFF527787);
    DrawTitleText("SETTINGS", 36, Gold, 3);

    DrawAudioToggle("BACKGROUND MUSIC", 68, screen.Audio.MusicEnabled, screen.SelectedIndex == 0);
    DrawVolumeSlider("MUSIC VOLUME", 99, screen.Audio.MusicVolume, screen.SelectedIndex == 1);
    DrawAudioToggle("SOUND FX", 124, screen.Audio.SoundEffectsEnabled, screen.SelectedIndex == 2);
    DrawVolumeSlider("SOUND FX VOLUME", 150, screen.Audio.SoundEffectsVolume, screen.SelectedIndex == 3);
    if (screen.UpdatesSupported)
    {
      DrawAudioToggle("OPT-IN PRE-RELEASE", 169, screen.Updates.IncludePrereleases, screen.SelectedIndex == 4);
      PixelFont.Draw(Frame, "(EXPERIMENTAL)", 52, 183, screen.SelectedIndex == 4 ? Gold : White);
      DrawTitleText("UPDATE SETTING APPLIES NEXT LAUNCH", 197, White, 1);
    }

    Frame.FillTranslucent(144, 207, 96, 17, Ink, 180);
    if (screen.SelectedIndex == screen.BackIndex)
      Frame.FillTranslucent(144, 207, 96, 17, Gold, 45);

    DrawTitleText(screen.SelectedIndex == screen.BackIndex ? "> BACK <" : "BACK", 212,
      screen.SelectedIndex == screen.BackIndex ? Gold : White, 1);

    DrawTitleText(_touchControls ? "ARROWS ADJUST  JUMP ACCEPT  ATTACK CANCEL" : "ARROWS ADJUST  ENTER/SPACE TOGGLE  ESC BACK", 230, White, 1);
  }

  public void DrawTitle(TitleScreen title, string updateStatus = "")
  {
    DrawBackground((int)(title.Elapsed * 10) % (Width * 4));
    Frame.OverlayBlack(100);
    DrawTitleText(updateStatus, 12, White, 1);
    DrawTitleText("Sam n' Ella", 34, Gold, 5, shadow: true);
    if (title.KonamiCodeEnabled)
      DrawTitleText("KONAMI CODE ACTIVATED - 30 LIVES", 72, Gold, 1);
    DrawTitleText("PLAYER SELECT", 86, White, 2);

    int animationFrame = (int)(title.Elapsed * 6) % 2 + 1;
    foreach (PlayerCharacter character in Enum.GetValues<PlayerCharacter>())
    {
      int left = character == PlayerCharacter.Sam ? 76 : 202;
      bool selected = title.SelectedCharacter == character;
      Frame.Fill(left, 97, 106, 69, selected ? Gold : 0xFF527787);
      Frame.Fill(left + 2, 99, 102, 65, Ink);
      Frame.Draw(_sprites[PlayerSprite(character, $"player-large-run-{animationFrame}")], left + 45, 108);
      string name = character.ToString().ToUpperInvariant();
      PixelFont.Draw(Frame, name, left + (106 - ((name.Length * 4) - 1)) / 2, 149, selected ? Gold : White);
      if (selected)
        PixelFont.Draw(Frame, ">", left + 10, 126, Gold);
    }

    DrawTitleText(_touchControls ? "LEFT/RIGHT PLAYER  SELECT MENU  JUMP ACCEPT" : "LEFT/RIGHT PLAYER  UP/DOWN MENU  ENTER SELECT", 173, White, 1);

    long ticks = (long)(title.Elapsed * 60);
    DrawCreditsRunner($"player-run-{animationFrame}", ticks, 0, 38, false, 224);
    DrawCreditsRunner($"ella-run-{animationFrame}", ticks, 215, 38, false, 224);
    DrawCreditsRunner($"toaster-cat-run-{animationFrame}", ticks, 140, -45, false, 224);
    DrawCreditsRunner((int)(title.Elapsed * 4) % 2 == 0 ? "enemy-idle" : "enemy-walk", ticks, 290, -24, false, 224);
    DrawCreditsRunner($"zombie-walk-{(int)(title.Elapsed * 3) % 2 + 1}", ticks, 380, -12, false, 224);
    DrawCreditsRunner($"dog-walk-{animationFrame}", ticks, 110, -28, false, 224);
    DrawMenuChoice("START GAME", 184, title.SelectedMenuIndex == 0);
    DrawMenuChoice("SETTINGS", 196, title.SelectedMenuIndex == 1);
    DrawMenuChoice("TOP 10", 208, title.SelectedMenuIndex == 2);
    Frame.Fill(0, 224, Width, 2, Gold);
    Frame.Fill(0, 226, Width, 14, Ink);

    PixelFont.Draw(Frame, VersionLabel, 6, Height - 9, White, preserveCase: true);

    const string Copyright = "Â© 2026 XENO INNOVATIONS, INC.";
    PixelFont.Draw(Frame, Copyright, Width - (Copyright.Length * 4) - 5, Height - 9, White);
  }

  private static bool IsPipeRow(Level level, int left, int width, int y)
  {
    if (level.Tile(left - 1, y) == 'P' || level.Tile(left + width, y) == 'P')
      return false;

    for (int offset = 0; offset < width; offset++)
    {
      if (level.Tile(left + offset, y) != 'P')
        return false;
    }

    return true;
  }

  private static string PipeSprite(Level level, int x, int y)
  {
    int left = x;
    int right = x;

    while (level.Tile(left - 1, y) == 'P')
      left--;

    while (level.Tile(right + 1, y) == 'P')
      right++;

    int width = right - left + 1;

    if (width > 2)
      return "pipe";

    int top = y;
    int bottom = y;

    while (IsPipeRow(level, left, width, top - 1))
      top--;

    while (IsPipeRow(level, left, width, bottom + 1))
      bottom++;

    // Irregular groups retain the original artwork rather than mismatched pipe pieces.
    for (int offset = 0; offset < width; offset++)
    {
      if (level.Tile(left + offset, top - 1) == 'P' || level.Tile(left + offset, bottom + 1) == 'P')
        return "pipe";
    }

    if (top == bottom)
      return "pipe";

    string section = y == top ? "top" : y == bottom ? "bottom" : "body";

    return width == 1
      ? $"pipe-small-{section}"
      : $"pipe-{section}-{(x == left ? "left" : "right")}";
  }

  private static string PlayerSprite(PlayerCharacter character, string name) =>
    character == PlayerCharacter.Ella ? name.Replace("player-", "ella-", StringComparison.Ordinal) : name;

  private static string SkinnyPipeSprite(Level level, int x, int y)
  {
    bool above = level.Tile(x, y - 1) == 'p';
    bool below = level.Tile(x, y + 1) == 'p';

    return (above, below) switch
    {
      (false, false) => "pipe",
      (false, true) => "pipe-small-top",
      (true, false) => "pipe-small-bottom",
      _ => "pipe-small-body",
    };
  }

  private void DrawActor(Sprite sprite, double x, double y, int cameraX, int cameraY, bool flip, bool keepInView = false, double bodyHeight = Game.BodyHeight)
  {
    int px = (int)Math.Round((x + (Game.BodyWidth / 2)) * TileSize) - (sprite.Width / 2) - cameraX;
    int py = HeaderHeight + (int)Math.Round((y + bodyHeight) * TileSize) - sprite.Height - cameraY;

    if (keepInView)
    {
      px = Math.Clamp(px, 0, Width - sprite.Width);
      py = Math.Clamp(py, HeaderHeight, Height - FooterHeight - sprite.Height);
    }

    Frame.Draw(sprite, px, py, flip);
  }

  private void DrawAudioToggle(string label, int y, bool enabled, bool selected)
  {
    if (selected)
      Frame.FillTranslucent(36, y - 5, Width - 72, 24, Gold, 40);

    PixelFont.Draw(Frame, selected ? ">" : " ", 40, y + 3, Gold);
    PixelFont.Draw(Frame, label, 52, y + 3, selected ? Gold : White);
    const int SwitchX = 272;
    Frame.Fill(SwitchX, y - 1, 64, 16, enabled ? 0xFF3A806C : 0xFF535D70);
    Frame.Fill(SwitchX + (enabled ? 47 : 2), y + 1, 15, 12, enabled ? White : 0xFFAFB4BF);
    PixelFont.Draw(Frame, enabled ? "ON" : "OFF", SwitchX + (enabled ? 10 : 27), y + 4, White);
  }

  private void DrawBackground(int cameraX, bool night = false)
  {
    for (int y = 0; y < Height; y++)
    {
      uint color = night
        ? y < 70 ? 0xFF101B35 : y < 125 ? 0xFF1A2946 : 0xFF243A50
        : y < 70 ? 0xFF608CC8 : y < 125 ? 0xFF79ADD7 : 0xFF9BCEE1;
      Frame.Fill(0, y, Width, 1, color);
    }

    if (night)
    {
      for (int index = 0; index < 22; index++)
      {
        int x = ((index * 53) + 17) % Width;
        int y = 34 + ((index * 29) % 66);
        Frame.Fill(x, y, 1, 1, 0xFFBFD7E8);
      }

      Frame.Draw(_sprites["moon"], 295, 38);
    }
    else
    {
      Frame.Fill(301, 45, 20, 20, 0xFFFFDF8A);
      Frame.Fill(297, 49, 28, 12, 0xFFFFDF8A);
    }

    for (int i = -1; i < 5; i++)
    {
      int x = (i * 112) - ((cameraX / 5) % 112);
      int y = 55 + ((i & 1) * 17);
      Frame.Fill(x + 8, y, 25, 6, night ? 0xFF2D405A : 0xFFDEEFF4);
      Frame.Fill(x, y + 6, 47, 9, night ? 0xFF2D405A : 0xFFDEEFF4);
      Frame.Fill(x + 5, y + 15, 35, 3, night ? 0xFF23344E : 0xFFB6DCEB);
    }

    for (int x = 0; x < Width; x++)
    {
      int far = Math.Abs(((x + (cameraX / 6)) % 180) - 90);
      int near = Math.Abs(((x + (cameraX / 3) + 60) % 150) - 75);
      int farTop = 110 + (far / 2);
      int nearTop = 150 + (near / 2);
      Frame.Fill(x, farTop, 1, Height - farTop, night ? 0xFF253854 : 0xFF679DA5);
      Frame.Fill(x, nearTop, 1, Height - nearTop, night ? 0xFF1B3041 : 0xFF4A887D);
    }
  }

  private void DrawCredits(Game game, EndCreditsScreen credits)
  {
    const int ViewTop = 12;
    const int ViewBottom = 198;
    const int ScrollSpeed = 30;
    Frame.OverlayBlack(180);
    long maximumOffset = ViewBottom + credits.ClosingOffset - 72;
    int offset = (int)Math.Min(maximumOffset, game.CreditsTicks * ScrollSpeed / 60);
    foreach (var line in credits.Lines)
    {
      int y = ViewBottom + line.Y - offset;
      if (y >= ViewBottom || y + (5 * line.Scale) <= ViewTop)
        continue;

      int x = (Width - (((line.Text.Length * 4) - 1) * line.Scale)) / 2;
      PixelFont.Draw(Frame, line.Text, x + 1, y + 1, Ink, line.Scale, ViewTop, ViewBottom, line.PreserveCase);
      PixelFont.Draw(Frame, line.Text, x, y, line.Highlight ? Gold : White, line.Scale, ViewTop, ViewBottom, line.PreserveCase);
    }

    int animationFrame = (int)(game.CreditsTicks / 6 % 2);
    Frame.Fill(0, 239, Width, 1, 0xFF355575);
    DrawCreditsRunner($"player-run-{animationFrame + 1}", game.CreditsTicks, 0, 46, false);
    DrawCreditsRunner($"player-large-run-{animationFrame + 1}", game.CreditsTicks, 160, 38, false);
    DrawCreditsRunner($"ella-large-run-{animationFrame + 1}", game.CreditsTicks, 340, 38, false);
    DrawCreditsRunner(animationFrame == 0 ? "enemy-idle" : "enemy-walk", game.CreditsTicks, 290, -32, false);
    DrawCreditsRunner(animationFrame == 0 ? "enemy-walk" : "enemy-idle", game.CreditsTicks, 80, 54, true);
    DrawCreditsRunner($"toaster-cat-run-{animationFrame + 1}", game.CreditsTicks, 380, -50, false);
    DrawCreditsRunner($"zombie-walk-{game.CreditsTicks / 20 % 2 + 1}", game.CreditsTicks, 220, -20, false);
    DrawCreditsRunner($"dog-walk-{animationFrame + 1}", game.CreditsTicks, 110, -28, false);

    if (game.Paused)
      PixelFont.Draw(Frame, "PAUSED", Width - 30, 4, Gold);
  }

  private void DrawCreditsRunner(string name, long ticks, int phase, int speed, bool flip, int bottom = 238)
  {
    const int WrapWidth = Width + 64;
    long distance = (ticks * speed / 60) + phase;
    int x = (int)(((distance % WrapWidth) + WrapWidth) % WrapWidth) - 32;
    var sprite = _sprites[name];
    Frame.Draw(sprite, x, bottom - sprite.Height, flip);
  }

  private void DrawDeathMessage(string quote)
  {
    const string Title = "YOU DIED!";
    const int TitleScale = 4;
    const int QuoteScale = 2;
    const int MaxQuoteCharacters = 42;
    const int MaxQuoteLines = 3;
    const uint TitleColor = 0xFFFF6B6B;
    int titleX = (Width - (((Title.Length * 4) - 1) * TitleScale)) / 2;
    PixelFont.Draw(Frame, Title, titleX + 2, 40, Ink, TitleScale);
    PixelFont.Draw(Frame, Title, titleX, 38, TitleColor, TitleScale);

    string remaining = quote.Trim();
    for (int lineIndex = 0; lineIndex < MaxQuoteLines && remaining.Length > 0; lineIndex++)
    {
      int length = Math.Min(MaxQuoteCharacters, remaining.Length);
      if (length < remaining.Length)
      {
        int space = remaining.LastIndexOf(' ', length - 1, length);
        if (space > 0)
          length = space;
      }

      string line = remaining[..length].Trim();
      remaining = remaining[length..].TrimStart();
      if (lineIndex == MaxQuoteLines - 1 && remaining.Length > 0)
        line = line[..Math.Min(line.Length, MaxQuoteCharacters - 3)] + "...";

      int x = (Width - (((line.Length * 4) - 1) * QuoteScale)) / 2;
      int y = 66 + (lineIndex * 12);
      PixelFont.Draw(Frame, line, x + 1, y + 1, Ink, QuoteScale);
      PixelFont.Draw(Frame, line, x, y, White, QuoteScale);
    }
  }

  private void DrawGameOver(Game game, bool deathContinueReady)
  {
    Frame.Clear(0xFF781C29);
    Frame.Fill(12, 12, Width - 24, 1, 0xFFFF6B6B);
    DrawTitleText("GAME OVER", 24, White, 4);
    DrawTitleText("OUT OF LIVES. OUT OF EXCUSES.", 54, Gold, 1);

    const int Scale = 3;
    const int StoneY = 72;
    var stone = _sprites["tombstone"];
    int stoneX = (Width - (stone.Width * Scale)) / 2;
    Frame.Fill(stoneX - 8, StoneY + (stone.Height * Scale), (stone.Width * Scale) + 16, 3, 0xFF43101B);

    for (int y = 0; y < stone.Height; y++)
    {
      for (int x = 0; x < stone.Width; x++)
      {
        uint color = stone.Pixel(x, y);
        if ((color >> 24) != 0)
          Frame.Fill(stoneX + (x * Scale), StoneY + (y * Scale), Scale, Scale, color);
      }
    }

    DrawTitleText("YOU DIED", 151, White, 2);
    DrawWrappedCentered(game.GameOverQuote, 171, White, maxLines: 2);
    DrawTitleText($"LEVEL {game.LevelLabel}  SCORE {game.Score:000000}  DEATHS {game.TotalDeaths}", 202, White, 1);

    Frame.Fill(12, 215, Width - 24, 1, 0xFFFF6B6B);
    DrawTitleText(deathContinueReady ? (_touchControls ? "JUMP OR START TO BEGIN AGAIN" : "PRESS ENTER TO START OVER")
      : "PLEASE WAIT...", 226, Gold, 1);
  }

  private void DrawGraveyard(int cameraX, int cameraY, int levelHeight)
  {
    int groundY = HeaderHeight + ((levelHeight - 2) * TileSize) - cameraY;
    for (int index = -1; index < 7; index++)
    {
      int farX = (index * 80) + 48 - ((cameraX / 2) % 80);
      Frame.DrawTranslucent(_sprites["tombstone-cross"], farX, groundY - 32, 150);
      int nearX = (index * 80) + 16 - (((cameraX * 3) / 4) % 80);
      var stone = _sprites[index % 2 == 0 ? "tombstone" : "tombstone-cross"];
      Frame.Draw(stone, nearX, groundY - stone.Height);
    }
  }

  private void DrawHud(Game game)
  {
    string message = _touchControls ? game.Message.Replace("X or CTRL", "ATTACK", StringComparison.OrdinalIgnoreCase)
      .Replace("Down or S", "DOWN", StringComparison.OrdinalIgnoreCase) : game.Message;
    Frame.Fill(0, 0, Width, HeaderHeight, Ink);
    Frame.Fill(0, HeaderHeight - 2, Width, 2, 0xFF355575);
    if (game.Level.IsSpeedRun)
    {
      PixelFont.Draw(Frame, $"SCORE {game.Score:000000}", 8, 8, White);
      PixelFont.Draw(Frame, "SPEED RUNS", 80, 8, Gold);
      PixelFont.Draw(Frame, message[..Math.Min(38, message.Length)], 8, 19, 0xFFA7CBDC);
      PixelFont.Draw(Frame, $"{game.LevelLabel}  LIVES {game.Lives}", 278, 8, White);
      PixelFont.Draw(Frame, $"COINS {game.Coins:00}", 278, 19, White);
      string countdown = game.LevelSecondsRemaining.ToString("00");
      int timerX = (Width - ((countdown.Length * 4 - 1) * 2)) / 2;
      PixelFont.Draw(Frame, "TIME", (Width - 15) / 2, 2, Gold);
      PixelFont.Draw(Frame, countdown, timerX, 11, game.LevelSecondsRemaining <= 3 ? 0xFFFF6B6B : Gold, 2);
    }
    else
    {
      PixelFont.Draw(Frame, "Sam n' Ella", 8, 8, Gold);
      PixelFont.Draw(Frame, $"SCORE {game.Score:000000}   COINS {game.Coins:00}   LIVES {game.Lives}   {game.LevelLabel}", 76, 8, White);
      string seconds = game.LevelSecondsRemaining.ToString("000");
      int timeX = Width - (((seconds.Length * 4) - 1) * 2) - 8;
      uint timeColor = game.LevelSecondsRemaining <= 10 ? 0xFFFF6B6B : Gold;
      PixelFont.Draw(Frame, "TIME", timeX - 23, 8, timeColor);
      PixelFont.Draw(Frame, seconds, timeX, 5, timeColor, 2);
      PixelFont.Draw(Frame, message, 8, 19, 0xFFA7CBDC);
    }

    Frame.Fill(0, Height - FooterHeight, Width, FooterHeight, Ink);
    PixelFont.Draw(Frame, _touchControls ? "ARROWS MOVE  DOWN PIPE  ATTACK  JUMP  START PAUSE" : "A/D MOVE  SPACE JUMP  DOWN/S PIPE  X FIRE  ENTER PAUSE  R RETRY", 8, Height - 9, White);
  }

  private void DrawMenuChoice(string text, int y, bool selected)
  {
    DrawTitleText(selected ? $"> {text} <" : text, y, selected ? Gold : White, 1);
  }

  private void DrawPauseMenu(int selection)
  {
    Frame.FillTranslucent(70, 76, 244, 104, Ink, 230);
    Frame.Fill(72, 78, 240, 1, Gold);
    DrawTitleText("PAUSED", 89, Gold, 2);
    DrawMenuChoice("RESUME GAME", 116, selection == 0);
    DrawMenuChoice("SETTINGS", 135, selection == 1);
    DrawTitleText(_touchControls ? "SELECT MENU  JUMP ACCEPT  ATTACK CANCEL" : "UP/DOWN SELECT  ENTER CONFIRM", 161, White, 1);
  }

  private void DrawPlayerDeath(Game game, int cameraX, int cameraY)
  {
    int age = Math.Clamp(Game.DeathDurationTicks - game.DeathTicksRemaining, 0, Game.DeathDurationTicks);
    bool flip = game.FacingDirection < 0;
    DrawActor(_sprites[PlayerSprite(game.Character, "player-splatter")], game.X, game.Y, cameraX, cameraY, flip, true);
    if (age < 48)
      DrawActor(_sprites[PlayerSprite(game.Character, $"player-explosion-{age / 12 + 1}")],
        game.X, game.Y, cameraX, cameraY, flip, true);

    int left = Math.Clamp((int)Math.Round((game.X + Game.BodyWidth / 2) * TileSize) - cameraX - 12, 0, Width - 24);
    int floor = Math.Clamp(HeaderHeight + (int)Math.Round((game.Y + Game.BodyHeight) * TileSize) - cameraY,
      HeaderHeight + 16, Height - FooterHeight) - 1;
    int center = left + 12;

    // The intestines uncoil and spill outward, then settle as the death timer ends.
    double spill = Math.Clamp((age - 6) / 108.0, 0, 1);
    int length = (int)Math.Round(30 * spill);
    for (int strand = 0; strand < 3; strand++)
    {
      int direction = (strand == 1 ? -1 : 1) * game.FacingDirection;
      for (int pixel = 0; pixel < length - strand * 3; pixel++)
      {
        int x = center + direction * pixel;
        int curl = (int)Math.Round(Math.Sin(pixel * 0.65 + strand * 2) * 2
          + Math.Sin(pixel * 0.3 + age * 0.18) * (1 - spill) * 3);
        int y = floor - 4 - strand * 2 + curl;
        Frame.Fill(x - 1, y - 1, 4, 4, 0xFF981D3B);
        Frame.Fill(x, y, 2, 2, 0xFFD98290);
        if (pixel % 3 == 0)
          Frame.Fill(x, y, 1, 1, 0xFFF1BB8B);
      }
    }

    for (int fragment = 0; fragment < 20; fragment++)
    {
      int elapsed = age - fragment % 5 * 4;
      if (elapsed < 0)
        continue;

      double launch = 1.3 + fragment % 4 * 0.23;
      double landing = (launch + Math.Sqrt(launch * launch + 1.92)) / 0.12;
      double flight = Math.Min(elapsed, landing);
      int direction = (fragment % 2 == 0 ? -1 : 1) * game.FacingDirection;
      int x = center + direction * (int)Math.Round(flight * (0.35 + fragment % 7 * 0.12));
      int y = Math.Min(floor - 2, floor - 8 + (int)Math.Round(-launch * flight + 0.06 * flight * flight));
      bool clothing = fragment % 5 == 0;
      uint color = clothing ? (game.Character == PlayerCharacter.Ella ? 0xFF8745B5u : 0xFF238D93u)
        : fragment % 3 == 0 ? 0xFF981D3Bu : 0xFFE83B3Bu;
      Frame.Fill(x, y, clothing ? 3 : 2, elapsed < landing ? 3 : 2, color);
      if (!clothing && fragment % 3 == 0)
        Frame.Fill(x + 1, y, 2, 1, 0xFFD98290);
    }
  }

  private void DrawTitleText(string text, int y, uint color, int scale, bool shadow = false)
  {
    int x = (Width - (((text.Length * 4) - 1) * scale)) / 2;
    if (shadow)
      PixelFont.Draw(Frame, text, x + 2, y + 3, Ink, scale);

    PixelFont.Draw(Frame, text, x, y, color, scale);
  }

  private void DrawUnderworld(int cameraX)
  {
    Frame.Clear(0xFF142522);
    int drift = cameraX / 3;
    for (int row = 0; row < 13; row++)
    {
      int y = HeaderHeight + row * 18;
      Frame.Fill(0, y, Width, 1, 0xFF0C1919);
      for (int x = -64; x < Width + 64; x += 48)
      {
        int seam = x - drift % 48 + (row % 2) * 24;
        Frame.Fill(seam, y, 1, 18, 0xFF0C1919);
        Frame.Fill(seam + 4, y + 3, 35, 1, 0xFF203730);
      }
    }

    // Corroded mains with flanges, bolts, downpipes, valves, and leaking drains.
    Frame.Fill(0, 63, Width, 19, 0xFF0A1716);
    Frame.Fill(0, 65, Width, 14, 0xFF31584B);
    Frame.Fill(0, 67, Width, 3, 0xFF53765B);
    Frame.Fill(0, 77, Width, 2, 0xFF203D36);
    for (int x = -120; x < Width + 120; x += 112)
    {
      int px = x - drift % 112;
      Frame.Fill(px, 60, 7, 24, 0xFF263D35);
      Frame.Fill(px + 1, 62, 2, 2, 0xFF94A079);
      Frame.Fill(px + 1, 79, 2, 2, 0xFF94A079);
      Frame.Fill(px + 40, 78, 17, 50, 0xFF0A1716);
      Frame.Fill(px + 42, 78, 13, 48, 0xFF31584B);
      Frame.Fill(px + 43, 80, 3, 44, 0xFF53765B);
      Frame.Fill(px + 38, 123, 21, 6, 0xFF754A44);
      int drip = (int)(_animationTime * 22 + x) % 46;
      if (drip < 0)
        drip += 46;

      Frame.Fill(px + 47, 131 + drip, 2, 4, 0xFF82BD58);
      for (int bar = 0; bar < 5; bar++)
        Frame.Fill(px + 72 + bar * 4, 178, 2, 19, 0xFF070F13);

      Frame.Fill(px + 68, 174, 25, 3, 0xFF263D35);
      Frame.Fill(px + 68, 197, 25, 3, 0xFF263D35);
    }
  }

  private void DrawVolumeSlider(string label, int y, int volume, bool selected)
  {
    if (selected)
      Frame.FillTranslucent(36, y - 5, Width - 72, 24, Gold, 40);

    uint color = selected ? Gold : White;
    PixelFont.Draw(Frame, selected ? ">" : " ", 40, y, Gold);
    PixelFont.Draw(Frame, label, 52, y, color);
    PixelFont.Draw(Frame, $"{volume}%", 208, y, color);
    Frame.Fill(244, y + 2, 92, 3, 0xFF525D70);
    Frame.Fill(244, y + 2, volume * 92 / 100, 3, Gold);
    Frame.Fill(244 + (volume * 85 / 100), y - 1, 7, 9, color);
  }

  private void DrawWeaponQuote(string quote, string title)
  {
    const int MaxCharacters = 42;
    var lines = new List<string>();
    string remaining = quote.Trim();
    while (remaining.Length > 0 && lines.Count < 3)
    {
      int length = Math.Min(MaxCharacters, remaining.Length);
      if (length < remaining.Length)
      {
        int space = remaining.LastIndexOf(' ', length - 1, length);
        if (space > 0)
          length = space;
      }

      string line = remaining[..length].Trim();
      remaining = remaining[length..].TrimStart();
      if (lines.Count == 2 && remaining.Length > 0)
        line = line[..Math.Min(line.Length, MaxCharacters - 3)] + "...";
      lines.Add(line);
    }

    Frame.Fill(12, 42, Width - 24, 22 + (lines.Count * 12), Ink);
    Frame.Fill(12, 42, Width - 24, 1, Gold);
    DrawTitleText(title, 48, Gold, 1);
    for (int index = 0; index < lines.Count; index++)
      DrawTitleText(lines[index], 60 + (index * 12), White, 2);
  }

  private void DrawWrappedCentered(string text, int y, uint color, int maxLines = 3)
  {
    const int MaxCharacters = 42;
    string remaining = text.Trim();

    for (int index = 0; index < maxLines && remaining.Length > 0; index++)
    {
      int length = Math.Min(MaxCharacters, remaining.Length);

      if (length < remaining.Length)
      {
        int space = remaining.LastIndexOf(' ', length - 1, length);
        if (space > 0)
          length = space;
      }

      string line = remaining[..length].Trim();
      remaining = remaining[length..].TrimStart();
      if (index == maxLines - 1 && remaining.Length > 0)
        line = line[..Math.Min(line.Length, MaxCharacters - 3)] + "...";
      DrawTitleText(line, y + (index * 12), color, 2);
    }
  }
}
