namespace BoulderDashNet.Core;

public enum GameState : byte
{
    Title,
    LevelIntro,
    Playing,
    Paused,
    Dying,
    LevelComplete,
    GameOver,
    Victory,
}

/// <summary>Entrée joueur transmise à chaque frame.</summary>
public readonly record struct PlayerInput(Direction Held, Direction Pressed, bool Snap);

/// <summary>
/// Partie en cours : enchaînement des niveaux, vies, score, chrono, caméra.
/// Indépendant de toute technologie d'affichage.
/// </summary>
public sealed class GameSession
{
    public const int StartLives = 3;
    public const int ExtraLifeEvery = 500;
    public const double IntroSeconds = 1.6;
    public const double DyingSeconds = 2.4;

    private readonly IReadOnlyList<CaveDefinition> _levels;
    private double _tickAccumulator;
    private double _stateTimer;
    private Direction _pendingDir = Direction.None;
    private int _nextExtraLife = ExtraLifeEvery;
    private GameState _stateBeforePause;

    public GameSession(IReadOnlyList<CaveDefinition>? levels = null)
    {
        _levels = levels ?? Levels.All;
    }

    public IReadOnlyList<CaveDefinition> LevelList => _levels;
    public GameState State { get; private set; } = GameState.Title;
    public Cave? Cave { get; private set; }
    public int LevelIndex { get; private set; }
    public int Lives { get; private set; } = StartLives;
    public int Score { get; private set; }
    public double TimeLeft { get; private set; }
    public int TimeLeftSeconds => (int)Math.Ceiling(Math.Max(0, TimeLeft));
    /// <summary>Événements accumulés depuis le dernier <see cref="TakeEvents"/>.</summary>
    public CaveEvents PendingEvents { get; private set; }

    public double CameraX { get; private set; }
    public double CameraY { get; private set; }
    public int ViewWidth { get; set; } = 20;
    public int ViewHeight { get; set; } = 12;

    /// <summary>Plus haut niveau atteint dans cette partie (pour débloquer la sélection).</summary>
    public int HighestLevelReached { get; private set; }

    public CaveEvents TakeEvents()
    {
        var e = PendingEvents;
        PendingEvents = CaveEvents.None;
        return e;
    }

    // ---------- transitions ----------

    public void NewGame(int levelIndex = 0)
    {
        Lives = StartLives;
        Score = 0;
        _nextExtraLife = ExtraLifeEvery;
        StartLevel(Math.Clamp(levelIndex, 0, _levels.Count - 1));
    }

    public void StartLevel(int index)
    {
        LevelIndex = index;
        HighestLevelReached = Math.Max(HighestLevelReached, index);
        Cave = new Cave(_levels[index]);
        TimeLeft = _levels[index].TimeSeconds;
        _tickAccumulator = 0;
        _pendingDir = Direction.None;
        State = GameState.LevelIntro;
        _stateTimer = IntroSeconds;
        SnapCamera();
    }

    public void TogglePause()
    {
        if (State == GameState.Paused) State = _stateBeforePause;
        else if (State is GameState.Playing or GameState.LevelIntro)
        {
            _stateBeforePause = State;
            State = GameState.Paused;
        }
    }

    /// <summary>Abandon du niveau (perd une vie), comme la touche Échap de l'original.</summary>
    public void GiveUp()
    {
        if (State == GameState.Playing && Cave is { PlayerAlive: true })
        {
            Cave.KillPlayer();
            BeginDying();
        }
    }

    public void BackToTitle() => State = GameState.Title;

    /// <summary>Saute l'intro / la fin de niveau (touche Entrée).</summary>
    public void Confirm()
    {
        switch (State)
        {
            case GameState.Title:
            case GameState.GameOver:
            case GameState.Victory:
                NewGame(0);
                break;
            case GameState.LevelIntro:
                State = GameState.Playing;
                break;
        }
    }

    // ---------- mise à jour ----------

    /// <summary>Avance la partie de <paramref name="dt"/> secondes.</summary>
    public void Update(double dt, PlayerInput input)
    {
        dt = Math.Clamp(dt, 0, 0.25);
        if (input.Pressed != Direction.None) _pendingDir = input.Pressed;

        switch (State)
        {
            case GameState.LevelIntro:
                _stateTimer -= dt;
                if (_stateTimer <= 0) State = GameState.Playing;
                break;

            case GameState.Playing:
                UpdatePlaying(dt, input);
                break;

            case GameState.Dying:
                RunTicks(dt, Direction.None, false);
                _stateTimer -= dt;
                if (_stateTimer <= 0)
                {
                    Lives--;
                    if (Lives <= 0) State = GameState.GameOver;
                    else StartLevel(LevelIndex);
                }
                break;

            case GameState.LevelComplete:
                _stateTimer -= dt;
                // Décompte du temps restant en points, comme l'original.
                while (_stateTimer <= 0 && TimeLeft >= 1)
                {
                    TimeLeft -= 1;
                    AddScore(1);
                    _stateTimer += 0.012;
                }
                if (TimeLeft < 1 && _stateTimer <= -1.2)
                {
                    if (LevelIndex + 1 >= _levels.Count) State = GameState.Victory;
                    else StartLevel(LevelIndex + 1);
                }
                break;
        }

        UpdateCamera(dt);
    }

    private void UpdatePlaying(double dt, PlayerInput input)
    {
        var cave = Cave!;
        var dir = input.Held != Direction.None ? input.Held : _pendingDir;
        RunTicks(dt, dir, input.Snap);

        if (cave.PlayerExited)
        {
            State = GameState.LevelComplete;
            TimeLeft = Math.Floor(TimeLeft);
            _stateTimer = 0.5;
            return;
        }

        TimeLeft -= dt;
        if (TimeLeft <= 0 && cave.PlayerAlive)
        {
            TimeLeft = 0;
            cave.KillPlayer();
        }

        if (!cave.PlayerAlive) BeginDying();
    }

    private void RunTicks(double dt, Direction dir, bool snap)
    {
        var cave = Cave!;
        var tickLen = cave.Definition.TickSeconds;
        _tickAccumulator += dt;
        while (_tickAccumulator >= tickLen)
        {
            _tickAccumulator -= tickLen;
            cave.Tick(dir, snap, tickLen);
            if (dir != Direction.None) _pendingDir = Direction.None;
            PendingEvents |= cave.Events;
            AddScore(cave.TakeScore());
            if (cave.PlayerExited || !cave.PlayerAlive) break;
        }
    }

    private void BeginDying()
    {
        if (State == GameState.Dying) return;
        State = GameState.Dying;
        _stateTimer = DyingSeconds;
    }

    private void AddScore(int points)
    {
        if (points <= 0) return;
        Score += points;
        while (Score >= _nextExtraLife)
        {
            _nextExtraLife += ExtraLifeEvery;
            Lives++;
            PendingEvents |= CaveEvents.ExtraLife;
        }
    }

    // ---------- caméra ----------

    private (double x, double y) CameraTarget()
    {
        var cave = Cave!;
        var tx = Math.Clamp(cave.PlayerX + 0.5 - ViewWidth / 2.0, 0, Math.Max(0, cave.Width - ViewWidth));
        var ty = Math.Clamp(cave.PlayerY + 0.5 - ViewHeight / 2.0, 0, Math.Max(0, cave.Height - ViewHeight));
        return (tx, ty);
    }

    private void SnapCamera()
    {
        (CameraX, CameraY) = CameraTarget();
    }

    private void UpdateCamera(double dt)
    {
        if (Cave is null) return;
        var (tx, ty) = CameraTarget();
        var k = 1 - Math.Exp(-dt * 6);
        CameraX += (tx - CameraX) * k;
        CameraY += (ty - CameraY) * k;
    }
}
