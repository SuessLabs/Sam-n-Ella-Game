// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace SamElla.Mobile;

internal sealed class GamePage : ContentPage, IDisposable
{
  private static readonly SKPoint[] ArrowPoints = [new(-0.30f, -0.12f), new(0.05f, -0.12f), new(0.05f, -0.30f),
    new(0.30f, 0), new(0.05f, 0.30f), new(0.05f, 0.12f), new(-0.30f, 0.12f)];

  private readonly SKBitmap _bitmap = new(PixelRenderer.Width, PixelRenderer.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
  private readonly SKPaint _buttonPaint = new() { IsAntialias = true };
  private readonly SKCanvasView _canvas = new() { EnableTouchEvents = true };
  private readonly Stopwatch _clock = Stopwatch.StartNew();
  private readonly SKFont _font = new();
  private readonly SKPaint _imagePaint = new() { IsAntialias = false };
  private readonly TouchInput _input = new();
  private readonly Image _loadingImage = new()
  {
    Source = "feature_graphic.png",
    Aspect = Aspect.Fill,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    InputTransparent = true,
  };

  private readonly int[] _pixels = new int[PixelRenderer.Width * PixelRenderer.Height];
  private readonly IDispatcherTimer _timer;

  private bool _active = true;
  private MobileGameAudio? _audio;
  private bool _deathContinueReady;
  private bool _disposed;
  private bool _largeControls;
  private MobileLayout? _layout;
  private bool _loading;
  private bool _menu = true;
  private string _message = "LOADING...";
  private double _previous;
  private GameSession? _session;

  public GamePage()
  {
    BackgroundColor = Colors.Black;
    SafeAreaEdges = SafeAreaEdges.All;
    Content = new Grid { Children = { _canvas, _loadingImage } };
    _canvas.PaintSurface += Paint;
    _canvas.Touch += Touch;
    _timer = Dispatcher.CreateTimer();
    _timer.Interval = TimeSpan.FromSeconds(Game.Step);
    _timer.Tick += (_, _) => Tick();
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;
    DeviceDisplay.Current.KeepScreenOn = false;

    _timer.Stop();
    _session?.Dispose();
    _audio?.Dispose();
    _input.Dispose();
    _canvas.PaintSurface -= Paint;
    _canvas.Touch -= Touch;
    _bitmap.Dispose();
    _imagePaint.Dispose();
    _buttonPaint.Dispose();
    _font.Dispose();
  }

  public void Resume()
  {
    if (_disposed)
      return;

    _active = _input.Active = true;
    _input.Clear();
    _audio?.SetSuspended(false);
    _previous = _clock.Elapsed.TotalSeconds;

    if (_session is not null)
      _timer.Start();
  }

  public void Suspend()
  {
    _active = _input.Active = false;
    _input.Clear();
    _timer.Stop();
    _session?.Suspend();
    _audio?.SetSuspended(true);
  }

  protected override async void OnAppearing()
  {
    base.OnAppearing();
    if (_loading || _session is not null || _disposed)
      return;

    _loading = true;
    DeviceDisplay.Current.KeepScreenOn = true;
    try
    {
      string root = await GameContent.PrepareAsync();
      _audio = await Task.Run(() => new MobileGameAudio());

      if (_disposed)
      {
        _audio.Dispose();
        return;
      }

      _session = new GameSession(Path.Combine(root, "Assets"), Path.Combine(root, "Levels"),
        Path.Combine(FileSystem.AppDataDirectory, "settings.txt"), _audio, desktopUpdates: false);
      _largeControls = _session.LargeMobileControls;
      _loadingImage.IsVisible = false;
      _previous = _clock.Elapsed.TotalSeconds;

      if (_active)
        _timer.Start();
      else
        _audio.SetSuspended(true);
    }
    catch (Exception exception)
    {
      _message = "COULD NOT LOAD GAME";
      _loadingImage.IsVisible = false;
      System.Diagnostics.Debug.WriteLine(exception);
      await DisplayAlertAsync("Sam n' Ella", exception.Message, "OK");
    }

    _canvas.InvalidateSurface();
  }

  protected override bool OnBackButtonPressed()
  {
    _input.SetPointer(long.MinValue, _session?.MenuControls == false ? 0x0D : 0x1B);
    _input.SetPointer(long.MinValue, null);
    return true;
  }

  private void DrawDirectionArrow(SKCanvas canvas, SKRect rectangle, int key)
  {
    using var arrow = new SKPath();
    float size = Math.Min(rectangle.Width, rectangle.Height);
    for (int index = 0; index < ArrowPoints.Length; index++)
    {
      var point = ArrowPoints[index];
      var rotated = key switch
      {
        0x25 => new SKPoint(-point.X, -point.Y),
        0x26 => new SKPoint(point.Y, -point.X),
        0x28 => new SKPoint(-point.Y, point.X),
        _ => point,
      };

      float x = rectangle.MidX + rotated.X * size, y = rectangle.MidY + rotated.Y * size;
      if (index == 0)
        arrow.MoveTo(x, y);
      else
        arrow.LineTo(x, y);
    }

    arrow.Close();
    canvas.DrawPath(arrow, _buttonPaint);
  }

  private void Paint(object? sender, SKPaintSurfaceEventArgs args)
  {
    var canvas = args.Surface.Canvas;
    canvas.Clear(SKColors.Black);

    if (_session is null)
    {
      _font.Size = 24;
      _buttonPaint.Color = SKColors.White;
      canvas.DrawText(_message, args.Info.Width / 2f, args.Info.Height / 2f, SKTextAlign.Center, _font, _buttonPaint);
      return;
    }

    double density = DeviceDisplay.MainDisplayInfo.Density;
    _layout = new MobileLayout(args.Info.Width, args.Info.Height, density, _menu, _session.StartEnabled, _largeControls);

    var view = _layout.Viewport;
    Buffer.BlockCopy(_session.Frame.Pixels, 0, _pixels, 0, _pixels.Length * sizeof(int));
    Marshal.Copy(_pixels, 0, _bitmap.GetPixels(), _pixels.Length);

    using var image = SKImage.FromBitmap(_bitmap);

    canvas.DrawImage(image, new SKRect((float)view.X, (float)view.Y,
      (float)(view.X + view.Width), (float)(view.Y + view.Height)), new SKSamplingOptions(SKFilterMode.Nearest), _imagePaint);

    foreach (var button in _layout.Buttons)
    {
      var rectangle = new SKRect((float)button.X, (float)button.Y,
        (float)(button.X + button.Width), (float)(button.Y + button.Height));

      _buttonPaint.Color = new SKColor(35, 59, 78, button.Key.HasValue ? (byte)88 : (byte)40);
      _buttonPaint.Style = SKPaintStyle.Fill;
      canvas.DrawRoundRect(rectangle, 12, 12, _buttonPaint);

      _buttonPaint.Color = new SKColor(255, 213, 107, button.Key.HasValue ? (byte)180 : (byte)80);
      _buttonPaint.Style = SKPaintStyle.Stroke;
      _buttonPaint.StrokeWidth = 2;

      canvas.DrawRoundRect(rectangle, 12, 12, _buttonPaint);

      _buttonPaint.Style = SKPaintStyle.Fill;
      _buttonPaint.Color = new SKColor(255, 213, 107, button.Key.HasValue ? (byte)220 : (byte)96);

      if (button.Label is "↑" or "↓" or "←" or "→")
      {
        DrawDirectionArrow(canvas, rectangle, button.Key!.Value);
      }
      else if (button.Heading is not null)
      {
        if (button.Heading == "○")
        {
          _buttonPaint.Style = SKPaintStyle.Stroke;
          _buttonPaint.StrokeWidth = rectangle.Height * 0.035f;
          canvas.DrawCircle(rectangle.MidX, rectangle.Top + rectangle.Height * 0.35f,
            rectangle.Height * 0.17f, _buttonPaint);
          _buttonPaint.Style = SKPaintStyle.Fill;
        }
        else
        {
          _font.Size = (float)button.Height * 0.48f;
          canvas.DrawText(button.Heading, rectangle.MidX, rectangle.Top + rectangle.Height * 0.52f,
            SKTextAlign.Center, _font, _buttonPaint);
        }

        _font.Size = (float)button.Height * 0.18f;
        canvas.DrawText(button.Label, rectangle.MidX, rectangle.Bottom - rectangle.Height * 0.14f,
          SKTextAlign.Center, _font, _buttonPaint);
      }
      else
      {
        _font.Size = (float)Math.Min(button.Height * 0.38, button.Width / (button.Label.Length * 0.65));
        canvas.DrawText(button.Label, rectangle.MidX, rectangle.MidY + _font.Size / 3,
          SKTextAlign.Center, _font, _buttonPaint);
      }
    }
  }

  private void Tick()
  {
    if (!_active || _disposed || _session is null)
      return;

    double now = _clock.Elapsed.TotalSeconds;
    _session.Tick(_input, now - _previous);
    _previous = now;
    _input.PumpEvents();

    if (_session.MenuControls != _menu || _session.DeathContinueReady != _deathContinueReady
      || _session.LargeMobileControls != _largeControls)
    {
      _menu = _session.MenuControls;
      _deathContinueReady = _session.DeathContinueReady;
      _largeControls = _session.LargeMobileControls;
      _layout = null;
      _input.SuppressUntilRelease();
    }

    if (_session.ExitRequested)
    {
      _session.Dispose();
      _timer.Stop();
      Application.Current?.Quit();
      return;
    }

    _canvas.InvalidateSurface();
  }

  private void Touch(object? sender, SKTouchEventArgs args)
  {
    if (!_active || _disposed)
      return;

    if (args.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled or SKTouchAction.Exited)
      _input.SetPointer(args.Id, null);
    else if (args.ActionType == SKTouchAction.Pressed)
      _input.SetPointer(args.Id, _layout?.KeyAt(args.Location.X, args.Location.Y));
    else if (args.ActionType == SKTouchAction.Moved)
      _input.MovePointer(args.Id, _layout?.KeyAt(args.Location.X, args.Location.Y));

    args.Handled = true;
  }
}
