// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SamElla;

// Win32 and GDI interop stays in the Windows backend; no engine or native package is required.
[SupportedOSPlatform("windows")]
internal static class NativeMethods
{
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]
  internal delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool AdjustWindowRect(ref Rectangle rectangle, uint style, bool menu);

  [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
  internal static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
    int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool DestroyWindow(nint window);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern nint DispatchMessageW(ref Message message);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool GetClientRect(nint window, out Rectangle rectangle);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern nint GetDC(nint window);

  [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
  internal static extern nint GetModuleHandleW(string? moduleName);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern int GetSystemMetrics(int index);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern nint LoadCursorW(nint instance, nint name);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern nint LoadImageW(nint instance, nint name, uint type, int width, int height, uint flags);

  [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
  internal static extern int MessageBoxW(nint owner, string text, string caption, uint type);

  [DllImport("gdi32.dll", ExactSpelling = true)]
  internal static extern bool PatBlt(nint dc, int x, int y, int width, int height, uint operation);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern void PostQuitMessage(int code);

  [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
  internal static extern ushort RegisterClassW(ref WindowClass windowClass);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern int ReleaseDC(nint window, nint dc);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern nint SendMessageW(nint window, uint message, nuint wParam, nint lParam);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool SetProcessDPIAware();

  [DllImport("gdi32.dll", ExactSpelling = true)]
  internal static extern int SetStretchBltMode(nint dc, int mode);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool ShowWindow(nint window, int command);

  [DllImport("gdi32.dll", ExactSpelling = true)]
  internal static extern int StretchDIBits(nint dc, int x, int y, int width, int height,
    int sourceX, int sourceY, int sourceWidth, int sourceHeight, nint bits, ref BitmapInfo info, uint usage, uint operation);

  [DllImport("user32.dll", ExactSpelling = true)]
  internal static extern bool TranslateMessage(ref Message message);

  [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
  internal static extern bool UnregisterClassW(string className, nint instance);

  [StructLayout(LayoutKind.Sequential)]
  internal struct BitmapInfo
  {
    public uint Size;
    public int Width;
    public int Height;
    public ushort Planes;
    public ushort BitCount;
    public uint Compression;
    public uint ImageSize;
    public int PixelsPerMeterX;
    public int PixelsPerMeterY;
    public uint ColorsUsed;
    public uint ColorsImportant;
    public uint Color;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct Message
  {
    public nint Window;
    public uint Id;
    public nuint WParam;
    public nint LParam;
    public uint Time;
    public Point Position;
    public uint Private;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct MinMaxInfo
  {
    public Point Reserved;
    public Point MaxSize;
    public Point MaxPosition;
    public Point MinTrack;
    public Point MaxTrack;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct Point
  {
    public int X;
    public int Y;
  }

  [StructLayout(LayoutKind.Sequential)]
  internal struct Rectangle
  {
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
  }

  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  internal struct WindowClass
  {
    public uint Style;
    public WindowProcedure Procedure;
    public int ClassExtra;
    public int WindowExtra;
    public nint Instance;
    public nint Icon;
    public nint Cursor;
    public nint Background;
    public string? MenuName;
    public string ClassName;
  }
}
