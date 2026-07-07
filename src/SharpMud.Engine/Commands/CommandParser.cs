namespace SharpMud.Engine.Commands;

public sealed class CommandParser : ICommandParser
{
    public ParsedCommand Parse(string rawInput)
    {
        var trimmed = rawInput.Trim();
        if (trimmed.Length == 0)
            return new ParsedCommand(string.Empty, [], rawInput);

        var tokens = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var verb = tokens[0].ToLowerInvariant();
        var args = tokens.Skip(1).ToArray();

        return new ParsedCommand(verb, args, rawInput);
    }
}
