using System.Text;
using Microsoft.Extensions.Options;

namespace BoulderDashNet.Server;

/// <summary>Options du journal des connexions (section "AccessLog").</summary>
public sealed class AccessLogOptions
{
    /// <summary>Active ou non le journal.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Dossier des journaux (relatif au dossier de l'application : /app/logs dans le conteneur).</summary>
    public string Directory { get; set; } = "logs";

    /// <summary>Nombre de jours de conservation. 0 = conservation illimitée.</summary>
    public int RetentionDays { get; set; } = 90;
}

/// <summary>Écrit une ligne par connexion dans un fichier par jour : access-AAAA-MM-JJ.log.</summary>
public sealed class AccessLogWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private readonly Lock _lock = new();
    private readonly AccessLogOptions _options;
    private readonly ILogger<AccessLogWriter> _logger;

    public AccessLogWriter(IOptions<AccessLogOptions> options, IHostEnvironment env, ILogger<AccessLogWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
        FullDirectory = Path.IsPathRooted(_options.Directory)
            ? _options.Directory
            : Path.Combine(env.ContentRootPath, _options.Directory);
    }

    public string FullDirectory { get; }

    public void Write(string line)
    {
        if (!_options.Enabled) return;
        try
        {
            var file = Path.Combine(FullDirectory, $"access-{DateTime.Now:yyyy-MM-dd}.log");
            lock (_lock)
            {
                System.IO.Directory.CreateDirectory(FullDirectory);
                File.AppendAllText(file, line + Environment.NewLine, Utf8NoBom);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Impossible d'écrire le journal des connexions");
        }
    }
}

/// <summary>
/// Journalise les chargements de page (requêtes GET de navigation) : date, IP réelle
/// (en-tête X-Forwarded-For pris en compte), chemin, code retour, navigateur.
/// Les fichiers statiques (_framework, js, css…) ne sont pas journalisés.
/// </summary>
public sealed class AccessLogMiddleware(RequestDelegate next, AccessLogWriter writer)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        // Chemin capturé avant le traitement (la route de repli le réécrit en /index.html).
        var path = $"{request.Path}{request.QueryString}";
        var isPage = HttpMethods.IsGet(request.Method) && IsPageRequest(request);

        await next(context);
        if (!isPage) return;

        var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "-";
        var agent = request.Headers.UserAgent.ToString().Replace('|', '/');
        var referer = request.Headers.Referer.ToString();
        writer.Write(string.Join(" | ",
            DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            ip,
            $"{request.Method} {path}",
            context.Response.StatusCode,
            string.IsNullOrEmpty(referer) ? "-" : referer,
            string.IsNullOrEmpty(agent) ? "-" : agent));
    }

    private static bool IsPageRequest(HttpRequest request)
    {
        var path = request.Path.Value ?? "/";
        if (path.StartsWith("/_framework", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path == "/" || path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) return true;
        // Navigation vers une route Blazor (pas d'extension, le navigateur demande du HTML)
        return !Path.HasExtension(path) && request.Headers.Accept.ToString().Contains("text/html");
    }
}

/// <summary>Supprime les journaux plus anciens que RetentionDays (au démarrage puis toutes les 6 heures).</summary>
public sealed class AccessLogCleanup(AccessLogWriter writer, IOptions<AccessLogOptions> options, ILogger<AccessLogCleanup> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            Cleanup();
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private void Cleanup()
    {
        var days = options.Value.RetentionDays;
        if (days <= 0 || !Directory.Exists(writer.FullDirectory)) return;
        var limit = DateTime.Now.Date.AddDays(-days);
        foreach (var file in Directory.EnumerateFiles(writer.FullDirectory, "access-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < limit)
                {
                    File.Delete(file);
                    logger.LogInformation("Journal supprimé (rétention {Days} j) : {File}", days, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Impossible de supprimer {File}", file);
            }
        }
    }
}
