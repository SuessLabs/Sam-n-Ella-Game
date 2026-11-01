// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Audio;

// Original chiptune effects: pulse lead, triangle bass, and a quiet FM overtone.
// Samples are generated once at startup; playback never synthesizes on the game loop.
internal static class GameSounds
{
  public const int SampleRate = 44100;

  public static short[] Create(GameSound sound)
  {
    if (sound == GameSound.KonamiActivated)
      return CreateKonamiSting();

    if (sound == GameSound.ToiletPaperThrow)
      return CreatePaperWhoosh();

    if (sound is GameSound.FartShort or GameSound.FartLong)
      return CreateFart(sound == GameSound.FartLong);

    if (sound == GameSound.ChainWhip)
      return CreateWhipCrack();

    if (sound == GameSound.TeenGroan)
      return CreateGroan();

    if (sound is GameSound.Flamethrower or GameSound.Shotgun)
      return CreateWeapon(sound);

    double duration = sound switch
    {
      GameSound.Jump => 0.18,
      GameSound.PowerUp => 0.48,
      GameSound.PowerDown => 0.42,
      GameSound.Died => 2.0,
      GameSound.EnemySmash => 0.14,
      GameSound.EnemyDied => 0.28,
      GameSound.BlockBreak => 0.22,
      GameSound.Coin => 0.18,
      _ => throw new ArgumentOutOfRangeException(nameof(sound)),
    };

    var samples = new short[(int)(SampleRate * duration)];
    double phase = 0, bassPhase = 0;
    uint noiseState = 0xACE1;
    double[] rising = [523.25, 659.25, 783.99, 1046.50];
    double[] falling = [783.99, 659.25, 523.25, 329.63];
    double[] death = [659.25, 622.25, 587.33, 440, 349.23, 293.66, 220, 164.81];
    double noteLength = sound == GameSound.Died ? 0.25 : duration / (sound == GameSound.Coin ? 2 : 4);

    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / SampleRate;
      int note = Math.Min((int)(time / noteLength), sound == GameSound.Died ? 7 : 3);
      double frequency = sound switch
      {
        GameSound.Jump => 220 * Math.Pow(4, time / duration),
        GameSound.PowerUp => rising[note],
        GameSound.PowerDown => falling[note] * (1 - 0.15 * time / duration),
        GameSound.EnemySmash => 180 * Math.Pow(0.15, time / duration),
        GameSound.EnemyDied => 320 * Math.Pow(0.10, time / duration),
        GameSound.BlockBreak => 540 * Math.Pow(0.12, time / duration),
        GameSound.Coin => note == 0 ? 1318.51 : 1975.53,
        _ => death[note],
      };

      phase = (phase + frequency / SampleRate) % 1;
      bassPhase = (bassPhase + frequency / (2 * SampleRate)) % 1;

      double pulse = phase < 0.25 ? 0.75 : -0.25;
      double triangle = 1 - 4 * Math.Abs(bassPhase - 0.5);
      double fm = Math.Sin(2 * Math.PI * phase + 1.5 * Math.Sin(4 * Math.PI * phase));
      bool continuous = sound is GameSound.Jump or GameSound.EnemySmash or GameSound.EnemyDied or GameSound.BlockBreak;
      double localTime = continuous ? time : time % noteLength;
      double localLength = continuous ? duration : noteLength;

      // Short attack/release ramps prevent clicks between notes and at either end.
      double envelope = Math.Min(1, localTime / 0.004)
        * Math.Min(1, (localLength - localTime) / 0.018)
        * Math.Min(1, (duration - time) / 0.08);

      double value = (pulse * 0.45 + triangle * 0.35 + fm * 0.20) * envelope;

      if (sound is GameSound.EnemySmash or GameSound.EnemyDied or GameSound.BlockBreak)
      {
        // Seeded noise and a falling bass thud give the smash a crunchy impact.
        noiseState ^= noiseState << 13;
        noiseState ^= noiseState >> 17;
        noiseState ^= noiseState << 5;
        double noise = (noiseState & 0xFFFF) / 32767.5 - 1;

        value = sound == GameSound.EnemyDied
          ? (pulse * 0.20 + triangle * 0.25 + fm * 0.20 + noise * 0.35)
            * envelope * Math.Exp(-3 * time / duration)
          : (pulse * 0.35 + triangle * 0.35 + noise * 0.30)
            * envelope * Math.Exp(-5 * time / duration);

        if (sound == GameSound.BlockBreak)
          value = (noise * 0.65 + pulse * 0.20 + fm * 0.15)
            * envelope * Math.Exp(-6 * time / duration);
      }

      samples[i] = (short)(value * short.MaxValue * 0.45);
    }

    return samples;
  }

  private static short[] CreateKonamiSting()
  {
    // Original two-bar, 160 BPM action sting: offbeat stabs and quantized triangle bass.
    // Zeroes leave deliberate sixteenth-note gaps; the final tonic rings for a beat.
    const double Step = 60.0 / 160 / 4;
    const double Duration = 32 * Step;
    int[] leadNotes = [0, 76, 79, 0, 83, 0, 79, 78, 0, 76, 0, 74, 76, 0, 83, 0,
      0, 79, 82, 0, 86, 0, 82, 79, 78, 0, 74, 0, 76, 0, 0, 0];
    int[] bassNotes = [40, 0, 40, 47, 0, 40, 0, 43, 40, 0, 47, 0, 38, 0, 43, 0,
      36, 0, 36, 43, 0, 39, 0, 43, 38, 0, 45, 0, 40, 0, 0, 0];
    var lead = leadNotes.Select(note => note == 0 ? 0 : 440 * Math.Pow(2, (note - 69) / 12.0)).ToArray();
    var bass = bassNotes.Select(note => note == 0 ? 0 : 440 * Math.Pow(2, (note - 69) / 12.0)).ToArray();
    var samples = new short[(int)(SampleRate * Duration)];
    uint noiseState = 0x4B4F4E41;

    for (int index = 0; index < samples.Length; index++)
    {
      double time = (double)index / SampleRate;
      int step = Math.Min(31, (int)(time / Step));
      int noteStep = Math.Min(step, 28);
      double noteTime = time - noteStep * Step;
      double length = noteStep == 28 ? Step * 4 : Step * 0.78;
      double envelope = Math.Min(1, noteTime / 0.003) * Math.Clamp((length - noteTime) / 0.009, 0, 1);
      double decay = Math.Exp(-noteTime * (noteStep == 28 ? 6 : 12));
      double chipTime = Math.Floor(noteTime * 11025) / 11025;
      double bassPhase = chipTime * bass[noteStep] % 1;
      double triangle = Math.Round((1 - 4 * Math.Abs(bassPhase - 0.5)) * 15) / 15;
      double low = bass[noteStep] == 0 ? 0 : triangle * envelope * decay * 0.38;
      double leadPhase = chipTime * lead[noteStep] % 1;
      double pulse = lead[noteStep] == 0 ? 0 : (leadPhase < 0.25 ? 0.75 : -0.25) * envelope * decay * 0.28;
      double fifthPhase = chipTime * lead[noteStep] * Math.Pow(2, -5.0 / 12) % 1;
      double fifth = lead[noteStep] == 0 ? 0 : (fifthPhase < 0.125 ? 0.875 : -0.125) * envelope * decay * 0.12;

      noiseState ^= noiseState << 13;
      noiseState ^= noiseState >> 17;
      noiseState ^= noiseState << 5;
      double noise = (noiseState & 0xFFFF) / 32767.5 - 1;
      double drumTime = time - step * Step;
      double attack = Math.Min(1, drumTime / 0.002);
      double kick = step % 16 is 0 or 7 or 10
        ? Math.Sin(2 * Math.PI * (48 * drumTime + 1.7 * (1 - Math.Exp(-35 * drumTime))))
          * Math.Exp(-35 * drumTime) * attack * 0.20 : 0;
      double snare = step % 16 is 4 or 12
        ? (noise * 0.8 + Math.Sin(2 * Math.PI * 175 * drumTime) * 0.2)
          * Math.Exp(-45 * drumTime) * attack * 0.15 : 0;
      double hat = step % 2 == 0 ? noise * Math.Exp(-180 * drumTime) * attack * 0.045 : 0;
      double fade = Math.Min(1, (Duration - time) / 0.05);
      samples[index] = (short)(Math.Clamp((low + pulse + fifth + kick + snare + hat) * fade, -0.85, 0.85) * short.MaxValue);
    }

    return samples;
  }

  private static short[] CreateFart(bool longFart)
  {
    double duration = longFart ? 0.55 : 0.35;
    var samples = new short[(int)(SampleRate * duration)];
    uint noise = longFart ? 0xFA472u : 0xFA471u;
    double phase = 0, rumble = 0;
    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / SampleRate;
      noise ^= noise << 13;
      noise ^= noise >> 17;
      noise ^= noise << 5;
      rumble += ((noise & 0xFFFF) / 32767.5 - 1 - rumble) * 0.07;
      phase += (longFart ? 65 : 95) * (1 - 0.45 * time / duration) / SampleRate;
      double buzz = Math.Sin(2 * Math.PI * phase + 2.4 * Math.Sin(4 * Math.PI * phase));
      double flutter = 0.6 + 0.4 * Math.Sin(2 * Math.PI * (longFart ? 14 : 23) * time);
      double envelope = Math.Min(1, time / 0.008) * Math.Min(1, (duration - time) / 0.06);
      samples[i] = (short)((buzz * 0.5 + rumble * 0.5) * flutter * envelope * short.MaxValue * 0.7);
    }

    return samples;
  }

  private static short[] CreateGroan()
  {
    // A falling voiced "uhhhh": harmonics emphasize the uh vowel's low formants.
    const double Duration = 0.75;
    var samples = new short[(int)(SampleRate * Duration)];
    double phase = 0;
    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / SampleRate;
      double pitch = 125 - 45 * time / Duration + 2 * Math.Sin(time * 36);
      phase += pitch / SampleRate;
      double voice = 0;
      for (int harmonic = 1; harmonic <= 18; harmonic++)
      {
        double frequency = pitch * harmonic;
        double formant = Math.Exp(-Math.Pow((frequency - 550) / 160, 2))
          + 0.55 * Math.Exp(-Math.Pow((frequency - 1100) / 240, 2));
        voice += Math.Sin(2 * Math.PI * phase * harmonic) * (0.08 + formant) / harmonic;
      }

      double envelope = Math.Min(1, time / 0.035) * Math.Min(1, (Duration - time) / 0.15);
      samples[i] = (short)(Math.Clamp(voice * envelope * 0.85, -0.9, 0.9) * short.MaxValue);
    }

    return samples;
  }

  private static short[] CreatePaperWhoosh()
  {
    const double Duration = 0.18;
    var samples = new short[(int)(SampleRate * Duration)];
    uint noise = 0x7A9E2;
    double filtered = 0;
    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / SampleRate;
      noise ^= noise << 13;
      noise ^= noise >> 17;
      noise ^= noise << 5;
      double value = (noise & 0xFFFF) / 32767.5 - 1;
      filtered += (value - filtered) * 0.28;
      double envelope = Math.Sin(Math.PI * time / Duration) * Math.Min(1, time / 0.005);
      samples[i] = (short)(filtered * envelope * short.MaxValue * 0.65);
    }

    return samples;
  }

  private static short[] CreateWeapon(GameSound sound)
  {
    bool flame = sound == GameSound.Flamethrower;
    double duration = flame ? 0.4 : 0.3;
    double volume = flame ? 0.6 : 0.95;

    var samples = new short[(int)(SampleRate * duration)];
    uint noiseState = flame ? 0xF1A4Eu : 0xB0057u;
    double filteredNoise = 0, phase = 0;

    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / SampleRate;
      noiseState ^= noiseState << 13;
      noiseState ^= noiseState >> 17;
      noiseState ^= noiseState << 5;

      double noise = (noiseState & 0xFFFF) / 32767.5 - 1;
      filteredNoise += (noise - filteredNoise) * (flame ? 0.18 : 0.08);

      // A low sine boom and muffled noise keep the shotgun distinct from stomps.
      double frequency = flame ? 80 : 95 * Math.Pow(0.45, time / duration);
      phase = (phase + frequency / SampleRate) % 1;
      double bass = flame ? 1 - 4 * Math.Abs(phase - 0.5) : Math.Sin(2 * Math.PI * phase);
      double envelope = Math.Min(1, time / 0.002)
        * Math.Min(1, (duration - time) / 0.004);

      double value = flame
        ? (filteredNoise * 0.8 + bass * 0.2) * (0.8 + 0.2 * Math.Sin(2 * Math.PI * 25 * time))
        : (filteredNoise * 0.35 + bass * 0.65) * Math.Exp(-10 * time);

      samples[i] = (short)(value * envelope * short.MaxValue * volume);
    }

    return samples;
  }

  private static short[] CreateWhipCrack()
  {
    const double Duration = 0.22;
    var samples = new short[(int)(SampleRate * Duration)];
    uint noise = 0xC4A17;
    double filtered = 0;

    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / SampleRate;
      noise ^= noise << 13;
      noise ^= noise >> 17;
      noise ^= noise << 5;

      double value = (noise & 0xFFFF) / 32767.5 - 1;

      filtered += (value - filtered) * 0.12;

      double whoosh = filtered * Math.Sin(Math.PI * Math.Min(1, time / 0.1)) * 0.5;
      double crack = time < 0.085 ? 0 : (value - filtered) * Math.Exp(-(time - 0.085) * 65);
      double envelope = Math.Min(1, time / 0.005) * Math.Min(1, (Duration - time) / 0.025);

      samples[i] = (short)(Math.Clamp((whoosh + crack) * envelope, -0.9, 0.9) * short.MaxValue);
    }

    return samples;
  }
}
