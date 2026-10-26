using System.Text;
using System.Text.RegularExpressions;

namespace BoulderDashNet.Core;

/// <summary>Une ligne du Hall of Fame.</summary>
public sealed record HighScoreEntry(string Name, int Score, int Level, DateTimeOffset Date);

/// <summary>Score envoyé par le client en fin de partie.</summary>
public sealed record HighScoreSubmission(string? Name, int Score, int Level);

/// <summary>Réponse du serveur après l'envoi d'un score.</summary>
public sealed record HighScoreResult(int Rank, IReadOnlyList<HighScoreEntry> Top);

/// <summary>Règles de validation des scores (partagées par le client et le serveur).</summary>
public static partial class HighScoreRules
{
    public const int NameMaxLength = 16;

    [GeneratedRegex(@"^[\p{L}\p{N} _\-\.'!]+$")]
    private static partial Regex AllowedName();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Nettoie un pseudo ; renvoie null s'il est vide ou contient des caractères interdits.</summary>
    public static string? NormalizeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var name = Spaces().Replace(raw.Normalize(NormalizationForm.FormC).Trim(), " ");
        if (name.Length > NameMaxLength) return null;
        return AllowedName().IsMatch(name) ? name : null;
    }

    /// <summary>
    /// Score maximal atteignable en arrivant au niveau <paramref name="levelReached"/> (1 = premier niveau).
    /// Borne volontairement généreuse : tous les diamants, rochers convertibles et papillons ramassés
    /// au prix fort, plus tout le temps en bonus. Elle écarte les scores manifestement truqués.
    /// </summary>
    public static int MaxPlausibleScore(IReadOnlyList<CaveDefinition> levels, int levelReached)
    {
        long max = 0;
        for (var i = 0; i < Math.Min(levelReached, levels.Count); i++)
        {
            var def = levels[i];
            var cave = new Cave(def);
            var gems = cave.Count(TileType.Diamond)
                       + cave.Count(TileType.Boulder)               // mur magique
                       + 9 * cave.Count(TileType.Butterfly)
                       + (cave.Count(TileType.Amoeba) > 0 ? def.AmoebaMaxSize : 0);
            max += (long)gems * Math.Max(def.DiamondValue, def.ExtraDiamondValue) + def.TimeSeconds;
        }
        return (int)Math.Min(max, int.MaxValue);
    }

    public static bool IsPlausible(IReadOnlyList<CaveDefinition> levels, int score, int level) =>
        level >= 1 && level <= levels.Count && score > 0 && score <= MaxPlausibleScore(levels, level);
}

/// <summary>
/// Tableau des meilleurs scores : un seul score (le meilleur) par pseudo, trié par score décroissant,
/// puis niveau atteint, puis ancienneté.
/// </summary>
public sealed class HighScoreBoard
{
    private readonly List<HighScoreEntry> _entries = [];

    public HighScoreBoard(int capacity = 100, IEnumerable<HighScoreEntry>? entries = null)
    {
        Capacity = capacity;
        if (entries is not null)
        {
            foreach (var e in entries) Insert(e);
        }
    }

    public int Capacity { get; }

    public IReadOnlyList<HighScoreEntry> Entries => _entries;

    public IReadOnlyList<HighScoreEntry> Top(int count) => _entries.Take(Math.Clamp(count, 0, Capacity)).ToList();

    /// <summary>Ajoute un score ; renvoie le rang (1 = premier) du joueur, ou 0 s'il n'entre pas au classement.</summary>
    public int Submit(HighScoreEntry entry)
    {
        Insert(entry);
        var index = _entries.FindIndex(e => SameName(e.Name, entry.Name));
        return index + 1;
    }

    private void Insert(HighScoreEntry entry)
    {
        var existing = _entries.FindIndex(e => SameName(e.Name, entry.Name));
        if (existing >= 0)
        {
            if (Compare(entry, _entries[existing]) >= 0) return; // l'ancien score reste meilleur
            _entries.RemoveAt(existing);
        }
        var pos = _entries.FindIndex(e => Compare(entry, e) < 0);
        _entries.Insert(pos < 0 ? _entries.Count : pos, entry);
        if (_entries.Count > Capacity) _entries.RemoveRange(Capacity, _entries.Count - Capacity);
    }

    /// <summary>Négatif si <paramref name="a"/> doit être classé avant <paramref name="b"/>.</summary>
    private static int Compare(HighScoreEntry a, HighScoreEntry b)
    {
        var c = b.Score.CompareTo(a.Score);
        if (c != 0) return c;
        c = b.Level.CompareTo(a.Level);
        return c != 0 ? c : a.Date.CompareTo(b.Date);
    }

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
