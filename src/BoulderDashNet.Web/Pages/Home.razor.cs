using System.Buffers.Binary;
using BoulderDashNet.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BoulderDashNet.Web.Pages;

public partial class Home : IAsyncDisposable
{
    // Drapeaux clavier envoyés par game.js
    private const int FlagSnap = 1, FlagEnter = 2, FlagPause = 4, FlagGiveUp = 8;
    private const int HeaderInts = 16;

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private readonly GameSession _game = new();
    private ElementReference _canvas;
    private IJSObjectReference? _module;
    private DotNetObjectReference<Home>? _selfRef;
    private GameState _lastState = (GameState)255;
    private byte[] _buffer = [];
    private int _unlockedLevel;
    private int _highScore;

    private GameState State => _game.State;
    private CaveDefinition? CurrentLevel => _game.Cave?.Definition;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/game.js");
        var progress = await _module.InvokeAsync<int[]>("loadProgress");
        _unlockedLevel = Math.Clamp(progress[0], 0, _game.LevelList.Count - 1);
        _highScore = progress[1];
        _selfRef = DotNetObjectReference.Create(this);
        await _module.InvokeVoidAsync("start", _selfRef, _canvas, _game.ViewWidth, _game.ViewHeight);
        StateHasChanged();
    }

    /// <summary>Appelée par la boucle requestAnimationFrame à chaque image.</summary>
    [JSInvokable]
    public byte[] Frame(double dtMs, int held, int pressed, int flags)
    {
        if ((flags & FlagEnter) != 0) _game.Confirm();
        if ((flags & FlagPause) != 0) _game.TogglePause();
        if ((flags & FlagGiveUp) != 0 && State == GameState.Paused)
        {
            _game.TogglePause();
            _game.GiveUp();
        }

        _game.Update(dtMs / 1000.0, new PlayerInput((Direction)held, (Direction)pressed, (flags & FlagSnap) != 0));

        var uiChanged = State != _lastState;
        if (uiChanged) OnStateChanged();
        return BuildFrame(uiChanged);
    }

    /// <summary>Rafraîchit les panneaux HTML (appelée en asynchrone par game.js quand l'état change).</summary>
    [JSInvokable]
    public void SyncUi() => StateHasChanged();

    private void OnStateChanged()
    {
        _lastState = State;
        var save = false;
        if (State == GameState.LevelIntro && _game.LevelIndex > _unlockedLevel)
        {
            _unlockedLevel = _game.LevelIndex;
            save = true;
        }
        if (State is GameState.GameOver or GameState.Victory or GameState.LevelComplete && _game.Score > _highScore)
        {
            _highScore = _game.Score;
            save = true;
        }
        if (save && _module is not null)
            _ = _module.InvokeVoidAsync("saveProgress", _unlockedLevel, _highScore).AsTask();
    }

    private byte[] BuildFrame(bool uiChanged)
    {
        var cave = _game.Cave;
        var showCave = cave is not null && State != GameState.Title;
        int w = showCave ? cave!.Width : 0, h = showCave ? cave!.Height : 0;
        var size = HeaderInts * 4 + w * h;
        if (_buffer.Length != size) _buffer = new byte[size];

        Span<int> header =
        [
            (int)State,
            w,
            h,
            (int)(_game.CameraX * 1000),
            (int)(_game.CameraY * 1000),
            cave?.DiamondsCollected ?? 0,
            cave?.DiamondsRequired ?? 0,
            cave is null ? 0 : cave.DiamondsCollected >= cave.DiamondsRequired ? cave.Definition.ExtraDiamondValue : cave.Definition.DiamondValue,
            _game.TimeLeftSeconds,
            _game.Score,
            _game.Lives,
            _game.LevelIndex + 1,
            (int)_game.TakeEvents(),
            cave is { ExitOpen: true } ? 1 : 0,
            uiChanged ? 1 : 0,
            _highScore,
        ];
        for (var i = 0; i < HeaderInts; i++)
            BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(i * 4), header[i]);

        var o = HeaderInts * 4;
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            _buffer[o++] = cave!.GetRenderCode(x, y);
        return _buffer;
    }

    // ---------- actions des boutons ----------

    private void Play(int level) => _game.NewGame(level);
    private void Resume() => _game.TogglePause();
    private void GiveUp()
    {
        _game.TogglePause();
        _game.GiveUp();
    }
    private void Menu() => _game.BackToTitle();

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("stop");
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        _selfRef?.Dispose();
    }
}
