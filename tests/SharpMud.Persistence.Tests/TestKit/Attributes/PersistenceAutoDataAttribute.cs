using AutoFixture;
using AutoFixture.Xunit3;

namespace SharpMud.Persistence.Tests.TestKit.Attributes;

public sealed class PersistenceAutoDataAttribute() : AutoDataAttribute(CreateFixture)
{
    internal static IFixture CreateFixture() => BaseFixtureFactory.CreateFixture();
}

public sealed class InlinePersistenceAutoDataAttribute(params object[] values)
    : InlineAutoDataAttribute(PersistenceAutoDataAttribute.CreateFixture, values);
