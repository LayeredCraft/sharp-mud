using Microsoft.Extensions.DependencyInjection;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;
using SharpMud.Engine.Ticking;

namespace SharpMud.Ruleset.Rpg.Tests;

public sealed class ServiceCollectionExtensionsTests
{
    // Proves builtin commands, this package's kill/attack/flee, and a
    // consumer's own registered command all end up in the same resolved
    // ICommandRegistry - the seam most likely to silently regress (one
    // registration source clobbering another) per
    // docs/adr/0008-ruleset-scaffolding-tier.md.
    [Fact]
    public void AddSharpMudRpgRuleset_ComposesBuiltinRpgAndConsumerCommands_IntoOneRegistry()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRandomSource>(Substitute.For<IRandomSource>());

        services.AddSharpMudRpgRuleset<FakeCombatOutcomeHandler>((_, registry) =>
            registry.Register(new FakeConsumerCommand()));

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ICommandRegistry>();

        registry.TryResolve("look", out _).Should().BeTrue("built-in commands must still be registered");
        registry.TryResolve("kill", out var killCommand).Should().BeTrue();
        registry.TryResolve("attack", out var attackAliasCommand).Should().BeTrue();
        killCommand.Should().BeSameAs(attackAliasCommand, "attack is kill's alias, not a separate command");
        registry.TryResolve("flee", out _).Should().BeTrue();
        registry.TryResolve("dance", out _).Should().BeTrue("a consumer's own command must not be clobbered");
    }

    [Fact]
    public void AddSharpMudRpgRuleset_RegistersCombatManagerAsBothItselfAndTickable_OffTheSameInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IRandomSource>(Substitute.For<IRandomSource>());

        services.AddSharpMudRpgRuleset<FakeCombatOutcomeHandler>();

        var provider = services.BuildServiceProvider();
        var combatManager = provider.GetRequiredService<ICombatManager>();
        var tickable = provider.GetRequiredService<ITickable>();

        tickable.Should().BeSameAs(combatManager);
    }

    private sealed class FakeCombatOutcomeHandler : ICombatOutcomeHandler
    {
        public Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct) => Task.CompletedTask;

        public Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct) => Task.FromResult(defeated);
    }

    private sealed class FakeConsumerCommand : ICommand
    {
        public string Verb => "dance";
        public IReadOnlyList<string> Aliases { get; } = [];

        public Task ExecuteAsync(CommandContext ctx, CancellationToken ct) => Task.CompletedTask;
    }
}
