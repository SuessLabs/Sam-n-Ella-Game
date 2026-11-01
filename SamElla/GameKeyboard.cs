// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla;

internal sealed class GameKeyboard
{
  public const int PauseKey = 0x13;

  private readonly HashSet<int> _held = [];
  private readonly HashSet<GameKey> _pressed = [];
  private readonly List<int> _keyPresses = [];

  public bool AnyKeyPressed { get; private set; }
  public IReadOnlyList<int> KeyPresses => _keyPresses;

  public bool Down(GameKey key) => _held.Any(code => MapKey(code) == key);

  public bool Pressed(GameKey key) => _pressed.Contains(key);

  public void BeginFrame()
  {
    AnyKeyPressed = false;
    _pressed.Clear();
    _keyPresses.Clear();
  }

  public void Clear()
  {
    _held.Clear();
    BeginFrame();
  }

  public void KeyDown(int code, bool isRepeat = false)
  {
    if (!_held.Add(code) || isRepeat)
      return;

    AnyKeyPressed = true;
    _keyPresses.Add(code);
    if (MapKey(code) is { } key)
      _pressed.Add(key);
  }

  public void KeyUp(int code) => _held.Remove(code);

  private static GameKey? MapKey(int code) => code switch
  {
    0x25 or 0x41 => GameKey.Left,
    0x27 or 0x44 => GameKey.Right,
    0x20 or 0x26 or 0x57 => GameKey.Jump,
    0x52 => GameKey.Restart,
    0x1B => GameKey.Escape,
    0x0D or PauseKey => GameKey.Start,
    0x58 or 0x11 => GameKey.Fire,
    0x28 or 0x53 => GameKey.Down,
    _ => null,
  };
}
