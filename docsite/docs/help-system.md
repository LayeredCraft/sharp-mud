# Help System

`SharpMud.Engine` ships an in-game help system: `help <topic>` looks up
authored content by name, keyword, or — if neither matches — a
semantic-search fallback that can find the right topic even when the
player's words don't literally appear in it. Content is authored entirely
in-game (no help files to edit and redeploy), and semantic search is
optional, swappable infrastructure, not a hard dependency on any cloud AI
service.

## How lookup works

`help <query>` tries three tiers, in order, stopping at the first hit:

1. **Exact match** — `<query>` matches a topic's key or one of its
   aliases, case-insensitive.
2. **Keyword match** — `<query>` matches one of the topic's keywords,
   case-insensitive.
3. **Semantic search** — `<query>` is embedded into a vector and compared
   against every topic's precomputed content vectors. The closest match is
   returned only if it clears a relevance threshold; otherwise you get
   `No help topic found for '<query>'.` rather than a wrong-but-confident
   guess.

`help` with no arguments still does what it always did — lists every
command you're allowed to run.

## Authoring content

There's no help-file format. Content is authored and edited in-game, by a
player holding `SecurityRole.MinorBuilder` — the same role
[world-building commands](moderation-and-world-building.md#world-building-commands)
use:

```
helptopic wizard To become a wizard, join the Arcane Guild and study for three years.
```

`helptopic <key> <body>` creates a topic if `<key>` doesn't exist yet, or
overwrites its body if it does.

!!! note "Aliases and keywords aren't settable yet"
    The underlying model supports them, but there's currently no command
    that sets them — only `<key>`/`<body>` are authorable today. A topic's
    `<key>` is always usable for exact lookup; the keyword tier has
    nothing to match against until a follow-up command adds this.

## The semantic-search index is a rebuildable derivative

Editing a topic's body does **not** automatically update what semantic
search matches against — the embedding index only changes when you
explicitly run:

```
helpindex rebuild
```

This re-embeds every topic's current content and replaces its search
index entirely. Nothing triggers this automatically (not on save, not at
server boot) — that's deliberate, so an embedding call (which could be
slow, or fail, for a real model) never sits in the content-edit path.
Exact/keyword lookup is unaffected either way, since both read a topic's
current content directly.

## Swapping in a real embedding model

By default, semantic search runs on `StubEmbeddingProvider` — a
deterministic placeholder with no external dependency, included so the
whole pipeline works out of the box. It only recognizes literal word
overlap: `help wizard` won't find a topic about "sorcery" unless the word
"wizard" (or something that hashes similarly) actually appears in it.

The sample app (`SharpMud.Samples.Classic`) shows how to swap in a real,
**local** embedding model — no cloud API, no API key — using
[`SmartComponents.LocalEmbeddings`](https://github.com/dotnet/smartcomponents),
a small Microsoft package that runs an ONNX embedding model in-process:

```csharp
public sealed class LocalEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    private readonly LocalEmbedder _embedder = new();

    public string ModelId => "smartcomponents-local-embeddings-default";
    public double RelevanceThreshold => 0.58;

    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var embedding = _embedder.Embed<EmbeddingF32>(text);
        return Task.FromResult(embedding.Values.ToArray());
    }

    public void Dispose() => _embedder.Dispose();
}
```

Registered *after* `AddSharpMudSqlitePersistence(...)`, so it overrides
the default:

```csharp
builder.Services.AddSharpMudSqlitePersistence(hostOptions.DbPath);
builder.Services.AddSingleton<IEmbeddingProvider, LocalEmbeddingProvider>();
```

```xml
<PackageReference Include="SmartComponents.LocalEmbeddings" Version="0.1.0-preview10148" />
```

The package downloads its default model (`bge-micro-v2`, ~23 MiB) at
build time via an MSBuild target — no runtime network call, and nothing
to manage yourself.

!!! warning "Pick your own relevance threshold — don't reuse the stub's"
    Different embedding models produce cosine similarity scores on very
    different scales, and the "unrelated" band can be wider than a quick
    test suggests. Measured directly against this model: longer, specific
    unrelated phrases like "the weather today" scored **~0.33**, but
    short, generic single-word queries — including built-in verbs like
    `up`/`who` — scored as high as **~0.54**. Real matches (including pure
    synonyms sharing no words at all) scored **~0.62–0.68**. A first
    attempt at `0.5` here caused `help up` to incorrectly match an
    unrelated topic; `0.58` was needed to leave real margin above the
    wider-than-expected unrelated band. The stub's `0.15` threshold —
    tuned for a sparse hashed-bag-of-words vector that scores near zero
    for unrelated text — would let almost everything through on a dense
    model like this one. `IEmbeddingProvider` has its own
    `RelevanceThreshold` property for exactly this reason: measure *your*
    model against short/generic queries as well as long/specific ones
    before picking a value, don't copy `0.15` or `0.58` blind.

With this in place, a topic that never mentions the word "wizard" at all
is still found by `help wizard`, `help sorcerer`, or a full natural-language
question like `help how do i cast spells` — genuine synonym/paraphrase
matching, not just literal word overlap.

## Under the hood

Both the embedding step and the search step sit behind small, swappable
interfaces:

- `IEmbeddingProvider` — text in, vector out, plus the model's own
  relevance threshold.
- `IHelpSearchIndex` — given a query, returns the best-matching topics.
  The shipped implementation (`CosineHelpSearchIndex`) is a simple,
  brute-force in-memory comparison — no vector database required at the
  scale of a typical help corpus.

Full design rationale — why semantic search is a fallback tier rather than
the primary lookup, why the embedding index is explicitly rebuilt rather
than kept live, and the numbers behind the `LocalEmbeddingProvider`
threshold above — is recorded in the sharp-mud repo's ADRs:

- [ADR-0010](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0010-help-system-semantic-search.md) — Help System + Semantic Search Fallback
- [ADR-0011](https://github.com/LayeredCraft/sharp-mud/blob/main/docs/adr/0011-local-embedding-provider-for-sample.md) — Local Embedding Provider for the Sample App
