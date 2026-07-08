namespace SharpMud.Host;

public sealed record HostOptions(bool UseTelnet, int TelnetPort)
{
    // CLI args win over env vars, which win over defaults - the usual
    // precedence for containerized apps (Dockerfile ENV sets a baseline,
    // `docker run`/orchestrator command overrides still work).
    public static HostOptions Parse(string[] args, IReadOnlyDictionary<string, string?> env)
    {
        var useTelnet = args is ["--telnet", ..]
            || string.Equals(env.GetValueOrDefault("SHARPMUD_MODE"), "telnet", StringComparison.OrdinalIgnoreCase);

        var port = 4000;
        if (args is ["--telnet", var portArg, ..] && int.TryParse(portArg, out var parsedArg))
            port = parsedArg;
        else if (int.TryParse(env.GetValueOrDefault("SHARPMUD_TELNET_PORT"), out var parsedEnv))
            port = parsedEnv;

        return new HostOptions(useTelnet, port);
    }
}
