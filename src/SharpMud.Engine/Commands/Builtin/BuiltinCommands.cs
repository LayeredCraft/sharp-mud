using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

// Ruleset-agnostic commands only - kill/attack/flee (and anything else that
// depends on a ruleset-specific behavior) register themselves separately;
// see SharpMud.Ruleset.Classic's equivalent registration method, called by
// Host alongside this one.
public static class BuiltinCommands
{
    public static void RegisterAll(ICommandRegistry registry)
    {
        registry.Register(new MoveCommand(Direction.North, "north", ["n"]));
        registry.Register(new MoveCommand(Direction.South, "south", ["s"]));
        registry.Register(new MoveCommand(Direction.East, "east", ["e"]));
        registry.Register(new MoveCommand(Direction.West, "west", ["w"]));
        registry.Register(new MoveCommand(Direction.NorthEast, "northeast", ["ne"]));
        registry.Register(new MoveCommand(Direction.NorthWest, "northwest", ["nw"]));
        registry.Register(new MoveCommand(Direction.SouthEast, "southeast", ["se"]));
        registry.Register(new MoveCommand(Direction.SouthWest, "southwest", ["sw"]));
        registry.Register(new MoveCommand(Direction.Up, "up", ["u"]));
        registry.Register(new MoveCommand(Direction.Down, "down", ["d"]));

        registry.Register(new LookCommand());
        registry.Register(new SayCommand());
        registry.Register(new EmoteCommand());
        registry.Register(new WhoCommand());
        registry.Register(new QuitCommand());
        registry.Register(new GetCommand());
        registry.Register(new DropCommand());
        registry.Register(new WearCommand());
        registry.Register(new RemoveCommand());
        registry.Register(new InventoryCommand());
        registry.Register(new GiveCommand());
        registry.Register(new HelpCommand(registry));
    }
}
