using BoulderDashNet.Core;

namespace BoulderDashNet.Tests;

public class HighScoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static HighScoreEntry E(string name, int score, int level = 1, int minutes = 0) =>
        new(name, score, level, T0.AddMinutes(minutes));

    [Theory]
    [InlineData("  Xavier  ", "Xavier")]
    [InlineData("Jean   Pierre", "Jean Pierre")]
    [InlineData("Élodie_42", "Élodie_42")]
    [InlineData("Rock-Ford.", "Rock-Ford.")]
    public void Valid_names_are_normalized(string raw, string expected) =>
        Assert.Equal(expected, HighScoreRules.NormalizeName(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("a/b")]
    [InlineData("ABCDEFGHIJKLMNOPQ")] // 17 caractères
    public void Invalid_names_are_rejected(string? raw) =>
        Assert.Null(HighScoreRules.NormalizeName(raw));

    [Fact]
    public void Board_is_sorted_by_score_then_level_then_date()
    {
        var board = new HighScoreBoard();
        board.Submit(E("A", 100, 1, 0));
        board.Submit(E("B", 300, 2, 1));
        board.Submit(E("C", 100, 3, 2));
        board.Submit(E("D", 100, 1, -5));
        Assert.Equal(["B", "C", "D", "A"], board.Entries.Select(e => e.Name));
    }

    [Fact]
    public void A_player_can_appear_several_times()
    {
        var board = new HighScoreBoard();
        Assert.Equal(1, board.Submit(E("xav", 200, 1, 0)));
        Assert.Equal(2, board.Submit(E("Xav", 150, 1, 1)));
        Assert.Equal(1, board.Submit(E("XAV", 500, 2, 2)));
        Assert.Equal(2, board.Submit(E("Bob", 300, 1, 3)));
        Assert.Equal([500, 300, 200, 150], board.Entries.Select(e => e.Score));
        Assert.Equal(3, board.Entries.Count(e => e.Name.Equals("xav", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Same_score_twice_ranks_the_newest_after_the_oldest()
    {
        var board = new HighScoreBoard();
        Assert.Equal(1, board.Submit(E("xav", 200, 1, 0)));
        Assert.Equal(2, board.Submit(E("xav", 200, 1, 1)));
        Assert.Equal(2, board.Entries.Count);
    }

    [Fact]
    public void Board_keeps_only_its_capacity_and_reports_rank()
    {
        var board = new HighScoreBoard(capacity: 3);
        board.Submit(E("A", 300));
        board.Submit(E("B", 200));
        board.Submit(E("C", 100));
        Assert.Equal(0, board.Submit(E("D", 50)));          // n'entre pas
        Assert.Equal(2, board.Submit(E("E", 250)));
        Assert.Equal(["A", "E", "B"], board.Entries.Select(e => e.Name));
    }

    [Fact]
    public void Plausibility_bound_grows_with_levels_and_rejects_absurd_scores()
    {
        var levels = Levels.All;
        var one = HighScoreRules.MaxPlausibleScore(levels, 1);
        var all = HighScoreRules.MaxPlausibleScore(levels, levels.Count);
        Assert.True(one > levels[0].TimeSeconds);
        Assert.True(all > one);
        Assert.True(HighScoreRules.IsPlausible(levels, 500, 1));
        Assert.False(HighScoreRules.IsPlausible(levels, 999_999, 1));
        Assert.False(HighScoreRules.IsPlausible(levels, 100, 0));
        Assert.False(HighScoreRules.IsPlausible(levels, 100, levels.Count + 1));
        Assert.False(HighScoreRules.IsPlausible(levels, 0, 1));
    }

    [Fact]
    public void A_real_game_score_is_always_plausible()
    {
        // Partie simulée : on termine un niveau en ramassant tout, le score reste sous la borne.
        var def = new CaveDefinition
        {
            Name = "mini",
            Width = 8,
            Height = 3,
            DiamondsRequired = 2,
            DiamondValue = 10,
            ExtraDiamondValue = 20,
            TimeSeconds = 60,
            Map = ["WWWWWWWW", "WPddddXW", "WWWWWWWW"],
        };
        var game = new GameSession([def]);
        game.NewGame();
        game.Confirm();
        var right = new PlayerInput(Direction.Right, Direction.None, false);
        for (var i = 0; i < 200 && game.State is GameState.Playing or GameState.LevelComplete; i++)
            game.Update(0.05, game.State == GameState.Playing ? right : default);
        Assert.Equal(GameState.Victory, game.State);
        Assert.True(HighScoreRules.IsPlausible([def], game.Score, 1), $"score {game.Score}");
    }
}
