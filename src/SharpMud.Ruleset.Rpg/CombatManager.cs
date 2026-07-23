using System.Diagnostics.CodeAnalysis;
using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;

namespace SharpMud.Ruleset.Rpg;

// Registered once with IGameLoop and resolves every active encounter each
// tick - simpler Host wiring than one ITickable per encounter, and all
// world-state mutation happens on the single game-loop "thread" (sequential
// awaits in GameLoop.RunAsync), so there's no concurrent-mutation risk.
//
// Combat-outcome side effects (XP awards, death penalties, respawn
// destination) are delegated to ICombatOutcomeHandler rather than touching a
// concrete ruleset's stats behavior or a hard-coded room directly - this
// package has zero reference to any concrete ruleset's types (see
// docs/adr/0008-ruleset-scaffolding-tier.md's Decision Outcome).
public sealed class CombatManager : ICombatManager, ITickable
{
    private readonly ICombatResolver _resolver;
    private readonly ICombatOutcomeHandler _outcomeHandler;
    private readonly Dictionary<ThingId, CombatEncounter> _encounters = [];

    public CombatManager(ICombatResolver resolver, ICombatOutcomeHandler outcomeHandler)
    {
        _resolver = resolver;
        _outcomeHandler = outcomeHandler;
    }

    public bool IsInCombat(ThingId thingId) => _encounters.ContainsKey(thingId);

    public void StartEncounter(Thing attacker, Thing defender) =>
        _encounters[attacker.Id] = new CombatEncounter { Attacker = attacker, Defender = defender };

    public void EndEncounter(ThingId thingId) => _encounters.Remove(thingId);

    public bool TryGetEncounter(ThingId thingId, [MaybeNullWhen(false)] out CombatEncounter encounter) =>
        _encounters.TryGetValue(thingId, out encounter);

    public async Task OnTickAsync(TickContext ctx, CancellationToken ct)
    {
        foreach (var thingId in _encounters.Keys.ToArray())
        {
            if (!_encounters.TryGetValue(thingId, out var encounter))
                continue;

            // Only AttackCommand calls StartEncounter, and only with a player
            // Thing as the attacker - encounter.Attacker always has a PlayerBehavior.
            var attackerBehavior = encounter.Attacker.FindBehavior<PlayerBehavior>()!;
            if (attackerBehavior.ConnectionState == ConnectionState.Linkdead)
            {
                // Attacker disconnected mid-fight (ADR-0004). Freeze the
                // encounter rather than ending it immediately - it resumes
                // automatically once LoginFlow reconnects them (ConnectionState
                // flips back to Playing). Only actually abandon it once the
                // same grace window LoginFlow/LinkdeadSweeper use has elapsed.
                // Linkdead always sets LinkdeadSinceUtc (PlayerBehavior.EnterLinkdead).
                if (ctx.Timestamp - attackerBehavior.LinkdeadSinceUtc!.Value >= ReconnectPolicy.GraceWindow)
                    _encounters.Remove(thingId);

                continue;
            }

            // Not Linkdead (checked above), so Session is the live, connected session.
            var session = attackerBehavior.Session!;

            var attackResult = _resolver.ResolveRound(encounter.Attacker, encounter.Defender);
            await session.WriteLineAsync(
                attackResult.Hit
                    ? $"You hit {encounter.Defender.Name} for {attackResult.Damage} damage."
                    : $"You miss {encounter.Defender.Name}.",
                ct);

            if (attackResult.DefenderDefeated)
            {
                await HandleDefenderDefeatedAsync(encounter, session, ct);
                continue;
            }

            // Classic mutual combat: the defender strikes back the same round.
            var counterResult = _resolver.ResolveRound(encounter.Defender, encounter.Attacker);
            await session.WriteLineAsync(
                counterResult.Hit
                    ? $"{encounter.Defender.Name} hits you for {counterResult.Damage} damage."
                    : $"{encounter.Defender.Name} misses you.",
                ct);

            if (counterResult.DefenderDefeated)
                await HandleAttackerDefeatedAsync(encounter, session, ct);
        }
    }

    private async Task HandleDefenderDefeatedAsync(CombatEncounter encounter, ISession session, CancellationToken ct)
    {
        await session.WriteLineAsync($"You have slain {encounter.Defender.Name}!", ct);

        await _outcomeHandler.OnVictoryAsync(encounter.Attacker, encounter.Defender, ct);

        encounter.Defender.Parent?.Remove(encounter.Defender);
        _encounters.Remove(encounter.Attacker.Id);
    }

    private async Task HandleAttackerDefeatedAsync(CombatEncounter encounter, ISession session, CancellationToken ct)
    {
        var attacker = encounter.Attacker;

        // Real, pre-existing bug fixed here: CombatResolver reads/writes
        // damage against CombatantBehavior.CurrentHitPoints, not any
        // ruleset-specific stats behavior. A respawn that only reset the
        // latter left CombatantBehavior.CurrentHitPoints at/below 0, so the
        // very next hit instantly re-triggered "defeated" regardless of the
        // roll. This reset is generic (CombatantBehavior is this package's
        // own type) so it happens here, unconditionally, before the
        // ruleset-specific outcome handler runs.
        var combatant = attacker.FindBehavior<CombatantBehavior>()!;
        combatant.CurrentHitPoints = combatant.MaxHitPoints;

        var destination = await _outcomeHandler.OnDefeatAsync(attacker, encounter.Defender, ct);

        await session.WriteLineAsync($"{encounter.Defender.Name} has slain you!", ct);

        _encounters.Remove(attacker.Id);

        attacker.Parent?.Remove(attacker);
        destination.Add(attacker);

        await LookCommand.SendRoomDescriptionAsync(attacker, destination, ct);
    }
}
