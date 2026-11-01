// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla;

// Screen-independent detection from fresh keypresses supplied by GameKeyboard.
internal sealed class LevelSelectCheat
{
  private static readonly int[] Code = [0x44, 0x45, 0x41, 0x44, 0x42, 0x45, 0x45, 0x46];
  private static readonly int[] DoubleKonamiCode = [0x26, 0x26, 0x26, 0x26, 0x28, 0x28, 0x28, 0x28,
    0x25, 0x25, 0x27, 0x27, 0x25, 0x25, 0x27, 0x27, 0x42, 0x42, 0x41, 0x41];
  private readonly Queue<int> _recentKeys = new();

  public bool ConsumesTitleConfirmation { get; private set; }

  public bool HandleInput(IReadOnlyList<int> keyPresses, bool windowActive, bool mobileControls = false)
  {
    ConsumesTitleConfirmation = false;
    if (!windowActive)
    {
      _recentKeys.Clear();
      return false;
    }

    bool requested = false;
    foreach (int code in keyPresses)
    {
      int key = mobileControls ? code switch
      {
        0x1B or 0x58 => 0x42, // Attack: menu Cancel or gameplay Fire.
        0x0D or 0x20 => 0x41, // Jump: menu Accept or gameplay Jump.
        _ => code,
      } : code;
      _recentKeys.Enqueue(key);
      if (_recentKeys.Count > DoubleKonamiCode.Length)
        _recentKeys.Dequeue();

      // The first A must not start the game before the second A completes the code.
      ConsumesTitleConfirmation |= _recentKeys.TakeLast(DoubleKonamiCode.Length - 1)
        .SequenceEqual(DoubleKonamiCode.Take(DoubleKonamiCode.Length - 1));
      if (_recentKeys.TakeLast(Code.Length).SequenceEqual(Code) || _recentKeys.SequenceEqual(DoubleKonamiCode))
      {
        requested = true;
        _recentKeys.Clear();
      }
    }

    return requested;
  }
}
