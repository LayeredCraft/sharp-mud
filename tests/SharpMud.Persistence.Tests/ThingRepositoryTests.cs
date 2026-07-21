using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Persistence.Tests.TestKit;
using SharpMud.Samples.Classic;

namespace SharpMud.Persistence.Tests;

public sealed class ThingRepositoryTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ThingRepository _sut;

    public ThingRepositoryTests()
    {
        _sut = new ThingRepository(_factory);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SaveTreeAsync_ThenLoadTreeAsync_RoundTripsRoomWithExit()
    {
        var origin = new Thing { Id = ThingId.New(), Name = "Origin", Description = "The origin room." };
        origin.Behaviors.Add(new RoomBehavior());

        var destination = new Thing { Id = ThingId.New(), Name = "Destination", Description = "The far room." };
        destination.Behaviors.Add(new RoomBehavior());

        var exit = new Thing { Id = ThingId.New(), Name = "north" };
        exit.Behaviors.Add(new ExitBehavior { Direction = Direction.North, Destination = destination });
        origin.Add(exit);

        // destination must exist as its own row for Destination to resolve on load
        await _sut.SaveTreeAsync(destination, TestContext.Current.CancellationToken);
        await _sut.SaveTreeAsync(origin, TestContext.Current.CancellationToken);

        var loadedOrigin = await _sut.LoadTreeAsync(origin.Id, TestContext.Current.CancellationToken);

        loadedOrigin.Should().NotBeNull();
        loadedOrigin!.Name.Should().Be("Origin");
        loadedOrigin.Description.Should().Be("The origin room.");

        var loadedExit = loadedOrigin.Children.Should().ContainSingle().Subject;
        var exitBehavior = loadedExit.FindBehavior<ExitBehavior>();
        exitBehavior.Should().NotBeNull();
        exitBehavior!.Direction.Should().Be(Direction.North);
        exitBehavior.Destination.Id.Should().Be(destination.Id);
        exitBehavior.Destination.Name.Should().Be("Destination");
    }

    [Fact]
    public async Task SaveTreeAsync_ThenLoadTreeAsync_RoundTripsPlayerWithStatsAndCarriedItem()
    {
        var player = new Thing { Id = ThingId.New(), Name = "Hero" };
        player.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash" });
        player.Behaviors.Add(new StatsBehavior
        {
            Race = Race.Elf,
            Class = CharacterClass.Mage,
            Strength = 8,
            Dexterity = 14,
            Constitution = 10,
            Intelligence = 16,
            Wisdom = 12,
            Charisma = 11,
            MaxHitPoints = 18,
            CurrentHitPoints = 15,
            Level = 3,
            Experience = 450,
        });
        player.Behaviors.Add(new CombatantBehavior
        {
            MaxHitPoints = 18,
            CurrentHitPoints = 15,
            ArmorClass = 12,
            DamageMin = 2,
            DamageMax = 6,
        });

        var item = new Thing { Id = ThingId.New(), Name = "gold coin" };
        item.Behaviors.Add(new ItemBehavior());
        player.Add(item);

        await _sut.SaveTreeAsync(player, TestContext.Current.CancellationToken);

        var loaded = await _sut.LoadTreeAsync(player.Id, TestContext.Current.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.HasBehavior<PlayerBehavior>().Should().BeTrue();

        var stats = loaded.FindBehavior<StatsBehavior>();
        stats.Should().NotBeNull();
        stats!.Race.Should().Be(Race.Elf);
        stats.Class.Should().Be(CharacterClass.Mage);
        stats.Intelligence.Should().Be(16);
        stats.Level.Should().Be(3);
        stats.Experience.Should().Be(450);

        var combatant = loaded.FindBehavior<CombatantBehavior>();
        combatant.Should().NotBeNull();
        combatant!.ArmorClass.Should().Be(12);

        var carriedItem = loaded.Children.Should().ContainSingle().Subject;
        carriedItem.Name.Should().Be("gold coin");
        carriedItem.HasBehavior<ItemBehavior>().Should().BeTrue();
    }

    [Fact]
    public async Task FindPlayerByUsernameAsync_ReturnsMatchingPlayer()
    {
        var player = new Thing { Id = ThingId.New(), Name = "Findable" };
        player.Behaviors.Add(new PlayerBehavior { Username = "Findable", PasswordHash = "test-hash" });
        await _sut.SaveTreeAsync(player, TestContext.Current.CancellationToken);

        var found = await _sut.FindPlayerByUsernameAsync("Findable", TestContext.Current.CancellationToken);

        found.Should().NotBeNull();
        found!.Id.Should().Be(player.Id);
    }

    [Fact]
    public async Task FindPlayerByUsernameAsync_ReturnsNull_WhenNoMatch()
    {
        var found = await _sut.FindPlayerByUsernameAsync("Nobody", TestContext.Current.CancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task FindPlayerByUsernameAsync_IgnoresNonPlayerThingsWithMatchingName()
    {
        var room = new Thing { Id = ThingId.New(), Name = "SameName" };
        room.Behaviors.Add(new RoomBehavior());
        await _sut.SaveTreeAsync(room, TestContext.Current.CancellationToken);

        var found = await _sut.FindPlayerByUsernameAsync("SameName", TestContext.Current.CancellationToken);

        found.Should().BeNull();
    }

    [Fact]
    public async Task SaveTreeAsync_CalledTwice_ReflectsUpdatedState()
    {
        var npc = new Thing { Id = ThingId.New(), Name = "cave rat" };
        npc.Behaviors.Add(new NpcBehavior());
        npc.Behaviors.Add(new CombatantBehavior { MaxHitPoints = 6, CurrentHitPoints = 6, ArmorClass = 8 });

        await _sut.SaveTreeAsync(npc, TestContext.Current.CancellationToken);

        npc.FindBehavior<CombatantBehavior>()!.CurrentHitPoints = 2;
        await _sut.SaveTreeAsync(npc, TestContext.Current.CancellationToken);

        var loaded = await _sut.LoadTreeAsync(npc.Id, TestContext.Current.CancellationToken);

        loaded!.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().Be(2);
    }

    [Fact]
    public async Task SaveTreeAsync_ThenLoadTreeAsync_RoundTripsLockableExit()
    {
        var origin = new Thing { Id = ThingId.New(), Name = "Origin" };
        origin.Behaviors.Add(new RoomBehavior());

        var destination = new Thing { Id = ThingId.New(), Name = "Destination" };
        destination.Behaviors.Add(new RoomBehavior());

        var key = new Thing { Id = ThingId.New(), Name = "brass key" };
        key.Behaviors.Add(new ItemBehavior());

        var exit = new Thing { Id = ThingId.New(), Name = "north" };
        exit.Behaviors.Add(new ExitBehavior { Direction = Direction.North, Destination = destination });
        exit.Behaviors.Add(new LockableBehavior { IsLocked = true, RequiredKey = key });
        origin.Add(exit);

        await _sut.SaveTreeAsync(destination, TestContext.Current.CancellationToken);
        await _sut.SaveTreeAsync(key, TestContext.Current.CancellationToken);
        await _sut.SaveTreeAsync(origin, TestContext.Current.CancellationToken);

        var loadedOrigin = await _sut.LoadTreeAsync(origin.Id, TestContext.Current.CancellationToken);

        var loadedExit = loadedOrigin!.Children.Should().ContainSingle().Subject;
        var lockable = loadedExit.FindBehavior<LockableBehavior>();
        lockable.Should().NotBeNull();
        lockable!.IsLocked.Should().BeTrue();
        lockable.RequiredKey.Should().NotBeNull();
        lockable.RequiredKey!.Id.Should().Be(key.Id);
    }

    [Fact]
    public async Task LoadTreeAsync_ReturnsNull_WhenRootDoesNotExist()
    {
        var result = await _sut.LoadTreeAsync(ThingId.New(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }
}
