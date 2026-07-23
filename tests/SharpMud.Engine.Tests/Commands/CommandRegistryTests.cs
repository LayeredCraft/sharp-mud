using SharpMud.Engine.Commands;

namespace SharpMud.Engine.Tests.Commands;

public sealed class CommandRegistryTests
{
    private sealed class FakeCommand(string verb, params string[] aliases) : ICommand
    {
        public string Verb { get; } = verb;
        public IReadOnlyList<string> Aliases { get; } = aliases;
        public Task ExecuteAsync(CommandContext ctx, CancellationToken ct) => Task.CompletedTask;
    }

    private readonly CommandRegistry _sut = new();

    [Fact]
    public void TryResolve_ReturnsCommand_WhenVerbMatchesCanonicalVerb()
    {
        var north = new FakeCommand("north", "n");
        _sut.RegisterOpen(north);

        _sut.TryResolve("north", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(north);
    }

    [Fact]
    public void TryResolve_ReturnsCommand_WhenVerbMatchesAlias()
    {
        var north = new FakeCommand("north", "n");
        _sut.RegisterOpen(north);

        _sut.TryResolve("n", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(north);
    }

    [Fact]
    public void TryResolve_ReturnsFalse_WhenVerbIsUnknown()
    {
        _sut.TryResolve("xyzzy", out var resolved).Should().BeFalse();
        resolved.Should().BeNull();
    }

    [Fact]
    public void TryResolve_PrefersCanonicalVerb_WhenAliasCollidesWithAnotherCommandsVerb()
    {
        // "n" is registered as an alias of "north" first; a later command
        // whose canonical verb IS "n" must still win when resolving "n" -
        // built-in directions take priority over aliases, regardless of
        // registration order (docs/commands.md).
        var north = new FakeCommand("north", "n");
        var literalN = new FakeCommand("n");

        _sut.RegisterOpen(north);
        _sut.RegisterOpen(literalN);

        _sut.TryResolve("n", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(literalN);
    }

    [Fact]
    public void Commands_ContainsEveryRegisteredCommandOnce_RegardlessOfAliasCount()
    {
        var north = new FakeCommand("north", "n");
        _sut.RegisterOpen(north);

        _sut.Commands.Should().ContainSingle().Which.Should().BeSameAs(north);
    }

    [Fact]
    public void RegisterOpen_ResolvesUnconditionally()
    {
        var command = new FakeCommand("dance");
        _sut.RegisterOpen(command);

        _sut.TryResolve("dance", out var resolved).Should().BeTrue();
        resolved.Should().BeSameAs(command);
    }

    [Fact]
    public void RegisterWithRole_ResolvesToARoleGuardedCommandWrappingTheGivenCommandAndRole()
    {
        var command = new FakeCommand("ban");
        _sut.RegisterWithRole(command, SecurityRole.FullAdmin);

        _sut.TryResolve("ban", out var resolved).Should().BeTrue();
        var guarded = resolved.Should().BeOfType<RoleGuardedCommand>().Subject;
        guarded.RequiredRole.Should().Be(SecurityRole.FullAdmin);
        guarded.Verb.Should().Be("ban");
    }
}
