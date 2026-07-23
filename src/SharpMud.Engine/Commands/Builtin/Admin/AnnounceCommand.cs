using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>The <c>announce</c> command (<see cref="SecurityRole.MinorAdmin"/>) - broadcasts to every currently-online player.</summary>
public sealed class AnnounceCommand : ICommand
{
    public string Verb => "announce";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Announce what?", ct))
            return;

        var message = string.Join(' ', ctx.Args);

        // Deliberately not WhoCommand's world.AllWithBehavior<PlayerBehavior>()
        // with no further filter - that also matches Linkdead players
        // (ADR-0004), which would attempt a write to their stale session.
        foreach (var player in ctx.World.AllWithBehavior<PlayerBehavior>())
        {
            if (AdminCommandHelpers.GetOnlineBehavior(player) is not { } behavior)
                continue;

            await behavior.Session!.WriteLineAsync($"[Announcement] {message}", ct);
        }
    }
}
