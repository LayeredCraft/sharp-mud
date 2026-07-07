namespace SharpMud.Engine.Commands.Builtin;

public sealed class WhoCommand : ICommand
{
    public string Verb => "who";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        await ctx.Session.WriteLineAsync("Online players:", ct);
        foreach (var player in ctx.World.AllPlayers)
            await ctx.Session.WriteLineAsync($"  {player.Name}", ct);
    }
}
