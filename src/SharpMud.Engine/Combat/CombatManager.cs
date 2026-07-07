using System.Diagnostics.CodeAnalysis;
using SharpMud.Engine.Characters;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Combat;

// Registered once with IGameLoop (not one ITickable per encounter) and
// resolves every active encounter each tick - simpler Host wiring, and all
// world-state mutation happens on the single game-loop "thread" (sequential
// awaits in GameLoop.RunAsync), so there's no concurrent-mutation risk.
public sealed class CombatManager(IWorld world, ICombatResolver resolver, RoomId hubRoomId)
    : ICombatManager, ITickable
{
    private readonly Dictionary<PlayerId, CombatEncounter> _encounters = [];

    public bool IsInCombat(PlayerId playerId) => _encounters.ContainsKey(playerId);

    public void StartEncounter(Player attacker, Npc defender) =>
        _encounters[attacker.Id] = new CombatEncounter { Attacker = attacker, Defender = defender };

    public void EndEncounter(PlayerId playerId) => _encounters.Remove(playerId);

    public bool TryGetEncounter(PlayerId playerId, [MaybeNullWhen(false)] out CombatEncounter encounter) =>
        _encounters.TryGetValue(playerId, out encounter);

    public async Task OnTickAsync(TickContext ctx, CancellationToken ct)
    {
        foreach (var playerId in _encounters.Keys.ToArray())
        {
            if (!_encounters.TryGetValue(playerId, out var encounter))
                continue;

            var session = world.GetSession(playerId);
            if (session is null)
            {
                // Player disconnected mid-fight. Real linkdead handling (a
                // grace period before the encounter is force-abandoned - see
                // docs/combat.md) isn't wired up yet; the encounter just ends.
                _encounters.Remove(playerId);
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
                await HandleNpcDefeatedAsync(encounter, session, ct);
                continue;
            }

            // Classic mutual combat: the NPC strikes back the same round.
            var counterResult = resolver.ResolveRound(encounter.Defender, encounter.Attacker);
            await session.WriteLineAsync(
                counterResult.Hit
                    ? $"{encounter.Defender.Name} hits you for {counterResult.Damage} damage."
                    : $"{encounter.Defender.Name} misses you.",
                ct);

            if (counterResult.DefenderDefeated)
                await HandlePlayerDefeatedAsync(encounter, session, ct);
        }
    }

    private async Task HandleNpcDefeatedAsync(CombatEncounter encounter, ISession session, CancellationToken ct)
    {
        await session.WriteLineAsync($"You have slain {encounter.Defender.Name}!", ct);

        encounter.Attacker.Experience += encounter.Defender.ExperienceReward;
        await session.WriteLineAsync($"You gain {encounter.Defender.ExperienceReward} experience.", ct);

        world.RemoveNpc(encounter.Defender.Id);
        _encounters.Remove(encounter.Attacker.Id);
    }

    private async Task HandlePlayerDefeatedAsync(CombatEncounter encounter, ISession session, CancellationToken ct)
    {
        var player = encounter.Attacker;

        // XP-loss death penalty (docs/combat.md decision) - exact percentage
        // is still an open item; 10% is a placeholder.
        var xpLoss = (long)(player.Experience * 0.10);
        player.Experience = Math.Max(0, player.Experience - xpLoss);

        // Respawn HP fraction is also an open item; 50% is a placeholder.
        player.CurrentHitPoints = Math.Max(1, player.MaxHitPoints / 2);
        player.CurrentRoomId = hubRoomId;

        await session.WriteLineAsync($"{encounter.Defender.Name} has slain you!", ct);
        await session.WriteLineAsync($"You lose {xpLoss} experience and awaken back in town.", ct);

        _encounters.Remove(player.Id);

        var hub = world.GetRoom(hubRoomId);
        if (hub is not null)
            await LookCommand.SendRoomDescriptionAsync(player, hub, world, session, ct);
    }
}
