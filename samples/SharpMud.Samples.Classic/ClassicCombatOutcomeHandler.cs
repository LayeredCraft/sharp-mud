using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Samples.Classic;

/// <summary>
/// Classic's <see cref="ICombatOutcomeHandler"/> - awards
/// <see cref="StatsBehavior.Experience"/> on a win, applies the XP-loss/
/// HP-halving death penalty on a loss, and respawns the loser in the hub
/// (<see cref="WorldContext.StartingRoom"/>). <see
/// cref="CombatantBehavior.CurrentHitPoints"/> is already reset by <see
/// cref="CombatManager"/> before <see cref="OnDefeatAsync"/> runs - this
/// only owns the ruleset-specific <see cref="StatsBehavior"/> touches.
/// </summary>
public sealed class ClassicCombatOutcomeHandler : ICombatOutcomeHandler
{
    private readonly WorldContext _worldContext;

    public ClassicCombatOutcomeHandler(WorldContext worldContext)
    {
        _worldContext = worldContext;
    }

    public async Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct)
    {
        var stats = victor.FindBehavior<StatsBehavior>();
        var combatant = defeated.FindBehavior<CombatantBehavior>();
        if (stats is null || combatant is null)
            return;

        stats.Experience += combatant.ExperienceReward;

        var session = victor.FindBehavior<PlayerBehavior>()?.Session;
        if (session is not null)
            await session.WriteLineAsync($"You gain {combatant.ExperienceReward} experience.", ct);
    }

    public async Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct)
    {
        var stats = defeated.FindBehavior<StatsBehavior>();
        if (stats is not null)
        {
            // XP-loss death penalty (docs/combat.md decision) - exact
            // percentage is still an open item; 10% is a placeholder.
            long xpLoss = (long)(stats.Experience * 0.10);
            stats.Experience = Math.Max(0, stats.Experience - xpLoss);

            // Respawn HP fraction is also an open item; 50% is a placeholder.
            stats.CurrentHitPoints = Math.Max(1, stats.MaxHitPoints / 2);

            var session = defeated.FindBehavior<PlayerBehavior>()?.Session;
            if (session is not null)
                await session.WriteLineAsync($"You lose {xpLoss} experience and awaken back in town.", ct);
        }

        return _worldContext.StartingRoom;
    }
}
