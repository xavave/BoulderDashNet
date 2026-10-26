namespace BoulderDashNet.Core;

public enum MagicWallState { Dormant, Active, Expired }

/// <summary>
/// État dynamique d'une caverne et règles du jeu (physique façon Boulder Dash / Emerald Mine).
/// La grille est balayée de haut en bas et de gauche à droite à chaque tick ;
/// chaque objet n'est traité qu'une fois par tick (via <see cref="Cell.ScanId"/>).
/// </summary>
public sealed class Cave
{
    private readonly Cell[] _cells;
    private readonly Rng _rng;
    private uint _tick;
    private double _amoebaSlowLeft;
    private int _amoebaCount;
    private bool _amoebaCanGrow;

    public CaveDefinition Definition { get; }
    public int Width { get; }
    public int Height { get; }

    public int DiamondsCollected { get; private set; }
    public int DiamondsRequired => Definition.DiamondsRequired;
    public bool ExitOpen { get; private set; }

    public bool PlayerAlive { get; private set; } = true;
    public bool PlayerExited { get; private set; }
    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }
    public Direction PlayerFacing { get; private set; } = Direction.Right;
    public bool PlayerMoving { get; private set; }

    public MagicWallState MagicWall { get; private set; } = MagicWallState.Dormant;
    public double MagicWallTimeLeft { get; private set; }

    public CaveEvents Events { get; private set; }
    /// <summary>Points gagnés depuis le dernier appel à <see cref="TakeScore"/>.</summary>
    private int _pendingScore;

    public uint TickCount => _tick;

    public Cave(CaveDefinition def)
    {
        Definition = def;
        Width = def.Width;
        Height = def.Height;
        _cells = def.CreateCells();
        _rng = new Rng(def.Seed * 7919 + 13);
        _amoebaSlowLeft = def.AmoebaSlowSeconds;
        LocatePlayer();
    }

    private void LocatePlayer()
    {
        for (var i = 0; i < _cells.Length; i++)
            if (_cells[i].Type == TileType.Player)
            {
                PlayerX = i % Width;
                PlayerY = i / Width;
                return;
            }
        PlayerAlive = false;
    }

    // ---------- accès grille ----------

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public Cell GetCell(int x, int y) => InBounds(x, y) ? _cells[y * Width + x] : Cell.Of(TileType.Steel);

    public TileType Get(int x, int y) => InBounds(x, y) ? _cells[y * Width + x].Type : TileType.Steel;

    /// <summary>Remplace une case (utile pour les tests et l'éditeur).</summary>
    public void Set(int x, int y, TileType t, Direction dir = Direction.Left, bool falling = false)
    {
        if (!InBounds(x, y)) return;
        _cells[y * Width + x] = new Cell { Type = t, Dir = dir, Falling = falling };
        if (t == TileType.Player) { PlayerX = x; PlayerY = y; PlayerAlive = true; }
    }

    public int Count(TileType t)
    {
        var n = 0;
        foreach (var c in _cells) if (c.Type == t) n++;
        return n;
    }

    public int TakeScore()
    {
        var s = _pendingScore;
        _pendingScore = 0;
        return s;
    }

    private ref Cell At(int x, int y) => ref _cells[y * Width + x];

    private static bool IsRounded(TileType t) => t is TileType.Boulder or TileType.Diamond or TileType.Wall;

    // ---------- boucle principale ----------

    /// <summary>Fait avancer la caverne d'un tick.</summary>
    /// <param name="dir">Direction demandée par le joueur.</param>
    /// <param name="snap">Ramasser/creuser sans bouger (Ctrl).</param>
    /// <param name="dt">Durée du tick en secondes (pour l'amibe et le mur magique).</param>
    public void Tick(Direction dir, bool snap, double dt)
    {
        _tick++;
        Events = CaveEvents.None;
        _amoebaCount = 0;
        _amoebaCanGrow = false;
        PlayerMoving = false;

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            ref var c = ref At(x, y);
            if (c.ScanId == _tick) continue;
            switch (c.Type)
            {
                case TileType.Player: UpdatePlayer(x, y, dir, snap); break;
                case TileType.Boulder:
                case TileType.Diamond: UpdateFallingObject(x, y); break;
                case TileType.Firefly:
                case TileType.Butterfly: UpdateEnemy(x, y); break;
                case TileType.Amoeba: UpdateAmoeba(x, y); break;
                case TileType.Explosion: UpdateExplosion(x, y); break;
            }
        }

        // Amibe : trop grande -> rochers, enfermée -> diamants.
        if (_amoebaCount > 0)
        {
            if (_amoebaCount >= Definition.AmoebaMaxSize) ReplaceAll(TileType.Amoeba, TileType.Boulder);
            else if (!_amoebaCanGrow) ReplaceAll(TileType.Amoeba, TileType.Diamond);
            _amoebaSlowLeft -= dt;
        }

        if (MagicWall == MagicWallState.Active)
        {
            MagicWallTimeLeft -= dt;
            if (MagicWallTimeLeft <= 0) MagicWall = MagicWallState.Expired;
        }

        if (!ExitOpen && DiamondsCollected >= DiamondsRequired)
        {
            ExitOpen = true;
            Events |= CaveEvents.ExitOpened;
        }
    }

    private void ReplaceAll(TileType from, TileType to)
    {
        for (var i = 0; i < _cells.Length; i++)
            if (_cells[i].Type == from)
                _cells[i] = new Cell { Type = to, ScanId = _tick };
    }

    private void Move(int x, int y, int nx, int ny)
    {
        ref var src = ref At(x, y);
        ref var dst = ref At(nx, ny);
        dst = src;
        dst.ScanId = _tick;
        src = new Cell { Type = TileType.Empty, ScanId = _tick };
    }

    // ---------- joueur ----------

    private void UpdatePlayer(int x, int y, Direction dir, bool snap)
    {
        At(x, y).ScanId = _tick;
        if (dir == Direction.None) return;

        var (dx, dy) = dir.Delta();
        if (dx != 0) PlayerFacing = dir;
        int tx = x + dx, ty = y + dy;
        if (!InBounds(tx, ty)) return;
        ref var target = ref At(tx, ty);

        switch (target.Type)
        {
            case TileType.Empty:
            case TileType.Dirt:
                Events |= target.Type == TileType.Dirt ? CaveEvents.Dig : CaveEvents.Step;
                if (snap) target = new Cell { Type = TileType.Empty, ScanId = _tick };
                else MovePlayer(x, y, tx, ty);
                break;

            case TileType.Diamond:
                CollectDiamond();
                if (snap) target = new Cell { Type = TileType.Empty, ScanId = _tick };
                else MovePlayer(x, y, tx, ty);
                break;

            case TileType.Exit when ExitOpen && !snap:
                At(x, y) = new Cell { Type = TileType.Empty, ScanId = _tick };
                PlayerX = tx; PlayerY = ty;
                PlayerExited = true;
                Events |= CaveEvents.Exited;
                break;

            case TileType.Boulder when dy == 0 && !snap && !target.Falling:
                int bx = tx + dx;
                if (Get(bx, ty) == TileType.Empty)
                {
                    Move(tx, ty, bx, ty);
                    MovePlayer(x, y, tx, ty);
                    Events |= CaveEvents.Push;
                }
                break;
        }
    }

    private void MovePlayer(int x, int y, int nx, int ny)
    {
        Move(x, y, nx, ny);
        PlayerX = nx;
        PlayerY = ny;
        PlayerMoving = true;
    }

    private void CollectDiamond()
    {
        _pendingScore += DiamondsCollected >= DiamondsRequired ? Definition.ExtraDiamondValue : Definition.DiamondValue;
        DiamondsCollected++;
        Events |= CaveEvents.Collect;
    }

    // ---------- rochers et diamants ----------

    private void UpdateFallingObject(int x, int y)
    {
        ref var c = ref At(x, y);
        c.ScanId = _tick;
        var below = Get(x, y + 1);

        if (below == TileType.Empty)
        {
            c.Falling = true;
            Move(x, y, x, y + 1);
            return;
        }

        if (c.Falling)
        {
            switch (below)
            {
                case TileType.Player:
                    Explode(x, y + 1, toDiamond: false);
                    return;
                case TileType.Firefly:
                    Explode(x, y + 1, toDiamond: false);
                    return;
                case TileType.Butterfly:
                    Explode(x, y + 1, toDiamond: true);
                    return;
                case TileType.MagicWall when MagicWall != MagicWallState.Expired:
                    PassThroughMagicWall(x, y);
                    return;
            }
        }

        if (IsRounded(below) && !GetCell(x, y + 1).Falling && TryRoll(x, y))
            return;

        if (c.Falling)
        {
            c.Falling = false;
            Events |= c.Type == TileType.Diamond ? CaveEvents.DiamondLand : CaveEvents.BoulderLand;
        }
    }

    private bool TryRoll(int x, int y)
    {
        foreach (var dx in (ReadOnlySpan<int>)[-1, 1])
        {
            if (Get(x + dx, y) == TileType.Empty && Get(x + dx, y + 1) == TileType.Empty)
            {
                At(x, y).Falling = true;
                Move(x, y, x + dx, y);
                return true;
            }
        }
        return false;
    }

    private void PassThroughMagicWall(int x, int y)
    {
        if (MagicWall == MagicWallState.Dormant)
        {
            MagicWall = MagicWallState.Active;
            MagicWallTimeLeft = Definition.MagicWallSeconds;
        }
        var converted = At(x, y).Type == TileType.Boulder ? TileType.Diamond : TileType.Boulder;
        At(x, y) = new Cell { Type = TileType.Empty, ScanId = _tick };
        if (Get(x, y + 2) == TileType.Empty)
            At(x, y + 2) = new Cell { Type = converted, Falling = true, ScanId = _tick };
        Events |= CaveEvents.Magic;
    }

    // ---------- ennemis ----------

    private void UpdateEnemy(int x, int y)
    {
        ref var c = ref At(x, y);
        c.ScanId = _tick;
        var isButterfly = c.Type == TileType.Butterfly;

        // Contact avec le joueur ou l'amibe : explosion.
        for (var d = Direction.Up; d <= Direction.Left; d++)
        {
            var (ddx, ddy) = d.Delta();
            var n = Get(x + ddx, y + ddy);
            if (n is TileType.Player or TileType.Amoeba)
            {
                Explode(x, y, toDiamond: isButterfly);
                return;
            }
        }

        // Luciole : longe le mur par la gauche ; papillon : par la droite.
        var dir = c.Dir;
        var turn = isButterfly ? dir.TurnRight() : dir.TurnLeft();
        var (tdx, tdy) = turn.Delta();
        if (Get(x + tdx, y + tdy) == TileType.Empty)
        {
            c.Dir = turn;
            Move(x, y, x + tdx, y + tdy);
            return;
        }
        var (fdx, fdy) = dir.Delta();
        if (Get(x + fdx, y + fdy) == TileType.Empty)
        {
            Move(x, y, x + fdx, y + fdy);
            return;
        }
        c.Dir = isButterfly ? dir.TurnLeft() : dir.TurnRight();
    }

    // ---------- amibe ----------

    private void UpdateAmoeba(int x, int y)
    {
        At(x, y).ScanId = _tick;
        _amoebaCount++;

        var canGrowHere = false;
        for (var d = Direction.Up; d <= Direction.Left; d++)
        {
            var (dx, dy) = d.Delta();
            if (Get(x + dx, y + dy) is TileType.Empty or TileType.Dirt) { canGrowHere = true; break; }
        }
        if (!canGrowHere) return;
        _amoebaCanGrow = true;

        var chance = _amoebaSlowLeft > 0 ? 0.03 : 0.25;
        if (_rng.NextDouble() >= chance) return;

        var (gx, gy) = ((Direction)(_rng.Next(4) + 1)).Delta();
        if (Get(x + gx, y + gy) is TileType.Empty or TileType.Dirt)
            At(x + gx, y + gy) = new Cell { Type = TileType.Amoeba, ScanId = _tick };
    }

    // ---------- explosions ----------

    /// <summary>Déclenche une explosion 3x3 centrée sur (cx, cy).</summary>
    public void Explode(int cx, int cy, bool toDiamond)
    {
        Events |= CaveEvents.Explosion;
        for (var y = cy - 1; y <= cy + 1; y++)
        for (var x = cx - 1; x <= cx + 1; x++)
        {
            if (!InBounds(x, y)) continue;
            ref var t = ref At(x, y);
            if (t.Type is TileType.Steel or TileType.Exit) continue;
            if (t.Type == TileType.Player) PlayerAlive = false;
            t = new Cell { Type = TileType.Explosion, ToDiamond = toDiamond, ScanId = _tick };
        }
    }

    /// <summary>Fait exploser le joueur (temps écoulé ou abandon).</summary>
    public void KillPlayer()
    {
        if (PlayerAlive && !PlayerExited && Get(PlayerX, PlayerY) == TileType.Player)
            Explode(PlayerX, PlayerY, toDiamond: false);
        PlayerAlive = false;
    }

    private void UpdateExplosion(int x, int y)
    {
        ref var c = ref At(x, y);
        c.ScanId = _tick;
        c.Stage++;
        if (c.Stage >= 4)
            c = new Cell { Type = c.ToDiamond ? TileType.Diamond : TileType.Empty, ScanId = _tick };
    }

    // ---------- rendu ----------

    public byte GetRenderCode(int x, int y)
    {
        var c = GetCell(x, y);
        return c.Type switch
        {
            TileType.Empty => RenderCode.Empty,
            TileType.Dirt => RenderCode.Dirt,
            TileType.Wall => RenderCode.Wall,
            TileType.Steel => RenderCode.Steel,
            TileType.Boulder => RenderCode.Boulder,
            TileType.Diamond => RenderCode.Diamond,
            TileType.Player => !PlayerMoving ? RenderCode.PlayerIdle
                : PlayerFacing == Direction.Left ? RenderCode.PlayerLeft : RenderCode.PlayerRight,
            TileType.Exit => ExitOpen ? RenderCode.ExitOpen : RenderCode.ExitClosed,
            TileType.Firefly => RenderCode.Firefly,
            TileType.Butterfly => RenderCode.Butterfly,
            TileType.Amoeba => RenderCode.Amoeba,
            TileType.MagicWall => MagicWall == MagicWallState.Active ? RenderCode.MagicWallActive : RenderCode.MagicWall,
            TileType.Explosion => (byte)((c.ToDiamond ? RenderCode.DiamondBirth : RenderCode.Explosion) + Math.Min(c.Stage, (byte)3)),
            _ => RenderCode.Empty,
        };
    }
}
