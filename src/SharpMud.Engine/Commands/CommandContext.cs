using SharpMud.Engine.Characters;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.World;

namespace SharpMud.Engine.Commands;

public sealed record CommandContext(
    Player Actor,
    Room CurrentRoom,
    IReadOnlyList<string> Args,
    IWorld World,
    ISession Session);
