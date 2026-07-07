namespace SharpMud.Persistence.Tests;

public sealed class TestInfrastructureSmokeTests
{
    [Theory, PersistenceAutoData]
    public void AutoFixture_ShouldGenerateDeterministicStrings(string value)
    {
        value.Should().NotBeNullOrEmpty();
    }

    public interface IProbe
    {
        string Name { get; }
    }

    [Theory, PersistenceAutoData]
    public void NSubstitute_ShouldAutoMockInterfaces([Frozen] IProbe probe)
    {
        probe.Name.Returns("configured");

        probe.Name.Should().Be("configured");
    }
}
