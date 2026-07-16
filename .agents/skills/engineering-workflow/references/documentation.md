# Documentation writing

- Every subsystem doc in `docs/` follows the same shape: what it does now,
  the decisions that shaped it, and *why* alternatives were rejected — not
  just a feature description. Look at `docs/persistence.md` or
  `docs/accounts-auth.md` as the template.
- Update docs in the same PR as the behavior change, not as follow-up
  cleanup.
- Research/external-reference notes go in `docs/research/`, kept separate
  from subsystem docs, with a `## Decisions` section at the end stating
  what was adopted vs. deliberately deviated from.
- Code should be self-documenting — clear names and small, well-shaped
  methods carry the "what." Inline comments explain the *why*: a
  workaround, a non-obvious invariant, a shadow-FK resolution order
  dependency, or a genuinely non-obvious algorithm. Every comment currently
  in this codebase (see `ExitBehavior.cs`, `Program.cs`'s
  `EnsureCreatedAsync` note) fits that bar — match it. If you find yourself
  writing a comment that just restates the line below it in English,
  delete the comment instead (or, more often, that's a sign the code below
  it should be renamed/restructured until it doesn't need the restating).
  Inline comments on genuinely non-obvious algorithmic code are encouraged
  and expected — the bar above is about *narration* comments, not about
  comments in general.
- **XML doc comments are required on every public member** — classes,
  interfaces, methods, properties, and events — across all projects. This
  is a real gap today: there are currently zero XML doc comments anywhere
  in this codebase. Treat it as tracked debt rather than a blocker on
  unrelated work: any public member you add or substantially change from
  now on gets a doc comment, and a dedicated backfill pass across all six
  projects (`SharpMud.Engine`, `SharpMud.Ruleset.Classic`,
  `SharpMud.Persistence`, `SharpMud.Host`, `SharpMud.Adapters.Telnet`,
  `SharpMud.Adapters.Cli`) is open, tracked work — pick it up as its own
  PR (or a PR per project) rather than a drive-by inside a feature change,
  same treatment as the `Directory.Build.props` gap in `coding-standards.md`.
  A good XML doc comment states what the member does and any contract a
  caller needs to know (thrown exceptions, null behavior, ordering
  requirements) — it shouldn't just restate the member's name in sentence
  form, the same "why, not what" bar inline comments are held to above.
- Commit messages: explain *why*, not *what* — the diff already shows what
  changed.
