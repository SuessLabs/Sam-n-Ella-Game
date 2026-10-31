using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SamElla.Screens;

namespace SamElla.Tests;

[TestClass]
public sealed class LeaderboardTests : GameTestBase
{
  private static readonly DateTimeOffset Date = new(2026, 10, 29, 12, 0, 0, TimeSpan.FromHours(-4));

  [TestMethod]
  public void ArrowsEditSelectedPositionAndEnterAcceptsWithoutClosingBoard()
  {
    var screen = new LeaderboardScreen(new Leaderboard());
    screen.TryOpenFor(WinGame());
    screen.HandleInput([0x25, 0x26, 0x27, 0x26, 0x27, 0x27, 0x28], true, Date);
    Assert.AreEqual("BB ", screen.Initials);
    Assert.AreEqual(2, screen.SelectedInitialIndex);

    screen.HandleInput([0x25, 0x26], true, Date);
    Assert.AreEqual("BC ", screen.Initials);
    Assert.AreEqual(1, screen.SelectedInitialIndex);
    Assert.IsTrue(screen.HandleInput([0x0D, 0x0D], true, Date));
    Assert.AreEqual("BC ", screen.Board.Entries[0].Initials);
    Assert.IsFalse(screen.IsEnteringInitials);
    Assert.IsTrue(screen.IsActive);
  }

  [TestMethod]
  public void CorruptLeaderboardLinesAreIgnoredAndValidRowsAreSorted()
  {
    Assert.HasCount(0, Leaderboard.Load(NextPath).Entries);

    File.WriteAllLines(NextPath,
    [
      $"Leaderboard.1=ABC|100|{Date:O}", $"Leaderboard.2=X19|900|{Date:O}",
      $"Leaderboard.3=BAD|-1|{Date:O}", "Leaderboard.4=XYZ|1000|invalid",
      $"Leaderboard.5=TOOLONG|500|{Date:O}", $"Leaderboard.11=XYZ|9999|{Date:O}",
      "Leaderboard.6=XYZ|9999999999999999999|invalid", "MusicEnabled=false", "garbage",
    ]);

    var board = Leaderboard.Load(NextPath);
    Assert.HasCount(2, board.Entries);
    Assert.AreEqual("X19", board.Entries[0].Initials);
    Assert.AreEqual(100, board.Entries[1].Score);
  }

  [TestMethod]
  public void GameOverPromptsAfterAnimationAndKeepsRunIntactUntilConfirmed()
  {
    var game = Map(
      "....................",
      "....................",
      ".S.o..............G.",
      "....................");

    for (int life = 0; life < 3; life++)
    {
      for (int tick = 0; tick < 180 && !game.IsDying; tick++)
        game.Update(1, false);

      Assert.IsTrue(game.IsDying);
      if (life < 2)
        game.ContinueAfterDeath();
    }

    Assert.IsGreaterThan(0, game.Score);
    var screen = new LeaderboardScreen(new Leaderboard());
    Assert.IsFalse(screen.TryOpenFor(game));
    Tick(game, Game.DeathDurationTicks);
    Assert.IsTrue(screen.TryOpenFor(game));
    Assert.AreEqual(LeaderboardContext.GameOver, screen.Context);
    Assert.IsTrue(screen.HandleInput([0x52, 0x41, 0x53, 0x0D], true, Date));
    Assert.AreEqual("RAS", screen.Board.Entries[0].Initials);
    Assert.AreEqual(0, game.Lives, "The R initial must not restart gameplay.");

    screen.HandleInput([0x0D], true, Date);
    Assert.IsFalse(screen.IsActive);
    Assert.IsTrue(game.ContinueAfterDeath());
    Assert.AreEqual(3, game.Lives);
  }

  [TestMethod]
  public void InitialsIgnoreUnfocusedAndUnsupportedKeysAndAllowTypingEditingAndNumpad()
  {
    var screen = new LeaderboardScreen(new Leaderboard());
    screen.TryOpenFor(WinGame());
    Assert.IsFalse(screen.HandleInput([0x41, 0x42, 0x31, 0x0D], false, Date));
    Assert.AreEqual("AAA", screen.Initials);
    Assert.AreEqual(0, screen.SelectedInitialIndex);
    Assert.IsFalse(screen.HandleInput([0x1B, 0xBA], true, Date));
    Assert.IsTrue(screen.IsEnteringInitials);

    screen.HandleInput([0x41, 0x60], true, Date);
    Assert.AreEqual("A0A", screen.Initials);
    Assert.AreEqual(2, screen.SelectedInitialIndex);
    Assert.IsTrue(screen.IsEnteringInitials);

    screen.HandleInput([0x69, 0x58], true, Date);
    Assert.AreEqual("A09", screen.Initials, "Input is limited to three characters.");
    Assert.IsTrue(screen.HandleInput([0x08, 0x42, 0x0D], true, Date));
    Assert.AreEqual("A0B", screen.Board.Entries[0].Initials);
    Assert.IsFalse(screen.HandleInput([0x41, 0x1B], true, Date));
    Assert.IsTrue(screen.IsActive, "Only Enter continues a finished run.");
  }

  [TestMethod]
  public void KeepsTenBestScoresAndExistingTiesKeepTheirRanks()
  {
    var board = new Leaderboard();
    Assert.IsFalse(board.Qualifies(0));
    for (int score = 100; score <= 1000; score += 100)
      Assert.IsTrue(board.TryAdd("ABC", score, Date));

    Assert.HasCount(10, board.Entries);
    CollectionAssert.AreEqual(new[] { 1000, 900, 800, 700, 600, 500, 400, 300, 200, 100 }, board.Entries.Select(entry => entry.Score).ToArray());
    Assert.IsFalse(board.TryAdd("XYZ", 100, Date.AddDays(1)));
    Assert.IsFalse(board.Qualifies(99));
    Assert.IsTrue(board.TryAdd("new", 900, Date.AddDays(1)));
    Assert.HasCount(10, board.Entries);
    Assert.AreEqual("ABC", board.Entries[1].Initials);
    Assert.AreEqual("NEW", board.Entries[2].Initials);
    Assert.AreEqual(200, board.Entries[^1].Score);
    Assert.IsTrue(board.TryAdd("A19", int.MaxValue, Date));
    Assert.AreEqual(int.MaxValue, board.Entries[0].Score);
  }

  [TestMethod]
  public void LeaderboardSurvivesAudioSavesAndPreservesOtherPlayerRecords()
  {
    var settings = new GameSettings();
    settings.Audio.MusicVolume = 35;
    settings.Records.RecordProgress(5000, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(10), Date);
    settings.Leaderboard.TryAdd("ab1", 1200, Date);
    settings.Leaderboard.TryAdd("XYZ", 5000, Date.AddHours(1));
    settings.Save(NextPath);
    var restored = GameSettings.Load(NextPath);
    Assert.AreEqual(35, restored.Audio.MusicVolume);
    Assert.AreEqual(5000, restored.Records.HighScore);
    Assert.HasCount(2, restored.Leaderboard.Entries);
    Assert.AreEqual("XYZ", restored.Leaderboard.Entries[0].Initials);
    Assert.AreEqual("AB1", restored.Leaderboard.Entries[1].Initials);
    Assert.AreEqual(Date, restored.Leaderboard.Entries[1].RecordedAt);
    Assert.AreEqual(Date.Offset, restored.Leaderboard.Entries[1].RecordedAt.Offset);

    restored.Audio.MusicEnabled = false;
    restored.Save(NextPath);
    Assert.HasCount(2, GameSettings.Load(NextPath).Leaderboard.Entries);
    Assert.AreEqual(TimeSpan.FromSeconds(60), GameSettings.Load(NextPath).Records.FastestCompletion);
  }

  [TestMethod]
  public void NonqualifyingRunsSkipEntryAndTitleViewerDoesNotSubmit()
  {
    var board = new Leaderboard();
    for (int score = 2000; score <= 11000; score += 1000)
      board.TryAdd("AAA", score, Date);

    var screen = new LeaderboardScreen(board);
    Assert.IsFalse(screen.TryOpenFor(WinGame()));
    screen.OpenViewer();
    Assert.AreEqual(LeaderboardContext.Title, screen.Context);
    Assert.IsFalse(screen.IsEnteringInitials);
    screen.HandleInput([0x41, 0x42, 0x43], true, Date);
    Assert.HasCount(10, board.Entries);
    screen.HandleInput([0x1B], true, Date);
    Assert.IsFalse(screen.IsActive);
  }

  [TestMethod]
  [DataRow("")]
  [DataRow("AB")]
  [DataRow("ABCD")]
  [DataRow("A!B")]
  [DataRow("ÅBC")]
  public void RejectsInvalidInitials(string initials)
  {
    var board = new Leaderboard();
    Assert.IsFalse(board.TryAdd(initials, 100, Date));
    Assert.HasCount(0, board.Entries);
  }

  [TestMethod]
  public void RendererHighlightsCurrentSlotAndShowsNewControls()
  {
    var screen = new LeaderboardScreen(new Leaderboard());
    screen.TryOpenFor(WinGame());
    screen.HandleInput([0x28], true, Date);
    var renderer = new PixelRenderer(Path.Combine(AppContext.BaseDirectory, "Assets"));
    renderer.DrawLeaderboard(screen);
    Assert.AreEqual(0xFFFFD56Bu, renderer.Frame.Pixels[101 * PixelRenderer.Width + 130]);
    Assert.AreEqual(0xFF527787u, renderer.Frame.Pixels[101 * PixelRenderer.Width + 174]);
    string? preview = Environment.GetEnvironmentVariable("SAMELLA_INITIALS_PREVIEW");
    if (preview is not null)
      renderer.Frame.SavePng(preview);

    screen.HandleInput([0x27], true, Date);
    renderer.DrawLeaderboard(screen);
    Assert.AreEqual(0xFF527787u, renderer.Frame.Pixels[101 * PixelRenderer.Width + 130]);
    Assert.AreEqual(0xFFFFD56Bu, renderer.Frame.Pixels[101 * PixelRenderer.Width + 174]);
  }

  [TestMethod]
  [DataRow(" AB")]
  [DataRow("AB ")]
  [DataRow("A B")]
  [DataRow("   ")]
  [DataRow(" 0 ")]
  [DataRow("a b")]
  public void SpacesInEveryPositionSurviveSettingsSaves(string initials)
  {
    var settings = new GameSettings();
    Assert.IsTrue(settings.Leaderboard.TryAdd(initials, 100, Date));
    settings.Save(NextPath);
    var restored = GameSettings.Load(NextPath);
    Assert.HasCount(1, restored.Leaderboard.Entries);
    Assert.AreEqual(initials.ToUpperInvariant(), restored.Leaderboard.Entries[0].Initials);
    restored.Audio.MusicEnabled = false;
    restored.Save(NextPath);
    Assert.AreEqual(initials.ToUpperInvariant(), GameSettings.Load(NextPath).Leaderboard.Entries[0].Initials);
  }

  [TestMethod]
  public void TopTenIsAvailableFromTitleAndPauseStillHasTwoChoices()
  {
    var title = new TitleScreen();
    Assert.AreEqual(MenuAction.Leaderboard, title.HandleInput(0, [0x28, 0x28, 0x0D], true));
    Assert.IsTrue(title.IsActive);
    var menu = new ScreenMenu();
    Assert.AreEqual(MenuAction.Primary, menu.HandleInput([0x28, 0x28, 0x0D], true));
  }

  [TestMethod]
  public void TypingSpaceAndArrowsCanBeMixedAndDefaultCanBeAccepted()
  {
    var screen = new LeaderboardScreen(new Leaderboard());
    screen.TryOpenFor(WinGame());
    screen.HandleInput([0x20, 0x42, 0x27, 0x28], true, Date);
    Assert.AreEqual(" B ", screen.Initials);
    Assert.IsTrue(screen.HandleInput([0x0D], true, Date));
    Assert.AreEqual(" B ", screen.Board.Entries[0].Initials);

    screen = new LeaderboardScreen(new Leaderboard());
    screen.TryOpenFor(WinGame());
    Assert.IsTrue(screen.HandleInput([0x0D], true, Date));
    Assert.AreEqual("AAA", screen.Board.Entries[0].Initials);
  }

  [TestMethod]
  public void UpAndDownCycleEveryCharacterAndWrapBetweenAAndSpace()
  {
    const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ";
    var screen = new LeaderboardScreen(new Leaderboard());
    screen.TryOpenFor(WinGame());
    for (int index = 1; index <= characters.Length; index++)
    {
      screen.HandleInput([0x26], true, Date);
      Assert.AreEqual(characters[index % characters.Length] + "AA", screen.Initials);
      Assert.AreEqual(0, screen.SelectedInitialIndex);
    }

    for (int index = 1; index <= characters.Length; index++)
    {
      screen.HandleInput([0x28], true, Date);
      Assert.AreEqual(characters[(characters.Length - index) % characters.Length] + "AA", screen.Initials);
    }
  }

  [TestMethod]
  public void VictoryPromptsBeforeCreditsAndSubmitsOnlyOncePerRun()
  {
    var game = WinGame();
    var board = new Leaderboard();
    var screen = new LeaderboardScreen(board);
    Assert.IsTrue(screen.TryOpenFor(game));
    Assert.AreEqual(0L, game.CreditsTicks);
    Assert.AreEqual(LeaderboardContext.Victory, screen.Context);
    Assert.IsTrue(screen.IsEnteringInitials);
    Assert.IsTrue(screen.HandleInput([0x41, 0x42, 0x31, 0x0D, 0x0D], true, Date));
    Assert.IsTrue(screen.IsActive, "The submit key must not also continue to credits.");
    Assert.IsFalse(screen.IsEnteringInitials);
    Assert.AreEqual("AB1", board.Entries[0].Initials);
    Assert.HasCount(1, board.Entries);
    Assert.IsFalse(screen.HandleInput([0x0D], true, Date));
    Assert.IsFalse(screen.IsActive);
    Assert.IsFalse(screen.TryOpenFor(game));
    Assert.HasCount(1, board.Entries);

    game.Restart();
    Assert.IsFalse(screen.TryOpenFor(game));

    for (int tick = 0; tick < 120 && !game.Won; tick++)
      game.Update(1, false);

    Assert.IsTrue(screen.TryOpenFor(game), "A fresh run can submit another score.");
  }

  private Game WinGame()
  {
    var game = Map("....................", "....................", ".S.G................", "####################");
    for (int tick = 0; tick < 120 && !game.Won; tick++)
      game.Update(1, false);

    Assert.IsTrue(game.Won);
    return game;
  }
}
