using System.Diagnostics.CodeAnalysis;

namespace SharpMud.Engine.Commands;

public sealed class CommandRegistry : ICommandRegistry
{
    // Canonical verbs and aliases are tracked separately so a canonical verb
    // always wins over any alias, regardless of registration order (see
    // docs/commands.md: "built-in directions take priority over user aliases").
    private readonly Dictionary<string, ICommand> _verbs = [];
    private readonly Dictionary<string, ICommand> _aliases = [];
    private readonly List<ICommand> _commands = [];

    public IReadOnlyList<ICommand> Commands => _commands;

    public void Register(ICommand command)
    {
        _verbs[command.Verb] = command;
        foreach (var alias in command.Aliases)
            _aliases.TryAdd(alias, command);

        _commands.Add(command);
    }

    public bool TryResolve(string verb, [MaybeNullWhen(false)] out ICommand command)
    {
        if (_verbs.TryGetValue(verb, out command))
            return true;

        return _aliases.TryGetValue(verb, out command);
    }
}
