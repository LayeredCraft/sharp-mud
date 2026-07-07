using System.Diagnostics.CodeAnalysis;

namespace SharpMud.Engine.Commands;

public interface ICommandRegistry
{
    IReadOnlyList<ICommand> Commands { get; }

    void Register(ICommand command);
    bool TryResolve(string verb, [MaybeNullWhen(false)] out ICommand command);
}
