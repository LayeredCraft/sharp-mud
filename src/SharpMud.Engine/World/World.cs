using SharpMud.Engine.Characters;
using SharpMud.Engine.Sessions;

namespace SharpMud.Engine.World;

// v1 in-memory implementation. Persistence (docs/persistence.md) is a later
// build-order phase - this is the "hand-built hub, no DB yet" version used
// to get movement/look/chat working end-to-end (docs/commands.md).
public sealed class World : IWorld
{
    private readonly Dictionary<RoomId, Room> _rooms = [];
    private readonly Dictionary<PlayerId, Player> _players = [];
    private readonly Dictionary<PlayerId, ISession> _sessions = [];
    private readonly Dictionary<NpcId, Npc> _npcs = [];

    public void RegisterRoom(Room room) => _rooms[room.Id] = room;

    public void RegisterNpc(Npc npc)
    {
        _npcs[npc.Id] = npc;
        GetRoom(npc.RoomId)?.Npcs.Add(npc.Id);
    }

    public Npc? GetNpc(NpcId id) => _npcs.GetValueOrDefault(id);

    public void RemoveNpc(NpcId id)
    {
        if (_npcs.Remove(id, out var npc))
            GetRoom(npc.RoomId)?.Npcs.Remove(id);
    }

    public void Connect(Player player, ISession session)
    {
        _players[player.Id] = player;
        _sessions[player.Id] = session;
    }

    public void Disconnect(PlayerId playerId)
    {
        _players.Remove(playerId);
        _sessions.Remove(playerId);
    }

    public Room? GetRoom(RoomId id) => _rooms.GetValueOrDefault(id);

    public Player? GetPlayer(PlayerId id) => _players.GetValueOrDefault(id);

    public IEnumerable<Player> PlayersInRoom(RoomId id) =>
        _players.Values.Where(player => player.CurrentRoomId == id);

    public IEnumerable<Player> AllPlayers => _players.Values;

    public ISession? GetSession(PlayerId playerId) => _sessions.GetValueOrDefault(playerId);

    public async Task MovePlayerAsync(Player player, Room from, Room to, Direction? direction, CancellationToken ct)
    {
        player.CurrentRoomId = to.Id;

        var leaveMessage = direction is { } d
            ? $"{player.Name} leaves {d.ToDisplayString()}."
            : $"{player.Name} leaves.";

        foreach (var occupant in PlayersInRoom(from.Id).Where(p => p.Id != player.Id))
        {
            if (_sessions.TryGetValue(occupant.Id, out var session))
                await session.WriteLineAsync(leaveMessage, ct);
        }

        foreach (var occupant in PlayersInRoom(to.Id).Where(p => p.Id != player.Id))
        {
            if (_sessions.TryGetValue(occupant.Id, out var session))
                await session.WriteLineAsync($"{player.Name} arrives.", ct);
        }
    }
}
