using System.Threading.RateLimiting;
using BoulderDashNet.Core;

namespace BoulderDashNet.Server;

/// <summary>API du Hall of Fame : GET /api/scores et POST /api/scores.</summary>
public static class HighScoreEndpoints
{
    public const string SubmitPolicy = "scores-submit";

    public static IServiceCollection AddHighScores(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<HighScoreOptions>(config.GetSection("HighScores"));
        services.AddSingleton<HighScoreStore>();

        // Anti-spam : 6 envois par minute et par adresse IP.
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(SubmitPolicy, ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1) }));
        });
        return services;
    }

    public static IEndpointRouteBuilder MapHighScores(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scores");

        group.MapGet("/", (HighScoreStore store, int? top) =>
            Results.Ok(store.Top(Math.Clamp(top ?? 10, 1, 100))));

        group.MapPost("/", (HighScoreSubmission body, HighScoreStore store, ILogger<HighScoreStore> logger, HttpContext http) =>
        {
            var name = HighScoreRules.NormalizeName(body.Name);
            if (name is null)
                return Results.BadRequest(new { error = "invalid-name" });
            if (!HighScoreRules.IsPlausible(Levels.All, body.Score, body.Level))
            {
                logger.LogWarning("Score refusé (implausible) : {Name} {Score} niveau {Level} depuis {Ip}",
                    name, body.Score, body.Level, http.Connection.RemoteIpAddress);
                return Results.BadRequest(new { error = "invalid-score" });
            }

            var result = store.Submit(new HighScoreEntry(name, body.Score, body.Level, DateTimeOffset.Now), top: 10);
            logger.LogInformation("Score enregistré : {Name} {Score} niveau {Level} -> rang {Rank}",
                name, body.Score, body.Level, result.Rank);
            return Results.Ok(result);
        }).RequireRateLimiting(SubmitPolicy);

        return app;
    }
}
