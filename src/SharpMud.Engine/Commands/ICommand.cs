namespace SharpMud.Engine.Commands;

public interface ICommand
{
    string Verb { get; }
    IReadOnlyList<string> Aliases { get; }

    Task ExecuteAsync(CommandContext ctx, CancellationToken ct);
}
