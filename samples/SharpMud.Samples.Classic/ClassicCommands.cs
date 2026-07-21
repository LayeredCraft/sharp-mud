using SharpMud.Engine.Commands;
using SharpMud.Engine.Core;

namespace SharpMud.Samples.Classic;

// Called by Host alongside SharpMud.Engine.Commands.Builtin.BuiltinCommands.RegisterAll -
// these commands depend on ruleset-specific behaviors (CombatantBehavior),
// which is exactly why they live here instead of Engine.
public static class ClassicCommands
{
    public static void RegisterAll(ICommandRegistry registry, ICombatManager combatManager, IRandomSource random)
    {
        registry.Register(new AttackCommand(combatManager));
        registry.Register(new FleeCommand(combatManager, random));
    }
}
