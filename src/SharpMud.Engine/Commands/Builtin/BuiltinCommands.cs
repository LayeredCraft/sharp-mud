using SharpMud.Engine.Behaviors;

namespace SharpMud.Engine.Commands.Builtin;

// Ruleset-agnostic commands only - kill/attack/flee (and anything else that
// depends on a ruleset-specific behavior) register themselves separately;
// see SharpMud.Ruleset.Rpg's equivalent registration, called by Hosting's
// AddSharpMudRuleset alongside this one. Admin/moderation commands
// (ADR-0005) also register separately (SharpMud.Engine.Commands.Builtin
// .Admin.AdminCommands) - they're role-gated, not ruleset-specific, but
// keeping them out of this method keeps "the commands every consumer gets
// with zero configuration" and "the commands a consumer opts into wiring up
// themselves" visibly separate.
public static class BuiltinCommands
{
    public static void RegisterAll(ICommandRegistry registry)
    {
        registry.RegisterOpen(new MoveCommand(Direction.North, "north", ["n"]));
        registry.RegisterOpen(new MoveCommand(Direction.South, "south", ["s"]));
        registry.RegisterOpen(new MoveCommand(Direction.East, "east", ["e"]));
        registry.RegisterOpen(new MoveCommand(Direction.West, "west", ["w"]));
        registry.RegisterOpen(new MoveCommand(Direction.NorthEast, "northeast", ["ne"]));
        registry.RegisterOpen(new MoveCommand(Direction.NorthWest, "northwest", ["nw"]));
        registry.RegisterOpen(new MoveCommand(Direction.SouthEast, "southeast", ["se"]));
        registry.RegisterOpen(new MoveCommand(Direction.SouthWest, "southwest", ["sw"]));
        registry.RegisterOpen(new MoveCommand(Direction.Up, "up", ["u"]));
        registry.RegisterOpen(new MoveCommand(Direction.Down, "down", ["d"]));

        registry.RegisterOpen(new LookCommand());
        // Mute enforcement (ADR-0005) is an Engine-level concern, not
        // per-consumer - wrapping here, not in each ruleset's own
        // registration, is what makes mute universal.
        registry.RegisterOpen(new MuteGuardedCommand(new SayCommand()));
        registry.RegisterOpen(new MuteGuardedCommand(new EmoteCommand()));
        registry.RegisterOpen(new WhoCommand());
        registry.RegisterOpen(new QuitCommand());
        registry.RegisterOpen(new GetCommand());
        registry.RegisterOpen(new DropCommand());
        registry.RegisterOpen(new WearCommand());
        registry.RegisterOpen(new RemoveCommand());
        registry.RegisterOpen(new InventoryCommand());
        registry.RegisterOpen(new GiveCommand());
        registry.RegisterOpen(new HelpCommand(registry));
    }
}
