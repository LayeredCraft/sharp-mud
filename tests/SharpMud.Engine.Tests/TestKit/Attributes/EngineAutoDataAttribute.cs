using AutoFixture;
using AutoFixture.Xunit3;

namespace SharpMud.Engine.Tests.TestKit.Attributes;

public sealed class EngineAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    internal static IFixture CreateFixture() => BaseFixtureFactory.CreateFixture();
}

public sealed class InlineEngineAutoDataAttribute(params object[] values)
    : InlineAutoDataAttribute(EngineAutoDataAttribute.CreateFixture, values);
