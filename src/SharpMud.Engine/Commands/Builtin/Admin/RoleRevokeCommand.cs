using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// The <c>rolerevoke &lt;username&gt; &lt;role&gt;</c> command (<see
/// cref="SecurityRole.FullAdmin"/>) - revokes a role from an
/// online-or-offline target via <see cref="PlayerBehavior.RevokeRole"/>
/// (hierarchy-invariant enforcement happens inside <see
/// cref="PlayerBehavior"/> itself, not here), saved immediately. Rejects
/// revoking your own <see cref="SecurityRole.FullAdmin"/> - same class of
/// lockout risk as <see cref="BanCommand"/>'s self-targeting guard: a sole
/// <see cref="SecurityRole.FullAdmin"/> revoking their own tier has no
/// in-game path back without another already present to re-grant it.
/// Revoking any other role from yourself, or <see
/// cref="SecurityRole.FullAdmin"/> from someone else, is unaffected.
/// </summary>
public sealed class RoleRevokeCommand : ICommand
{
    private readonly IThingRepository _repository;

    public RoleRevokeCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "rolerevoke";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count < 2)
        {
            await ctx.Session.WriteLineAsync("Usage: rolerevoke <username> <role>", ct);
            return;
        }

        var username = ctx.Args[0];
        if (!AdminCommandHelpers.TryParseGrantableRole(ctx.Args[1], out var role))
        {
            await ctx.Session.WriteLineAsync($"'{ctx.Args[1]}' isn't a revocable role.", ct);
            return;
        }

        var actorUsername = ctx.Actor.FindBehavior<PlayerBehavior>()?.Username;
        if (role == SecurityRole.FullAdmin && string.Equals(actorUsername, username, StringComparison.OrdinalIgnoreCase))
        {
            await ctx.Session.WriteLineAsync("You cannot revoke your own FullAdmin.", ct);
            return;
        }

        var target = await AdminCommandHelpers.FindTargetAsync(ctx.World, _repository, username, ct);
        if (target is null)
        {
            await ctx.Session.WriteLineAsync($"No player named {username} was found.", ct);
            return;
        }

        var failure = target.FindBehavior<PlayerBehavior>()!.RevokeRole(role);
        if (failure is not null)
        {
            await ctx.Session.WriteLineAsync(failure, ct);
            return;
        }

        await _repository.SaveTreeAsync(target, ct);

        await ctx.Session.WriteLineAsync($"Revoked {role} from {target.Name}.", ct);
    }
}
