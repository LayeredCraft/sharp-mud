namespace SharpMud.Engine.Commands.Builtin;

public sealed class SayCommand : ICommand
{
    public string Verb => "say";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Say what?", ct))
            return;

        var message = string.Join(' ', ctx.Args);
        await ctx.Session.WriteLineAsync($"You say: {message}", ct);
        await RoomBroadcast.ToOccupantsAsync(ctx.CurrentRoom, ctx.Actor, $"{ctx.Actor.Name} says: {message}", ct);
    }
}
