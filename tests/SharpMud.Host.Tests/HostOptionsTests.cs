using SharpMud.Host;

namespace SharpMud.Host.Tests;

public sealed class HostOptionsTests
{
    [Fact]
    public void Parse_DefaultsToCliMode_WhenNoArgsOrEnv()
    {
        var options = HostOptions.Parse([], new Dictionary<string, string?>());

        options.UseTelnet.Should().BeFalse();
    }

    [Fact]
    public void Parse_UsesTelnetMode_WhenTelnetArgGiven()
    {
        var options = HostOptions.Parse(["--telnet"], new Dictionary<string, string?>());

        options.UseTelnet.Should().BeTrue();
        options.TelnetPort.Should().Be(4000);
    }

    [Fact]
    public void Parse_UsesPortFromArgs_WhenGiven()
    {
        var options = HostOptions.Parse(["--telnet", "5555"], new Dictionary<string, string?>());

        options.TelnetPort.Should().Be(5555);
    }

    [Fact]
    public void Parse_UsesTelnetMode_WhenEnvVarSet()
    {
        var env = new Dictionary<string, string?> { ["SHARPMUD_MODE"] = "telnet" };

        var options = HostOptions.Parse([], env);

        options.UseTelnet.Should().BeTrue();
    }

    [Fact]
    public void Parse_UsesPortFromEnvVar_WhenArgsDoNotSpecifyPort()
    {
        var env = new Dictionary<string, string?>
        {
            ["SHARPMUD_MODE"] = "telnet",
            ["SHARPMUD_TELNET_PORT"] = "6001",
        };

        var options = HostOptions.Parse(["--telnet"], env);

        options.TelnetPort.Should().Be(6001);
    }

    [Fact]
    public void Parse_ArgsPortWinsOverEnvVarPort()
    {
        var env = new Dictionary<string, string?>
        {
            ["SHARPMUD_MODE"] = "telnet",
            ["SHARPMUD_TELNET_PORT"] = "6001",
        };

        var options = HostOptions.Parse(["--telnet", "7777"], env);

        options.TelnetPort.Should().Be(7777);
    }

    [Fact]
    public void Parse_TelnetModeIsCaseInsensitive()
    {
        var env = new Dictionary<string, string?> { ["SHARPMUD_MODE"] = "TELNET" };

        var options = HostOptions.Parse([], env);

        options.UseTelnet.Should().BeTrue();
    }
}
