using SharpMud.Hosting;

namespace SharpMud.Hosting.Tests;

public sealed class SharpMudHostOptionsTests
{
    [Fact]
    public void Parse_DefaultsDbPath_WhenEnvVarNotSet()
    {
        var options = SharpMudHostOptions.Parse(new Dictionary<string, string?>());

        options.DbPath.Should().Be("./sharpmud.db");
    }

    [Fact]
    public void Parse_UsesDbPathFromEnvVar_WhenSet()
    {
        var env = new Dictionary<string, string?> { ["SHARPMUD_DB_PATH"] = "/data/sharpmud.db" };

        var options = SharpMudHostOptions.Parse(env);

        options.DbPath.Should().Be("/data/sharpmud.db");
    }

    [Fact]
    public void Parse_DefaultsInitialAdminUsernameToNull_WhenEnvVarNotSet()
    {
        var options = SharpMudHostOptions.Parse(new Dictionary<string, string?>());

        options.InitialAdminUsername.Should().BeNull();
    }

    [Fact]
    public void Parse_UsesInitialAdminUsernameFromEnvVar_WhenSet()
    {
        var env = new Dictionary<string, string?> { ["SHARPMUD_INITIAL_ADMIN"] = "Root" };

        var options = SharpMudHostOptions.Parse(env);

        options.InitialAdminUsername.Should().Be("Root");
    }
}
