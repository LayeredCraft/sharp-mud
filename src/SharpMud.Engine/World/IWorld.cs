using SharpMud.Engine.Characters;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.World;

public interface IWorld
{
    Room? GetRoom(RoomId id);
    Player? GetPlayer(PlayerId id);
    IEnumerable<Player> PlayersInRoom(RoomId id);
    IEnumerable<Player> AllPlayers { get; }
    ISession? GetSession(PlayerId playerId);

    Npc? GetNpc(NpcId id);
    void RemoveNpc(NpcId id);

    Task MovePlayerAsync(Player player, Room from, Room to, Direction? direction, CancellationToken ct);
}
