using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.Commands;

public sealed record CommandContext(
    Thing Actor,
    Thing CurrentRoom,
    IReadOnlyList<string> Args,
    IWorld World,
    ISession Session);
