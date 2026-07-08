namespace SharpMud.Engine.Commands;

// Adopted from WheelMUD's GameAction.CommonGuards (docs/research/wheelmud-findings.md
// §3) - covers the repeated preconditions every command was hand-rolling.
// Returns true (and sends the message) when the command should stop; false
// when it's fine to proceed.
public static class CommandGuards
{
    public static async Task<bool> RequireArgsAsync(
        CommandContext ctx, string emptyMessage, CancellationToken ct)
    {
        if (ctx.Args.Count > 0)
            return false;

        await ctx.Session.WriteLineAsync(emptyMessage, ct);
        return true;
    }
}
