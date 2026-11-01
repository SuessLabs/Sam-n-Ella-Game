// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

namespace SamElla.Screens;

internal readonly record struct CreditLine(string Text, int Y, int Scale, bool Highlight, bool PreserveCase = false);

internal sealed class EndCreditsScreen
{
  private readonly List<CreditLine> _lines = [];

  private int _y;

  public EndCreditsScreen(string assetsPath, TimeSpan completionTime, int deaths, int score, int itemsMissed)
  {
    OpeningQuote = ChooseQuote(Path.Combine(assetsPath, "credits-opening-quotes.txt"),
      "You survived. The reaper hates overtime.");

    ClosingQuote = ChooseQuote(Path.Combine(assetsPath, "credits-closing-quotes.txt"),
      "One more run. The morgue saved your seat.");

    AddQuote(OpeningQuote);
    _y += 32;
    Add("Sam n' Ella", 4, true);
    Add("ALL LEVELS COMPLETE");
    _y += 16;
    Add("CREATED BY", 1);
    Add("Damian Suess", 2, true);
    Add("Suess Labs");
    Add("Xeno Innovations");
    _y += 24;
    Add("SPECIAL THANKS");
    Add("Little Stinker", 3, true);
    _y += 24;
    Add("YOUR RUN", 3, true);
    Add($"Time Wasted: {(int)completionTime.TotalHours:00}:{completionTime.Minutes:00}:{completionTime.Seconds:00}", preserveCase: true);
    Add($"TIMES DIED {deaths}");
    Add($"POINTS {score}");
    Add($"ITEMS MISSED {itemsMissed}");
    _y += 120;
    ClosingOffset = _y;
    AddQuote(ClosingQuote);
    _y += 16;
    Add("R TO PLAY AGAIN", 2, true);
  }

  public int ClosingOffset { get; }
  public string ClosingQuote { get; }
  public IReadOnlyList<CreditLine> Lines => _lines;
  public string OpeningQuote { get; }

  private static string ChooseQuote(string path, string fallback)
  {
    string[] quotes = File.Exists(path)
      ? File.ReadAllLines(path).Select(line => line.Trim())
        .Where(line => line.Length > 0 && !line.StartsWith(';')).ToArray()
      : [];

    return quotes.Length == 0 ? fallback : quotes[Random.Shared.Next(quotes.Length)];
  }

  private void Add(string text, int scale = 2, bool highlight = false, bool preserveCase = false)
  {
    _lines.Add(new CreditLine(text, _y, scale, highlight, preserveCase));
    _y += (5 * scale) + 18;
  }

  private void AddQuote(string quote)
  {
    string remaining = quote.Trim();
    while (remaining.Length > 0)
    {
      int length = Math.Min(40, remaining.Length);

      if (length < remaining.Length)
      {
        int space = remaining.LastIndexOf(' ', length - 1, length);
        if (space > 0)
          length = space;
      }

      Add(remaining[..length].Trim(), 2, true);
      remaining = remaining[length..].TrimStart();
    }
  }
}
