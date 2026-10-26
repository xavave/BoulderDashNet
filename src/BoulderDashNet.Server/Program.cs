using BoulderDashNet.Server;
using Microsoft.AspNetCore.HttpOverrides;

// Hôte ASP.NET Core : sert l'application Blazor WebAssembly (le jeu tourne dans le navigateur)
// et tient un journal des connexions (un fichier par jour).
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AccessLogOptions>(builder.Configuration.GetSection("AccessLog"));
builder.Services.AddSingleton<AccessLogWriter>();
builder.Services.AddHostedService<AccessLogCleanup>();

// Derrière un reverse proxy (Nginx Proxy Manager, Traefik…) : récupérer l'IP réelle du client.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
    app.UseWebAssemblyDebugging();

app.UseMiddleware<AccessLogMiddleware>();

app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTimeOffset.Now }));

app.MapFallbackToFile("index.html");

app.Run();
