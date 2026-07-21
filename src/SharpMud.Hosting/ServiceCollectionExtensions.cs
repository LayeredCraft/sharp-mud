using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;

namespace SharpMud.Hosting;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a consumer's ruleset commands, plus every engine-level
    /// built-in command (<c>look</c>/<c>move</c>/<c>quit</c>/inventory,
    /// etc.) - a consumer's <paramref name="registerRuleset"/> callback
    /// only ever needs to add their own ruleset's commands on top, never
    /// has to remember to call <c>BuiltinCommands.RegisterAll</c> itself.
    /// The callback also receives the built <see cref="IServiceProvider"/>
    /// so ruleset-specific command dependencies (e.g. a combat manager)
    /// can be resolved from DI.
    /// </summary>
    public static IServiceCollection AddSharpMudRuleset(this IServiceCollection services, Action<IServiceProvider, ICommandRegistry> registerRuleset)
    {
        ArgumentNullException.ThrowIfNull(registerRuleset);

        services.AddSingleton<ICommandRegistry>(sp =>
        {
            var registry = new CommandRegistry();
            BuiltinCommands.RegisterAll(registry);
            registerRuleset(sp, registry);
            return registry;
        });

        return services;
    }

    /// <summary>Registers a consumer's world content/bootstrap logic - see <see cref="IWorldBuilder"/>.</summary>
    public static IServiceCollection AddSharpMudWorld<TWorldBuilder>(this IServiceCollection services)
        where TWorldBuilder : class, IWorldBuilder
    {
        services.AddSingleton<IWorldBuilder, TWorldBuilder>();
        return services;
    }

    /// <summary>Registers a consumer's player-creation logic - see <see cref="IPlayerFactory"/>.</summary>
    public static IServiceCollection AddSharpMudPlayerFactory<TPlayerFactory>(this IServiceCollection services)
        where TPlayerFactory : class, IPlayerFactory
    {
        services.AddSingleton<IPlayerFactory, TPlayerFactory>();
        return services;
    }

    // Called once from SharpMudApplicationBuilder's constructor - see
    // docs/adr/0006-nuget-package-distribution.md's SharpMud.Hosting shape.
    // Registration order matters on both ends of the host lifecycle:
    // - Start: WorldLoaderHostedService must run before GameLoopHostedService/
    //   any transport BackgroundService, since the generic host awaits each
    //   hosted service's StartAsync in registration order before starting
    //   the next.
    // - Stop: the generic host stops hosted services in REVERSE registration
    //   order, so GameLoopHostedService is registered after
    //   ShutdownSaveHostedService here - GameLoop's tick loop fully quiesces
    //   (its BackgroundService.StopAsync cancels and awaits ExecuteAsync)
    //   before ShutdownSaveHostedService's StopAsync takes its snapshot,
    //   instead of racing a still-running tick that could still be mutating
    //   the world mid-save. A consumer's transport BackgroundService is
    //   registered later still (after this method returns), so it stops
    //   before either of these - no new connections/input once shutdown
    //   starts.
    internal static IServiceCollection AddSharpMudHostingCore(this IServiceCollection services)
    {
        services.AddSingleton<WorldContext>();
        services.AddSingleton<ICommandParser, CommandParser>();
        services.AddSingleton<IRandomSource, RandomSource>();
        services.AddSingleton(new GameLoopOptions());
        services.AddSingleton<IGameLoop, GameLoop>();

        // Engine-level tickables - not ruleset-specific, so Hosting
        // registers these itself rather than requiring every consumer to
        // remember to. Resolved lazily (inside GameLoopHostedService's
        // ExecuteAsync, after WorldLoaderHostedService has already
        // populated WorldContext).
        services.AddSingleton<ITickable>(sp => new WanderManager(sp.GetRequiredService<WorldContext>().World, sp.GetRequiredService<IRandomSource>()));
        services.AddSingleton<ITickable>(sp => new LinkdeadSweeper(sp.GetRequiredService<WorldContext>().World, sp.GetRequiredService<IThingRepository>()));

        // Singleton, not per-connection Transient/Scoped - all three hold
        // no per-connection state, only injected singleton dependencies
        // (WorldContext/parser/registry/repository/factory), so one shared
        // instance is equivalent and avoids a DI-scope dance in every
        // transport's connection-accept loop.
        services.AddSingleton<SessionLoop>();
        services.AddSingleton<LoginFlow>();
        services.AddSingleton<PlayerLogin>();

        services.AddHostedService<WorldLoaderHostedService>();
        services.AddHostedService<ShutdownSaveHostedService>();
        services.AddHostedService<GameLoopHostedService>();

        return services;
    }
}
