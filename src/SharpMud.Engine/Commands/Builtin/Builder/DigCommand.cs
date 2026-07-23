using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Builder;

/// <summary>
/// The <c>dig &lt;direction&gt; &lt;new room name&gt;</c> command (<see
/// cref="SecurityRole.MinorBuilder"/>) - creates a new room (empty
/// description, set afterward via <see cref="DescribeCommand"/>) as a
/// sibling of the builder's current room, and wires a two-way exit between
/// them via <see cref="RoomConnector.Connect"/>.
/// </summary>
public sealed class DigCommand : ICommand
{
    private readonly IThingRepository _repository;

    public DigCommand(IThingRepository repository)
    {
        _repository = repository;
    }

    public string Verb => "dig";
    public IReadOnlyList<string> Aliases { get; } = [];

    public async Task ExecuteAsync(CommandContext ctx, CancellationToken ct)
    {
        if (ctx.Args.Count < 2)
        {
            await ctx.Session.WriteLineAsync("Usage: dig <direction> <new room name>", ct);
            return;
        }

        if (!BuilderCommandHelpers.TryParseDirection(ctx.Args[0], out var direction))
        {
            await ctx.Session.WriteLineAsync($"'{ctx.Args[0]}' isn't a direction.", ct);
            return;
        }

        if (BuilderCommandHelpers.HasExit(ctx.CurrentRoom, direction))
        {
            await ctx.Session.WriteLineAsync($"There's already an exit {direction.ToDisplayString()} from here.", ct);
            return;
        }

        var name = string.Join(' ', ctx.Args.Skip(1));

        if (ctx.CurrentRoom.Parent is not { } area)
        {
            await ctx.Session.WriteLineAsync("The current room has nowhere to attach a new room to.", ct);
            return;
        }

        var room = new Thing { Id = ThingId.New(), Name = name, Description = "" };
        room.Behaviors.Add(new RoomBehavior());
        area.Add(room);
        ctx.World.Register(room);

        RoomConnector.Connect(ctx.World, ctx.CurrentRoom, room, direction);

        var root = BuilderCommandHelpers.FindRoot(ctx.CurrentRoom);
        await _repository.SaveTreeAsync(root, ct);

        await ctx.Session.WriteLineAsync(
            $"You dig {direction.ToDisplayString()}, creating {name}.", ct);
    }
}
