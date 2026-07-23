using AutoFixture;
using AutoFixture.Xunit3;

namespace SharpMud.Ruleset.Basic.Tests.TestKit.Attributes;

public sealed class BasicAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    internal static IFixture CreateFixture() => BaseFixtureFactory.CreateFixture();
}

public sealed class InlineBasicAutoDataAttribute(params object[] values)
    : InlineAutoDataAttribute(BasicAutoDataAttribute.CreateFixture, values);
