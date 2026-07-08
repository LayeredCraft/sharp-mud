using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;

namespace SharpMud.Ruleset.Classic;

// Registered once with IGameLoop and resolves every active encounter each
// tick - simpler Host wiring than one ITickable per encounter, and all
// world-state mutation happens on the single game-loop "thread" (sequential
// awaits in GameLoop.RunAsync), so there's no concurrent-mutation risk.
public sealed class CombatManager(ICombatResolver resolver, Thing hubRoom) : ICombatManager, ITickable
{
    private readonly Dictionary<ThingId, CombatEncounter> _encounters = [];

    public bool IsInCombat(ThingId thingId) => _encounters.ContainsKey(thingId);

    public void StartEncounter(Thing attacker, Thing defender) =>
        _encounters[attacker.Id] = new CombatEncounter { Attacker = attacker, Defender = defender };

    public void EndEncounter(ThingId thingId) => _encounters.Remove(thingId);

    public bool TryGetEncounter(ThingId thingId, out CombatEncounter? encounter) =>
        _encounters.TryGetValue(thingId, out encounter);

    public async Task OnTickAsync(TickContext ctx, CancellationToken ct)
    {
        foreach (var thingId in _encounters.Keys.ToArray())
        {
            if (!_encounters.TryGetValue(thingId, out var encounter))
                continue;

            var session = encounter.Attacker.FindBehavior<PlayerBehavior>()?.Session;
            if (session is null)
            {
                // Attacker disconnected mid-fight. Real linkdead handling (a
                // grace period before the encounter is force-abandoned - see
                // docs/combat.md) isn't wired up yet; the encounter just ends.
                _encounters.Remove(thingId);
                continue;
            }

            var attackResult = resolver.ResolveRound(encounter.Attacker, encounter.Defender);
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
            var counterResult = resolver.ResolveRound(encounter.Defender, encounter.Attacker);
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

        var combatant = encounter.Defender.FindBehavior<CombatantBehavior>()!;
        var stats = encounter.Attacker.FindBehavior<StatsBehavior>();
        if (stats is not null)
        {
            stats.Experience += combatant.ExperienceReward;
            await session.WriteLineAsync($"You gain {combatant.ExperienceReward} experience.", ct);
        }

        encounter.Defender.Parent?.Remove(encounter.Defender);
        _encounters.Remove(encounter.Attacker.Id);
    }

    private async Task HandleAttackerDefeatedAsync(CombatEncounter encounter, ISession session, CancellationToken ct)
    {
        var attacker = encounter.Attacker;
        var stats = attacker.FindBehavior<StatsBehavior>();

        // XP-loss death penalty (docs/combat.md decision) - exact percentage
        // is still an open item; 10% is a placeholder.
        long xpLoss = 0;
        if (stats is not null)
        {
            xpLoss = (long)(stats.Experience * 0.10);
            stats.Experience = Math.Max(0, stats.Experience - xpLoss);

            // Respawn HP fraction is also an open item; 50% is a placeholder.
            stats.CurrentHitPoints = Math.Max(1, stats.MaxHitPoints / 2);
        }

        await session.WriteLineAsync($"{encounter.Defender.Name} has slain you!", ct);
        await session.WriteLineAsync($"You lose {xpLoss} experience and awaken back in town.", ct);

        _encounters.Remove(attacker.Id);

        attacker.Parent?.Remove(attacker);
        hubRoom.Add(attacker);

        await LookCommand.SendRoomDescriptionAsync(attacker, hubRoom, ct);
    }
}
