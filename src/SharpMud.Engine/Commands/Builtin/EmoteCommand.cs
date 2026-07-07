namespace SharpMud.Engine.Commands.Builtin;

public sealed class EmoteCommand : ICommand
{
    public string Verb => "emote";
    public IReadOnlyList<string> Aliases { get; } = [":"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Emote what?", ct);
            return;
        }

        var message = $"{ctx.Actor.Name} {string.Join(' ', ctx.Args)}";
        await ctx.Session.WriteLineAsync(message, ct);

        foreach (var other in ctx.World.PlayersInRoom(ctx.CurrentRoom.Id).Where(p => p.Id != ctx.Actor.Id))
        {
            var session = ctx.World.GetSession(other.Id);
            if (session is not null)
                await session.WriteLineAsync(message, ct);
        }
    }
}
