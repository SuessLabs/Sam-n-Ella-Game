// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace SamElla;

internal sealed class PlayerRecords
{
  public TimeSpan? FastestCompletion { get; private set; }
  public DateTimeOffset? FastestCompletionDate { get; private set; }
  public TimeSpan? FastestGameOver { get; private set; }
  public DateTimeOffset? FastestGameOverDate { get; private set; }
  public int HighScore { get; private set; }

  public DateTimeOffset? HighScoreDate { get; private set; }

  public static PlayerRecords Load(string path)
  {
    var records = new PlayerRecords();
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    try
    {
      foreach (string line in File.ReadAllLines(path))
      {
        string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
          values[parts[0]] = parts[1];
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return records;
    }

    if (values.TryGetValue(nameof(HighScore), out string? scoreText) &&
      int.TryParse(scoreText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int score) && score > 0 &&
      TryReadDate(values, nameof(HighScoreDate), out var scoreDate))
    {
      records.HighScore = score;
      records.HighScoreDate = scoreDate;
    }

    if (TryReadTime(values, "FastestCompletionTicks", out var completion) &&
      TryReadDate(values, nameof(FastestCompletionDate), out var completionDate))
    {
      records.FastestCompletion = completion;
      records.FastestCompletionDate = completionDate;
    }

    if (TryReadTime(values, "FastestGameOverTicks", out var gameOver) &&
      TryReadDate(values, nameof(FastestGameOverDate), out var gameOverDate))
    {
      records.FastestGameOver = gameOver;
      records.FastestGameOverDate = gameOverDate;
    }

    return records;
  }

  public bool RecordProgress(int score, TimeSpan? completion, TimeSpan? gameOver, DateTimeOffset recordedAt)
  {
    bool changed = false;
    if (score > HighScore)
    {
      HighScore = score;
      HighScoreDate = recordedAt;
      changed = true;
    }

    if (completion is { } finished && finished > TimeSpan.Zero &&
      (FastestCompletion is null || finished < FastestCompletion))
    {
      FastestCompletion = finished;
      FastestCompletionDate = recordedAt;
      changed = true;
    }

    if (gameOver is { } died && died > TimeSpan.Zero &&
      (FastestGameOver is null || died < FastestGameOver))
    {
      FastestGameOver = died;
      FastestGameOverDate = recordedAt;
      changed = true;
    }

    return changed;
  }

  public IEnumerable<string> ToLines()
  {
    if (HighScoreDate is { } scoreDate)
    {
      yield return $"{nameof(HighScore)}={HighScore.ToString(CultureInfo.InvariantCulture)}";
      yield return $"{nameof(HighScoreDate)}={scoreDate.ToString("O", CultureInfo.InvariantCulture)}";
    }

    if (FastestCompletion is { } completion && FastestCompletionDate is { } completionDate)
    {
      yield return $"FastestCompletionTicks={completion.Ticks.ToString(CultureInfo.InvariantCulture)}";
      yield return $"{nameof(FastestCompletionDate)}={completionDate.ToString("O", CultureInfo.InvariantCulture)}";
    }

    if (FastestGameOver is { } gameOver && FastestGameOverDate is { } gameOverDate)
    {
      yield return $"FastestGameOverTicks={gameOver.Ticks.ToString(CultureInfo.InvariantCulture)}";
      yield return $"{nameof(FastestGameOverDate)}={gameOverDate.ToString("O", CultureInfo.InvariantCulture)}";
    }
  }

  private static bool TryReadDate(IReadOnlyDictionary<string, string> values, string key, out DateTimeOffset date)
  {
    date = default;
    return values.TryGetValue(key, out string? text) &&
      DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
  }

  private static bool TryReadTime(IReadOnlyDictionary<string, string> values, string key, out TimeSpan time)
  {
    time = default;
    if (!values.TryGetValue(key, out string? text) ||
      !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ticks) || ticks <= 0)
      return false;

    time = TimeSpan.FromTicks(ticks);
    return true;
  }
}
