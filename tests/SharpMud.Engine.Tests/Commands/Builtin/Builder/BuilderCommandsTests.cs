using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin.Builder;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Tests.Commands.Builtin.Builder;

public sealed class BuilderCommandsTests
{
    [Fact]
    public void RegisterAll_RegistersAllThreeCommandsAtMinorBuilder()
    {
        var registry = Substitute.For<ICommandRegistry>();
        var repository = Substitute.For<IThingRepository>();

        BuilderCommands.RegisterAll(registry, repository);

        registry.Received(1).RegisterWithRole(Arg.Any<DigCommand>(), SecurityRole.MinorBuilder);
        registry.Received(1).RegisterWithRole(Arg.Any<TunnelCommand>(), SecurityRole.MinorBuilder);
        registry.Received(1).RegisterWithRole(Arg.Any<DescribeCommand>(), SecurityRole.MinorBuilder);
        registry.DidNotReceiveWithAnyArgs().RegisterOpen(default!);
    }
}
