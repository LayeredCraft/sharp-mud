# Task: Respond to PR feedback

**Trigger:** asked to address, respond to, or handle feedback/comments left
on an existing PR in this repo — "address that feedback", "handle the
review comments", "what did Codex/the reviewer say, take care of it".
Distinct from `tasks/pr-review.md` (which *produces* findings on a PR);
this task *consumes* findings someone else already posted. If you're doing
both in one sitting (reviewing, then later handling replies to your own
review), still treat them as two separate passes of this repo's process —
don't blend "propose new findings" and "resolve existing ones" into one
undifferentiated pass.

## Load these references first

Always:
- `references/coding-standards.md`
- `references/documentation.md`
- `references/code-of-conduct.md`
- `references/review-emoji-legend.md`

Conditionally, based on what the feedback actually touches once you've
read it:
- `references/design-decisions.md` — feedback questions an architecture
  choice, or a fix needs its own ADR/plan update
- `references/testing.md` — any fix needs a regression test (it almost
  always does)
- `references/persistence.md` / `references/security.md` — feedback
  touches that territory

## Procedure

This is three distinct phases, in order — don't collapse them. Phase 1 is
read-only and sequential (one comment at a time, waiting on the user each
time). Phase 2 is a single batch of implementation. Phase 3 is a cleanup
pass over every comment from phase 1.

### Phase 1 — triage every comment, one at a time

1. **Fetch every comment on the PR**, not just the newest ones, and not
   just the first page — a busy PR can exceed one page on any of these,
   and "fetch every comment" means every comment, not "every comment that
   fit on page one":
   - Inline review comments: `gh api --paginate
     repos/{owner}/{repo}/pulls/{pr}/comments`
   - Top-level issue comments: `gh pr view <number> --json comments` (this
     one already returns the full list, not paginated REST)
   - Review bodies: `gh api --paginate
     repos/{owner}/{repo}/pulls/{pr}/reviews`
   - Resolution state via GraphQL, so you don't re-triage something
     already resolved — page through with `endCursor`/`hasNextPage` if a
     PR has more than 100 threads:
   ```
   gh api graphql -f query='
     query($owner:String!, $repo:String!, $pr:Int!, $after:String) {
       repository(owner:$owner, name:$repo) {
         pullRequest(number:$pr) {
           reviewThreads(first:100, after:$after) {
             pageInfo { hasNextPage endCursor }
             nodes {
               id isResolved
               comments(first:10) { nodes { id body path line author { login } } }
             }
           }
         }
       }
     }' -f owner=<owner> -f repo=<repo> -F pr=<number>
   ```
   If `pageInfo.hasNextPage` is `true`, repeat the query passing
   `pageInfo.endCursor` as `-f after=<cursor>` until it's `false`.
2. **For each unresolved comment/thread**, using the loaded references,
   classify it as one of:
   - **Real issue** — a genuine defect, standards deviation, or gap per
     the loaded references; needs a code/doc change.
   - **Deferred** — a valid point, but legitimately out of scope for this
     PR (e.g. a larger refactor, a pre-existing issue merely surfaced
     here). Per `design-decisions.md`/`contributing.md`, deferring doesn't
     mean dropping it — it becomes a tracked follow-up (a note in the
     relevant `docs/*.md` Open Items, or an explicit statement that a new
     issue/PR will track it).
   - **No action** — acknowledgment, praise, a bot's boilerplate, or
     something already addressed by a prior commit.
3. **Display each classified comment to the user one at a time** — the
   comment text, your classification, and your recommendation (what fix
   you'd make, or why it should be deferred, or why no action is needed).
   Wait for the user's decision on *that* comment before moving to the
   next one. Record the user's decision (fix / defer / no action, and any
   specifics they added) — this record drives phases 2 and 3. Do not
   batch-display the whole list up front the way `pr-review.md` does with
   fresh findings — these are already-posted comments from someone else,
   and each may need independent judgment before you've even reasoned
   about the next one.

### Phase 2 — implement, once, after triage is complete

4. Once every comment has a recorded decision, implement all "fix"
   decisions together (not one commit per comment unless the changes are
   genuinely unrelated) — following `coding-standards.md`, adding
   regression tests per `testing.md`, updating `docs/*.md` per
   `documentation.md` in the same change. For "deferred" decisions, make
   the tracking update now too (the Open Items note, the follow-up
   reference) — deferring shouldn't mean doing nothing.
5. Build and run the full test suite before committing — don't push
   something that doesn't compile or regresses existing coverage.
6. Commit and push per `git-workflow`'s conventions (or this repo's
   established commit-message style if `git-workflow` isn't in play) —
   one coherent commit (or a small number of logically-grouped commits)
   covering everything from phase 1's "fix" decisions.

### Phase 3 — reply and resolve, once the fix is pushed

7. **Reply to every comment/thread from phase 1**, not just the ones that
   got code changes. GitHub has two different comment types with two
   different reply endpoints — use the one matching what you fetched in
   step 1, not just the review-comment one for everything:
   - **Inline review comments** (from the `pulls/{pr}/comments` fetch) —
     reply on the thread:
     ```
     gh api repos/{owner}/{repo}/pulls/{pr}/comments/{comment_id}/replies \
       -f body="<reply text>"
     ```
   - **Top-level issue comments and review bodies** (from `gh pr view
     --json comments` / `pulls/{pr}/reviews`) — these aren't inline review
     comments, so the endpoint above doesn't apply to them (it will either
     404 or silently reply to the wrong thing). Reply as a new top-level
     PR comment instead:
     ```
     gh pr comment <number> --body "<reply text>"
     ```
     or equivalently `gh api repos/{owner}/{repo}/issues/{pr}/comments -f
     body="<reply text>"` (pull requests are issues for this endpoint).
   - Real issue, fixed → reply pointing at the fixing commit SHA and what
     changed. No severity emoji needed (it's a resolution, not a new
     finding) — plain text, or a 👍 if also acknowledging the original
     point was correct.
   - Deferred → reply explaining why, and where it's now tracked (the doc
     section, the follow-up).
   - No action → a short acknowledgment reply, or skip the reply entirely
     for pure praise/thank-you comments — resolving the thread is enough
     (inline only — top-level comments have no "resolve," see step 8).
8. **Resolve every inline review thread** handled in this pass:
   ```
   gh api graphql -f query='
     mutation($id:ID!) {
       resolveReviewThread(input:{threadId:$id}) { thread { isResolved } }
     }' -f id=<thread id from step 1's query>
   ```
   `resolveReviewThread` only applies to inline review-comment threads —
   top-level issue comments and review bodies have no "resolved" state on
   GitHub at all; the reply in step 7 is the entire deliverable for those.

## Output

The deliverable is the pushed fix commit(s) plus every triaged thread
replied-to and resolved on GitHub — not a private summary. If the user
declines to act on every single comment (e.g. explicitly says "skip that
one"), still reply on it explaining the decision and resolve it, rather
than leaving it dangling unresolved with no trail.
