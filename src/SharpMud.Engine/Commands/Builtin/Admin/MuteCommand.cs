using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>The <c>mute</c> command (<see cref="SecurityRole.MinorAdmin"/>) - sets <c>IsMuted</c> on an online-or-offline target, saved immediately.</summary>
public sealed class MuteCommand : ICommand
{
    private readonly IThingRepository _repository;

    public MuteCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "mute";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Mute whom?", ct))
            return;

        var username = string.Join(' ', ctx.Args);
        var target = await AdminCommandHelpers.FindTargetAsync(ctx.World, _repository, username, ct);
        if (target is null)
        {
            await ctx.Session.WriteLineAsync($"No player named {username} was found.", ct);
            return;
        }

        target.FindBehavior<PlayerBehavior>()!.Mute();
        await _repository.SaveTreeAsync(target, ct);

        await ctx.Session.WriteLineAsync($"You muted {target.Name}.", ct);
    }
}
