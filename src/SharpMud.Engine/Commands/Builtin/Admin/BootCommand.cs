using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>The <c>boot</c> command (<see cref="SecurityRole.MinorAdmin"/>) - disconnects a currently-online target.</summary>
public sealed class BootCommand : ICommand
{
    public string Verb => "boot";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (await CommandGuards.RequireArgsAsync(ctx, "Boot whom?", ct))
            return;

        var username = string.Join(' ', ctx.Args);
        var target = AdminCommandHelpers.FindLiveTarget(ctx.World, username);

        if (target is null || !AdminCommandHelpers.IsOnline(target))
        {
            await ctx.Session.WriteLineAsync($"{username} is not online.", ct);
            return;
        }

        var targetBehavior = target.FindBehavior<PlayerBehavior>()!;
        var targetSession = targetBehavior.Session!;

        // Before disconnecting - without this, the target's own SessionLoop
        // sees an ordinary dropped connection and takes the Linkdead path
        // (ADR-0004), letting them just reconnect and resume where they
        // were, making boot a no-op as a moderation tool.
        targetBehavior.MarkBooted();

        await targetSession.WriteLineAsync("You have been disconnected by an administrator.", ct);
        await targetSession.DisconnectAsync("Booted by an administrator.", ct);

        await ctx.Session.WriteLineAsync($"You booted {target.Name}.", ct);
    }
}
