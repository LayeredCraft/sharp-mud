using AutoFixture;
using AutoFixture.Xunit3;

namespace SharpMud.Ruleset.Classic.Tests.TestKit.Attributes;

public sealed class ClassicAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    internal static IFixture CreateFixture() => BaseFixtureFactory.CreateFixture();
}

public sealed class InlineClassicAutoDataAttribute(params object[] values)
    : InlineAutoDataAttribute(ClassicAutoDataAttribute.CreateFixture, values);
