namespace SamElla.Audio;

// Each voice scales its own samples; Windows' shared app-session volume is untouched.
internal sealed class SamplePlayback(short[] samples, bool loop = false)
{
  private int _position;
  private int _volume = 100;

  public bool HasRemaining => samples.Length > 0 && (loop || _position < samples.Length);

  public int Volume
  {
    get => _volume;
    set => _volume = Math.Clamp(value, 0, 100);
  }

  public int Read(Span<short> destination)
  {
    int count = 0;
    while (count < destination.Length && HasRemaining)
    {
      if (_position == samples.Length)
        _position = 0;

      destination[count++] = (short)(samples[_position++] * _volume / 100);
    }

    return count;
  }
}
