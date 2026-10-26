using System.Text.Json;
using BoulderDashNet.Core;
using Microsoft.Extensions.Options;

namespace BoulderDashNet.Server;

/// <summary>Options du Hall of Fame (section "HighScores").</summary>
public sealed class HighScoreOptions
{
    /// <summary>Fichier JSON des scores (relatif au dossier de l'application : /app/data dans le conteneur).</summary>
    public string File { get; set; } = "data/highscores.json";

    /// <summary>Nombre de scores conservés au classement.</summary>
    public int MaxEntries { get; set; } = 100;
}

/// <summary>Hall of Fame persisté dans un fichier JSON (écriture atomique, accès sérialisé).</summary>
public sealed class HighScoreStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly Lock _lock = new();
    private readonly string _path;
    private readonly HighScoreBoard _board;
    private readonly ILogger<HighScoreStore> _logger;

    public HighScoreStore(IOptions<HighScoreOptions> options, IHostEnvironment env, ILogger<HighScoreStore> logger)
    {
        _logger = logger;
        var o = options.Value;
        _path = Path.IsPathRooted(o.File) ? o.File : Path.Combine(env.ContentRootPath, o.File);
        _board = new HighScoreBoard(Math.Max(1, o.MaxEntries), Load());
    }

    public IReadOnlyList<HighScoreEntry> Top(int count)
    {
        lock (_lock) return _board.Top(count);
    }

    public HighScoreResult Submit(HighScoreEntry entry, int top)
    {
        lock (_lock)
        {
            var rank = _board.Submit(entry);
            if (rank > 0) Save();
            return new HighScoreResult(rank, _board.Top(top));
        }
    }

    private List<HighScoreEntry> Load()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            return JsonSerializer.Deserialize<List<HighScoreEntry>>(File.ReadAllText(_path), Json) ?? [];
        }
        catch (Exception ex)
        {
            // Fichier illisible : on le met de côté plutôt que de l'écraser.
            var backup = _path + $".corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            try { File.Move(_path, backup); } catch { /* ignoré */ }
            _logger.LogError(ex, "Hall of Fame illisible, sauvegardé sous {Backup}", backup);
            return [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_board.Entries, Json));
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Impossible d'enregistrer le Hall of Fame dans {Path}", _path);
        }
    }
}
