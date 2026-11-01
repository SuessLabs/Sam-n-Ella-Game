// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace SamElla;

internal readonly record struct LeaderboardEntry(string Initials, int Score, DateTimeOffset RecordedAt);

internal sealed class Leaderboard
{
  public const int Capacity = 10;
  private readonly List<LeaderboardEntry> _entries = [];

  public IReadOnlyList<LeaderboardEntry> Entries => _entries;

  public static Leaderboard Load(string path)
  {
    var board = new Leaderboard();
    var slots = new SortedDictionary<int, LeaderboardEntry>();
    try
    {
      foreach (string line in File.ReadAllLines(path))
      {
        string[] parts = line.Split('=', 2);
        string key = parts[0].Trim();
        const string Prefix = "Leaderboard.";
        if (parts.Length != 2 || !key.StartsWith(Prefix, StringComparison.Ordinal) ||
          !int.TryParse(key[Prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int slot) ||
          slot < 1 || slot > Capacity)
          continue;

        string[] fields = parts[1].TrimEnd().Split('|');
        if (fields.Length != 3 || !ValidInitials(fields[0]) ||
          !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int score) || score <= 0 ||
          !DateTimeOffset.TryParseExact(fields[2], "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
          continue;

        slots[slot] = new LeaderboardEntry(fields[0].ToUpperInvariant(), score, date);
      }
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
      return board;
    }

    board._entries.AddRange(slots.Values.OrderByDescending(entry => entry.Score).Take(Capacity));
    return board;
  }

  public bool Qualifies(int score) => score > 0 &&
      (_entries.Count < Capacity || score > _entries[^1].Score);

  public int RankFor(int score) => _entries.Count(entry => entry.Score >= score) + 1;

  public IEnumerable<string> ToLines()
  {
    for (int index = 0; index < _entries.Count; index++)
    {
      var entry = _entries[index];
      yield return string.Format(CultureInfo.InvariantCulture, "Leaderboard.{0}={1}|{2}|{3:O}",
        index + 1, entry.Initials, entry.Score, entry.RecordedAt);
    }
  }

  public bool TryAdd(string initials, int score, DateTimeOffset recordedAt)
  {
    if (!ValidInitials(initials) || !Qualifies(score))
      return false;

    int index = RankFor(score) - 1;
    _entries.Insert(index, new LeaderboardEntry(initials.ToUpperInvariant(), score, recordedAt));

    if (_entries.Count > Capacity)
      _entries.RemoveAt(Capacity);

    return true;
  }

  private static bool ValidInitials(string initials) =>
    initials.Length == 3 && initials.All(character => char.IsAsciiLetterOrDigit(character) || character == ' ');
}
