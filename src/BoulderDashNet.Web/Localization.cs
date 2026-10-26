using BoulderDashNet.Core;

namespace BoulderDashNet.Web;

public enum Lang { Fr, En }

/// <summary>Textes de l'interface en français et en anglais.</summary>
public static class Texts
{
    private static readonly Dictionary<string, (string Fr, string En)> Table = new()
    {
        ["tagline"] = ("Hommage aux classiques Amiga Boulder Dash & Emerald Mine", "A tribute to the Amiga classics Boulder Dash & Emerald Mine"),
        ["play"] = ("Jouer", "Play"),
        ["key.enter"] = ("Entrée", "Enter"),
        ["key.esc"] = ("Échap", "Esc"),
        ["key.space"] = ("Espace", "Space"),
        ["key.wasd"] = ("ZQSD", "WASD"),
        ["levels"] = ("Niveaux", "Levels"),
        ["highscore"] = ("Meilleur score", "High score"),
        ["level"] = ("NIVEAU", "LEVEL"),
        ["diamonds"] = ("diamants", "diamonds"),
        ["pause"] = ("PAUSE", "PAUSED"),
        ["resume"] = ("Reprendre", "Resume"),
        ["giveup"] = ("Abandonner le niveau", "Give up this level"),
        ["menu"] = ("Menu principal", "Main menu"),
        ["levelDone"] = ("Niveau terminé !", "Level complete!"),
        ["timeBonus"] = ("Bonus temps", "Time bonus"),
        ["score"] = ("Score", "Score"),
        ["gameOver"] = ("GAME OVER", "GAME OVER"),
        ["retry"] = ("Réessayer ce niveau", "Retry this level"),
        ["victory"] = ("Bravo, mineur !", "Well done, miner!"),
        ["victoryText"] = ("Toutes les cavernes sont vidées.", "Every cave has been cleared."),
        ["finalScore"] = ("Score final", "Final score"),
        ["help.move"] = ("déplacer", "move"),
        ["help.snap"] = ("+ direction : ramasser sans bouger", "+ direction: collect without moving"),
        ["help.pause"] = ("pause", "pause"),
        ["help.sound"] = ("son", "sound"),
        ["language"] = ("Langue", "Language"),
        ["colon"] = (" :", ":"),
    };

    public static string Get(Lang lang, string key) =>
        Table.TryGetValue(key, out var t) ? (lang == Lang.En ? t.En : t.Fr) : key;

    public static string LevelName(Lang lang, CaveDefinition def) =>
        lang == Lang.En && !string.IsNullOrEmpty(def.NameEn) ? def.NameEn : def.Name;

    public static string LevelHint(Lang lang, CaveDefinition def) =>
        lang == Lang.En && !string.IsNullOrEmpty(def.HintEn) ? def.HintEn : def.Hint;

    public static string Code(Lang lang) => lang == Lang.En ? "en" : "fr";

    public static Lang Parse(string? code) =>
        code is not null && code.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? Lang.En : Lang.Fr;
}
