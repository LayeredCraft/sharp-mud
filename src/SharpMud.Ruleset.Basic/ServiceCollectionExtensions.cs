using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Commands;
using SharpMud.Hosting;
using SharpMud.Persistence;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic;

/// <summary>DI registration entry point for this package.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything a consumer needs for a runnable, playable basic
    /// game on top of <c>SharpMud.Ruleset.Rpg</c>'s combat scaffolding -
    /// this package's stats behavior mapping, default world, player
    /// factory, and combat-outcome handler. Combined with <c>Engine</c>/
    /// <c>Hosting</c>/a persistence provider/a transport adapter, this is
    /// the actual "<c>dotnet add package</c>, a few lines in
    /// <c>Program.cs</c>, run a basic game" quick-start
    /// (docs/adr/0008-ruleset-scaffolding-tier.md).
    /// </summary>
    /// <param name="services">The consumer's <see cref="IServiceCollection"/>.</param>
    /// <param name="configureOptions">Tunes the starting numbers for a fresh player character - see <see cref="BasicRulesetOptions"/>. Validated immediately after this callback runs; an invalid combination throws <see cref="ArgumentOutOfRangeException"/> at startup rather than at first combat.</param>
    /// <param name="registerConsumerCommands">Forwarded to <c>AddSharpMudRpgRuleset(...)</c> - a consumer's own commands, registered alongside <c>kill</c>/<c>attack</c>/<c>flee</c>.</param>
    public static IServiceCollection AddSharpMudBasicRuleset(
        this IServiceCollection services,
        Action<BasicRulesetOptions>? configureOptions = null,
        Action<IServiceProvider, ICommandRegistry>? registerConsumerCommands = null)
    {
        var options = new BasicRulesetOptions();
        configureOptions?.Invoke(options);
        options.Validate();
        services.AddSingleton(options);

        services.AddSingleton<IBehaviorMappingContributor, BasicBehaviorMappingContributor>();
        services.AddSharpMudWorld<BasicWorldBuilder>();
        services.AddSharpMudPlayerFactory<BasicPlayerFactory>();

        services.AddSharpMudRpgRuleset<BasicCombatOutcomeHandler>(registerConsumerCommands);

        return services;
    }
}
