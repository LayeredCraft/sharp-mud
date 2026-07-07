namespace SharpMud.Engine.Commands.Builtin;

public sealed class HelpCommand(ICommandRegistry registry) : ICommand
{
    public string Verb => "help";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        await ctx.Session.WriteLineAsync("Available commands:", ct);
        foreach (var command in registry.Commands.OrderBy(c => c.Verb, StringComparer.Ordinal))
        {
            var aliasSuffix = command.Aliases.Count > 0
                ? $" ({string.Join(", ", command.Aliases)})"
                : "";
            await ctx.Session.WriteLineAsync($"  {command.Verb}{aliasSuffix}", ct);
        }
    }
}
