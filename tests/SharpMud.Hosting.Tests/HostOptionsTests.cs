using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class HostOptionsTests
{
    [Fact]
    public void Parse_DefaultsDbPath_WhenEnvVarNotSet()
    {
        var options = HostOptions.Parse(new Dictionary<string, string?>());

        options.DbPath.Should().Be("./sharpmud.db");
    }

    [Fact]
    public void Parse_UsesDbPathFromEnvVar_WhenSet()
    {
        var env = new Dictionary<string, string?> { ["SHARPMUD_DB_PATH"] = "/data/sharpmud.db" };

        var options = HostOptions.Parse(env);

        options.DbPath.Should().Be("/data/sharpmud.db");
    }
}
