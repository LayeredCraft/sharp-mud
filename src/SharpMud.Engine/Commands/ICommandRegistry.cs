using System.Diagnostics.CodeAnalysis;

namespace SharpMud.Engine.Commands;

/// <summary>
/// Resolves verbs/aliases to registered commands. Registration has exactly
/// two intentional entry points, <see cref="RegisterOpen"/> and <see
/// cref="RegisterWithRole"/> - there is no plain <c>Register(ICommand)</c>,
/// per ADR-0005: every command's access level must be a legible,
/// intentional statement at its registration call site, not an easily
/// forgotten guard call inside <c>ExecuteAsync</c>.
/// </summary>
public interface ICommandRegistry
{
    /// <summary>Every registered command, each counted once regardless of alias count.</summary>
    IReadOnlyList<ICommand> Commands { get; }

    /// <summary>Registers a command with no access restriction - anyone can run it.</summary>
    void RegisterOpen(ICommand command);

    /// <summary>Registers a command wrapped in a <see cref="RoleGuardedCommand"/> requiring at least one of <paramref name="requiredRole"/>'s flags.</summary>
    void RegisterWithRole(ICommand command, SecurityRole requiredRole);

    /// <summary>Attempts to resolve a verb or alias to its registered command.</summary>
    bool TryResolve(string verb, [MaybeNullWhen(false)] out ICommand command);
}
