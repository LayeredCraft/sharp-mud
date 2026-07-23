using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Builder;

/// <summary>
/// Registers the world-building/OLC command set (ADR-0009) -
/// <c>dig</c>/<c>tunnel</c>/<c>describe</c>, all at <see
/// cref="SecurityRole.MinorBuilder"/>. Not called automatically by <see
/// cref="BuiltinCommands.RegisterAll"/> - a consumer calls this themselves
/// (passing their own <see cref="IThingRepository"/>), the same opt-in
/// shape <see cref="Admin.AdminCommands.RegisterAll"/> already uses.
/// </summary>
public static class BuilderCommands
{
    public static void RegisterAll(ICommandRegistry registry, IThingRepository repository)
    {
        registry.RegisterWithRole(new DigCommand(repository), SecurityRole.MinorBuilder);
        registry.RegisterWithRole(new TunnelCommand(repository), SecurityRole.MinorBuilder);
        registry.RegisterWithRole(new DescribeCommand(repository), SecurityRole.MinorBuilder);
    }
}
