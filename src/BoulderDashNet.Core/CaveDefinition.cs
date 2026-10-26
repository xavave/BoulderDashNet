namespace BoulderDashNet.Core;

/// <summary>
/// Description statique d'une caverne : paramètres + contenu (carte ASCII ou construction procédurale).
/// </summary>
public sealed class CaveDefinition
{
    public required string Name { get; init; }
    public string Hint { get; init; } = "";
    /// <summary>Nom anglais (facultatif ; <see cref="Name"/> sinon).</summary>
    public string? NameEn { get; init; }
    /// <summary>Astuce en anglais (facultative ; <see cref="Hint"/> sinon).</summary>
    public string? HintEn { get; init; }
    public int Width { get; init; } = 40;
    public int Height { get; init; } = 22;
    public int DiamondsRequired { get; init; } = 10;
    public int DiamondValue { get; init; } = 10;
    public int ExtraDiamondValue { get; init; } = 15;
    public int TimeSeconds { get; init; } = 150;
    public int AmoebaSlowSeconds { get; init; } = 30;
    public int AmoebaMaxSize { get; init; } = 200;
    public int MagicWallSeconds { get; init; } = 20;
    /// <summary>Durée d'un tick de jeu (vitesse de la caverne).</summary>
    public double TickSeconds { get; init; } = 0.13;
    public uint Seed { get; init; } = 1;

    /// <summary>
    /// Carte ASCII. Légende : W acier, w briques, . terre, (espace) vide, r rocher, d diamant,
    /// P joueur, X sortie, q luciole, B papillon, a amibe, m mur magique.
    /// Les lignes trop courtes sont complétées par de la terre ; la bordure est toujours en acier.
    /// </summary>
    public string[]? Map { get; init; }

    /// <summary>Construction procédurale (utilisée si <see cref="Map"/> est null).</summary>
    public Action<CaveBuilder>? Build { get; init; }

    public Cell[] CreateCells()
    {
        var b = new CaveBuilder(Width, Height, Seed);
        if (Map is not null)
            b.LoadAscii(Map);
        else
            Build?.Invoke(b);
        b.Border();
        return b.Cells;
    }

    public static Cell FromChar(char c) => c switch
    {
        'W' => Cell.Of(TileType.Steel),
        'w' => Cell.Of(TileType.Wall),
        '.' => Cell.Of(TileType.Dirt),
        ' ' => Cell.Of(TileType.Empty),
        'r' => Cell.Of(TileType.Boulder),
        'd' => Cell.Of(TileType.Diamond),
        'P' => Cell.Of(TileType.Player, Direction.Right),
        'X' => Cell.Of(TileType.Exit),
        'q' => Cell.Of(TileType.Firefly, Direction.Left),
        'B' => Cell.Of(TileType.Butterfly, Direction.Down),
        'a' => Cell.Of(TileType.Amoeba),
        'm' => Cell.Of(TileType.MagicWall),
        _ => Cell.Of(TileType.Dirt),
    };
}

/// <summary>Outils de construction de caverne, dans l'esprit des cavernes Boulder Dash (remplissage aléatoire + objets).</summary>
public sealed class CaveBuilder
{
    public int Width { get; }
    public int Height { get; }
    public Cell[] Cells { get; }
    public Rng Rng { get; }

    public CaveBuilder(int width, int height, uint seed)
    {
        Width = width;
        Height = height;
        Rng = new Rng(seed);
        Cells = new Cell[width * height];
        for (var i = 0; i < Cells.Length; i++) Cells[i] = Cell.Of(TileType.Dirt);
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public TileType Get(int x, int y) => InBounds(x, y) ? Cells[y * Width + x].Type : TileType.Steel;

    public CaveBuilder Set(int x, int y, TileType t, Direction dir = Direction.Left)
    {
        if (InBounds(x, y)) Cells[y * Width + x] = Cell.Of(t, dir);
        return this;
    }

    public CaveBuilder LoadAscii(string[] rows)
    {
        for (var y = 0; y < Height && y < rows.Length; y++)
        for (var x = 0; x < Width && x < rows[y].Length; x++)
            Cells[y * Width + x] = CaveDefinition.FromChar(rows[y][x]);
        return this;
    }

    /// <summary>Remplissage aléatoire de l'intérieur (probabilités en %).</summary>
    public CaveBuilder RandomFill(int boulderPct, int diamondPct, int emptyPct, TileType background = TileType.Dirt)
    {
        for (var y = 1; y < Height - 1; y++)
        for (var x = 1; x < Width - 1; x++)
        {
            var r = Rng.Next(100);
            var t = r < boulderPct ? TileType.Boulder
                : r < boulderPct + diamondPct ? TileType.Diamond
                : r < boulderPct + diamondPct + emptyPct ? TileType.Empty
                : background;
            Set(x, y, t);
        }
        return this;
    }

    public CaveBuilder Line(int x1, int y1, int x2, int y2, TileType t, Direction dir = Direction.Left)
    {
        int dx = Math.Sign(x2 - x1), dy = Math.Sign(y2 - y1);
        int x = x1, y = y1;
        while (true)
        {
            Set(x, y, t, dir);
            if (x == x2 && y == y2) break;
            if (x != x2) x += dx;
            if (y != y2) y += dy;
        }
        return this;
    }

    public CaveBuilder FillRect(int x, int y, int w, int h, TileType t, Direction dir = Direction.Left)
    {
        for (var j = y; j < y + h; j++)
        for (var i = x; i < x + w; i++)
            Set(i, j, t, dir);
        return this;
    }

    /// <summary>Rectangle creux : contour <paramref name="border"/>, intérieur <paramref name="inner"/>.</summary>
    public CaveBuilder Box(int x, int y, int w, int h, TileType border, TileType inner = TileType.Empty)
    {
        FillRect(x, y, w, h, border);
        FillRect(x + 1, y + 1, w - 2, h - 2, inner);
        return this;
    }

    /// <summary>Dégage une zone autour d'un point (case de départ du joueur, par exemple).</summary>
    public CaveBuilder Clear(int cx, int cy, int radius, TileType t = TileType.Dirt)
        => FillRect(cx - radius, cy - radius, radius * 2 + 1, radius * 2 + 1, t);

    public CaveBuilder Border()
    {
        for (var x = 0; x < Width; x++) { Set(x, 0, TileType.Steel); Set(x, Height - 1, TileType.Steel); }
        for (var y = 0; y < Height; y++) { Set(0, y, TileType.Steel); Set(Width - 1, y, TileType.Steel); }
        return this;
    }

    public int Count(TileType t) => Cells.Count(c => c.Type == t);
}
