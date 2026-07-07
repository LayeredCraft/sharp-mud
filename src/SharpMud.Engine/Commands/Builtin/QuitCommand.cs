namespace SharpMud.Engine.Commands.Builtin;

public sealed class QuitCommand : ICommand
{
    public string Verb => "quit";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        await ctx.Session.WriteLineAsync("Goodbye!", ct);
        await ctx.Session.DisconnectAsync("Player quit.", ct);
    }
}
