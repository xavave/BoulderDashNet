namespace BoulderDashNet.Core;

/// <summary>
/// Cavernes du jeu. Créations originales dans l'esprit de Boulder Dash / Emerald Mine
/// (remplissage aléatoire à graine fixe + objets dessinés, ou carte ASCII).
/// Interior : x = 1..38, y = 1..20 (la bordure d'acier est ajoutée automatiquement).
/// </summary>
public static class Levels
{
    public static readonly IReadOnlyList<CaveDefinition> All =
    [
        new()
        {
            Name = "Premiers pas",
            NameEn = "First steps",
            Hint = "Ramassez 10 diamants puis rejoignez la sortie. Attention aux rochers qui tombent !",
            HintEn = "Collect 10 diamonds, then head for the exit. Watch out for falling boulders!",
            DiamondsRequired = 10, DiamondValue = 10, ExtraDiamondValue = 15, TimeSeconds = 150,
            Map =
            [
                "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
                "WP.....r..d.....r....d.....r....d......W",
                "W......r........r..........r...........W",
                "W..d...........ddd.........ww....r.....W",
                "W.....wwwwww...............ww..d.......W",
                "W..r..........r....r...........r.......W",
                "W.....d.......d....d....wwwwwwww.......W",
                "W..r....r...........r..................W",
                "W.......d.....rrr.......d.......d...r..W",
                "W..wwwwwwww...ddd.......r...........d..W",
                "W...........................wwwwwww....W",
                "W..d...r.......d.....r...........r.....W",
                "W..........ww......d...............d...W",
                "W...r......ww..........r...d...........W",
                "W.......d......wwwwwwwww...............W",
                "W..r.................d.....r....rr.....W",
                "W......r.....d.............d....dd.....W",
                "W..d.........r.......wwww..............W",
                "W...........r....d...........r......d..W",
                "W....r.............r.........d.........W",
                "W.........d..............r...........X.W",
                "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW",
            ],
        },
        new()
        {
            Name = "Les galeries",
            NameEn = "The galleries",
            Hint = "Les rochers roulent sur les murs de briques. Creusez avec prudence.",
            HintEn = "Boulders roll off brick walls. Dig carefully.",
            DiamondsRequired = 15, DiamondValue = 10, ExtraDiamondValue = 20, TimeSeconds = 150, Seed = 2024,
            Build = b =>
            {
                b.RandomFill(boulderPct: 12, diamondPct: 6, emptyPct: 3);
                b.Line(1, 6, 32, 6, TileType.Wall);
                b.Line(7, 11, 38, 11, TileType.Wall);
                b.Line(1, 16, 32, 16, TileType.Wall);
                b.Clear(2, 2, 1).Set(2, 2, TileType.Player, Direction.Right);
                b.Set(37, 19, TileType.Exit);
            },
        },
        new()
        {
            Name = "Lucioles",
            NameEn = "Fireflies",
            Hint = "Les lucioles longent les parois. Touchez-en une et c'est l'explosion ! Écrasez-les sous un rocher.",
            HintEn = "Fireflies follow the walls. Touch one and it explodes! Crush them with a boulder.",
            DiamondsRequired = 18, DiamondValue = 10, ExtraDiamondValue = 20, TimeSeconds = 160, Seed = 77,
            Build = b =>
            {
                b.RandomFill(boulderPct: 10, diamondPct: 6, emptyPct: 2);
                foreach (var (x, y) in new[] { (7, 3), (19, 7), (29, 3), (11, 13), (25, 14) })
                {
                    b.Box(x, y, 5, 4, TileType.Wall);
                    b.Set(x + 1, y + 1, TileType.Firefly, Direction.Left);
                }
                b.Clear(2, 19, 1).Set(2, 19, TileType.Player, Direction.Right);
                b.Set(37, 2, TileType.Exit);
            },
        },
        new()
        {
            Name = "Papillons",
            NameEn = "Butterflies",
            Hint = "Un papillon écrasé se transforme en 9 diamants. Faites-leur tomber un rocher dessus.",
            HintEn = "A crushed butterfly turns into 9 diamonds. Drop a boulder on them.",
            DiamondsRequired = 22, DiamondValue = 15, ExtraDiamondValue = 25, TimeSeconds = 170, Seed = 1337,
            Build = b =>
            {
                b.RandomFill(boulderPct: 10, diamondPct: 5, emptyPct: 2);
                foreach (var (x, y) in new[] { (5, 6), (16, 12), (27, 5), (30, 15) })
                {
                    b.FillRect(x - 1, y - 3, 7, 6, TileType.Dirt);
                    b.FillRect(x, y, 5, 2, TileType.Empty);
                    b.Line(x, y - 2, x + 4, y - 2, TileType.Boulder);
                    b.Set(x + 2, y, TileType.Butterfly, Direction.Down);
                }
                b.Clear(2, 2, 1).Set(2, 2, TileType.Player, Direction.Right);
                b.Set(37, 19, TileType.Exit);
            },
        },
        new()
        {
            Name = "Le mur magique",
            NameEn = "The magic wall",
            Hint = "Un rocher qui traverse le mur magique devient diamant… tant que le mur est actif.",
            HintEn = "A boulder falling through the magic wall becomes a diamond… while the wall is active.",
            DiamondsRequired = 20, DiamondValue = 10, ExtraDiamondValue = 20, TimeSeconds = 160,
            MagicWallSeconds = 25, Seed = 4242,
            Build = b =>
            {
                b.RandomFill(boulderPct: 14, diamondPct: 6, emptyPct: 2);
                b.FillRect(4, 3, 32, 6, TileType.Dirt);
                for (var x = 5; x <= 34; x += 2) b.Set(x, 5, TileType.Boulder);
                for (var x = 6; x <= 34; x += 3) b.Set(x, 3, TileType.Boulder);
                b.Line(3, 10, 36, 10, TileType.Wall);
                b.Line(5, 10, 34, 10, TileType.MagicWall);
                b.Line(4, 11, 35, 11, TileType.Empty);
                b.Clear(2, 2, 1).Set(2, 2, TileType.Player, Direction.Right);
                b.Set(37, 19, TileType.Exit);
            },
        },
        new()
        {
            Name = "L'amibe",
            NameEn = "The amoeba",
            Hint = "L'amibe grandit sans cesse. Enfermée, elle devient diamants ; trop grosse, elle se pétrifie en rochers.",
            HintEn = "The amoeba keeps growing. Enclosed, it turns into diamonds; too big, it hardens into boulders.",
            DiamondsRequired = 16, DiamondValue = 10, ExtraDiamondValue = 30, TimeSeconds = 180,
            AmoebaSlowSeconds = 45, AmoebaMaxSize = 120, Seed = 99,
            Build = b =>
            {
                b.RandomFill(boulderPct: 9, diamondPct: 6, emptyPct: 1);
                b.FillRect(18, 9, 4, 3, TileType.Empty);
                b.Set(19, 10, TileType.Amoeba);
                b.Box(8, 14, 5, 4, TileType.Wall);
                b.Set(9, 15, TileType.Firefly, Direction.Left);
                b.Box(28, 4, 5, 4, TileType.Wall);
                b.Set(29, 5, TileType.Butterfly, Direction.Down);
                b.Clear(2, 2, 1).Set(2, 2, TileType.Player, Direction.Right);
                b.Set(37, 19, TileType.Exit);
            },
        },
        new()
        {
            Name = "Le grand final",
            NameEn = "The grand finale",
            Hint = "Tout ce que vous avez appris, en plus rapide. Bonne chance, mineur !",
            HintEn = "Everything you have learned, only faster. Good luck, miner!",
            DiamondsRequired = 30, DiamondValue = 15, ExtraDiamondValue = 30, TimeSeconds = 220,
            TickSeconds = 0.115, MagicWallSeconds = 20, Seed = 31337,
            Build = b =>
            {
                b.RandomFill(boulderPct: 13, diamondPct: 7, emptyPct: 2);
                b.Line(10, 1, 10, 15, TileType.Wall);
                b.Line(20, 6, 20, 20, TileType.Wall);
                b.Line(30, 1, 30, 14, TileType.Wall);
                b.Box(13, 2, 5, 4, TileType.Wall);
                b.Set(14, 3, TileType.Firefly, Direction.Left);
                b.Box(23, 15, 5, 4, TileType.Wall);
                b.Set(24, 16, TileType.Firefly, Direction.Left);
                b.FillRect(33, 6, 4, 2, TileType.Empty);
                b.Set(34, 6, TileType.Butterfly, Direction.Down);
                b.Line(2, 17, 8, 17, TileType.MagicWall);
                b.Line(2, 18, 8, 18, TileType.Empty);
                b.Clear(2, 2, 1).Set(2, 2, TileType.Player, Direction.Right);
                b.Set(37, 19, TileType.Exit);
            },
        },
    ];
}
