# Deployment

See [README.md](README.md) for how this doc relates to `SPEC.md` and the
other subsystem docs, and [networking.md](networking.md) for the Telnet
server this containerizes.

## Container

`Dockerfile` (repo root) is a multi-stage build: `mcr.microsoft.com/dotnet/sdk`
restores/publishes `SharpMud.Host` (which pulls in every project it
references — Engine, Ruleset.Classic, Persistence, both adapters), then the
published output is copied into a much smaller `mcr.microsoft.com/dotnet/runtime`
Alpine image. Both stages are pinned to the exact SDK/runtime version in
`global.json` (`11.0.100-preview.5`) — confirmed to exist on MCR as of this
writing; see [architecture.md](architecture.md) Open Items for the .NET 10
LTS fallback plan if that ever stops being true.

```
docker build -t sharpmud .
docker run -p 4000:4000 sharpmud
```

## Runtime Configuration

`HostOptions.Parse` (`src/SharpMud.Host/HostOptions.cs`) resolves the run
mode from, in precedence order: CLI args, then environment variables, then a
default (CLI/local mode). This is what lets the same image work both as
`docker run sharpmud` (telnet, via the Dockerfile's `ENV` defaults) and
`docker run -e SHARPMUD_MODE=cli -it sharpmud` (single-player CLI, e.g. for
debugging inside the container) without rebuilding.

| Setting | CLI arg | Env var | Default |
|---|---|---|---|
| Mode | `--telnet` (first positional arg) | `SHARPMUD_MODE=telnet` | CLI (local single-player) |
| Telnet port | second positional arg after `--telnet` | `SHARPMUD_TELNET_PORT` | `4000` |
| SQLite DB path | — | `SHARPMUD_DB_PATH` | `./sharpmud.db` (`/data/sharpmud.db` in the container image) |

The Dockerfile sets `SHARPMUD_MODE=telnet`, `SHARPMUD_TELNET_PORT=4000`, and
`SHARPMUD_DB_PATH=/data/sharpmud.db` as image defaults, plus `EXPOSE 4000`
and `VOLUME /data` — the container runs as a persistent telnet server by
default, matching the "one long-running containerized process" model in
`SPEC.md`'s Deployment section. **The `/data` volume must actually be
mounted** (`docker run -v <volume>:/data ...`) for data to survive a
redeploy — without it, the DB lives in the container's writable layer and is
discarded along with the container itself on `docker rm`/replace (a restart
of the *same* container without removal would still happen to work, but that
is not the real redeploy scenario and shouldn't be relied on).

Graceful shutdown (both `SIGINT` and `SIGTERM`, via `PosixSignalRegistration`
— see [persistence.md](persistence.md)'s Write Frequency section for why
`Console.CancelKeyPress` was wrong for this) is what makes `docker stop`
actually save state before the container exits; see Verified below for a
real `docker stop` → container removed → fresh container from the same
image → data still there test.

`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true` is also set — the base (non
`-extra`) Alpine runtime image ships without ICU, and game text is plain
ASCII/English, so invariant globalization mode avoids needing the larger
`-extra` image variant for culture data nothing uses.

## Verified

Built and run locally via Docker: image builds clean (no warnings), the
container starts and listens on the configured port, and a real TCP client
connecting from the host could create a character, `look`, and `quit`
successfully against the containerized server. Image size ~144MB
(Alpine-based runtime, no SDK).

**Full redeploy-with-volume round trip**, run after persistence landed
(see [persistence.md](persistence.md)): created a named Docker volume,
started a container mounting it at `/data`, connected and created a
character, picked up items, moved rooms; `docker stop` (real `SIGTERM`,
not a test shortcut) exited cleanly rather than requiring `-t 0`/force
kill; `docker rm` the container entirely (not just stop — the actual "old
container replaced by a new one" redeploy shape); started a **new**
container from the same image against the same volume; confirmed the log
reported "Loaded persisted world"; reconnected with the same character name
and confirmed location and inventory both survived. This is what caught the
`Console.CancelKeyPress`/`SIGTERM` bug in the first place — the container
wouldn't stop cleanly before the fix.

## Open Items

- AWS/ECS Fargate deployment itself (task definitions, networking, the
  DynamoDB EF Core provider pairing) — `SPEC.md` names this as the intended
  eventual home but nothing AWS-specific has been built yet.
- Health check endpoint / `HEALTHCHECK` instruction — not added; nothing to
  check yet beyond "process is running."
- Multi-arch image builds (arm64 + amd64) — currently only built for the
  local dev machine's architecture.
- CI-driven image builds/publishing — no pipeline exists yet; images are
  built manually.
- No documentation/tooling yet for backing up the `/data` volume itself —
  the volume mount solves "survives a redeploy," not "survives host disk
  loss."
