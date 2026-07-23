using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Builder;

/// <summary>
/// The <c>tunnel &lt;direction&gt; &lt;existing room name&gt;</c> command
/// (<see cref="SecurityRole.MinorBuilder"/>) - wires a two-way exit (via
/// <see cref="RoomConnector.Connect"/>) between the builder's current room
/// and an already-existing room found by exact <see cref="Thing.Name"/>
/// match. Unlike <see cref="DigCommand"/>, creates nothing new.
/// </summary>
public sealed class TunnelCommand : ICommand
{
    private readonly IThingRepository _repository;

    public TunnelCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "tunnel";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count < 2)
        {
            await ctx.Session.WriteLineAsync("Usage: tunnel <direction> <existing room name>", ct);
            return;
        }

        if (!BuilderCommandHelpers.TryParseDirection(ctx.Args[0], out var direction))
        {
            await ctx.Session.WriteLineAsync($"'{ctx.Args[0]}' isn't a direction.", ct);
            return;
        }

        var name = string.Join(' ', ctx.Args.Skip(1));
        var matches = BuilderCommandHelpers.FindRoomsByName(ctx.World, name);

        if (matches.Count == 0)
        {
            await ctx.Session.WriteLineAsync($"No room named {name} was found.", ct);
            return;
        }

        if (matches.Count > 1)
        {
            await ctx.Session.WriteLineAsync($"Ambiguous: {matches.Count} rooms named {name} were found.", ct);
            return;
        }

        var destination = matches[0];
        if (ReferenceEquals(destination, ctx.CurrentRoom))
        {
            await ctx.Session.WriteLineAsync("You can't tunnel a room to itself.", ct);
            return;
        }

        if (BuilderCommandHelpers.HasExit(ctx.CurrentRoom, direction))
        {
            await ctx.Session.WriteLineAsync($"There's already an exit {direction.ToDisplayString()} from here.", ct);
            return;
        }

        if (BuilderCommandHelpers.HasExit(destination, direction.Opposite()))
        {
            await ctx.Session.WriteLineAsync(
                $"{destination.Name} already has an exit {direction.Opposite().ToDisplayString()}.", ct);
            return;
        }

        RoomConnector.Connect(ctx.World, ctx.CurrentRoom, destination, direction);

        // The destination room isn't necessarily in the same tree as the
        // current room (multiple areas) - save both roots, not just one,
        // so the destination's new reverse exit isn't silently dropped.
        var currentRoot = BuilderCommandHelpers.FindRoot(ctx.CurrentRoom);
        await _repository.SaveTreeAsync(currentRoot, ct);

        var destinationRoot = BuilderCommandHelpers.FindRoot(destination);
        if (!ReferenceEquals(destinationRoot, currentRoot))
            await _repository.SaveTreeAsync(destinationRoot, ct);

        await ctx.Session.WriteLineAsync(
            $"You tunnel {direction.ToDisplayString()} to {destination.Name}.", ct);
    }
}
