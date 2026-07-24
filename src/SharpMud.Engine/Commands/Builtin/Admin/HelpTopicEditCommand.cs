using SharpMud.Engine.Help;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// The <c>helptopic &lt;key&gt; &lt;body&gt;</c> command (<see
/// cref="SecurityRole.MinorBuilder"/>, ADR-0010) - creates a new
/// <see cref="HelpTopic"/> or overwrites an existing one's <see
/// cref="HelpTopic.Body"/>, keyed by exact, case-insensitive <see
/// cref="HelpTopic.Key"/> match. This is the only authoring path for help
/// content - no file-based alternative (see ADR-0010's Decision Outcome).
/// Aliases/keywords aren't settable via this command yet - deliberately out
/// of v1 scope, see PLAN-0010's Open questions.
/// </summary>
public sealed class HelpTopicEditCommand : ICommand
{
    private readonly IHelpRepository _repository;

    /// <summary>Creates the command, saving through <paramref name="repository"/>.</summary>
    public HelpTopicEditCommand(IHelpRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public string Verb => "helptopic";

    /// <inheritdoc/>
    public IReadOnlyList<string> Aliases { get; } = [];

    /// <inheritdoc/>
    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count < 2)
        {
            await ctx.Session.WriteLineAsync("Usage: helptopic <key> <body>", ct);
            return;
        }

        var key = ctx.Args[0];
        var body = string.Join(' ', ctx.Args.Skip(1));

        var topic = await _repository.FindByNameOrAliasAsync(key, ct) ?? new HelpTopic { Id = HelpTopicId.New(), Key = key };
        topic.Key = key;
        topic.Body = body;
        topic.Touch();

        await _repository.SaveTopicAsync(topic, ct);

        await ctx.Session.WriteLineAsync($"Help topic '{key}' saved.", ct);
    }
}
