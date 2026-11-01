// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

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
  bool Closed { get; }
  IReadOnlyList<int> KeyPresses { get; }

  bool Down(GameKey key);

  void Present(PixelBuffer frame);

  bool Pressed(GameKey key);

  void PumpEvents();
}
