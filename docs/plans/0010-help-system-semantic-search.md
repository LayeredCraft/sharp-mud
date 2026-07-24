# [PLAN-0010] Help System + Semantic Search Fallback

**Implements:** [ADR-0010](../adr/0010-help-system-semantic-search.md)

**Status:** Done

**Last updated:** 2026-07-24

## Goal

`help <topic>` resolves a real, persisted `HelpTopic` (exact name/alias,
then keyword, then a threshold-gated semantic-search fallback), authored
and edited only via a new in-game admin command; `helpindex rebuild`
regenerates the embedding index on demand. Command-listing behavior of
today's `help` (no arguments) is unchanged.

## Scope

In scope: `HelpTopic`/`HelpTopicChunk` schema + `IHelpRepository` (SQLite),
`IEmbeddingProvider` deterministic stub, `IHelpSearchIndex` brute-force
cosine implementation, `HelpCommand` topic-lookup pipeline, an in-game
admin command to create/edit help topics, the `helpindex rebuild` admin
command, tests for each layer.

Out of scope (per ADR-0010's Negative Consequences / Links): a real
embedding model (local or API-based) swapped in behind `IEmbeddingProvider`;
`sqlite-vec`/ANN-backed `IHelpSearchIndex`; DynamoDB support for
`IHelpRepository`; any git-file content path; automatic rebuild-on-edit or
rebuild-at-boot.

Also out of scope, discovered during implementation (not in the original
ADR-0010 scoping, added here rather than to the ADR since it's a "how"
detail, not a "what/why" one): setting `HelpTopic.Aliases`/`Keywords` via
any admin command. The domain model and persistence fully support them
(`SetAliases`/`SetKeywords`, round-tripped by `HelpRepository`), but
`helptopic` only takes `<key> <body>` — see Open questions / blockers.

## Tasks

- [x] **Schema / persistence**
  - [x] `HelpTopic` aggregate root + `HelpTopicChunk` child entity — placed
        under `src/SharpMud.Engine/Help/` (a new feature folder, per
        `coding-standards.md`'s "organize by feature folder" rule), not
        `SharpMud.Engine/Core` as originally scoped; `IThingRepository`
        lives in `Core` because `Thing` does, but `HelpTopic` isn't a
        `Core` concept the same way.
  - [x] `IHelpRepository` interface — same reason, lives in
        `SharpMud.Engine/Help`, not `Core`. Method names as scoped
        (`FindByNameOrAliasAsync`, `FindByKeywordAsync`, `GetAllTopicsAsync`,
        `SaveTopicAsync`, `DeleteTopicAsync`).
  - [x] `HelpRepository` impl in `SharpMud.Persistence`, DI-registered in
        `SharpMud.Persistence.Sqlite` (`AddSharpMudSqlitePersistence`,
        alongside `IThingRepository` — always-available infrastructure,
        not opt-in, since `help` itself is always part of
        `BuiltinCommands.RegisterAll`). `HelpTopicChunk` is its own table
        with an explicit `HelpTopicId` FK, loaded/saved manually by
        `HelpRepository` (mirrors `ThingRepository`'s `Behaviors`
        handling) rather than an EF navigation collection — simpler than
        `ThingRepository`'s full Rehydration-event machinery, which
        doesn't apply here (no event-firing/polymorphism concerns for
        `HelpTopic`/`HelpTopicChunk`).
  - [x] `HelpTopic`/`HelpTopicChunk` `DbSet`s added to `GameDbContext`.
- [x] **Embedding + search abstractions**
  - [x] `IEmbeddingProvider` + `StubEmbeddingProvider` (feature-hashed
        bag-of-words, FNV-1a hashing, L2-normalized, 128 dimensions) —
        both in `SharpMud.Engine/Help`, no external dependency.
        DI-registered as the default in `AddSharpMudSqlitePersistence`.
  - [x] `IHelpSearchIndex` + `CosineHelpSearchIndex` — brute-force,
        reads through `IHelpRepository`, per-topic best-chunk score,
        `RelevanceThreshold = 0.15` (tuned for the stub provider).
  - [x] `HelpTopicChunker.Split` — paragraph-boundary (blank-line)
        splitting.
- [x] **Commands**
  - [x] `HelpCommand` extended: exact → keyword → semantic fallback → "no
        help topic found." No-argument listing behavior unchanged
        (verified — same test coverage as before this slice still passes).
  - [x] `helptopic <key> <body>` (`HelpTopicEditCommand`,
        `SecurityRole.MinorBuilder`) — creates or overwrites a topic's
        `Body`. Aliases/keywords not settable via this command (see Scope).
  - [x] `helpindex rebuild` (`HelpIndexRebuildCommand`,
        `SecurityRole.MinorBuilder`) — re-chunks/re-embeds every topic,
        replaces chunks wholesale via `HelpTopic.ReplaceChunks`.
  - [x] `HelpAdminCommands.RegisterAll` — new registration class
        (`SharpMud.Engine/Commands/Builtin/Admin`), same opt-in shape as
        `AdminCommands`/`BuilderCommands`; wired in the sample's
        `Program.cs` alongside those two.
- [x] **Tests**
  - [x] `HelpRepositoryTests` (persistence round-trip: topic + aliases +
        keywords + chunks as one unit, exact/keyword lookup, chunk
        replace-wholesale, delete).
  - [x] `CosineHelpSearchIndexTests` (threshold pass/fail, ordering,
        best-chunk-per-topic).
  - [x] `StubEmbeddingProviderTests` (determinism, case-insensitivity,
        normalization, disjoint-text divergence).
  - [x] `HelpTopicChunkerTests`, `HelpTopicTests` (domain object behavior).
  - [x] `HelpCommandTests` extended with the three-tier pipeline + no-match
        case; existing command-listing tests updated for the new
        constructor, still passing unchanged.
  - [x] `HelpTopicEditCommandTests`, `HelpIndexRebuildCommandTests`.
  - [x] `SharpMud.Ruleset.Rpg.Tests`' `ServiceCollectionExtensionsTests`
        needed `IHelpRepository`/`IHelpSearchIndex` test doubles added —
        `AddSharpMudRuleset` now resolves them to build `HelpCommand` as
        part of `BuiltinCommands.RegisterAll`, a new hard dependency for
        any consumer of that method (same tier as the existing
        `IRandomSource` requirement in that test).
- [x] **Docs**
  - [x] New `docs/help-system.md`, indexed in `docs/README.md`.
  - [x] `docs/commands.md`'s Meta section and V1 Verb List updated; its
        `help` Open Item removed (now resolved).
  - [x] [PLAN-0001](0001-wheelmud-reconciliation-roadmap.md)'s Slice 5 row
        updated to `Done`.

## Critical files

New:
- `src/SharpMud.Engine/Help/HelpTopicId.cs`, `HelpTopic.cs`,
  `HelpTopicChunk.cs`, `HelpContentHashing.cs`, `IHelpRepository.cs`,
  `IEmbeddingProvider.cs`, `IHelpSearchIndex.cs`, `HelpSearchHit.cs`,
  `StubEmbeddingProvider.cs`, `CosineHelpSearchIndex.cs`,
  `HelpTopicChunker.cs`
- `src/SharpMud.Engine/Commands/Builtin/Admin/HelpTopicEditCommand.cs`,
  `HelpIndexRebuildCommand.cs`, `HelpAdminCommands.cs`
- `src/SharpMud.Persistence/Configurations/HelpTopicConfiguration.cs`,
  `HelpTopicChunkConfiguration.cs`
- `src/SharpMud.Persistence/HelpRepository.cs`
- `docs/help-system.md`
- Tests: `tests/SharpMud.Engine.Tests/Help/*`,
  `tests/SharpMud.Engine.Tests/Commands/Builtin/Admin/HelpTopicEditCommandTests.cs`,
  `HelpIndexRebuildCommandTests.cs`, `tests/SharpMud.Persistence.Tests/HelpRepositoryTests.cs`

Modified:
- `src/SharpMud.Engine/Commands/Builtin/HelpCommand.cs`,
  `BuiltinCommands.cs`
- `src/SharpMud.Persistence/GameDbContext.cs`
- `src/SharpMud.Persistence.Sqlite/ServiceCollectionExtensions.cs`
- `src/SharpMud.Hosting/ServiceCollectionExtensions.cs`
  (`AddSharpMudRuleset` resolves `IHelpRepository`/`IHelpSearchIndex`)
- `samples/SharpMud.Samples.Classic/Program.cs`
- `tests/SharpMud.Engine.Tests/Commands/HelpCommandTests.cs`
- `tests/SharpMud.Ruleset.Rpg.Tests/ServiceCollectionExtensionsTests.cs`
- `docs/commands.md`, `docs/README.md`,
  `docs/plans/0001-wheelmud-reconciliation-roadmap.md`

## Test plan

Unit coverage as scoped, plus real end-to-end manual verification (see
below) — this slice touches persistence and a network-reachable command
surface, so unit tests alone weren't treated as sufficient, per
`testing.md`.

## Verification

- `help <exact topic name>` ✅ verified live.
- `help <keyword>` — not exercised live (no admin command sets keywords
  yet, see Scope); covered at the unit level
  (`HelpRepositoryTests.FindByKeywordAsync_ReturnsMatchingTopics`,
  `HelpCommandTests.ExecuteAsync_FallsBackToKeywordMatch_...`).
- `help <natural-language query with no exact/keyword overlap>` ✅ verified
  live — `help how do i become a wizard` resolved via
  `CosineHelpSearchIndex` semantic fallback to a topic created only under
  the key `wizard`.
- `help <unrelated query>` ✅ verified live — `"No help topic found for
  '...'."`, not a wrong guess.
- Editing a topic via `helptopic` without a follow-up `helpindex rebuild`
  leaving prior search results unchanged — covered at the unit level
  (`HelpRepositoryTests.SaveTopicAsync_CalledTwice_ReplacesChunksWholesale`
  proves chunks only change on an explicit chunk-replacing save, which only
  `helpindex rebuild` triggers); not separately re-verified live beyond
  confirming `helpindex rebuild` itself works.
- `help` with no arguments ✅ verified live — command listing unchanged,
  role-gated `helptopic`/`helpindex` visible to the `MinorBuilder` actor
  that granted itself the role.

Manual run: `SharpMud.Samples.Classic` over real Telnet
(`--telnet 4099 --db-path <temp>`), `SHARPMUD_INITIAL_ADMIN` bootstrap to
grant `FullAdmin`, then `rolegrant Adventurer minorbuilder` to reach
`helptopic`/`helpindex`'s required role, then the sequence above.

## Open questions / blockers

- Real embedding provider (local model vs. API-based) — deliberately
  deferred per ADR-0010; needs its own follow-up decision once retrieval
  quality actually matters.
- DynamoDB mapping for `HelpTopicChunk.Embedding` (`byte[]` BLOB) —
  out of scope here, needs a decision if/when DynamoDB support for help
  topics is picked up.
- No admin command sets `HelpTopic.Aliases`/`Keywords` — the model/
  persistence support them fully (round-tripped in
  `HelpRepositoryTests`), but there's no in-game way to populate them yet.
  A follow-up (either extending `helptopic` or a small new command, given
  `CommandParser`'s no-quoted-strings constraint per ADR-0009) is needed
  before the keyword-lookup tier has any real content to match against in
  practice.
