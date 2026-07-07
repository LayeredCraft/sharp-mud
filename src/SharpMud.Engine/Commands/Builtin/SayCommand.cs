namespace SharpMud.Engine.Commands.Builtin;

public sealed class SayCommand : ICommand
{
    public string Verb => "say";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count == 0)
        {
            await ctx.Session.WriteLineAsync("Say what?", ct);
            return;
        }

        var message = string.Join(' ', ctx.Args);
        await ctx.Session.WriteLineAsync($"You say: {message}", ct);

        foreach (var other in ctx.World.PlayersInRoom(ctx.CurrentRoom.Id).Where(p => p.Id != ctx.Actor.Id))
        {
            var session = ctx.World.GetSession(other.Id);
            if (session is not null)
                await session.WriteLineAsync($"{ctx.Actor.Name} says: {message}", ct);
        }
    }
}
