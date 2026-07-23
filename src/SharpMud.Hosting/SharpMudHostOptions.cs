namespace SharpMud.Hosting;

// Deployment config read from environment variables - stays manually
// parsed rather than IOptions<T>-bound, per security.md's reasoning for
// keeping deployment config env-var-only. Transport selection
// (SHARPMUD_MODE/SHARPMUD_TELNET_PORT/--telnet) is not part of this type -
// it moved to the consumer's own composition root, since Hosting must not
// know which transport(s) a consumer wants (docs/adr/0006-nuget-package-distribution.md).
// Named SharpMudHostOptions, not HostOptions - the latter collides with
// Microsoft.Extensions.Hosting.HostOptions, a real BCL type every consumer
// of this package will also have in scope.
public sealed record SharpMudHostOptions(string DbPath, string? InitialAdminUsername = null)
{
    public static SharpMudHostOptions Parse(IReadOnlyDictionary<string, string?> env)
    {
        var dbPath = env.GetValueOrDefault("SHARPMUD_DB_PATH") ?? "./sharpmud.db";

        // ADR-0005 bootstrap - the only path to a FullAdmin on a fresh
        // deployment, since granting a role itself requires FullAdmin.
        // Consumed by LoginFlow, not parsed/threaded through per-connection
        // context types (see docs/plans/0005-security-role-model-and-moderation-commands.md).
        var initialAdminUsername = env.GetValueOrDefault("SHARPMUD_INITIAL_ADMIN");

        return new SharpMudHostOptions(dbPath, initialAdminUsername);
    }
}
