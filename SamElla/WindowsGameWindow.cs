// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SamElla;

[SupportedOSPlatform("windows")]
internal sealed class WindowsGameWindow : IGameWindow
{
  private const uint WindowStyle = 0x00CF0000;

  private readonly string _className = $"SamElla-{Guid.NewGuid():N}";
  private readonly int _height;
  private readonly nint _instance;
  private readonly GameKeyboard _keyboard = new();
  private readonly NativeMethods.WindowProcedure _procedure;
  private readonly int _width;
  private bool _disposed;
  private bool _focused;
  private nint _handle;
  private bool _minimized;

  public WindowsGameWindow(int width, int height)
  {
    _width = width;
    _height = height;
    _procedure = HandleMessage;
    _instance = NativeMethods.GetModuleHandleW(null);
    NativeMethods.SetProcessDPIAware();

    var windowClass = new NativeMethods.WindowClass
    {
      Procedure = _procedure,
      Instance = _instance,
      Icon = LoadApplicationIcon(_instance, small: false),
      Cursor = NativeMethods.LoadCursorW(0, 32512),
      ClassName = _className,
    };

    if (NativeMethods.RegisterClassW(ref windowClass) == 0)
      throw new Win32Exception(Marshal.GetLastWin32Error());

    var bounds = new NativeMethods.Rectangle { Right = width * 3, Bottom = height * 3 };
    NativeMethods.AdjustWindowRect(ref bounds, WindowStyle, false);
    _handle = NativeMethods.CreateWindowExW(0, _className, "Sam n' Ella", WindowStyle,
      80, 80, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0, 0, _instance, 0);

    if (_handle == 0)
    {
      int error = Marshal.GetLastWin32Error();
      NativeMethods.UnregisterClassW(_className, _instance);
      throw new Win32Exception(error);
    }

    NativeMethods.SendMessageW(_handle, 0x0080, 0, LoadApplicationIcon(_instance, small: true)); // WM_SETICON, ICON_SMALL
    NativeMethods.ShowWindow(_handle, 5);
  }

  public bool Active => _focused && !_minimized;

  public bool AnyKeyPressed => _keyboard.AnyKeyPressed;
  public bool Closed { get; private set; }
  public IReadOnlyList<int> KeyPresses => _keyboard.KeyPresses;

  public static void ShowError(string message) => NativeMethods.MessageBoxW(0, message, "Sam n' Ella", 0x10);

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;

    if (_handle != 0)
      NativeMethods.DestroyWindow(_handle);

    NativeMethods.UnregisterClassW(_className, _instance);
    GC.KeepAlive(_procedure);
  }

  public bool Down(GameKey key) => _keyboard.Down(key);

  public unsafe void Present(PixelBuffer frame)
  {
    if (Closed || _minimized)
      return;

    NativeMethods.GetClientRect(_handle, out var bounds);
    int scale = Math.Max(1, Math.Min(bounds.Right / frame.Width, bounds.Bottom / frame.Height));
    int width = frame.Width * scale;
    int height = frame.Height * scale;
    nint dc = NativeMethods.GetDC(_handle);

    if (dc == 0)
      throw new Win32Exception("Could not obtain the window drawing surface.");

    try
    {
      var bitmap = new NativeMethods.BitmapInfo
      {
        Size = 40,
        Width = frame.Width,
        Height = -frame.Height,
        Planes = 1,
        BitCount = 32,
      };

      int left = (bounds.Right - width) / 2;
      int top = (bounds.Bottom - height) / 2;
      // Clear only the letterbox bars, avoiding a black flash beneath each frame.
      NativeMethods.PatBlt(dc, 0, 0, bounds.Right, top, 0x00000042);
      NativeMethods.PatBlt(dc, 0, top + height, bounds.Right, bounds.Bottom - top - height, 0x00000042);
      NativeMethods.PatBlt(dc, 0, top, left, height, 0x00000042);
      NativeMethods.PatBlt(dc, left + width, top, bounds.Right - left - width, height, 0x00000042);
      NativeMethods.SetStretchBltMode(dc, 3);

      fixed (uint* pixels = frame.Pixels)
      {
        if (NativeMethods.StretchDIBits(dc, left, top, width, height, 0, 0, frame.Width, frame.Height,
          (nint)pixels, ref bitmap, 0, 0x00CC0020) == 0)
          throw new Win32Exception("Could not present the pixel frame.");
      }
    }
    finally
    {
      NativeMethods.ReleaseDC(_handle, dc);
    }
  }

  public bool Pressed(GameKey key) => _keyboard.Pressed(key);

  public void PumpEvents()
  {
    _keyboard.BeginFrame();

    while (NativeMethods.PeekMessageW(out var message, 0, 0, 0, 1))
    {
      if (message.Id == 0x0012)
      {
        Closed = true;
        break;
      }

      NativeMethods.TranslateMessage(ref message);
      NativeMethods.DispatchMessageW(ref message);
    }
  }

  private static nint LoadApplicationIcon(nint instance, bool small)
  {
    // The SDK embeds ApplicationIcon as resource 32512. LR_SHARED owns its lifetime.
    return NativeMethods.LoadImageW(instance, 32512, 1,
      NativeMethods.GetSystemMetrics(small ? 49 : 11), NativeMethods.GetSystemMetrics(small ? 50 : 12), 0x8000);
  }

  private nint HandleMessage(nint window, uint message, nuint wParam, nint lParam)
  {
    switch (message)
    {
      case 0x0002: // WM_DESTROY
        Closed = true;
        _handle = 0;
        NativeMethods.PostQuitMessage(0);
        return 0;

      case 0x0005: // WM_SIZE
        _minimized = wParam == 1;
        return 0;

      case 0x0006: // WM_ACTIVATE
        _focused = (wParam & 0xFFFF) != 0;

        if (!_focused)
        {
          _keyboard.Clear();
        }

        return 0;

      case 0x0014: // WM_ERASEBKGND; the pixel renderer owns the client area.
        return 1;

      case 0x0024: // WM_GETMINMAXINFO
        var limits = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);
        limits.MinTrack = new NativeMethods.Point { X = _width + 32, Y = _height + 64 };
        Marshal.StructureToPtr(limits, lParam, false);
        return 0;

      case 0x0100: // WM_KEYDOWN; ignore OS repeats, preserving a single press edge.
        _keyboard.KeyDown((int)wParam, (lParam.ToInt64() & (1L << 30)) != 0);
        return 0;

      case 0x0101: // WM_KEYUP
        _keyboard.KeyUp((int)wParam);
        return 0;

      case 0x0104: // WM_SYSKEYDOWN; preserve system shortcuts such as Alt+F4.
        _keyboard.KeyDown((int)wParam, (lParam.ToInt64() & (1L << 30)) != 0);
        break;

      case 0x0105: // WM_SYSKEYUP
        _keyboard.KeyUp((int)wParam);
        break;
    }

    return NativeMethods.DefWindowProcW(window, message, wParam, lParam);
  }
}
