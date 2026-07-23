using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>The <c>unmute</c> command (<see cref="SecurityRole.MinorAdmin"/>) - clears <c>IsMuted</c> on an online-or-offline target, saved immediately.</summary>
public sealed class UnmuteCommand : ICommand
{
    private readonly IThingRepository _repository;

    public UnmuteCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "unmute";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Unmute whom?", ct))
            return;

        var username = string.Join(' ', ctx.Args);
        var target = await AdminCommandHelpers.FindTargetAsync(ctx.World, _repository, username, ct);
        if (target is null)
        {
            await ctx.Session.WriteLineAsync($"No player named {username} was found.", ct);
            return;
        }

        target.FindBehavior<PlayerBehavior>()!.Unmute();
        await _repository.SaveTreeAsync(target, ct);

        await ctx.Session.WriteLineAsync($"You unmuted {target.Name}.", ct);
    }
}
