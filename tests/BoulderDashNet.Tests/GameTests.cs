using BoulderDashNet.Core;

namespace BoulderDashNet.Tests;

public class GameTests
{
    public static TheoryData<int> LevelIndexes()
    {
        var data = new TheoryData<int>();
        for (var i = 0; i < Levels.All.Count; i++) data.Add(i);
        return data;
    }

    [Theory]
    [MemberData(nameof(LevelIndexes))]
    public void Every_level_is_well_formed(int index)
    {
        var def = Levels.All[index];
        var cave = new Cave(def);
        Assert.Equal(1, cave.Count(TileType.Player));
        Assert.Equal(1, cave.Count(TileType.Exit));
        Assert.True(cave.PlayerAlive);
        Assert.False(string.IsNullOrWhiteSpace(def.NameEn), $"{def.Name} : nom anglais manquant");
        Assert.False(string.IsNullOrWhiteSpace(def.HintEn), $"{def.Name} : astuce anglaise manquante");
        Assert.True(cave.Count(TileType.Diamond) >= def.DiamondsRequired,
            $"{def.Name}: {cave.Count(TileType.Diamond)} diamants pour {def.DiamondsRequired} requis");
        for (var x = 0; x < def.Width; x++)
        {
            Assert.Equal(TileType.Steel, cave.Get(x, 0));
            Assert.Equal(TileType.Steel, cave.Get(x, def.Height - 1));
        }
    }

    [Theory]
    [MemberData(nameof(LevelIndexes))]
    public void Every_level_survives_idle_simulation(int index)
    {
        // Le joueur ne bouge pas : la caverne doit se stabiliser sans exception
        // et le joueur ne doit pas mourir dès les premières secondes.
        var cave = new Cave(Levels.All[index]);
        for (var i = 0; i < 30; i++) cave.Tick(Direction.None, false, 0.13);
        Assert.True(cave.PlayerAlive, Levels.All[index].Name);
    }

    [Fact]
    public void Time_out_costs_a_life()
    {
        var def = new CaveDefinition
        {
            Name = "chrono",
            Width = 6,
            Height = 4,
            TimeSeconds = 1,
            Map = ["WWWWWW", "WP..XW", "W....W", "WWWWWW"],
        };
        var game = new GameSession([def]);
        game.NewGame();
        game.Confirm(); // passe l'intro
        Assert.Equal(GameState.Playing, game.State);
        for (var i = 0; i < 30; i++) game.Update(0.05, default);
        Assert.Equal(GameState.Dying, game.State);
        for (var i = 0; i < 60; i++) game.Update(0.05, default);
        Assert.Equal(GameSession.StartLives - 1, game.Lives);
        Assert.Equal(GameState.LevelIntro, game.State);
    }

    [Fact]
    public void Finishing_last_level_leads_to_victory_with_time_bonus()
    {
        var def = new CaveDefinition
        {
            Name = "fin",
            Width = 6,
            Height = 3,
            TimeSeconds = 50,
            DiamondsRequired = 1,
            Map = ["WWWWWW", "WPdX W", "WWWWWW"],
        };
        var game = new GameSession([def]);
        game.NewGame();
        game.Confirm();
        var right = new PlayerInput(Direction.Right, Direction.None, false);
        for (var i = 0; i < 20 && game.State == GameState.Playing; i++) game.Update(0.05, right);
        Assert.Equal(GameState.LevelComplete, game.State);
        for (var i = 0; i < 400 && game.State == GameState.LevelComplete; i++) game.Update(0.02, default);
        Assert.Equal(GameState.Victory, game.State);
        Assert.True(game.Score > 10 + 40, $"score {game.Score}");
    }

    [Fact]
    public void Short_key_tap_is_not_lost_between_ticks()
    {
        var def = new CaveDefinition
        {
            Name = "tap",
            Width = 6,
            Height = 3,
            Map = ["WWWWWW", "WP..XW", "WWWWWW"],
        };
        var game = new GameSession([def]);
        game.NewGame();
        game.Confirm();
        game.Update(0.01, new PlayerInput(Direction.None, Direction.Right, false)); // appui très bref
        for (var i = 0; i < 10; i++) game.Update(0.03, default);
        Assert.Equal(2, game.Cave!.PlayerX);
    }

    [Fact]
    public void Extra_life_every_500_points()
    {
        var row = "WP" + new string('d', 60) + "XW";
        var def = new CaveDefinition
        {
            Name = "riche",
            Width = row.Length,
            Height = 3,
            DiamondsRequired = 1,
            DiamondValue = 10,
            ExtraDiamondValue = 10,
            TimeSeconds = 999,
            Map = [new string('W', row.Length), row, new string('W', row.Length)],
        };
        var game = new GameSession([def]);
        game.NewGame();
        game.Confirm();
        var right = new PlayerInput(Direction.Right, Direction.None, false);
        for (var i = 0; i < 400 && game.Cave!.DiamondsCollected < 55; i++) game.Update(0.05, right);
        Assert.Equal(GameSession.StartLives + 1, game.Lives);
    }
}
