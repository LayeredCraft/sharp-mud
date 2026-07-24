using SharpMud.Engine.Help;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// Registers the help-authoring command set (ADR-0010) - <c>helptopic</c>/
/// <c>helpindex</c>, both at <see cref="SecurityRole.MinorBuilder"/> (same
/// tier as world-building/OLC - authoring content is the same class of
/// privilege). Not called automatically by <see
/// cref="BuiltinCommands.RegisterAll"/> - a consumer calls this themselves,
/// the same opt-in shape <see cref="Admin.AdminCommands.RegisterAll"/>/
/// <see cref="Builder.BuilderCommands.RegisterAll"/> already use. <c>help</c>
/// itself (topic lookup) is always registered by
/// <see cref="BuiltinCommands.RegisterAll"/> - only authoring is opt-in.
/// </summary>
public static class HelpAdminCommands
{
    /// <summary>Registers <c>helptopic</c>/<c>helpindex</c> against <paramref name="registry"/>.</summary>
    public static void RegisterAll(ICommandRegistry registry, IHelpRepository repository, IEmbeddingProvider embeddingProvider)
    {
        registry.RegisterWithRole(new HelpTopicEditCommand(repository), SecurityRole.MinorBuilder);
        registry.RegisterWithRole(new HelpIndexRebuildCommand(repository, embeddingProvider), SecurityRole.MinorBuilder);
    }
}
