using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

// "give <item> to <player>" - works against any player in the room via
// Children/PlayerBehavior. The local CLI only ever connects one player in
// v1, but the plumbing doesn't assume that.
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

        var item = ObjectMatcher.FindMatch(CarriedItems.Of(ctx.Actor), itemTarget, i => i.Name);
        if (item is null)
        {
            await ctx.Session.WriteLineAsync("You aren't carrying that.", ct);
            return;
        }

        var recipient = ctx.CurrentRoom.Children
            .Where(c => c != ctx.Actor && c.HasBehavior<PlayerBehavior>())
            .FirstOrDefault(p => p.Name.Contains(playerName, StringComparison.OrdinalIgnoreCase));

        if (recipient is null)
        {
            await ctx.Session.WriteLineAsync("They aren't here.", ct);
            return;
        }

        if (!ctx.Actor.Remove(item))
        {
            await ctx.Session.WriteLineAsync("You can't give that away.", ct);
            return;
        }

        recipient.Add(item);

        await ctx.Session.WriteLineAsync($"You give {item.Name} to {recipient.Name}.", ct);

        var recipientSession = recipient.FindBehavior<PlayerBehavior>()?.Session;
        if (recipientSession is not null)
            await recipientSession.WriteLineAsync($"{ctx.Actor.Name} gives you {item.Name}.", ct);
    }
}
