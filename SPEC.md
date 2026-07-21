# sharp-mud — Design Spec

**Status: alpha, pre-1.0.** No SemVer compatibility guarantees between
releases yet — APIs, package boundaries, and persisted-data shape can all
still change.

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
- **Engine, not just a game**: `SharpMud.Engine` must support a different game
  (different stats, different combat rules, different content) being built on
  top of it without forking or modifying the engine itself. This is a
  from-the-start design goal, not an afterthought — see
  [docs/research/wheelmud-findings.md](docs/research/wheelmud-findings.md) for
  the prior art this is adapted from and
  [docs/engine-vs-ruleset.md](docs/engine-vs-ruleset.md) for the concrete
  design. The game we're actually building (classic D&D-flavored stats/combat,
  the hand-built hub) lives in `samples/SharpMud.Samples.Classic`, a reference
  consumer of the published `SharpMud.*` NuGet packages
  ([ADR-0006](docs/adr/0006-nuget-package-distribution.md)) that consumes the
  engine the same way a third party's game would.

## Architecture

### Entity model: `Thing` + `Behavior` composition

Every game object — room, player, item, NPC, exit, area — is the same sealed
`Thing` class from `SharpMud.Engine`. What an object *is* comes entirely from
which `Behavior`s are attached to it, not from subclassing. A player is a
`Thing` with a `PlayerBehavior` (identity/session link); a room is a `Thing`
with a `RoomBehavior`; an exit is a child `Thing` with an `ExitBehavior` (and
optionally a `LockableBehavior`). This is adapted directly from WheelMUD (see
[docs/research/wheelmud-findings.md](docs/research/wheelmud-findings.md)) and
is the mechanism that makes the engine/ruleset split real: `SharpMud.Engine`
ships generic, ruleset-agnostic behaviors (rooms, exits, containment,
identity); `SharpMud.Samples.Classic` adds the D&D-flavored ones (stats,
combat, dice-roll character creation) purely by composing more `Behavior`s
onto the same `Thing`s — the engine never references ruleset types. Full
design in [docs/engine-vs-ruleset.md](docs/engine-vs-ruleset.md).

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

### Persistence ✅ (implemented — save-on-shutdown/-disconnect, not yet
save-on-every-mutation; see docs/persistence.md Open Items)

A single `IThingRepository` (revised from separate per-concept repositories —
`Thing` is the one entity type now, see [docs/engine-vs-ruleset.md](docs/engine-vs-ruleset.md))
sits between game logic and storage. Game logic depends only on this
interface — never on EF Core, a specific provider, or SQL directly. Full
design, including the EF Core TPH mapping for `Behavior` and why loading a
`Thing` tree can't be plain EF navigation fixup, in
[docs/persistence.md](docs/persistence.md).

- **EF Core** is the ORM layer implementing the repository.
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
- Rooms/areas/exits are `Thing`s composed from `RoomBehavior`/`AreaBehavior`/
  `ExitBehavior` (see Entity Model above), not dedicated classes — the world
  model must not assume its data came from C# source either way, which is what
  allows moving from hardcoded content to a data-driven/JSON format later
  without disturbing the engine.

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

1. **Foundation**: session abstraction, local CLI adapter, command parser,
   world model, movement, `look`, chat/social commands (say/tell/emote). Prove
   out the core loop before adding systems.
2. **Combat**: simple round-based combat (Diku/Circle-style) — auto-attack on
   the global tick, hit/miss/damage messages, minimal per-round input required
   once engaged.
3. **Inventory & items**: pick up/drop/wear/wield, carry weight or slots.
4. **Engine/ruleset split** (retrofit, done now rather than deferred): convert
   the entity model built in phases 1–3 to `Thing`/`Behavior` composition and
   extract the D&D-specific stat/combat rules into `SharpMud.Samples.Classic`,
   per [docs/engine-vs-ruleset.md](docs/engine-vs-ruleset.md). Doing this
   before NPCs/networking/accounts land means those phases are built against
   the real engine boundary instead of needing their own retrofit later.
5. **NPCs**: basic AI. Wandering (tick-driven `WanderingBehavior`/
   `WanderManager`, engine-level since it needs no ruleset-specific state) is
   implemented; shopkeepers/quest givers are deferred until currency/trade/
   quest systems exist to give them something to do.
6. **Networking**: Telnet adapter and multi-session `Host` implemented —
   `SharpMud.Adapters.Telnet` plus a shared `SessionLoop` used by every
   transport, confirmed with concurrent players seeing/interacting with each
   other over real TCP connections. SSH/WebSocket adapters, idle timeouts,
   connection limits, and telnet protocol negotiation (MCCP/MXP/NAWS) remain
   deferred — see [docs/networking.md](docs/networking.md) Open Items.
7. **Accounts & auth**: see below. New Telnet connections currently prompt
   for a plain name (no identity verification) as a placeholder until this
   phase lands.
8. **Moderation/admin tooling**, **procedural frontier generation**,
   **in-game building/scripting**: later phases (see Deferred/Open Items).

## Accounts & Auth ✅ implemented

Traditional username/password (revised — was external OAuth; see
[docs/accounts-auth.md](docs/accounts-auth.md) for the full reversal
rationale and current implementation/verification status). Classic MUD
login prompt, one character per login (no separate Account entity, no
"alts"). Hashed via `PasswordHasher<TUser>` (PBKDF2 + salt). Telnet's
placeholder name-only prompt has been replaced by the real thing; local CLI
still has no login at all, per the original decision.

## Deployment

Containerized (Docker) ✅ — multi-stage `Dockerfile` at the repo root, built
and verified locally (real telnet client connecting to the containerized
server end-to-end). AWS is still the intended eventual home (pairing with the
DynamoDB EF Core provider and other AWS-managed infrastructure) but nothing
AWS-specific exists yet. The MUD's always-on tick loop and persistent
connections mean it runs as one long-running containerized process (e.g.
ECS/Fargate), not a serverless/request-response model. See
[docs/deployment.md](docs/deployment.md) for the container's runtime
configuration and open items.

## Deferred / Open Items

Explicitly out of scope for v1, to revisit later:

- **Moderation/admin tooling**: permission levels, mute/kick/ban, admin
  commands — designed, see
  [ADR-0005](docs/adr/0005-security-role-model-and-moderation-commands.md)
  and
  [PLAN-0005](docs/plans/0005-security-role-model-and-moderation-commands.md);
  not yet implemented. Audit logging remains undesigned, tracked as an
  open item in PLAN-0005.
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
- **Hot-swap ruleset/plugin loading**: ruleset assemblies are loaded via a
  compile-time project reference plus a startup assembly-scan (see
  [docs/engine-vs-ruleset.md](docs/engine-vs-ruleset.md)), not a MEF-style
  drop-a-DLL-in-a-folder mechanism. True dynamic loading
  (`AssemblyLoadContext`-based) is deferred until there's a real third party
  wanting to redistribute a ruleset without a sharp-mud source checkout.
