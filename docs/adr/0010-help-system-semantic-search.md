# [ADR-0010] Help System + Semantic Search Fallback

**Status:** Accepted

**Date:** 2026-07-24

**Decision Makers:** solo (design dive conducted with the user)

## Context

Per [ADR-0001](0001-wheelmud-reconciliation-roadmap.md), this is Slice 5 of
the WheelMUD reconciliation roadmap. Today `help` (`src/SharpMud.Engine/Commands/Builtin/HelpCommand.cs`)
does one thing: list every registered command's verb + aliases the actor is
allowed to see. There is no help *topic* concept — no authored content, no
per-topic lookup, nothing persisted.

The user wants a real help-topic system, with a semantic-search fallback
(intended as the retrieval half of a future RAG-style feature) so a
natural-language query like "how do I become a wizard?" can still surface
the right existing topic even when it doesn't match any alias/keyword
exactly. See §12 of [`wheelmud-findings.md`](../research/wheelmud-findings.md)
for the WheelMUD precedent researched for this ADR: `HelpManager` loads
help topics from files on disk (`systemdata/Files/Help/`) into memory at
boot, resolved by exact case-insensitive alias match only — no keyword
search, no semantic/NL search (predates that being reasonable to build), a
static `Instance` singleton, and no in-game authoring at all (content edited
by hand-editing files).

Two of WheelMUD's choices were already ruled out by standing sharp-mud
decisions before this ADR started: static singletons
(`design-decisions.md`/§10 of the findings doc), and — per
[ADR-0009](0009-world-building-olc-command-surface.md)'s precedent of
in-game `dig`/`tunnel`/`describe` authoring over WheelMUD's boot-time-only
room creation — file-on-disk content, discussed directly with the user (see
Decision Outcome).

## Decision Drivers

- `persistence.md`: SQLite via EF Core, no migrations (`EnsureCreatedAsync`
  only), no raw SQL, a repository is only justified for a DDD aggregate
  root.
- `design-decisions.md`: no new `Thing` subtype, no MEF/static singletons,
  DI-only wiring.
- User's explicit constraints (see prior discussion): help content stays the
  canonical, authored source of truth; semantic search retrieves existing
  topics only, never generates content; the embedding provider and vector
  storage must sit behind an abstraction so either can change later; exact/
  alias/keyword lookup must keep working unchanged, semantic search only
  supplements it; weak matches must return "no help topic found," not a bad
  guess; embeddings are a rebuildable index, not authoritative state.
- Avoid pulling in a large AI/RAG framework for what's currently a small,
  bounded corpus (dozens–hundreds of help topics, not millions) — small,
  explicit, testable over general-purpose infrastructure.
- No repo precedent exists for a hybrid file-seed + in-game-live-edit
  content model; the two established patterns (pure git-file docs, pure
  in-game OLC authoring) were both pure, and hybrid was rejected explicitly
  by the user during this ADR's design dive once that was surfaced (see
  Decision Outcome).

## Considered Options

**Content authoring:**

1. **File-based only** — help topics as git-tracked files (matching
   WheelMUD and matching `SPEC.md`/docs), loaded into SQLite as a
   rebuildable index at boot; editing is a PR, no in-game command.
2. **In-game admin command only (OLC-style)** — a `helptopic`-style command,
   DB is the sole source of truth, no file involved; matches
   [ADR-0009](0009-world-building-olc-command-surface.md)'s `dig`/`tunnel`/
   `describe` pattern exactly.
3. **Hybrid** — file-seed at first boot, then in-game admin edits diverge
   from the file with no defined sync/merge story.

**Embedding provider (v1):**

1. **External embedding API** (e.g. an OpenAI-compatible endpoint) — best
   retrieval quality, needs network/API key/cost, breaks offline
   self-hosting.
2. **Local/offline in-process model** — no external dependency, needs a
   shipped model asset.
3. **Deterministic stub now, real model swapped in later** — validates the
   full pipeline/abstraction end-to-end with no external dependency and no
   model asset yet; retrieval quality is not representative until a real
   model replaces it.

**Vector storage/similarity:**

1. **Brute-force cosine over a `BLOB` column** — `float[]` serialized into
   SQLite, similarity computed in app code over chunks loaded through the
   repository. No new dependency.
2. **`sqlite-vec` (or equivalent) native extension** — approximate
   nearest-neighbor search, scales further, adds a native dependency and
   more moving parts.

**Rebuild trigger:**

1. **Explicit admin command** (`helpindex rebuild`) — deliberate, matches
   the OLC command style, keeps embedding calls out of any hot/edit path.
2. **Automatic on topic save/edit** — simplest mentally, couples every
   content edit to embedding-provider latency/failures.
3. **Automatic staleness check at boot** — no manual step, but boot time
   becomes provider-dependent.

## Decision Outcome

Chosen: **content authoring — option 2 (in-game admin command only)**;
**embedding provider — option 3 (deterministic stub now, real model
later)**; **vector storage — option 1 (brute-force cosine over `BLOB`)**;
**rebuild trigger — option 1 (explicit admin command)**.

**Content authoring**: the user was initially drawn to the hybrid model but
asked directly whether there was repo precedent for it. There isn't — the
two patterns that exist today (`SPEC.md`/`docs/*.md` as pure git-file
content, and Slice 4's `dig`/`tunnel`/`describe` as pure in-game DB
authoring) are both single-source, and hybrid's unresolved
divergence-once-edited problem is exactly why neither existing pattern is
hybrid. Once that was surfaced, the user chose in-game-admin-only, which
also means help topics follow the exact same authoring shape as world
content — one content-authoring story for the repo, not two. This is a
deliberate deviation from WheelMUD's own file-based `HelpManager` (§12 of
the findings doc), justified by ADR-0009's precedent rather than by
anything intrinsic to help content.

**Embedding provider**: the real value of this slice right now is proving
the abstraction boundary and the retrieval pipeline (exact → keyword →
semantic-with-threshold) work end-to-end and are swappable, not shipping
production-quality semantic recall. A deterministic stub
(`IEmbeddingProvider`) with no external dependency keeps the slice small,
fully offline, and fully testable (deterministic vectors mean deterministic
similarity scores in tests); a real model — local or API-based — is a
follow-up swap behind the same interface, tracked as an open item below
rather than blocking this slice.

**Vector storage**: the help corpus is dozens to low hundreds of topics —
nowhere near the scale where brute-force in-memory cosine similarity is a
real cost, and it needs no new dependency (`sqlite-vec` or equivalent).
Keeping the comparison in app code, reading through `IHelpRepository`,
also keeps the abstraction genuinely storage-agnostic: a future ANN-backed
swap replaces `IHelpSearchIndex`'s implementation without touching
`HelpCommand` or `IHelpRepository`'s contract.

**Rebuild trigger**: matches the explicit, role-gated admin-command shape
already established by `dig`/`tunnel`/`describe`/the Slice 3 admin
commands, and keeps embedding-provider calls (which may be slow or, for a
future real provider, may fail/rate-limit) out of the content-edit hot
path entirely — a stale index is an accepted, explicit state between edits
and the next `helpindex rebuild`, not something the system tries to hide.

**Mechanism, in full:**

- `HelpTopic` — new EF Core aggregate root (own repository, per
  `persistence.md`'s "independently addressable" rule — not a `Thing`, not
  a `Behavior`): `Id`, `Key` (canonical name), `Aliases`, `Keywords`,
  `Category`, `Body` (authored text), `ContentHash`, `UpdatedAt`. Edited
  only via a new in-game admin command (`SecurityRole`-gated, mirroring
  `RoleGuardedCommand`), never by hand-editing a file.
- `HelpTopicChunk` — child of `HelpTopic`, saved/loaded with it as one unit
  (same shape as `Thing` + its `Behavior`s): `Id`, `HelpTopicId` FK,
  `ChunkIndex`, `ChunkText`, `Embedding` (`byte[]` BLOB, serialized
  `float[]`), `EmbeddingModelId`, `SourceContentHash` (detects staleness
  against the parent topic's current `ContentHash`).
- `IHelpRepository` (interface in `SharpMud.Engine/Core`, impl in
  `SharpMud.Persistence`, DI-registered in `SharpMud.Persistence.Sqlite`) —
  the aggregate-root repository, following `IThingRepository`'s existing
  shape exactly.
- `IEmbeddingProvider` (interface in `SharpMud.Engine/Core`) —
  `Task<float[]> EmbedAsync(string text, ct)` + `ModelId`. v1 impl is the
  deterministic stub, living in `SharpMud.Engine`, no external dependency.
- `IHelpSearchIndex` (interface in `SharpMud.Engine/Core`) —
  `Task<IReadOnlyList<HelpSearchHit>> SearchAsync(string query, ct)`. v1
  impl computes brute-force cosine similarity over chunks pulled through
  `IHelpRepository`, applies a configurable relevance threshold, and groups
  hits by topic. Depends only on `IHelpRepository`/`IEmbeddingProvider`, not
  on SQLite directly, so it's already storage-agnostic at v1.
- `HelpCommand` pipeline: exact name/alias match → keyword match → (only if
  both miss) semantic search through `IHelpSearchIndex`, filtered by
  threshold → "no help topic found" if nothing clears the bar. Semantic
  search never runs ahead of, or replaces, the first two tiers.
- `helpindex rebuild` — new role-gated admin command. Walks every
  `HelpTopic`, re-chunks `Body`, calls `IEmbeddingProvider` per chunk,
  writes new `HelpTopicChunk` rows (replacing stale ones for that topic).
  No automatic trigger on save/edit, no automatic staleness check at boot.

### Positive Consequences

- One content-authoring story across the whole repo (help topics now follow
  the same in-game-admin-command shape as world-building), not two
  divergent ones.
- The embedding-provider and vector-storage boundaries are real
  abstractions from day one (`IEmbeddingProvider`/`IHelpSearchIndex`), not
  something retrofitted once a real model is added — swapping either later
  touches one implementation class, not `HelpCommand` or the schema.
- Exact/alias/keyword lookup is completely unchanged in behavior; semantic
  search is strictly additive and only reachable on a miss.
- No new external dependency, no new native extension, fully testable
  offline (deterministic stub means deterministic test assertions on
  similarity/threshold behavior).

### Negative Consequences

- The v1 embedding stub has no real semantic quality — "how do I become a
  wizard?" will not actually retrieve a wizard-class topic until a real
  model is swapped in behind `IEmbeddingProvider`. Tracked as an explicit
  follow-up, not silently deferred.
- The index can go stale between a `helpindex` content edit and the next
  explicit `helpindex rebuild` — accepted, matches the "explicit rebuild"
  driver, but worth calling out since it's a real, visible-to-users gap if
  an admin forgets to rebuild after editing.
- Brute-force cosine means `SearchAsync`'s cost grows linearly with total
  chunk count; fine at today's/near-term corpus size, would need
  revisiting (likely the `sqlite-vec` option considered above) if the help
  corpus ever grows by orders of magnitude — no trigger for that exists
  today, so not designed for now, matching this repo's general
  don't-design-for-hypothetical-scale stance.
- `IHelpRepository`'s `byte[]` BLOB embedding column has no defined DynamoDB
  mapping — this slice only wires SQLite (`SharpMud.Persistence.Sqlite`);
  DynamoDB support for help topics is out of scope here and tracked as an
  open item.

## Pros and Cons of the Options

### Content authoring option 1: File-based only

- Good, because it matches WheelMUD's actual precedent and this repo's own
  docs/SPEC.md pattern.
- Bad, because it would be the *only* content-authoring path in the repo
  that isn't in-game (world-building already isn't), reintroducing a second
  content model for no reason specific to help topics.

### Content authoring option 2: In-game admin command only (chosen)

- Good, because it reuses ADR-0009's established mechanism/shape exactly,
  and keeps exactly one content-authoring story in the repo.
- Bad, because help content no longer gets git history/PR review the way
  `SPEC.md`/docs do — an admin can edit a topic with no review trail beyond
  whatever audit logging the admin-command layer already has.

### Content authoring option 3: Hybrid

- Good, because it offers PR-reviewable initial content plus fast in-game
  iteration afterward.
- Bad, because there's no defined sync/merge story once DB and file
  diverge, and no precedent anywhere in this repo for that shape — rejected
  directly by the user once that gap was surfaced.

### Embedding provider option 1: External API

- Good, because it's the best real-world retrieval quality available today.
- Bad, because it breaks offline self-hosting and adds network/cost/key
  management this slice doesn't need yet to prove the pipeline.

### Embedding provider option 2: Local/offline in-process model

- Good, because it keeps everything offline while giving real semantic
  quality.
- Bad, because it requires shipping and integrating a model asset now,
  before the abstraction/pipeline around it has even been validated.

### Embedding provider option 3: Deterministic stub now (chosen)

- Good, because it proves the full pipeline and abstraction boundary with
  zero external dependency, fully deterministic and testable.
- Bad, because retrieval quality is not representative — a real model swap
  is required before this is a usable natural-language search in practice.

### Vector storage option 1: Brute-force cosine, BLOB (chosen)

- Good, because it needs no new dependency and is simple/explicit/testable,
  matching the corpus's actual scale.
- Bad, because it doesn't scale past a small-to-moderate corpus without
  revisiting.

### Vector storage option 2: `sqlite-vec` extension

- Good, because it scales much further via real ANN search.
- Bad, because it's a native dependency and more moving parts than this
  slice's actual corpus size justifies.

### Rebuild trigger option 1: Explicit admin command (chosen)

- Good, because it matches the established OLC-style command pattern and
  keeps embedding-provider calls out of the edit hot path.
- Bad, because the index can go stale if an admin forgets to run it after
  an edit.

### Rebuild trigger option 2: Automatic on save/edit

- Good, because the index can never go stale relative to content.
- Bad, because it couples every content edit to embedding-provider
  latency/failure modes, inside what is otherwise a simple synchronous
  admin command.

### Rebuild trigger option 3: Automatic staleness check at boot

- Good, because no manual step is ever required.
- Bad, because boot time becomes provider-dependent, and a boot-time
  failure now blocks server startup for a concern (search quality) that
  shouldn't be able to do that.

## Links

- [ADR-0001](0001-wheelmud-reconciliation-roadmap.md) — WheelMUD
  Reconciliation Roadmap (this is Slice 5).
- [ADR-0009](0009-world-building-olc-command-surface.md) — the in-game
  admin-command authoring precedent this ADR's content-authoring decision
  follows directly.
- [PLAN-0010](../plans/0010-help-system-semantic-search.md) — execution
  plan for this decision.
- `docs/research/wheelmud-findings.md` §12 — WheelMUD's `HelpManager`/
  `HelpTopic` source dive this ADR's Context and Decision Outcome are based
  on; what's adopted (alias-based exact match as tier one) vs. not (file
  content, static singleton) is recorded there.
- `src/SharpMud.Engine/Commands/Builtin/HelpCommand.cs` — existing
  command-listing behavior this ADR extends with topic lookup; command-list
  behavior is unchanged.
- `src/SharpMud.Engine/Core/IThingRepository.cs` — existing aggregate-root
  repository shape `IHelpRepository` follows.
