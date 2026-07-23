using AutoFixture;
using AutoFixture.Xunit3;

namespace SharpMud.Ruleset.Rpg.Tests.TestKit.Attributes;

public sealed class RpgAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    internal static IFixture CreateFixture() => BaseFixtureFactory.CreateFixture();
}

public sealed class InlineRpgAutoDataAttribute(params object[] values)
    : InlineAutoDataAttribute(RpgAutoDataAttribute.CreateFixture, values);
