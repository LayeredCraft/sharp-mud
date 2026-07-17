# Task: PR review

**Trigger:** asked to review a pull request or diff in this repo — "review
this PR", "review my changes", "look over this before I merge". Distinct
from the generic `/code-review` skill, which hunts for correctness bugs
and simplification/efficiency cleanups in the diff itself — this task is
about **sharp-mud's own process conformance** on top of that: does the
change carry the decision trail it's supposed to, does it follow this
repo's established standards, and does it get a real, complete,
inline-commented review posted to the PR rather than a private summary.
Run `/code-review` too for something non-trivial; this task doesn't
replace it, it wraps it with repo-specific context and the actual posting
mechanics. **This task produces new findings on a PR** — for handling
feedback someone else already posted (including replying to your own
review from a prior run of this task), see
`tasks/respond-to-pr-feedback.md` instead.

## Load these references first

Always:
- `references/coding-standards.md`
- `references/design-decisions.md`
- `references/documentation.md`
- `references/code-of-conduct.md` (how to phrase findings)
- `references/review-emoji-legend.md` (every comment and the verdict need
  the right emoji — this is the only place that mapping is defined)

Conditionally, based on what the diff actually touches:
- `references/testing.md` — any new/changed behavior in `src/`
- `references/persistence.md` — anything under `SharpMud.Persistence`, a
  new/changed `Behavior`, or an EF Core config class
- `references/security.md` — auth, secrets, config, or anything on the
  Telnet (untrusted) input path

## Procedure

A review is **complete** or it hasn't happened — don't stop after the
first finding and leave the rest of the diff for "next time." Walk every
changed file in one pass before posting anything, and use the findings
list built in step 4 as the single source of truth for what gets posted;
don't post opportunistically while still reading.

1. **Get the diff.** If reviewing a GitHub PR, `gh pr diff <number>` (or
   `gh pr view <number>` for the description/commits) rather than guessing
   from memory. If reviewing local uncommitted/committed-but-unpushed work,
   `git diff`/`git log` against the base branch. Note the PR number and the
   head commit SHA now (`gh pr view <number> --json headRefOid -q
   .headRefOid`) — inline comments need it later.
2. **Identify what changed** — which subsystems (`SharpMud.Engine` /
   `SharpMud.Ruleset.Classic` / `SharpMud.Persistence` / `SharpMud.Host` /
   `SharpMud.Adapters.*`) and what kind of change (new feature, bug fix,
   refactor, docs-only). This decides which conditional references from
   above actually apply.
3. **Find the associated ADR/plan.** Look for an `ADR-NNNN`/`PLAN-NNNN`
   reference in the PR title, description, commit messages, or branch
   name. Per `design-decisions.md`'s light/deep-dive rule: if the change is
   non-trivial and no ADR exists, that's a real finding, not a nice-to-have
   — track it like any other. If an ADR is referenced, read it and its
   plan, and confirm the code actually matches the ADR's Decision Outcome —
   a diverging implementation is either a finding or a sign the ADR needs a
   follow-up; decide which and say so in the finding.
4. **Build the complete findings list before posting anything.** Walk
   every changed file/hunk against the loaded references:
   - `coding-standards.md` conformance — naming, nullable annotations,
     async patterns, DI/constructor style, error handling, file layout,
     parameter-count rule.
   - `testing.md` conformance — new engine/ruleset/persistence behavior
     has new tests, following this repo's xUnit v3 + AutoFixture +
     NSubstitute + AwesomeAssertions conventions.
   - `documentation.md` conformance — relevant `docs/*.md` updated in the
     same PR, not deferred.
   - The conditional references from above, wherever the diff touches
     that territory (EF Core mapping correctness, no plaintext secrets,
     Telnet input treated as untrusted, etc).
   - Also run `/code-review`-style correctness/simplification scanning
     over the same diff if that skill hasn't already been run separately.

   For each finding, record: file, line, a severity per
   `review-emoji-legend.md` (🐛/⚠️/📝/❓), and a one-line summary + concrete
   failure mode (per `code-of-conduct.md`'s voice — specific, not vague).
   Present the **entire list at once**, not one at a time, so the user sees
   the full picture before anything is posted.
5. **Confirm with the user before posting.** For each finding in the list,
   get explicit agreement that it should be posted — the user may accept
   all, reject some, or adjust a severity. Never post a finding the user
   didn't confirm. Don't silently drop a finding the user didn't
   explicitly address, either — resolve every item in the list one way or
   another (post it, or note it was intentionally dropped) before moving
   on to posting.
6. **Post each confirmed finding as an inline PR comment**, emoji-prefixed
   per `review-emoji-legend.md`:
   ```
   gh api repos/{owner}/{repo}/pulls/{pr}/comments \
     -f body="🐛 <finding text>" \
     -f commit_id="<head SHA from step 1>" \
     -f path="<file path>" \
     -F line=<line number> \
     -f side=RIGHT
   ```
   Post every confirmed finding this way before moving to the verdict —
   don't interleave posting with more review reading.
7. **Determine the overall verdict** per `review-emoji-legend.md`: 🎉
   Approve if no 🐛 findings are open, 🛑 Request changes if any are,
   💬 Comment only in the rare case neither applies yet. Confirm the
   verdict (and its summary body) with the user before submitting.
8. **Submit the verdict**, emoji-prefixed, summarizing what was found and
   linking back to the inline comments rather than repeating them in full:
   ```
   gh pr review <number> --approve -b "🎉 <summary>"
   gh pr review <number> --request-changes -b "🛑 <summary>"
   gh pr review <number> --comment -b "💬 <summary>"
   ```

## Output

If invoked as part of a formal review flow that expects structured
findings (e.g. the `ReportFindings` tool) in addition to posting to
GitHub, use that too. Otherwise, the record of a completed review *is*
the posted inline comments plus the submitted verdict — that's the
deliverable, not a private chat-only summary.
