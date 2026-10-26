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
        ["help.music"] = ("musique", "music"),
        ["music.title"] = ("Musique de l'écran d'accueil", "Title screen music"),
        ["music.on"] = ("Musique", "Music"),
        ["music.off"] = ("Musique", "Music"),
        ["language"] = ("Langue", "Language"),
        ["colon"] = (" :", ":"),
        ["hof.title"] = ("Hall of Fame", "Hall of Fame"),
        ["hof.loading"] = ("Chargement…", "Loading…"),
        ["hof.unavailable"] = ("Classement indisponible pour le moment.", "Leaderboard unavailable right now."),
        ["hof.empty"] = ("Aucun score pour l'instant : soyez le premier !", "No scores yet: be the first!"),
        ["hof.player"] = ("Joueur", "Player"),
        ["hof.level"] = ("Niv.", "Lvl"),
        ["hof.close"] = ("Fermer", "Close"),
        ["hof.pseudo"] = ("Inscrivez votre pseudo au Hall of Fame", "Enter your name in the Hall of Fame"),
        ["hof.placeholder"] = ("Votre pseudo", "Your name"),
        ["hof.save"] = ("Enregistrer", "Save"),
        ["hof.ranked"] = ("Vous êtes n° {0} du classement !", "You are #{0} on the leaderboard!"),
        ["hof.notRanked"] = ("Score enregistré, mais pas encore assez pour le classement.", "Score saved, but not high enough for the leaderboard yet."),
        ["hof.badName"] = ("Pseudo invalide : 1 à 16 lettres, chiffres, espaces ou - _ . ' !", "Invalid name: 1 to 16 letters, digits, spaces or - _ . ' !"),
        ["hof.rejected"] = ("Score refusé par le serveur.", "Score rejected by the server."),
        ["hof.tooMany"] = ("Trop d'envois, réessayez dans une minute.", "Too many submissions, try again in a minute."),
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
