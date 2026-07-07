# sharp-mud — Design Spec

A modern C# reimagining of a classic MUD. Faithful to the genre's feel (verb-first
commands, room-based navigation, persistent world) while using current .NET
architecture patterns instead of the C/C++ codebases (Diku/Circle/LP-family) that
originated the genre. Personal long-standing goal: build and actually run a
persistent public MUD.

## Vision

- Nostalgic, faithful-feeling gameplay (classic verb commands, N/S/E/W navigation,
  round-based combat) — not a redesign of what a MUD *is*.
- Built with modern C#/.NET architecture: clean separation of concerns, testable
  core, no legacy-C idioms carried over just for tradition.
- Start as a local single-player CLI for fast iteration, grow into a networked,
  persistent, publicly-run game without rewriting the core.

## Architecture

### Transport-agnostic sessions

The engine never talks to `Console` or a socket directly. All I/O goes through an
abstract session interface (read a line, write a line, disconnect, receive
out-of-band notifications). Concrete adapters implement this interface:

1. **Local CLI adapter** (v1) — stdin/stdout, single process, single player.
2. **Telnet/raw TCP adapter** (later) — classic MUD client compatibility
   (Mudlet, TinTin++, etc).
3. **SSH adapter** (later) — secure terminal access.
4. **WebSocket adapter** (later) — browser play via xterm.js, no client install.

Adding a transport is additive (a new adapter class); it never requires changes
to game logic, command parsing, or world state.

### Persistence

Repository interfaces (`IPlayerRepository`, `IWorldRepository`, etc.) sit between
game logic and storage. Game logic depends only on these interfaces — never on
EF Core, a specific provider, or SQL directly.

- **EF Core** is the ORM layer implementing the repositories.
- **v1 provider: SQLite** — zero infra, single file, transactional, easy local
  dev and debugging.
- **Planned providers**: MongoDB (existing EF Core provider) and a **custom
  DynamoDB EF Core provider** (in active co-development with Jonas Ha) — the
  intended production backend once ready, pairing naturally with an AWS
  deployment.
- Switching providers later is a connection-string/DI-registration change, not
  an engine rewrite.

### Concurrency model

**Single global tick loop** (classic MUD model): one server-wide heartbeat
(e.g. every 1–2 seconds) advances combat rounds, regen, NPC AI, and other
time-based state. Simple to reason about, predictable, matches the genre's
traditional feel. Player-issued commands (movement, look, chat, etc.) are
processed as received, independent of the tick, the same way classic Diku/Circle
derivatives separate "instant" commands from round-based combat resolution.

## World Model

### Hybrid authoring: hand-built hub + generated frontier

- **Core/hub area** (starting town, key NPCs, tutorial content): hand-authored,
  either in code or data files, for maximum polish and control.
- **Wilderness/dungeon frontier**: procedurally generated, but **generated once
  and persisted** — not regenerated per visit. Once created, a frontier room is
  saved and stays fixed, exactly like a hand-built room from the player's
  perspective. This preserves classic MUD navigation muscle memory (N N E E S W
  means the same thing every time) while removing the grind of hand-authoring
  every room.
- In-memory world model (`Room`, `Area`, `Exit`, etc.) must not assume its data
  came from C# source — this is what allows moving from hardcoded rooms to a
  data-driven/JSON format later without disturbing the rest of the engine.

### Content authoring evolution (not all v1)

1. Hardcoded rooms in C# (bootstrap only, to get the loop working).
2. Data-driven world files (JSON/YAML) loaded at startup — natural next step,
   also the foundation for in-game building commands later.
3. In-game building commands (`@dig`, `@describe`, etc.) writing to the same
   data model.

## Command System

- **Classic verb-first commands**: `look`, `north`/`n`, `get sword`,
  `kill goblin`, with standard MUD abbreviations and directional shortcuts.
- **Aliases/macros**: players can define their own command shortcuts.
- **Soft-code / in-game scripting for builders**: explicitly **deferred**.
  v1 NPC/room behavior is data/config-driven only (states, simple declarative
  triggers) — no embedded scripting language (Roslyn/Lua/custom DSL) until a
  real need emerges. Revisit once content complexity demands it.

## Build Order

1. **Foundation**: session abstraction, local CLI adapter, command parser,
   world model, movement, `look`, chat/social commands (say/tell/emote). Prove
   out the core loop before adding systems.
2. **Combat**: simple round-based combat (Diku/Circle-style) — auto-attack on
   the global tick, hit/miss/damage messages, minimal per-round input required
   once engaged.
3. **Inventory & items**: pick up/drop/wear/wield, carry weight or slots.
4. **NPCs**: basic AI — wandering mobs, shopkeepers, quest givers, driven by
   the same data-config model as rooms.
5. **Networking**: Telnet/SSH/WebSocket adapters, multi-player concurrency
   hardening.
6. **Accounts & auth**: see below.
7. **Moderation/admin tooling**, **procedural frontier generation**,
   **in-game building/scripting**: later phases (see Deferred/Open Items).

## Accounts & Auth

External OAuth (Google/GitHub/Discord) rather than engine-managed
username/password. No password storage/reset burden; less classic-MUD-feeling
at the login prompt, but appropriate for a modern, publicly-run service.
Deferred until networked play lands (v1 local CLI has no login).

## Deployment

Containerized (Docker) from the start, with AWS as the intended eventual home
(pairing with the DynamoDB EF Core provider and other AWS-managed
infrastructure). The MUD's always-on tick loop and persistent connections mean
it runs as one long-running containerized process (e.g. ECS/Fargate), not a
serverless/request-response model.

## Deferred / Open Items

Explicitly out of scope for v1, to revisit later:

- **Moderation/admin tooling**: permission levels (player/builder/admin),
  mute/kick/ban, admin commands, audit logging. Known future need, not
  designed yet.
- **Soft-code/scripting engine**: revisit once data/config-driven NPC and room
  behavior proves insufficient.
- **Procedural frontier generation algorithm**: choice of generation approach
  (grid-based wilderness, dungeon graph, etc.) not yet decided — v1 world is
  fully hand-built hub only; frontier generation is a post-foundation phase.
- **Telnet/SSH/WebSocket adapters**: designed for structurally (via the session
  abstraction) but not implemented until after the local CLI foundation is
  solid.
- **DynamoDB EF Core provider readiness**: production persistence target
  depends on this provider (co-developed with Jonas Ha) reaching a usable
  state; SQLite remains the dev/default backend until then.
