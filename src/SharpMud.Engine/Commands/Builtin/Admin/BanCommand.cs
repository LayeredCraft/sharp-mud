using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// The <c>ban</c> command (<see cref="SecurityRole.FullAdmin"/>) - sets
/// <c>IsBanned</c> on an online-or-offline target, saved immediately, and
/// disconnects the target immediately if they're currently online
/// (<c>SessionLoop</c> never re-checks <c>IsBanned</c> mid-session).
/// Rejects self-targeting - a ban has no in-game recovery path.
/// </summary>
public sealed class BanCommand : ICommand
{
    private readonly IThingRepository _repository;

    public BanCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "ban";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Ban whom?", ct))
            return;

        var username = string.Join(' ', ctx.Args);
        var actorUsername = ctx.Actor.FindBehavior<PlayerBehavior>()?.Username;
        if (string.Equals(actorUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Session.WriteLineAsync("You cannot ban yourself.", ct);
            return;
        }

        var target = await AdminCommandHelpers.FindTargetAsync(ctx.World, _repository, username, ct);
        if (target is null)
        {
            await ctx.Session.WriteLineAsync($"No player named {username} was found.", ct);
            return;
        }

        var targetBehavior = target.FindBehavior<PlayerBehavior>()!;
        targetBehavior.Ban();

        if (AdminCommandHelpers.IsOnline(target))
        {
            targetBehavior.MarkBooted();
            var targetSession = targetBehavior.Session!;
            await targetSession.WriteLineAsync("You have been banned by an administrator.", ct);
            await targetSession.DisconnectAsync("Banned by an administrator.", ct);
        }

        await _repository.SaveTreeAsync(target, ct);

        await ctx.Session.WriteLineAsync($"You banned {target.Name}.", ct);
    }
}
