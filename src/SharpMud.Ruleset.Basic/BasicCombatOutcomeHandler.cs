using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic;

/// <summary>
/// Basic's <see cref="ICombatOutcomeHandler"/> - awards <see
/// cref="BasicStatsBehavior.Experience"/> on a win, applies a flat XP-loss/
/// HP-halving death penalty on a loss (same shape as Classic's, deliberately
/// simple), and always respawns at the world's starting room. Basic has no
/// "hub room" concept of its own - <see cref="WorldContext.StartingRoom"/>
/// already is one.
/// </summary>
public sealed class BasicCombatOutcomeHandler : ICombatOutcomeHandler
{
    private readonly WorldContext _worldContext;

    public BasicCombatOutcomeHandler(WorldContext worldContext)
    {
        _worldContext = worldContext;
    }

    public async Task OnVictoryAsync(Thing victor, Thing defeated, CancellationToken ct)
    {
        var stats = victor.FindBehavior<BasicStatsBehavior>();
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
        var stats = defeated.FindBehavior<BasicStatsBehavior>();
        if (stats is not null)
        {
            // XP-loss death penalty (docs/combat.md decision) - exact
            // percentage is still an open item; 10% is a placeholder,
            // matching Classic's.
            long xpLoss = (long)(stats.Experience * 0.10);
            stats.Experience = Math.Max(0, stats.Experience - xpLoss);

            var session = defeated.FindBehavior<PlayerBehavior>()?.Session;
            if (session is not null)
                await session.WriteLineAsync($"You lose {xpLoss} experience and awaken back in the clearing.", ct);
        }

        return _worldContext.StartingRoom;
    }
}
