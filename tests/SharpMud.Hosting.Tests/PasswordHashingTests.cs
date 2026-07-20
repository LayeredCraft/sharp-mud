using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class PasswordHashingTests
{
    [Fact]
    public void Verify_ReturnsTrue_ForTheCorrectPassword()
    {
        var hash = PasswordHashing.Hash("correct horse battery staple");

        PasswordHashing.Verify(hash, "correct horse battery staple").Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForTheWrongPassword()
    {
        var hash = PasswordHashing.Hash("correct horse battery staple");

        PasswordHashing.Verify(hash, "wrong password").Should().BeFalse();
    }

    [Fact]
    public void Hash_ProducesDifferentOutput_ForTheSamePasswordEachTime()
    {
        // Random salt per hash - two hashes of the same password should not
        // be byte-identical, even though both verify correctly.
        var hash1 = PasswordHashing.Hash("same password");
        var hash2 = PasswordHashing.Hash("same password");

        hash1.Should().NotBe(hash2);
        PasswordHashing.Verify(hash1, "same password").Should().BeTrue();
        PasswordHashing.Verify(hash2, "same password").Should().BeTrue();
    }
}
