// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Mobile;

// Multiple fingers can hold the same action; releasing one must not release the other.
internal sealed class TouchInput : IGameWindow
{
  private readonly HashSet<long> _activePointers = [];
  private readonly GameKeyboard _keyboard = new();
  private readonly Dictionary<long, int> _pointers = [];
  private readonly HashSet<long> _suppressedPointers = [];
  public bool Active { get; set; } = true;
  public bool AnyKeyPressed => _keyboard.AnyKeyPressed;
  public bool Closed => false;
  public IReadOnlyList<int> KeyPresses => _keyboard.KeyPresses;

  public void Clear()
  {
    _activePointers.Clear();
    _suppressedPointers.Clear();
    _pointers.Clear();
    _keyboard.Clear();
  }

  public void Dispose() => Clear();

  public bool Down(GameKey key) => _keyboard.Down(key);

  public void MovePointer(long id, int? key)
  {
    if (_activePointers.Contains(id))
      ApplyPointer(id, key);
  }

  public void Present(PixelBuffer frame)
  { }

  public bool Pressed(GameKey key) => _keyboard.Pressed(key);

  public void PumpEvents() => _keyboard.BeginFrame();

  public void SetPointer(long id, int? key)
  {
    if (key is null)
    {
      _activePointers.Remove(id);
      _suppressedPointers.Remove(id);
    }
    else
      _activePointers.Add(id);

    ApplyPointer(id, key);
  }

  public void SuppressUntilRelease()
  {
    _suppressedPointers.UnionWith(_activePointers);
    _pointers.Clear();
    _keyboard.Clear();
  }

  private void ApplyPointer(long id, int? key)
  {
    if (_suppressedPointers.Contains(id))
      return;

    if (_pointers.TryGetValue(id, out int previous))
    {
      if (previous == key)
        return;

      _pointers.Remove(id);
      if (!_pointers.ContainsValue(previous))
        _keyboard.KeyUp(previous);
    }

    if (key is int code)
    {
      bool held = _pointers.ContainsValue(code);
      _pointers[id] = code;
      if (!held)
        _keyboard.KeyDown(code);
    }
  }
}
