using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

public sealed class HelpCommand(ICommandRegistry registry) : ICommand
{
    public string Verb => "help";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        await ctx.Session.WriteLineAsync("Available commands:", ct);

        var actorRoles = ctx.Actor.FindBehavior<PlayerBehavior>()?.Roles ?? SecurityRole.None;
        foreach (var command in registry.Commands.OrderBy(c => c.Verb, StringComparer.Ordinal))
        {
            // RoleGuardedCommand passes Verb/Aliases straight through from
            // the command it wraps, so without this check every admin
            // command would list unconditionally to every player - not an
            // exploit (the gate still blocks execution) but a real,
            // unpolished info leak.
            if (command is RoleGuardedCommand guarded && (actorRoles & guarded.RequiredRole) == SecurityRole.None)
                continue;

            var aliasSuffix = command.Aliases.Count > 0
                ? $" ({string.Join(", ", command.Aliases)})"
                : "";
            await ctx.Session.WriteLineAsync($"  {command.Verb}{aliasSuffix}", ct);
        }
    }
}
