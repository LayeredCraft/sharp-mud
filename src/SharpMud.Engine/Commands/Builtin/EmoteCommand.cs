namespace SharpMud.Engine.Commands.Builtin;

public sealed class EmoteCommand : ICommand
{
    public string Verb => "emote";
    public IReadOnlyList<string> Aliases { get; } = [":"];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Emote what?", ct))
            return;

        var message = $"{ctx.Actor.Name} {string.Join(' ', ctx.Args)}";
        await ctx.Session.WriteLineAsync(message, ct);
        await RoomBroadcast.ToOccupantsAsync(ctx.CurrentRoom, ctx.Actor, message, ct);
    }
}
