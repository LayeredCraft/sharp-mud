using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SharpMud.Adapters.Cli;
using SharpMud.Adapters.Telnet;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;
using SharpMud.Hosting;
using SharpMud.Persistence;
using SharpMud.Persistence.Sqlite;
using SharpMud.Ruleset.Classic;

var builder = SharpMudApplication.CreateBuilder(args);

// Non-secret configuration (Serilog levels/sinks) lives in appsettings.json,
// with environment variables able to override it - see ADR-0003
// (docs/adr/0003-allow-appsettingsjson-for-non-secret-config.md). Secrets
// still never go in appsettings.json; only env vars (SharpMudHostOptions.Parse
// below) carry those. The generic host's default builder already loads
// appsettings.json (+ env vars, + command line) - no explicit AddJsonFile
// call needed here.

// Console sink only - this repo runs in Docker (docs/deployment.md), stdout
// is the sink Docker already captures. See ADR-0002
// (docs/adr/0002-telnet-protocol-negotiation.md) for why Serilog was
// introduced. The EF Core "log every SQL command at Information" override
// lives in appsettings.json's Serilog:MinimumLevel:Override section.
var serilogLogger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(serilogLogger, dispose: true);

// --db-path wins over SHARPMUD_DB_PATH, same CLI-arg-over-env-var
// precedence as --telnet/SHARPMUD_MODE below - handled here rather than in
// SharpMudHostOptions.Parse itself, matching how transport CLI args are
// also the sample's own composition-root decision, not Hosting's (docs/adr
// /0006-nuget-package-distribution.md). Not a secret (see security.md) -
// just a filesystem path - so a CLI arg carries no exposure risk env-only
// deployment config (credentials, connection strings) would have.
var dbPathArg = args is ["--db-path", var dbPathValue, ..] ? dbPathValue : null;
var env = new Dictionary<string, string?>
{
    ["SHARPMUD_DB_PATH"] = dbPathArg ?? Environment.GetEnvironmentVariable("SHARPMUD_DB_PATH"),
};
var hostOptions = SharpMudHostOptions.Parse(env);

builder.Services.AddSharpMudSqlitePersistence(hostOptions.DbPath);
builder.Services.AddSingleton<IBehaviorMappingContributor, ClassicBehaviorMappingContributor>();
builder.Services.AddSharpMudWorld<ClassicWorldBuilder>();
builder.Services.AddSharpMudPlayerFactory<ClassicPlayerFactory>();

builder.Services.AddSingleton<ICombatResolver, CombatResolver>();
// Registered once as ICombatManager and once as ITickable, same underlying
// instance - CombatManager both drives the kill/flee commands and advances
// active encounters each tick.
builder.Services.AddSingleton<ICombatManager>(sp => new CombatManager(sp.GetRequiredService<ICombatResolver>(), sp.GetRequiredService<WorldContext>().StartingRoom));
builder.Services.AddSingleton(sp => (ITickable)sp.GetRequiredService<ICombatManager>());
builder.Services.AddSharpMudRuleset((sp, registry) =>
    ClassicCommands.RegisterAll(registry, sp.GetRequiredService<ICombatManager>(), sp.GetRequiredService<IRandomSource>()));

// Transport mode: SHARPMUD_MODE/SHARPMUD_TELNET_PORT/--telnet, same
// precedence as before (CLI arg wins over env var) - this is now the
// sample's own composition-root decision, since SharpMud.Hosting must not
// know which transport(s) exist (docs/adr/0006-nuget-package-distribution.md).
var useTelnet = args is ["--telnet", ..]
    || string.Equals(Environment.GetEnvironmentVariable("SHARPMUD_MODE"), "telnet", StringComparison.OrdinalIgnoreCase);

if (useTelnet)
{
    var port = 4000;
    if (args is ["--telnet", var portArg, ..] && int.TryParse(portArg, out var parsedArg))
        port = parsedArg;
    else if (int.TryParse(Environment.GetEnvironmentVariable("SHARPMUD_TELNET_PORT"), out var parsedEnv))
        port = parsedEnv;

    builder.Services.AddSharpMudTelnetTransport(port);
}
else
{
    builder.Services.AddSharpMudCliTransport();
}

var mud = builder.Build();
await mud.RunAsync();
