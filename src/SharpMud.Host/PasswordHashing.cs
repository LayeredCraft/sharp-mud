using Microsoft.AspNetCore.Identity;

namespace SharpMud.Host;

// Wraps Microsoft.Extensions.Identity.Core's PasswordHasher<TUser> - PBKDF2
// with a random salt, versioned hash format. Lives in Host, not Engine -
// this is login-flow infrastructure, not game logic, and pulling in an
// Identity package into Engine would be an unwarranted dependency for
// something only the networked login flow needs (docs/accounts-auth.md).
// TUser is unused by the default hasher's algorithm; Thing is a reasonable
// stand-in rather than inventing an unused marker type.
public static class PasswordHashing
{
    private static readonly PasswordHasher<object> Hasher = new();

    public static string Hash(string password) => Hasher.HashPassword(new object(), password);

    public static bool Verify(string hash, string password) =>
        Hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;
}
