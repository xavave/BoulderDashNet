namespace BoulderDashNet.Core;

/// <summary>Types de tuiles d'une caverne.</summary>
public enum TileType : byte
{
    Empty,
    Dirt,
    Wall,        // mur de briques (arrondi : les rochers glissent dessus)
    Steel,       // mur d'acier indestructible
    Boulder,
    Diamond,
    Player,
    Exit,
    Firefly,     // luciole : explose en vide
    Butterfly,   // papillon : explose en diamants
    Amoeba,
    MagicWall,
    Explosion,
}

public enum Direction : byte
{
    None = 0,
    Up = 1,
    Right = 2,
    Down = 3,
    Left = 4,
}

public static class DirectionExtensions
{
    public static (int dx, int dy) Delta(this Direction d) => d switch
    {
        Direction.Up => (0, -1),
        Direction.Right => (1, 0),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        _ => (0, 0),
    };

    public static Direction TurnLeft(this Direction d) => d == Direction.Up ? Direction.Left : (Direction)((int)d - 1);
    public static Direction TurnRight(this Direction d) => d == Direction.Left ? Direction.Up : (Direction)((int)d + 1);
}

/// <summary>Une case de la grille.</summary>
public struct Cell
{
    public TileType Type;
    public Direction Dir;
    public bool Falling;
    public byte Stage;       // étape d'explosion
    public bool ToDiamond;   // l'explosion produit un diamant
    public uint ScanId;      // dernier tick où la case a été traitée

    public static Cell Of(TileType t, Direction dir = Direction.Left) => new() { Type = t, Dir = dir };
}

/// <summary>Événements produits par un tick (utilisés pour les sons et effets).</summary>
[Flags]
public enum CaveEvents
{
    None = 0,
    BoulderLand = 1,
    DiamondLand = 2,
    Collect = 4,
    Explosion = 8,
    ExitOpened = 16,
    Dig = 32,
    Push = 64,
    Magic = 128,
    Step = 256,
    ExtraLife = 512,
    Exited = 1024,
}

/// <summary>Codes de rendu envoyés au canvas (un sprite animé par code).</summary>
public static class RenderCode
{
    public const byte Empty = 0, Dirt = 1, Wall = 2, Steel = 3, Boulder = 4, Diamond = 5,
        PlayerIdle = 6, PlayerLeft = 7, PlayerRight = 8, ExitClosed = 9, ExitOpen = 10,
        Firefly = 11, Butterfly = 12, Amoeba = 13, MagicWall = 14, MagicWallActive = 15,
        Explosion = 16,          // 16..19
        DiamondBirth = 20;       // 20..23
}
