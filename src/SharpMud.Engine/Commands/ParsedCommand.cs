namespace SharpMud.Engine.Commands;

public sealed record ParsedCommand(string Verb, IReadOnlyList<string> Args, string RawInput);
