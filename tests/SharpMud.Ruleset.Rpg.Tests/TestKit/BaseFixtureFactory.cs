using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace SharpMud.Ruleset.Rpg.Tests.TestKit;

public static class BaseFixtureFactory
{
    public static IFixture CreateFixture(Action<IFixture>? customizeAction = null)
    {
        var fixture = new Fixture();

        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        fixture.Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });

        customizeAction?.Invoke(fixture);

        return fixture;
    }
}
