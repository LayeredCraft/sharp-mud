using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Engine.Ticking;

namespace SharpMud.Engine.Tests.Sessions;

public sealed class LinkdeadSweeperTests
{
    private static Thing MakePlayer(string name, ConnectionState state, DateTimeOffset? linkdeadSince)
    {
        var player = new Thing { Id = ThingId.New(), Name = name };
        var behavior = new PlayerBehavior { Username = name, PasswordHash = "test-hash" };
        if (state == ConnectionState.Linkdead)
            behavior.EnterLinkdead(linkdeadSince!.Value);
        player.Behaviors.Add(behavior);
        return player;
    }

    [Fact]
    public async Task OnTickAsync_RemovesPlayer_WhenLinkdeadPastGraceWindow()
    {
        var world = new World();
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = MakePlayer("Hero", ConnectionState.Linkdead, DateTimeOffset.UtcNow - ReconnectPolicy.GraceWindow - TimeSpan.FromSeconds(1));
        room.Add(player);
        world.Register(room);
        world.Register(player);

        var repository = Substitute.For<IThingRepository>();
        var sut = new LinkdeadSweeper(world, repository);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        room.Children.Should().NotContain(player);
        world.GetThing(player.Id).Should().BeNull();
        await repository.Received(1).SaveTreeAsync(player, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnTickAsync_LeavesPlayerAlone_WhenLinkdeadWithinGraceWindow()
    {
        var world = new World();
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = MakePlayer("Hero", ConnectionState.Linkdead, DateTimeOffset.UtcNow);
        room.Add(player);
        world.Register(room);
        world.Register(player);

        var repository = Substitute.For<IThingRepository>();
        var sut = new LinkdeadSweeper(world, repository);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        room.Children.Should().Contain(player);
        world.GetThing(player.Id).Should().Be(player);
        await repository.DidNotReceive().SaveTreeAsync(Arg.Any<Thing>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnTickAsync_LeavesPlayerAlone_WhenStillPlaying()
    {
        var world = new World();
        var room = new Thing { Id = ThingId.New(), Name = "Room" };
        var player = MakePlayer("Hero", ConnectionState.Playing, null);
        room.Add(player);
        world.Register(room);
        world.Register(player);

        var repository = Substitute.For<IThingRepository>();
        var sut = new LinkdeadSweeper(world, repository);

        await sut.OnTickAsync(new TickContext(DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        room.Children.Should().Contain(player);
        world.GetThing(player.Id).Should().Be(player);
    }
}
