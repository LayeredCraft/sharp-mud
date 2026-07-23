using LayeredCraft.StructuredLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SharpMud.Adapters.Cli;
using SharpMud.Adapters.Telnet;
using SharpMud.Engine.Commands.Builtin.Admin;
using SharpMud.Engine.Commands.Builtin.Builder;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Persistence;
using SharpMud.Persistence.Sqlite;
using SharpMud.Ruleset.Rpg;
using SharpMud.Samples.Classic;

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
// Looked up by index, not a positional pattern match on args[0] - both
// --db-path and --telnet need to work regardless of which comes first or
// whether both are present at once (e.g. `--telnet 4000 --db-path
// /data/x.db`), which a leading-args-element pattern can't express.
var dbPathIndex = Array.IndexOf(args, "--db-path");
var dbPathArg = dbPathIndex >= 0 && dbPathIndex + 1 < args.Length ? args[dbPathIndex + 1] : null;
var env = new Dictionary<string, string?>
{
    ["SHARPMUD_DB_PATH"] = dbPathArg ?? Environment.GetEnvironmentVariable("SHARPMUD_DB_PATH"),
    ["SHARPMUD_INITIAL_ADMIN"] = Environment.GetEnvironmentVariable("SHARPMUD_INITIAL_ADMIN"),
};
var hostOptions = SharpMudHostOptions.Parse(env);

// LoginFlow consumes this directly (ADR-0005 bootstrap) - not otherwise
// registered by SharpMud.Hosting itself, since it's parsed here in the
// consumer's own composition root alongside every other env-var option.
builder.Services.AddSingleton(hostOptions);

builder.Services.AddSharpMudSqlitePersistence(hostOptions.DbPath);
builder.Services.AddSingleton<IBehaviorMappingContributor, ClassicBehaviorMappingContributor>();
builder.Services.AddSharpMudWorld<ClassicWorldBuilder>();
builder.Services.AddSharpMudPlayerFactory<ClassicPlayerFactory>();

// SharpMud.Ruleset.Rpg's combat scaffolding (ICombatResolver, ICombatManager
// as both itself and ITickable, the dice service, its own
// IBehaviorMappingContributor, and the kill/attack/flee commands) - see
// docs/adr/0008-ruleset-scaffolding-tier.md. The registerConsumerCommands
// callback wires AdminCommands (ADR-0005 moderation) and BuilderCommands
// (ADR-0009 world-building/OLC) - Rpg has no notion of security roles
// itself, so both are Classic's own composition-root concern.
builder.Services.AddSharpMudRpgRuleset<ClassicCombatOutcomeHandler>((sp, registry) =>
{
    var repository = sp.GetRequiredService<IThingRepository>();
    AdminCommands.RegisterAll(registry, repository);
    BuilderCommands.RegisterAll(registry, repository);
});

// Transport mode: SHARPMUD_MODE/SHARPMUD_TELNET_PORT/--telnet, same
// precedence as before (CLI arg wins over env var) - this is now the
// sample's own composition-root decision, since SharpMud.Hosting must not
// know which transport(s) exist (docs/adr/0006-nuget-package-distribution.md).
// Looked up by index (see --db-path above), not args[0] - --telnet must
// still be recognized when it's not the very first arg.
var telnetIndex = Array.IndexOf(args, "--telnet");
var useTelnet = telnetIndex >= 0
    || string.Equals(Environment.GetEnvironmentVariable("SHARPMUD_MODE"), "telnet", StringComparison.OrdinalIgnoreCase);

if (useTelnet)
{
    var port = 4000;
    if (telnetIndex >= 0 && telnetIndex + 1 < args.Length && int.TryParse(args[telnetIndex + 1], out var parsedArg))
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
