# [ADR-0011] Local Embedding Provider for the Sample App

**Status:** Accepted

**Date:** 2026-07-24

**Decision Makers:** solo (light dive, discussed with the user)

## Context

[ADR-0010](0010-help-system-semantic-search.md) shipped `IEmbeddingProvider`
as a seam specifically so a real embedding model could be swapped in later
without touching `HelpCommand`/`IHelpSearchIndex` — the only implementation
that shipped with it, `StubEmbeddingProvider`, is a deterministic
hashed-bag-of-words placeholder with no real semantic understanding, by
design. The user asked to actually do that swap in the sample app
(`SharpMud.Samples.Classic`) and try it, while still not requiring a cloud
service (AWS Bedrock or similar) — consistent with ADR-0010's "avoid a
large AI framework" driver, which was about the *default*/core
implementation, not a blanket ban on ever using a real model.

This is a light dive: the shape of the change is already decided by
ADR-0010 (implement `IEmbeddingProvider`, register it in place of the
stub) — the only open question is which library, since this is the first
third-party ML dependency anywhere in this repo.

## Decision Drivers

- Must run fully in-process, no separate server/API to stand up (rules out
  Ollama for a sample app — a genuinely local option, but an extra process
  to install/run alongside the MUD, not just a NuGet package).
- Must not require a cloud API/key (rules out AWS Bedrock, OpenAI, etc. —
  the whole point of the ask).
- Should be a small, purpose-built dependency, not a heavyweight ML
  framework, matching ADR-0010's "keep it small, explicit, testable" spirit
  even for this follow-up.
- Only needs to live in the sample project — `SharpMud.Engine` stays free
  of any ML dependency either way, since `IEmbeddingProvider` is the
  abstraction and nothing else references a concrete implementation
  directly (same shape as `IWorldBuilder`/`ClassicWorldBuilder`).

## Considered Options

1. **`SmartComponents.LocalEmbeddings`** (Microsoft, `dotnet/smartcomponents`) —
   a small package purpose-built for in-process local embeddings; bundles a
   default ONNX model (`bge-micro-v2`, ~23 MiB) downloaded at build time via
   an MSBuild target, no runtime network call once built.
2. **Raw ONNX Runtime + a sentence-transformer model** (e.g.
   `all-MiniLM-L6-v2` exported to ONNX) via `Microsoft.ML.OnnxRuntime`
   directly, with a separate tokenizer library
   (`Microsoft.ML.Tokenizers`).
3. **ML.NET** (`Microsoft.ML`) - classic-ML-pipeline-oriented; embeddings
   aren't a first-class primitive the way `LocalEmbedder.Embed` is.
4. **Ollama** - a local HTTP server (`nomic-embed-text` or similar), genuinely
   local/offline but a separate process to install and run.
5. **LLamaSharp** - llama.cpp bindings, supports embedding models; heavier
   native-binary dependency surface than this needs.

## Decision Outcome

Chosen: **"1 — `SmartComponents.LocalEmbeddings`"**, for the sample project
only. `LocalEmbeddingProvider` (new,
`samples/SharpMud.Samples.Classic/LocalEmbeddingProvider.cs`) implements
`IEmbeddingProvider` by wrapping `LocalEmbedder.Embed<EmbeddingF32>(text)`
and returning its `.Values` (a `ReadOnlyMemory<float>`) as a `float[]`.
Registered in `Program.cs` as `AddSingleton<IEmbeddingProvider,
LocalEmbeddingProvider>()`, called *after*
`AddSharpMudSqlitePersistence(...)` — the last registration of a
non-keyed service wins when .NET's DI container resolves it, so this
overrides the stub the persistence extension registers by default,
without changing that extension itself (every other consumer still gets
the stub unless they opt in the same way).

Closest match to the drivers: no external service/API, no separate process
to run, purpose-built for exactly "give me an embedding vector from text
in-process," and the model ships via the package's own build-time download
step rather than the sample app needing its own model-management code.

**A real second finding, discovered by actually running this, not
anticipated when writing this ADR**: `CosineHelpSearchIndex`'s relevance
threshold (`0.15`) was hardcoded and tuned only against
`StubEmbeddingProvider`'s output. Measured directly against
`LocalEmbeddingProvider`: a topic body about spellcasting scored
**~0.62–0.68** cosine similarity against real synonym queries (`wizard`,
`sorcerer`, `how do i cast spells` — none of those words appear in the
topic's key or body) but also **~0.33–0.40** against genuinely unrelated
queries (`rusty sword`, `north`, `the weather today`). `0.15` sits below
both clusters — with the stub's threshold, semantic search returned a
hit for *every* query, including ones with no real relationship to any
topic. This is a general property of dense sentence-embedding models
(anisotropic embedding space — unrelated text still tends to score
noticeably above zero), not a bug in this particular model.

Fixed by moving `RelevanceThreshold` from a `CosineHelpSearchIndex`
constant onto `IEmbeddingProvider` itself — the threshold that makes sense
is a property of *which model produced the vectors*, not of the search
algorithm comparing them. `StubEmbeddingProvider.RelevanceThreshold`
stays `0.15`; `LocalEmbeddingProvider.RelevanceThreshold` is `0.58`.
`CosineHelpSearchIndex` now reads `_embeddingProvider.RelevanceThreshold`
instead of owning a constant.

**A second round of the same problem, caught by actually using the
feature, not just the earlier synonym test**: `0.5` (the first value
chosen) still let `help up` incorrectly match the spellcaster topic.
Measuring short, generic single-word queries specifically (built-in verbs:
`up`, `who`, `look`, `quit`, `inventory`) showed the "unrelated" cluster is
wider than the first pass suggested - as high as **~0.54** (`up`), not
capped at the ~0.40 seen from longer, more specific unrelated phrases like
"the weather today" (~0.33) or "rusty sword" (~0.40). `0.58` leaves real
margin above that wider unrelated band and below the ~0.62+ related
cluster; re-verified live afterward (`help up`/`help who`/`help north` all
correctly report no match, `help wizard`/`help sorcerer`/a full
natural-language paraphrase still find the topic). Still an empirical,
single-corpus calibration, not a formula - flagged in the code comment and
here as something to revisit once real topic content and real player
queries exist to test against.

## Positive Consequences

- Sample app now demonstrates a *real* semantic embedding model behind the
  same `IHelpSearchIndex`/`HelpCommand` pipeline, no other code changed —
  concrete proof the ADR-0010 abstraction boundary actually holds.
- Still fully offline once built — no API key, no per-call network request,
  no cloud billing.
- `SharpMud.Engine`/`SharpMud.Persistence.Sqlite` gain no new dependency;
  the ML package reference lives only in the sample project's `.csproj`.

## Negative Consequences

- `SmartComponents.LocalEmbeddings` is a preview-only package
  (`0.1.0-preview10148`) from an experimental Microsoft repo, not a
  1.0/GA library — acceptable for a sample app demonstrating the seam, not
  a decision to depend on it anywhere load-bearing.
- Adds a real, if small (~23 MiB), model download to the sample's build —
  first time this repo's build has needed network access for anything
  beyond ordinary NuGet restore.
- `LocalEmbedder` implements `IDisposable`; registering it as a DI
  singleton means the container disposes it at shutdown, not per-request —
  fine for this always-resident use, called out so it isn't mistaken for
  an oversight.

## Pros and Cons of the Options

### `SmartComponents.LocalEmbeddings` (chosen)

- Good, because it's purpose-built for exactly this ("give me a
  `float[]`/embedding from text, locally"), with the least code needed to
  wire up.
- Good, because the model ships via the package's own build step, no
  separate model-management code in this repo.
- Bad, because it's a preview-quality package from an experimental repo.

### Raw ONNX Runtime + a sentence-transformer model

- Good, because it avoids depending on an experimental/preview package,
  and gives full control over model choice.
- Bad, because it requires this repo to own tokenization and model-file
  management itself - meaningfully more code for the same outcome.

### ML.NET

- Good, because it's a mainstream, well-supported Microsoft ML library.
- Bad, because embeddings aren't a natural first-class primitive in its
  API the way `LocalEmbedder.Embed` is — a worse fit for this narrow need.

### Ollama

- Good, because it's genuinely local/offline with no cloud dependency,
  and supports swapping models trivially.
- Bad, because it's a separate server process to install and run
  alongside the MUD, not just a library reference — a bigger operational
  ask for a sample app than a NuGet package.

### LLamaSharp

- Good, because llama.cpp is a proven, widely-used local-inference engine.
- Bad, because its native-binary dependency surface is heavier than a
  narrow "embed this text" need justifies here.

## Links

- [ADR-0010](0010-help-system-semantic-search.md) - the `IEmbeddingProvider`
  seam this ADR fills with a real implementation.
- [docs/help-system.md](../help-system.md) - updated to describe this as an
  available (not default) provider.
- `docsite/docs/help-system.md` - user-facing walkthrough of swapping in a
  real provider, written from this ADR's Decision Outcome.
- [dotnet/smartcomponents](https://github.com/dotnet/smartcomponents/blob/main/docs/local-embeddings.md) -
  library documentation consulted for the exact API shape.
