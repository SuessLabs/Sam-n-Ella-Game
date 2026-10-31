namespace SamElla;

internal enum GameKey
{
  Left,
  Right,
  Jump,
  Restart,
  Escape,
  Start,
  Fire,
  Down,
}

// Hosts provide input, frame presentation, and lifecycle alongside IGameAudio.
internal interface IGameWindow : IDisposable
{
  bool Active { get; }

  bool AnyKeyPressed { get; }
  IReadOnlyList<int> KeyPresses { get; }

  bool Closed { get; }

  bool Down(GameKey key);

  bool Pressed(GameKey key);

  void Present(PixelBuffer frame);

  void PumpEvents();
}
