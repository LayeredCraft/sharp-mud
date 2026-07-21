# Multi-stage build. Pinned to the same .NET 11 preview SDK/runtime version
# as global.json - see docs/architecture.md Open Items for the fallback-to-
# .NET-10-LTS plan if preview tooling support lags.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.6 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/SharpMud.Engine/SharpMud.Engine.csproj src/SharpMud.Engine/
COPY src/SharpMud.Hosting/SharpMud.Hosting.csproj src/SharpMud.Hosting/
COPY src/SharpMud.Persistence/SharpMud.Persistence.csproj src/SharpMud.Persistence/
COPY src/SharpMud.Persistence.Sqlite/SharpMud.Persistence.Sqlite.csproj src/SharpMud.Persistence.Sqlite/
COPY src/SharpMud.Adapters.Cli/SharpMud.Adapters.Cli.csproj src/SharpMud.Adapters.Cli/
COPY src/SharpMud.Adapters.Telnet/SharpMud.Adapters.Telnet.csproj src/SharpMud.Adapters.Telnet/
COPY samples/SharpMud.Samples.Classic/SharpMud.Samples.Classic.csproj samples/SharpMud.Samples.Classic/
RUN dotnet restore samples/SharpMud.Samples.Classic/SharpMud.Samples.Classic.csproj

COPY src/ src/
COPY samples/ samples/
RUN dotnet publish samples/SharpMud.Samples.Classic/SharpMud.Samples.Classic.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:11.0.0-preview.6-alpine3.24 AS runtime
WORKDIR /app
COPY --from=build /app .

# Container's default mode - matches docs/networking.md's "runs as one
# long-running containerized process" deployment model. Override with
# `docker run -e SHARPMUD_MODE=cli` for a local single-player shell instead.
ENV SHARPMUD_MODE=telnet
ENV SHARPMUD_TELNET_PORT=4000
EXPOSE 4000

# /data, not the default ./sharpmud.db in the working directory - the DB
# must live in a mounted volume (`docker run -v ...:/data`) or every restart
# only *appears* to persist (survives within a container, but a container
# rm/replace - the actual redeploy scenario - loses everything, since the
# writable container layer is discarded with it). See docs/deployment.md.
ENV SHARPMUD_DB_PATH=/data/sharpmud.db
VOLUME /data

# The base (non "-extra") alpine runtime image ships without ICU; game text
# is plain ASCII/English so invariant globalization is fine and avoids
# needing the larger -extra image just for culture data we don't use.
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

ENTRYPOINT ["dotnet", "SharpMud.Samples.Classic.dll"]
