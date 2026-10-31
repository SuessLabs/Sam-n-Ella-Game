namespace SamElla.Mobile;

// Multiple fingers can hold the same action; releasing one must not release the other.
internal sealed class TouchInput : IGameWindow
{
  private readonly Dictionary<long, int> _pointers = [];
  private readonly GameKeyboard _keyboard = new();

  public bool Active { get; set; } = true;
  public bool Closed => false;
  public bool AnyKeyPressed => _keyboard.AnyKeyPressed;
  public IReadOnlyList<int> KeyPresses => _keyboard.KeyPresses;

  public bool Down(GameKey key) => _keyboard.Down(key);

  public bool Pressed(GameKey key) => _keyboard.Pressed(key);

  public void SetPointer(long id, int? key)
  {
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

  public void Clear()
  {
    _pointers.Clear();
    _keyboard.Clear();
  }

  public void PumpEvents() => _keyboard.BeginFrame();

  public void Present(PixelBuffer frame) { }

  public void Dispose() => Clear();
}
