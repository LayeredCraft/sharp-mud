using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands;

/// <summary>
/// Wraps an inner <see cref="ICommand"/> so it only executes for an actor
/// holding at least one of <see cref="RequiredRole"/>'s flags (any-of
/// semantics, bitwise AND) - the Decorator mechanism ADR-0005 chose over a
/// forced <c>ICommand.RequiredRole</c> interface member. Created by <see
/// cref="ICommandRegistry.RegisterWithRole"/>, not meant to be constructed
/// directly by ordinary command registration code.
/// </summary>
public sealed class RoleGuardedCommand : ICommand
{
    private readonly ICommand _inner;

    /// <summary>Wraps <paramref name="inner"/>, requiring at least one of <paramref name="requiredRole"/>'s flags.</summary>
    public RoleGuardedCommand(ICommand inner, SecurityRole requiredRole)
    {
        _inner = inner;
        RequiredRole = requiredRole;
    }

    /// <summary>
    /// The role(s) an actor must hold at least one of to reach the wrapped
    /// command - exposed publicly so <c>HelpCommand</c> can filter its
    /// listing by the same check, not just internally.
    /// </summary>
    public SecurityRole RequiredRole { get; }

    /// <inheritdoc/>
    public string Verb => _inner.Verb;

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases => _inner.Aliases;

    /// <summary>Checks the actor's roles, then delegates to the wrapped command or sends a rejection message.</summary>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var actorRoles = ctx.Actor.FindBehavior<PlayerBehavior>()?.Roles ?? SecurityRole.None;
        if ((actorRoles & RequiredRole) == SecurityRole.None)
        {
            await ctx.Session.WriteLineAsync("You don't have permission to do that.", ct);
            return;
        }

        await _inner.ExecuteAsync(ctx, ct);
    }
}
