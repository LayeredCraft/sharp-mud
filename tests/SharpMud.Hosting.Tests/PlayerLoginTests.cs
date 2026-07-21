using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class PlayerLoginTests
{
    private static Thing MakeRoom() => new() { Id = ThingId.New(), Name = "Room" };

    private static (PlayerLogin playerLogin, IThingRepository repository, IPlayerFactory playerFactory) MakePlayerLogin(World world, Thing room)
    {
        var worldContext = new WorldContext();
        worldContext.Initialize(world, room, room);
        var repository = Substitute.For<IThingRepository>();
        var playerFactory = Substitute.For<IPlayerFactory>();

        return (new PlayerLogin(worldContext, repository, playerFactory), repository, playerFactory);
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ReturnsAlreadyOnlinePlayer_WithoutTouchingRepositoryOrFactory()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var player = new Thing { Id = ThingId.New(), Name = "Adventurer" };
        player.Behaviors.Add(new PlayerBehavior { Username = "Adventurer", PasswordHash = "hash" });
        room.Add(player);
        world.Register(player);

        var (playerLogin, repository, playerFactory) = MakePlayerLogin(world, room);

        var result = await playerLogin.ResolveOrCreateAsync("Adventurer", TestContext.Current.CancellationToken);

        result.Should().Be(player);
        await repository.DidNotReceive().FindPlayerByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        playerFactory.DidNotReceive().CreatePlayer(Arg.Any<World>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Thing>());
    }

    [Fact]
    public async Task ResolveOrCreateAsync_ReattachesPersistedPlayer_ToLiveStartingRoom()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var loaded = new Thing { Id = ThingId.New(), Name = "Adventurer" };
        loaded.Behaviors.Add(new PlayerBehavior { Username = "Adventurer", PasswordHash = "hash" });

        var (playerLogin, repository, playerFactory) = MakePlayerLogin(world, room);
        repository.FindPlayerByUsernameAsync("Adventurer", Arg.Any<CancellationToken>()).Returns(loaded);

        var result = await playerLogin.ResolveOrCreateAsync("Adventurer", TestContext.Current.CancellationToken);

        result.Should().Be(loaded);
        room.Children.Should().Contain(loaded);
        world.GetThing(loaded.Id).Should().Be(loaded);
        playerFactory.DidNotReceive().CreatePlayer(Arg.Any<World>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Thing>());
    }

    [Fact]
    public async Task ResolveOrCreateAsync_CreatesFreshPlayer_ViaPlayerFactory_WhenNoneFound()
    {
        var world = new World();
        var room = MakeRoom();
        world.Register(room);

        var (playerLogin, repository, playerFactory) = MakePlayerLogin(world, room);
        repository.FindPlayerByUsernameAsync("Adventurer", Arg.Any<CancellationToken>()).Returns((Thing?)null);

        var created = new Thing { Id = ThingId.New(), Name = "Adventurer" };
        playerFactory.CreatePlayer(world, "Adventurer", Arg.Any<string>(), room).Returns(created);

        var result = await playerLogin.ResolveOrCreateAsync("Adventurer", TestContext.Current.CancellationToken);

        result.Should().Be(created);
    }
}
