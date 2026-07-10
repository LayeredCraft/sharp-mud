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
- Code comments: none by default. Only write one when the *why* isn't
  derivable from reading the code — a workaround, a non-obvious invariant,
  a shadow-FK resolution order dependency. Every comment currently in this
  codebase (see `ExitBehavior.cs`, `Program.cs`'s `EnsureCreatedAsync` note)
  fits that bar — match it, don't add narration comments.
- Commit messages: explain *why*, not *what* — the diff already shows what
  changed.
