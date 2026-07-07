namespace SharpMud.Engine.Commands;

public interface ICommandParser
{
    ParsedCommand Parse(string rawInput);
}
