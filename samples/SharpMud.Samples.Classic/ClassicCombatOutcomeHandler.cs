using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Samples.Classic;

/// <summary>
/// Classic's <see cref="ICombatOutcomeHandler"/> - awards
/// <see cref="StatsBehavior.Experience"/> on a win, applies the XP-loss/
/// HP-halving death penalty on a loss, and respawns the loser in the hub
/// (<see cref="WorldContext.StartingRoom"/>). <see cref="CombatManager"/>
/// resets <see cref="CombatantBehavior.CurrentHitPoints"/> to full as a
/// safe baseline before calling <see cref="OnDefeatAsync"/> - the actual
/// combat HP used by <see cref="CombatResolver"/>, not <see
/// cref="StatsBehavior.CurrentHitPoints"/> - so this handler halves it
/// again here to make the documented death penalty real, not just cosmetic
/// against a field combat no longer reads.
/// </summary>
public sealed class ClassicCombatOutcomeHandler : ICombatOutcomeHandler
{
    private readonly WorldContext _worldContext;

    /// <summary>Creates the handler against the shared <see cref="WorldContext"/> (for the respawn destination).</summary>
    public ClassicCombatOutcomeHandler(WorldContext worldContext)
    {
        _worldContext = worldContext;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task<Thing> OnDefeatAsync(Thing defeated, Thing victor, CancellationToken ct)
    {
        // Respawn HP fraction is an open item (docs/combat.md); 50% is a
        // placeholder. This is the HP CombatResolver actually reads/writes -
        // CombatManager already reset it to full before calling this method,
        // so halving it here (not just StatsBehavior's copy below) is what
        // makes the penalty real rather than cosmetic.
        var combatant = defeated.FindBehavior<CombatantBehavior>();
        if (combatant is not null)
            combatant.CurrentHitPoints = Math.Max(1, combatant.MaxHitPoints / 2);

        var stats = defeated.FindBehavior<StatsBehavior>();
        if (stats is not null)
        {
            // XP-loss death penalty (docs/combat.md decision) - exact
            // percentage is still an open item; 10% is a placeholder.
            long xpLoss = (long)(stats.Experience * 0.10);
            stats.Experience = Math.Max(0, stats.Experience - xpLoss);

            // StatsBehavior's own HP mirrors the character-sheet display
            // value; kept in sync with CombatantBehavior's halving above so
            // the two don't drift, even though CombatResolver never reads
            // this copy directly.
            stats.CurrentHitPoints = Math.Max(1, stats.MaxHitPoints / 2);

            var session = defeated.FindBehavior<PlayerBehavior>()?.Session;
            if (session is not null)
                await session.WriteLineAsync($"You lose {xpLoss} experience and awaken back in town.", ct);
        }

        return _worldContext.StartingRoom;
    }
}
