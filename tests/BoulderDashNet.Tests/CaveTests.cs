using BoulderDashNet.Core;

namespace BoulderDashNet.Tests;

public class CaveTests
{
    private const double Dt = 0.13;

    private static Cave Make(int required = 1, params string[] rows) =>
        new(new CaveDefinition
        {
            Name = "test",
            Width = rows.Max(r => r.Length),
            Height = rows.Length,
            DiamondsRequired = required,
            Map = rows,
        });

    private static void Ticks(Cave c, int n, Direction d = Direction.None, bool snap = false)
    {
        for (var i = 0; i < n; i++) c.Tick(d, snap, Dt);
    }

    [Fact]
    public void Boulder_falls_into_empty_space()
    {
        var c = Make(1,
            "WWWWW",
            "W r W",
            "W   W",
            "W   W",
            "WP  W",
            "WWWWW");
        Ticks(c, 1);
        Assert.Equal(TileType.Empty, c.Get(2, 1));
        Assert.Equal(TileType.Boulder, c.Get(2, 2));
        Assert.True(c.GetCell(2, 2).Falling);
        Ticks(c, 3);
        Assert.Equal(TileType.Boulder, c.Get(2, 4));
        Assert.False(c.GetCell(2, 4).Falling);
    }

    [Fact]
    public void Boulder_rolls_off_another_boulder()
    {
        var c = Make(1,
            "WWWWW",
            "W r W",
            "W r W",
            "W...W",
            "WP..W",
            "WWWWW");
        Ticks(c, 1);
        Assert.Equal(TileType.Boulder, c.Get(1, 1));
        Assert.Equal(TileType.Empty, c.Get(2, 1));
    }

    [Fact]
    public void Boulder_rolls_off_brick_wall_but_not_steel()
    {
        var brick = Make(1,
            "WWWWW",
            "W r W",
            "W.w.W",
            "WP..W",
            "WWWWW");
        // gauche bloquée par la terre : roule à droite ? non, droite = vide mais dessous terre -> reste
        Ticks(brick, 1);
        Assert.Equal(TileType.Boulder, brick.Get(2, 1));

        var rolls = Make(1,
            "WWWWW",
            "W r W",
            "W.w W",
            "WP. W",
            "WWWWW");
        Ticks(rolls, 1);
        Assert.Equal(TileType.Boulder, rolls.Get(3, 1));

        var steel = Make(1,
            "WWWWW",
            "W r W",
            "W W W",
            "WP  W",
            "WWWWW");
        Ticks(steel, 1);
        Assert.Equal(TileType.Boulder, steel.Get(2, 1));
    }

    [Fact]
    public void Falling_boulder_kills_player()
    {
        var c = Make(1,
            "WWWWW",
            "W r W",
            "W   W",
            "W P W",
            "W...W",
            "WWWWW");
        Ticks(c, 2);
        Assert.False(c.PlayerAlive);
        Assert.Equal(TileType.Explosion, c.Get(2, 3));
    }

    [Fact]
    public void Resting_boulder_does_not_kill_player_below()
    {
        var c = Make(1,
            "WWWWW",
            "W r W",
            "W P W",
            "W...W",
            "WWWWW");
        Ticks(c, 5);
        Assert.True(c.PlayerAlive);
        Assert.Equal(TileType.Boulder, c.Get(2, 1));
    }

    [Fact]
    public void Digging_down_under_a_boulder_is_deadly()
    {
        var c = Make(1,
            "WWWWW",
            "W r W",
            "W P W",
            "W...W",
            "W...W",
            "W...W",
            "WWWWW");
        c.Tick(Direction.Down, false, Dt); // le joueur descend, le rocher commence à tomber au tick suivant
        c.Tick(Direction.None, false, Dt);
        c.Tick(Direction.None, false, Dt);
        Assert.False(c.PlayerAlive);
    }

    [Fact]
    public void Collecting_diamonds_scores_and_opens_exit()
    {
        var c = Make(2,
            "WWWWWWW",
            "WPddX W",
            "WWWWWWW");
        Ticks(c, 1, Direction.Right);
        Assert.Equal(1, c.DiamondsCollected);
        Assert.Equal(10, c.TakeScore());
        Assert.False(c.ExitOpen);
        Ticks(c, 1, Direction.Right);
        Assert.True(c.ExitOpen);
        Assert.True(c.Events.HasFlag(CaveEvents.ExitOpened));
        Ticks(c, 1, Direction.Right);
        Assert.True(c.PlayerExited);
    }

    [Fact]
    public void Closed_exit_blocks_player()
    {
        var c = Make(5,
            "WWWWW",
            "WPX W",
            "WWWWW");
        Ticks(c, 2, Direction.Right);
        Assert.False(c.PlayerExited);
        Assert.Equal(1, c.PlayerX);
    }

    [Fact]
    public void Snap_collects_without_moving()
    {
        var c = Make(1,
            "WWWWW",
            "WPd W",
            "WWWWW");
        Ticks(c, 1, Direction.Right, snap: true);
        Assert.Equal(1, c.PlayerX);
        Assert.Equal(TileType.Empty, c.Get(2, 1));
        Assert.Equal(1, c.DiamondsCollected);
    }

    [Fact]
    public void Player_pushes_boulder_horizontally()
    {
        var c = Make(1,
            "WWWWWW",
            "WPr  W",
            "WWWWWW");
        Ticks(c, 1, Direction.Right);
        Assert.Equal(2, c.PlayerX);
        Assert.Equal(TileType.Boulder, c.Get(3, 1));
    }

    [Fact]
    public void Player_cannot_push_boulder_into_dirt()
    {
        var c = Make(1,
            "WWWWWW",
            "WPr. W",
            "WWWWWW");
        Ticks(c, 3, Direction.Right);
        Assert.Equal(1, c.PlayerX);
        Assert.Equal(TileType.Boulder, c.Get(2, 1));
    }

    [Fact]
    public void Crushed_butterfly_turns_into_diamonds()
    {
        var c = Make(1,
            "WWWWWWW",
            "W..r..W",
            "W.. ..W",
            "W..B..W",
            "W.....W",
            "W....PW",
            "WWWWWWW");
        // Le rocher tombe sur le papillon (qui reste coincé tant qu'il peut tourner autour).
        var exploded = false;
        for (var i = 0; i < 20 && !exploded; i++)
        {
            c.Tick(Direction.None, false, Dt);
            exploded = c.Events.HasFlag(CaveEvents.Explosion);
        }
        Assert.True(exploded);
        Ticks(c, 5);
        Assert.True(c.Count(TileType.Diamond) >= 6);
        Assert.Equal(0, c.Count(TileType.Butterfly));
    }

    [Fact]
    public void Firefly_explodes_when_touching_player()
    {
        var c = Make(1,
            "WWWWWW",
            "WPq..W",
            "W....W",
            "WWWWWW");
        Ticks(c, 1);
        Assert.False(c.PlayerAlive);
        Ticks(c, 5);
        Assert.Equal(0, c.Count(TileType.Diamond));
    }

    [Fact]
    public void Firefly_patrols_inside_a_box()
    {
        var c = Make(1,
            "WWWWWWWW",
            "Wwwwww.W",
            "Ww  ww.W",
            "Ww q w.W",
            "Wwwwww.W",
            "W....P.W",
            "WWWWWWWW");
        for (var i = 0; i < 40; i++)
        {
            c.Tick(Direction.None, false, Dt);
            Assert.Equal(1, c.Count(TileType.Firefly));
        }
        // ne quitte jamais la boîte
        for (var y = 0; y < c.Height; y++)
        for (var x = 0; x < c.Width; x++)
            if (c.Get(x, y) == TileType.Firefly)
                Assert.InRange(y, 2, 3);
    }

    [Fact]
    public void Magic_wall_turns_boulder_into_diamond()
    {
        var c = Make(1,
            "WWWWW",
            "W r W",
            "W   W",
            "WwmwW",
            "W   W",
            "W   W",
            "WP..W",
            "WWWWW");
        Ticks(c, 3);
        Assert.Equal(MagicWallState.Active, c.MagicWall);
        Ticks(c, 3);
        Assert.Equal(0, c.Count(TileType.Boulder));
        Assert.Equal(1, c.Count(TileType.Diamond));
    }

    [Fact]
    public void Enclosed_amoeba_turns_into_diamonds()
    {
        var c = Make(1,
            "WWWWWW",
            "WwwwwW",
            "WwaawW",
            "WwwwwW",
            "WP...W",
            "WWWWWW");
        Ticks(c, 1);
        Assert.Equal(0, c.Count(TileType.Amoeba));
        Assert.Equal(2, c.Count(TileType.Diamond));
    }

    [Fact]
    public void Oversized_amoeba_turns_into_boulders()
    {
        var c = new Cave(new CaveDefinition
        {
            Name = "amoeba",
            Width = 6,
            Height = 5,
            AmoebaMaxSize = 3,
            Map =
            [
                "WWWWWW",
                "Waaa.W",
                "W....W",
                "WP...W",
                "WWWWWW",
            ],
        });
        c.Tick(Direction.None, false, Dt);
        Assert.Equal(0, c.Count(TileType.Amoeba));
        Assert.Equal(3, c.Count(TileType.Boulder));
    }

    [Fact]
    public void Explosion_spares_steel_walls()
    {
        var c = Make(1,
            "WWWWW",
            "W...W",
            "W.P.W",
            "W...W",
            "WWWWW");
        c.KillPlayer();
        Ticks(c, 6);
        Assert.Equal(TileType.Steel, c.Get(0, 0));
        Assert.Equal(TileType.Empty, c.Get(2, 2));
    }
}
