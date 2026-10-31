namespace SamElla.Audio;

internal enum GameMusic
{
  None,
  Title,
  GameOver,
  Credits,
  Day1,
  Day2,
  Day3,
  Night1,
  Night2,
  Night3,
}

// Original scores rendered as layered FM lead, triangle bass, chords, and drums.
internal static class GameMusicTracks
{
  public static GameMusic ForScreen(bool titleActive, Game game)
  {
    if (titleActive)
      return GameMusic.Title;

    if (game.Lives == 0 && game.AwaitingRestart)
      return GameMusic.GameOver;

    if (game.Won && game.Credits is not null)
      return GameMusic.Credits;

    if (game.IsDying || game.Lives == 0)
      return GameMusic.None;

    return ForLevel(game.CurrentBackground, game.CurrentBackground == game.Level.Background ? game.ThemeLevelNumber : 1);
  }

  public static GameMusic ForLevel(BackgroundTheme theme, int themeLevelNumber)
  {
    if (themeLevelNumber < 1)
      throw new ArgumentOutOfRangeException(nameof(themeLevelNumber));

    int song = (themeLevelNumber - 1) % 3;

    return (theme, song) switch
    {
      (BackgroundTheme.Underworld, _) => GameMusic.Night2,
      (BackgroundTheme.Night, 0) => GameMusic.Night1,
      (BackgroundTheme.Night, 1) => GameMusic.Night2,
      (BackgroundTheme.Night, _) => GameMusic.Night3,
      (_, 0) => GameMusic.Day1,
      (_, 1) => GameMusic.Day2,
      _ => GameMusic.Day3,
    };
  }

  public static bool Loops(GameMusic music) => music is not (GameMusic.None or GameMusic.GameOver);

  public static short[] Create(GameMusic music)
  {
    bool march = music == GameMusic.GameOver;
    bool dark = music is GameMusic.Night1 or GameMusic.Night2 or GameMusic.Night3;
    int[] minorBars = [];
    int[] melody;
    int[] roots;
    double tempo;

    switch (music)
    {
      case GameMusic.Title:
        minorBars = [4];
        tempo = 120;
        roots = [48, 53, 55, 48, 57, 53, 55, 48];
        melody = [72, 76, 79, 76, 74, 76, 79, 0, 77, 81, 84, 81, 79, 77, 76, 0,
          74, 79, 83, 79, 77, 74, 71, 74, 72, 76, 79, 84, 79, 76, 72, 0,
          76, 81, 84, 81, 79, 76, 72, 76, 77, 81, 84, 86, 84, 81, 77, 0,
          79, 83, 86, 83, 81, 79, 77, 74, 76, 79, 84, 79, 76, 74, 72, 0];
        break;

      case GameMusic.GameOver:
        tempo = 96;
        roots = [36, 31];
        // A slow minor-key march with repeated notes and a descending resolution.
        melody = [55, 0, 55, 55, 58, 0, 56, 55, 53, 0, 53, 53, 51, 50, 48, 0];
        break;

      case GameMusic.Credits:
        minorBars = [3, 4];
        tempo = 132;
        roots = [50, 55, 57, 54, 59, 55, 57, 50];
        melody = [78, 81, 86, 85, 83, 81, 78, 0, 79, 83, 86, 88, 86, 83, 79, 0,
          81, 85, 88, 90, 88, 85, 81, 0, 78, 81, 85, 88, 85, 81, 78, 0,
          83, 86, 90, 86, 85, 83, 81, 78, 79, 83, 86, 88, 90, 88, 86, 83,
          81, 85, 88, 85, 83, 81, 79, 76, 78, 81, 86, 90, 86, 81, 78, 0];
        break;

      case GameMusic.Day1:
        // "Sunlit Sprint": bright C-major arpeggios and a skipping hook.
        tempo = 138;
        minorBars = [2, 6];
        roots = [48, 55, 57, 53, 48, 53, 57, 55];
        melody = [72, 79, 76, 79, 84, 0, 79, 76, 74, 79, 83, 86, 83, 79, 74, 0,
          76, 81, 84, 81, 79, 76, 72, 0, 77, 81, 84, 81, 79, 77, 76, 74,
          72, 76, 79, 84, 88, 84, 79, 0, 81, 77, 81, 84, 89, 84, 81, 77,
          76, 81, 84, 88, 84, 81, 79, 76, 74, 79, 83, 79, 77, 74, 71, 0];
        break;

      case GameMusic.Day2:
        // "Cloud Hopper": a bouncy G-major call-and-response melody.
        tempo = 150;
        minorBars = [1, 5];
        roots = [43, 52, 48, 50, 43, 52, 48, 50];
        melody = [79, 0, 83, 86, 83, 79, 81, 0, 83, 79, 76, 79, 83, 86, 88, 0,
          84, 83, 79, 76, 79, 84, 88, 84, 86, 81, 78, 81, 86, 88, 90, 0,
          83, 86, 91, 86, 83, 81, 79, 0, 88, 86, 83, 79, 76, 79, 83, 0,
          84, 88, 91, 88, 86, 84, 83, 79, 81, 86, 90, 86, 84, 81, 78, 0];
        break;

      case GameMusic.Day3:
        // "Meadow Parade": a cheerful F-major tune with a climbing refrain.
        tempo = 126;
        minorBars = [2, 6];
        roots = [41, 46, 50, 48, 41, 46, 50, 48];
        melody = [77, 81, 84, 0, 81, 79, 77, 0, 82, 86, 89, 86, 84, 82, 81, 0,
          81, 77, 74, 77, 81, 84, 86, 0, 79, 84, 88, 84, 82, 79, 76, 0,
          77, 79, 81, 84, 89, 88, 86, 84, 82, 84, 86, 89, 93, 89, 86, 82,
          86, 84, 81, 77, 74, 77, 81, 86, 84, 79, 76, 79, 82, 79, 76, 0];
        break;

      case GameMusic.Night1:
        // "Graveyard Watch": sparse C-minor bells over an unsettling low drone.
        tempo = 72;
        roots = [36, 36, 32, 31, 36, 29, 32, 31];
        melody = [72, 0, 0, 67, 0, 0, 70, 0, 71, 0, 72, 0, 0, 63, 0, 0,
          68, 0, 0, 63, 0, 66, 0, 0, 67, 0, 62, 0, 65, 0, 66, 0,
          72, 0, 75, 0, 0, 71, 72, 0, 65, 0, 0, 60, 0, 63, 0, 0,
          68, 0, 66, 0, 63, 0, 0, 0, 67, 0, 66, 0, 62, 0, 59, 0];
        break;

      case GameMusic.Night2:
        // "Something Follows": E-minor footsteps and chromatic warning notes.
        tempo = 84;
        roots = [40, 41, 40, 34, 36, 41, 35, 40];
        melody = [64, 0, 71, 0, 70, 0, 67, 0, 65, 0, 0, 72, 0, 68, 0, 66,
          64, 0, 63, 64, 0, 71, 0, 0, 70, 0, 65, 0, 64, 0, 61, 0,
          72, 0, 71, 0, 67, 0, 63, 0, 65, 0, 68, 0, 72, 0, 73, 0,
          71, 0, 66, 0, 62, 0, 65, 0, 64, 0, 63, 0, 59, 0, 0, 0];
        break;

      case GameMusic.Night3:
        // "Below the Moon": a slow D-minor lament with dissonant bell answers.
        tempo = 66;
        roots = [38, 33, 34, 32, 38, 39, 34, 33];
        melody = [74, 0, 0, 0, 69, 0, 68, 0, 69, 0, 64, 0, 65, 0, 0, 0,
          70, 0, 0, 65, 0, 68, 0, 0, 68, 0, 63, 0, 62, 0, 0, 0,
          74, 0, 77, 0, 0, 73, 74, 0, 75, 0, 70, 0, 69, 0, 66, 0,
          70, 0, 69, 0, 65, 0, 62, 0, 69, 0, 68, 0, 64, 0, 61, 0];
        break;

      default:
        throw new ArgumentOutOfRangeException(nameof(music));
    }

    double beat = 60 / tempo;
    double eighth = beat / 2;
    double duration = roots.Length * 4 * beat;
    var samples = new short[(int)Math.Round(duration * GameSounds.SampleRate)];

    double[] notes = melody.Select(note => note == 0 ? 0 : Frequency(note)).ToArray();
    double[] bass = roots.Select(Frequency).ToArray();
    double[] thirds = roots.Select((root, bar) => Frequency(root +
      (march || dark || minorBars.Contains(bar) ? 3 : 4))).ToArray();

    uint noiseState = 0x4D555349;

    for (int i = 0; i < samples.Length; i++)
    {
      double time = (double)i / GameSounds.SampleRate;
      int step = Math.Min(notes.Length - 1, (int)(time / eighth));
      int bar = Math.Min(roots.Length - 1, step / 8);
      double noteTime = time % eighth;
      double beatTime = time % beat;

      double leadEnvelope = Math.Min(1, noteTime / 0.006)
        * Math.Clamp((eighth * 0.88 - noteTime) / 0.025, 0, 1);

      double leadPhase = 2 * Math.PI * notes[step] * time;

      double lead = notes[step] == 0 ? 0 : Math.Sin(leadPhase +
        (dark ? 2.7 : march ? 0.5 : 1.1) * Math.Sin(2 * leadPhase)) * leadEnvelope
        * (dark ? 0.11 : march ? 0.20 : 0.16);

      double bassFrequency = bass[bar] * ((int)(time / beat) % 2 == 0 ? 0.5 : 0.75);
      double bassPhase = bassFrequency * time % 1;

      double low = (1 - 4 * Math.Abs(bassPhase - 0.5)) * Math.Exp(-4 * beatTime / beat)
        * Math.Min(1, beatTime / 0.004) * 0.12;

      double barTime = time % (4 * beat);
      double chordEnvelope = Math.Min(1, barTime / 0.01) * Math.Min(1, (4 * beat - barTime) / 0.03);
      double chord = (Math.Sin(2 * Math.PI * bass[bar] * time)
        + Math.Sin(2 * Math.PI * thirds[bar] * time)
        + Math.Sin(2 * Math.PI * bass[bar] * (dark ? Math.Sqrt(2) : 1.5) * time)) * chordEnvelope * 0.025;
      double drone = dark ? (Math.Sin(2 * Math.PI * bass[bar] * 0.25 * time)
        + 0.45 * Math.Sin(2 * Math.PI * bass[bar] * 0.2515 * time)) * chordEnvelope * 0.09 : 0;

      noiseState ^= noiseState << 13;
      noiseState ^= noiseState >> 17;
      noiseState ^= noiseState << 5;

      double noise = (noiseState & 0xFFFF) / 32767.5 - 1;
      bool downbeat = (int)(time / beat) % 2 == 0;

      double drum = downbeat
        ? Math.Sin(2 * Math.PI * 65 * beatTime) * Math.Exp(-35 * beatTime) * 0.10
        : noise * Math.Exp(-45 * beatTime) * (dark ? 0.025 : march ? 0.05 : 0.08);

      double hat = march || dark ? 0 : noise * Math.Exp(-130 * noteTime) * 0.025;
      double fade = Math.Min(1, time / 0.008) * Math.Min(1, (duration - time) / (march ? 0.35 : 0.008));

      samples[i] = (short)((lead + low + chord + drone + drum + hat) * fade * short.MaxValue);
    }

    return samples;
  }

  private static double Frequency(int midi) => 440 * Math.Pow(2, (midi - 69) / 12.0);

}
