using SharpMud.Engine.Combat;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class AttackCommand(ICombatManager combatManager) : ICommand
{
    public string Verb => "kill";
    public IReadOnlyList<string> Aliases { get; } = ["attack"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Kill what?", ct);
            return;
        }

        if (combatManager.IsInCombat(ctx.Actor.Id))
        {
            await ctx.Session.WriteLineAsync("You are already fighting!", ct);
            return;
        }

        var targetName = string.Join(' ', ctx.Args);
        var npc = ObjectMatcher.FindMatch(
            ctx.CurrentRoom.Npcs.Select(ctx.World.GetNpc).OfType<Npc>(),
            targetName,
            n => n.Name);

        if (npc is null)
        {
            await ctx.Session.WriteLineAsync("You don't see that here.", ct);
            return;
        }

        combatManager.StartEncounter(ctx.Actor, npc);
        await ctx.Session.WriteLineAsync($"You attack {npc.Name}!", ct);
    }
}
