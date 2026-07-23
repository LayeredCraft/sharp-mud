using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands;

/// <summary>
/// Wraps an inner <see cref="ICommand"/> so it only executes for an actor
/// whose own <c>PlayerBehavior.IsMuted</c> is <see langword="false"/> - the
/// same Decorator shape as <see cref="RoleGuardedCommand"/>, reused for a
/// cross-cutting concern that isn't role-based at all (ADR-0005). Gates the
/// *actor's own* ability to speak (<c>say</c>/<c>emote</c>), not anything
/// they're doing to a target.
/// </summary>
public sealed class MuteGuardedCommand : ICommand
{
    private readonly ICommand _inner;

    /// <summary>Wraps <paramref name="inner"/>.</summary>
    public MuteGuardedCommand(ICommand inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    public string Verb => _inner.Verb;

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases => _inner.Aliases;

    /// <summary>Checks the actor's own mute state, then delegates to the wrapped command or sends a rejection message.</summary>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        var isMuted = ctx.Actor.FindBehavior<PlayerBehavior>()?.IsMuted ?? false;
        if (isMuted)
        {
            await ctx.Session.WriteLineAsync("You have been muted and cannot do that.", ct);
            return;
        }

        await _inner.ExecuteAsync(ctx, ct);
    }
}
