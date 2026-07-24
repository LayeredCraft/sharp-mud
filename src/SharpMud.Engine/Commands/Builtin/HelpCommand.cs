using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Help;

namespace SharpMud.Engine.Commands.Builtin;

/// <summary>
/// The <c>help</c> command. With no arguments, lists every command the
/// actor can see (unchanged behavior). With an argument, resolves a
/// <see cref="HelpTopic"/> via ADR-0010's three-tier pipeline: exact
/// <see cref="HelpTopic.Key"/>/<see cref="HelpTopic.Aliases"/> match, then
/// <see cref="HelpTopic.Keywords"/> match, then <see cref="IHelpSearchIndex"/>
/// semantic search - falling back to "no help topic found" only once all
/// three miss, never a weak guess.
/// </summary>
public sealed class HelpCommand : ICommand
{
    private readonly ICommandRegistry _registry;
    private readonly IHelpRepository _helpRepository;
    private readonly IHelpSearchIndex _helpSearchIndex;

    /// <summary>Creates a help command listing commands from <paramref name="registry"/> and resolving topics via <paramref name="helpRepository"/>/<paramref name="helpSearchIndex"/>.</summary>
    public HelpCommand(ICommandRegistry registry, IHelpRepository helpRepository, IHelpSearchIndex helpSearchIndex)
    {
        _registry = registry;
        _helpRepository = helpRepository;
        _helpSearchIndex = helpSearchIndex;
    }

    /// <inheritdoc/>
    public string Verb => "help";

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases { get; } = [];

    /// <inheritdoc/>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count > 0)
        {
            await ExecuteTopicLookupAsync(ctx, ct);
            return;
        }

        await ExecuteCommandListingAsync(ctx, ct);
    }

    private async Task ExecuteTopicLookupAsync(CommandContext ctx, CancellationToken ct)
    {
        var query = string.Join(' ', ctx.Args);

        var topic = await _helpRepository.FindByNameOrAliasAsync(query, ct);

        if (topic is null)
        {
            var keywordMatches = await _helpRepository.FindByKeywordAsync(query, ct);
            topic = keywordMatches.FirstOrDefault();
        }

        if (topic is null)
        {
            var hits = await _helpSearchIndex.SearchAsync(query, ct);
            topic = hits.FirstOrDefault()?.Topic;
        }

        if (topic is null)
        {
            await ctx.Session.WriteLineAsync($"No help topic found for '{query}'.", ct);
            return;
        }

        await ctx.Session.WriteLineAsync(topic.Body, ct);
    }

    private async Task ExecuteCommandListingAsync(CommandContext ctx, CancellationToken ct)
    {
        await ctx.Session.WriteLineAsync("Available commands:", ct);

        var actorRoles = ctx.Actor.FindBehavior<PlayerBehavior>()?.Roles ?? SecurityRole.None;
        foreach (var command in _registry.Commands.OrderBy(c => c.Verb, StringComparer.Ordinal))
        {
            // RoleGuardedCommand passes Verb/Aliases straight through from
            // the command it wraps, so without this check every admin
            // command would list unconditionally to every player - not an
            // exploit (the gate still blocks execution) but a real,
            // unpolished info leak.
            if (command is RoleGuardedCommand guarded && (actorRoles & guarded.RequiredRole) == SecurityRole.None)
                continue;

            var aliasSuffix = command.Aliases.Count > 0
                ? $" ({string.Join(", ", command.Aliases)})"
                : "";
            await ctx.Session.WriteLineAsync($"  {command.Verb}{aliasSuffix}", ct);
        }
    }
}
