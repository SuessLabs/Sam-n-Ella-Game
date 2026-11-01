// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla;

// Screen-independent detection from fresh keypresses supplied by GameKeyboard.
internal sealed class LevelSelectCheat
{
  private static readonly int[] Code = [0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46];
  private readonly Queue<int> _recentKeys = new();

  public bool HandleInput(IReadOnlyList<int> keyPresses, bool windowActive)
  {
    if (!windowActive)
    {
      _recentKeys.Clear();
      return false;
    }

    bool requested = false;
    foreach (int code in keyPresses)
    {
      _recentKeys.Enqueue(code);
      if (_recentKeys.Count > Code.Length)
        _recentKeys.Dequeue();

      if (_recentKeys.SequenceEqual(Code))
      {
        requested = true;
        _recentKeys.Clear();
      }

    }

    return requested;
  }

}
