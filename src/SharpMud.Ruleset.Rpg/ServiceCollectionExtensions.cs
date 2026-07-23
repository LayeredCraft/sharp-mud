using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;
using SharpMud.Hosting;
using SharpMud.Persistence;

namespace SharpMud.Ruleset.Rpg;

/// <summary>DI registration entry point for this package.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers this package's combat scaffolding - <see
    /// cref="ICombatResolver"/>, <see cref="ICombatManager"/> (as both
    /// itself and <see cref="ITickable"/>, off the same instance), the dice
    /// service, this package's <see cref="IBehaviorMappingContributor"/>,
    /// and the <c>kill</c>/<c>attack</c>/<c>flee</c> commands - reproducing
    /// what every consumer previously had to hand-wire in their own
    /// <c>Program.cs</c>. <typeparamref name="TCombatOutcomeHandler"/> is
    /// the consumer's own <see cref="ICombatOutcomeHandler"/> implementation
    /// (XP awards, death penalty, respawn destination).
    /// </summary>
    /// <param name="services">The consumer's <see cref="IServiceCollection"/>.</param>
    /// <param name="registerConsumerCommands">
    /// Optional callback for the consumer's own commands, invoked after this
    /// package's own commands are registered. This package calls the
    /// underlying <c>Hosting.AddSharpMudRuleset(...)</c> exactly once
    /// internally - a consumer must not call it again themselves, since
    /// DI's last-registration-wins resolution for <see cref="ICommandRegistry"/>
    /// would silently drop whichever call came first (see
    /// docs/adr/0008-ruleset-scaffolding-tier.md's Decision Outcome).
    /// </param>
    public static IServiceCollection AddSharpMudRpgRuleset<TCombatOutcomeHandler>(
        this IServiceCollection services,
        Action<IServiceProvider, ICommandRegistry>? registerConsumerCommands = null)
        where TCombatOutcomeHandler : class, ICombatOutcomeHandler
    {
        services.AddSingleton<IBehaviorMappingContributor, RpgBehaviorMappingContributor>();
        services.AddSingleton<IDiceRoller, DiceRoller>();
        services.AddSingleton<ICombatOutcomeHandler, TCombatOutcomeHandler>();
        services.AddSingleton<ICombatResolver, CombatResolver>();

        // Registered once as ICombatManager and once as ITickable, same
        // underlying instance - CombatManager both drives the kill/flee
        // commands and advances active encounters each tick.
        services.AddSingleton<ICombatManager>(sp =>
            new CombatManager(sp.GetRequiredService<ICombatResolver>(), sp.GetRequiredService<ICombatOutcomeHandler>()));
        services.AddSingleton(sp => (ITickable)sp.GetRequiredService<ICombatManager>());

        services.AddSharpMudRuleset((sp, registry) =>
        {
            registry.Register(new AttackCommand(sp.GetRequiredService<ICombatManager>()));
            registry.Register(new FleeCommand(
                sp.GetRequiredService<ICombatManager>(),
                sp.GetRequiredService<IDiceRoller>(),
                sp.GetRequiredService<IRandomSource>()));

            registerConsumerCommands?.Invoke(sp, registry);
        });

        return services;
    }
}
