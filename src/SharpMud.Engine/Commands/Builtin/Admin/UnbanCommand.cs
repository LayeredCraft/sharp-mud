using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// The <c>unban</c> command (<see cref="SecurityRole.FullAdmin"/>) - clears
/// <c>IsBanned</c> on an online-or-offline target, saved immediately. No
/// self-targeting guard needed, unlike <see cref="BanCommand"/> - undoing
/// your own ban isn't reachable, since you can't be logged in while banned.
/// </summary>
public sealed class UnbanCommand : ICommand
{
    private readonly IThingRepository _repository;

    public UnbanCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "unban";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Unban whom?", ct))
            return;

        var username = string.Join(' ', ctx.Args);
        var target = await AdminCommandHelpers.FindTargetAsync(ctx.World, _repository, username, ct);
        if (target is null)
        {
            await ctx.Session.WriteLineAsync($"No player named {username} was found.", ct);
            return;
        }

        target.FindBehavior<PlayerBehavior>()!.Unban();
        await _repository.SaveTreeAsync(target, ct);

        await ctx.Session.WriteLineAsync($"You unbanned {target.Name}.", ct);
    }
}
