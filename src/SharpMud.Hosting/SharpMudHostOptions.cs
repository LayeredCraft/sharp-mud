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
public sealed record SharpMudHostOptions(string DbPath)
{
    public static SharpMudHostOptions Parse(IReadOnlyDictionary<string, string?> env)
    {
        var dbPath = env.GetValueOrDefault("SHARPMUD_DB_PATH") ?? "./sharpmud.db";

        return new SharpMudHostOptions(dbPath);
    }
}
