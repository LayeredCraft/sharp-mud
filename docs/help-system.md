# Help System

See [README.md](README.md) for how this doc relates to `SPEC.md` and the
other subsystem docs. See [commands.md](commands.md) for the general
command pipeline `help`/`helptopic`/`helpindex` plug into.

Decided in [ADR-0010](adr/0010-help-system-semantic-search.md), Slice 5 of
the [WheelMUD reconciliation roadmap](adr/0001-wheelmud-reconciliation-roadmap.md)
(execution tracked in [PLAN-0010](plans/0010-help-system-semantic-search.md)).
[ADR-0011](adr/0011-local-embedding-provider-for-sample.md) adds a real,
local embedding provider option on top of ADR-0010's abstraction, used by
the sample app.

See also the user-facing [docsite](https://sharp-mud.layeredcraft.dev/) —
[Moderation & World Building](https://github.com/LayeredCraft/sharp-mud/blob/main/docsite/docs/moderation-and-world-building.md)-style
page: `docsite/docs/help-system.md` — for a consumer-oriented walkthrough
(as opposed to this doc's internal-implementation focus).

## What it does today

`help <query>` resolves a `HelpTopic` through a three-tier pipeline, each
tier only consulted if the previous one misses:

1. **Exact match** — `HelpTopic.Key` or `HelpTopic.Aliases`, case-insensitive.
2. **Keyword match** — `HelpTopic.Keywords`, case-insensitive.
3. **Semantic search** — `IHelpSearchIndex.SearchAsync`, cosine similarity
   over embedded chunks, filtered by `IEmbeddingProvider.RelevanceThreshold`.
   Returns no result (not a guess) below the threshold.

If all three miss, the player sees `"No help topic found for '<query>'."`.
`help` with no arguments is unchanged from before this slice — it still
lists every command the actor can see (`HelpCommand`'s original behavior).

Help content is authored and edited only in-game, via
`helptopic <key> <body>` (`SecurityRole.MinorBuilder`) — there is no
file-based content path. This command creates a topic if `<key>` doesn't
exist yet, or overwrites its `Body` if it does. Aliases and keywords aren't
settable via a command yet (see Open Items).

The embedding index is a **rebuildable derivative** of topic content, never
authoritative — `helpindex rebuild` (`SecurityRole.MinorBuilder`) re-chunks
every topic's `Body` (paragraph-boundary splitting,
`HelpTopicChunker.Split`) and re-embeds each chunk via `IEmbeddingProvider`,
replacing that topic's chunk set wholesale. Nothing rebuilds the index
automatically — not on `helptopic` save, not at boot — so a topic edited
without a follow-up `helpindex rebuild` keeps matching on its *previous*
content for semantic search (exact/keyword lookup, which reads `Body`
directly, is unaffected).

## Abstractions

- `IHelpRepository` (`SharpMud.Engine/Help`, impl in `SharpMud.Persistence`) —
  the aggregate-root repository for `HelpTopic`, same one-repository-per-
  aggregate-root shape as `IThingRepository` (see
  [persistence.md](persistence.md)).
- `IEmbeddingProvider` — turns text into a vector, and declares its own
  `RelevanceThreshold`. The threshold is owned by the provider, not by
  `CosineHelpSearchIndex`, because different embedding models produce
  cosine scores on very different scales — see ADR-0011's Decision
  Outcome for the measured numbers behind that call. Two implementations
  exist:
  - `StubEmbeddingProvider` (`SharpMud.Engine`, the default registered by
    `AddSharpMudSqlitePersistence`) — a deterministic feature-hashed
    bag-of-words vector (the "hashing trick"), no external dependency or
    model asset. Reflects literal word overlap only — no synonym/semantic
    understanding — and exists to validate the pipeline end-to-end with
    fully reproducible output, not to be a production-quality semantic
    model. `RelevanceThreshold = 0.15`.
  - `LocalEmbeddingProvider` (`samples/SharpMud.Samples.Classic`, ADR-0011) —
    wraps `SmartComponents.LocalEmbeddings`' `LocalEmbedder`, a real,
    local (offline, no API key) sentence-embedding model running via ONNX
    Runtime. Registered in the sample's `Program.cs` in place of the
    stub. `RelevanceThreshold = 0.58`, empirically chosen (see ADR-0011,
    including a caught false-positive with a first-pass value of `0.5`).
    See the docsite page for how to swap this into your own app.
- `IHelpSearchIndex` — semantic search over topics. The only implementation
  today is `CosineHelpSearchIndex`: brute-force cosine similarity computed
  in app code over every chunk loaded through `IHelpRepository`, no vector-
  search dependency. Depends only on `IHelpRepository`/`IEmbeddingProvider`,
  not on SQLite directly, so a future storage swap (e.g. an ANN-backed
  index) replaces this implementation without touching `HelpCommand` or any
  other caller.

Both interfaces are the seam a real embedding model and/or a different
vector-storage backend swap in behind later, without changing the lookup
pipeline itself.

## Persistence

`HelpTopic` and `HelpTopicChunk` are plain EF Core entities in
`SharpMud.Persistence` (`HelpTopicConfiguration`/
`HelpTopicChunkConfiguration`) — unlike `Thing`/`Behavior`, they have no
Rehydration/event-firing concerns, so `HelpRepository` uses EF Core
normally (real property conversions, no shadow FKs, no manual graph
reconstruction) rather than `ThingRepository`'s hand-rolled pattern:

- `HelpTopic.Aliases`/`Keywords` are `IReadOnlyList<string>` backed by a
  private field, with no public setter (mutate via `SetAliases`/
  `SetKeywords`) — EF binds directly to the backing field
  (`PropertyAccessMode.Field`) and converts to/from a single delimited
  string column.
- `HelpTopic.Chunks` is `Ignore`'d on the EF side; `HelpTopicChunk` is its
  own table with an explicit `HelpTopicId` FK column (not a shadow FK, not
  an EF navigation collection), loaded/saved manually by `HelpRepository` —
  the same shape `ThingRepository` already uses for the `Behaviors` table.
- `HelpTopicChunk.Embedding` is stored as a `BLOB` (a little-endian
  reinterpretation of the `float[]`, not meant to be portable across
  machine architectures) rather than a delimited string.
- `HelpTopic.ContentHash` is derived from `Body` (SHA-256), not a stored
  column — it's always in sync with `Body` by construction, so persisting
  it separately would just be a value that could drift.
- `SaveTopicAsync` deletes then re-inserts (two `SaveChangesAsync` calls),
  same PK-conflict-avoidance shape as `ThingRepository.SaveTreeAsync`.
- `GetAllTopicsAsync` loads the entire topic/chunk corpus per call, the same
  "load everything" shape `ThingRepository` already uses — fine at
  help-topic scale (dozens–hundreds of topics), not designed to scale past
  that without revisiting (see ADR-0010's Negative Consequences).

`IHelpRepository`/`StubEmbeddingProvider`/`CosineHelpSearchIndex` are
registered by `AddSharpMudSqlitePersistence` (`SharpMud.Persistence.Sqlite`)
alongside `IThingRepository` — always-available infrastructure, not opt-in,
since `help` (unlike `helptopic`/`helpindex`) is part of
`BuiltinCommands.RegisterAll` for every consumer. `DynamoDB` support for
`IHelpRepository` doesn't exist yet — out of scope for this slice.

## Verified

Manually verified end-to-end over a real Telnet connection against the
sample host (`SharpMud.Samples.Classic`, SQLite-backed): granted
`MinorBuilder`, created a topic via `helptopic wizard <body>`, ran
`helpindex rebuild`, then confirmed `help wizard` (exact match) and
`help how do i become a wizard` (no exact/keyword match — resolved via the
`CosineHelpSearchIndex` semantic-search fallback) both returned the
topic's body, `help <unrelated query>` returned "No help topic found", and
no-argument `help` still lists commands exactly as before this slice.

Re-verified with `LocalEmbeddingProvider` in place of the stub (ADR-0011):
a topic keyed `mage`, with `wizard`/`sorcerer` appearing nowhere in its
key or body, was correctly resolved by `help wizard`, `help sorcerer`, and
`help how do i cast spells` — genuine synonym/paraphrase matching, which
`StubEmbeddingProvider` cannot do. `help rusty sword` (genuinely unrelated)
correctly returned no match. A first-pass threshold of `0.5` was caught
returning a false positive for `help up` (a built-in movement verb, no
help topic exists for it) — short, generic single-word queries scored
higher against the topic than longer unrelated phrases did. Raised to
`0.58`; re-verified that `help up`/`help who`/`help north` all correctly
report no match while the real synonym/paraphrase queries above still
succeed.

## Open Items

- Aliases/keywords aren't settable via any admin command yet — `HelpTopic`
  supports them (`SetAliases`/`SetKeywords`, persisted), but `helptopic`
  only sets `Key`/`Body`. Deliberately deferred (PLAN-0010) to keep the
  admin-command surface small for this slice; a follow-up command (or an
  extension to `helptopic`) is needed before the keyword-lookup tier has
  any real content to match against.
- `StubEmbeddingProvider` (still the *default*, for every consumer that
  doesn't opt into `LocalEmbeddingProvider`) is a deterministic
  placeholder, not a real semantic model — a natural-language query only
  matches a topic that shares literal words with it, not true
  synonym/concept understanding. `LocalEmbeddingProvider` (ADR-0011)
  resolves this for the sample app specifically; a consumer wanting real
  semantic matching needs to opt in the same way (or write their own
  `IEmbeddingProvider`/`RelevanceThreshold`).
- No DynamoDB mapping for `IHelpRepository` yet.
