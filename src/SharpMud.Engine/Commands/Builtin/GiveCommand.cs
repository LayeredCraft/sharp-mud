using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands.Builtin;

// "give <item> to <player>" (docs/commands.md v1 verb list). Works against
// any player in the room via IWorld.PlayersInRoom - the local CLI only ever
// connects one player in v1, but the plumbing doesn't assume that.
public sealed class GiveCommand : ICommand
{
    public string Verb => "give";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var args = ctx.Args;
        var toIndex = -1;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i].Equals("to", StringComparison.OrdinalIgnoreCase))
            {
                toIndex = i;
                break;
            }
        }

        if (toIndex <= 0 || toIndex == args.Count - 1)
        {
            await ctx.Session.WriteLineAsync("Give what to whom?", ct);
            return;
        }

        var itemTarget = string.Join(' ', args.Take(toIndex));
        var playerName = string.Join(' ', args.Skip(toIndex + 1));

        var item = ObjectMatcher.FindMatch(
            ctx.Actor.Inventory.Select(ctx.World.GetItem).OfType<Item>(),
            itemTarget,
            i => i.Name);

        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't carrying that.", ct);
            return;
        }

        var recipient = ctx.World.PlayersInRoom(ctx.CurrentRoom.Id)
            .FirstOrDefault(p => p.Id != ctx.Actor.Id && p.Name.Contains(playerName, StringComparison.OrdinalIgnoreCase));

        if (recipient is null)
        {
            await ctx.Session.WriteLineAsync("They aren't here.", ct);
            return;
        }

        ctx.Actor.Inventory.Remove(item.Id);
        recipient.Inventory.Add(item.Id);

        await ctx.Session.WriteLineAsync($"You give {item.Name} to {recipient.Name}.", ct);

        var recipientSession = ctx.World.GetSession(recipient.Id);
        if (recipientSession is not null)
            await recipientSession.WriteLineAsync($"{ctx.Actor.Name} gives you {item.Name}.", ct);
    }
}
