using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Ruleset.Basic.Tests;

public sealed class BasicCombatOutcomeHandlerTests
{
    [Fact]
    public async Task OnVictoryAsync_AwardsExperienceFromCombatantReward()
    {
        var session = Substitute.For<ISession>();
        var victor = new Thing { Id = ThingId.New(), Name = "Hero" };
        victor.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        victor.Behaviors.Add(new BasicStatsBehavior { Experience = 0 });

        var defeated = new Thing { Id = ThingId.New(), Name = "wild boar" };
        defeated.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10 });

        var worldContext = new WorldContext();
        var sut = new BasicCombatOutcomeHandler(worldContext);

        await sut.OnVictoryAsync(victor, defeated, TestContext.Current.CancellationToken);

        victor.FindBehavior<BasicStatsBehavior>()!.Experience.Should().Be(10);
    }

    [Fact]
    public async Task OnDefeatAsync_AppliesXpLossAndHalvesCombatHitPoints_AndReturnsStartingRoom()
    {
        var session = Substitute.For<ISession>();
        var defeated = new Thing { Id = ThingId.New(), Name = "Hero" };
        defeated.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        defeated.Behaviors.Add(new BasicStatsBehavior { Experience = 100 });
        defeated.Behaviors.Add(new CombatantBehavior { MaxHitPoints = 20, CurrentHitPoints = 20 });

        var victor = new Thing { Id = ThingId.New(), Name = "wild boar" };

        var startingRoom = new Thing { Id = ThingId.New(), Name = "Clearing" };
        startingRoom.Behaviors.Add(new RoomBehavior());
        var worldContext = new WorldContext();
        worldContext.Initialize(new World(), startingRoom, startingRoom);

        var sut = new BasicCombatOutcomeHandler(worldContext);

        var destination = await sut.OnDefeatAsync(defeated, victor, TestContext.Current.CancellationToken);

        destination.Should().Be(startingRoom);
        defeated.FindBehavior<BasicStatsBehavior>()!.Experience.Should().Be(90);

        // The value CombatResolver actually reads/writes during combat -
        // BasicStatsBehavior carries no HP field of its own, so this is the
        // only assertion that would catch the death penalty regressing to
        // a no-op (full-HP respawn).
        defeated.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().Be(10);
    }
}
