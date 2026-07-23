using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// The <c>rolegrant &lt;username&gt; &lt;role&gt;</c> command (<see
/// cref="SecurityRole.FullAdmin"/>) - grants a role to an online-or-offline
/// target via <see cref="PlayerBehavior.GrantRole"/> (accumulation happens
/// inside <see cref="PlayerBehavior"/> itself, not here), saved
/// immediately. Gated at <see cref="SecurityRole.FullAdmin"/> specifically
/// so a <see cref="SecurityRole.MinorAdmin"/> can never self-escalate.
/// </summary>
public sealed class RoleGrantCommand : ICommand
{
    private readonly IThingRepository _repository;

    public RoleGrantCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "rolegrant";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count < 2)
        {
            await ctx.Session.WriteLineAsync("Usage: rolegrant <username> <role>", ct);
            return;
        }

        var username = ctx.Args[0];
        if (!AdminCommandHelpers.TryParseGrantableRole(ctx.Args[1], out var role))
        {
            await ctx.Session.WriteLineAsync($"'{ctx.Args[1]}' isn't a grantable role.", ct);
            return;
        }

        var target = await AdminCommandHelpers.FindTargetAsync(ctx.World, _repository, username, ct);
        if (target is null)
        {
            await ctx.Session.WriteLineAsync($"No player named {username} was found.", ct);
            return;
        }

        target.FindBehavior<PlayerBehavior>()!.GrantRole(role);
        await _repository.SaveTreeAsync(target, ct);

        await ctx.Session.WriteLineAsync($"Granted {role} to {target.Name}.", ct);
    }
}
