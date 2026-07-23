using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Builder;

/// <summary>
/// The <c>describe &lt;text&gt;</c> command (<see
/// cref="SecurityRole.MinorBuilder"/>) - sets the description of the
/// builder's current room to the rest of the line, saved immediately.
/// Only targets the current room; there's no remote-room variant (matches
/// <see cref="TunnelCommand"/>/<see cref="DigCommand"/>'s current-room-only
/// scope, per ADR-0009).
/// </summary>
public sealed class DescribeCommand : ICommand
{
    private readonly IThingRepository _repository;

    public DescribeCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "describe";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Describe the room as what?", ct))
            return;

        ctx.CurrentRoom.Description = string.Join(' ', ctx.Args);

        var root = BuilderCommandHelpers.FindRoot(ctx.CurrentRoom);
        await _repository.SaveTreeAsync(root, ct);

        await ctx.Session.WriteLineAsync("Description updated.", ct);
    }
}
