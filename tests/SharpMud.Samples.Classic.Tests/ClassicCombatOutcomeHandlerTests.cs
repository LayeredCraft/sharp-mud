using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;
using SharpMud.Hosting;
using SharpMud.Ruleset.Rpg;

namespace SharpMud.Samples.Classic.Tests;

public sealed class ClassicCombatOutcomeHandlerTests
{
    [Fact]
    public async Task OnVictoryAsync_AwardsExperienceFromCombatantReward()
    {
        var session = Substitute.For<ISession>();
        var victor = new Thing { Id = ThingId.New(), Name = "Hero" };
        victor.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        victor.Behaviors.Add(new StatsBehavior { Experience = 0 });

        var defeated = new Thing { Id = ThingId.New(), Name = "cave rat" };
        defeated.Behaviors.Add(new CombatantBehavior { ExperienceReward = 10 });

        var worldContext = new WorldContext();
        var sut = new ClassicCombatOutcomeHandler(worldContext);

        await sut.OnVictoryAsync(victor, defeated, TestContext.Current.CancellationToken);

        victor.FindBehavior<StatsBehavior>()!.Experience.Should().Be(10);
    }

    [Fact]
    public async Task OnDefeatAsync_AppliesXpLossAndHitPointHalving_AndReturnsHubRoom()
    {
        var session = Substitute.For<ISession>();
        var defeated = new Thing { Id = ThingId.New(), Name = "Hero" };
        defeated.Behaviors.Add(new PlayerBehavior { Username = "TestUser", PasswordHash = "test-hash", Session = session });
        defeated.Behaviors.Add(new StatsBehavior { Experience = 100, MaxHitPoints = 20 });
        defeated.Behaviors.Add(new CombatantBehavior { MaxHitPoints = 20, CurrentHitPoints = 20 });

        var victor = new Thing { Id = ThingId.New(), Name = "cave rat" };

        var hubRoom = new Thing { Id = ThingId.New(), Name = "Hub" };
        hubRoom.Behaviors.Add(new RoomBehavior());
        var worldContext = new WorldContext();
        worldContext.Initialize(new World(), hubRoom, hubRoom);

        var sut = new ClassicCombatOutcomeHandler(worldContext);

        var destination = await sut.OnDefeatAsync(defeated, victor, TestContext.Current.CancellationToken);

        destination.Should().Be(hubRoom);
        defeated.FindBehavior<StatsBehavior>()!.Experience.Should().Be(90);
        defeated.FindBehavior<StatsBehavior>()!.CurrentHitPoints.Should().Be(10);

        // The value CombatResolver actually reads/writes during combat -
        // the whole point of the ADR-0008-extraction bug fix. Asserting
        // only StatsBehavior's copy (above) would still pass even if the
        // CombatantBehavior halving were deleted.
        defeated.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().Be(10);
    }
}
