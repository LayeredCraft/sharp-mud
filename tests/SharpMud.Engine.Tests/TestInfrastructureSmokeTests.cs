namespace SharpMud.Engine.Tests;

public sealed class TestInfrastructureSmokeTests
{
    [Theory, EngineAutoData]
    public void AutoFixture_ShouldGenerateDeterministicStrings(string value)
    {
        value.Should().NotBeNullOrEmpty();
    }

    public interface IProbe
    {
        string Name { get; }
    }

    [Theory, EngineAutoData]
    public void NSubstitute_ShouldAutoMockInterfaces([Frozen] IProbe probe)
    {
        probe.Name.Returns("configured");

        probe.Name.Should().Be("configured");
    }
}
