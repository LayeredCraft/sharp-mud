using SharpMud.Engine.Help;

namespace SharpMud.Engine.Tests.Help;

public sealed class StubEmbeddingProviderTests
{
    private readonly StubEmbeddingProvider _sut = new();

    [Fact]
    public async Task EmbedAsync_ReturnsIdenticalVectors_ForIdenticalText()
    {
        var first = await _sut.EmbedAsync("how do I become a wizard", TestContext.Current.CancellationToken);
        var second = await _sut.EmbedAsync("how do I become a wizard", TestContext.Current.CancellationToken);

        first.Should().BeEquivalentTo(second, "the stub provider must be deterministic across calls");
    }

    [Fact]
    public async Task EmbedAsync_ReturnsIdenticalVectors_IgnoringCase()
    {
        var lower = await _sut.EmbedAsync("wizard magic", TestContext.Current.CancellationToken);
        var upper = await _sut.EmbedAsync("WIZARD MAGIC", TestContext.Current.CancellationToken);

        lower.Should().BeEquivalentTo(upper);
    }

    [Fact]
    public async Task EmbedAsync_ReturnsDifferentVectors_ForDisjointText()
    {
        var wizard = await _sut.EmbedAsync("wizard", TestContext.Current.CancellationToken);
        var shopkeeper = await _sut.EmbedAsync("shopkeeper commerce", TestContext.Current.CancellationToken);

        wizard.Should().NotBeEquivalentTo(shopkeeper);
    }

    [Fact]
    public async Task EmbedAsync_ReturnsNormalizedVector()
    {
        var vector = await _sut.EmbedAsync("wizard magic spell", TestContext.Current.CancellationToken);

        var magnitude = Math.Sqrt(vector.Sum(v => (double)v * v));
        magnitude.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public async Task EmbedAsync_ReturnsZeroVector_ForEmptyText()
    {
        var vector = await _sut.EmbedAsync("", TestContext.Current.CancellationToken);

        vector.Should().OnlyContain(v => v == 0f);
    }
}
