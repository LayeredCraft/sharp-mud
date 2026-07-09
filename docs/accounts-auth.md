# Accounts & Auth

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md)/[engine-vs-ruleset.md](engine-vs-ruleset.md)
for `Player`/`PlayerBehavior`, and [persistence.md](persistence.md) for the
lookup this flow depends on.

## Decision (revised — supersedes the earlier OAuth design)

Traditional username/password, not external OAuth. **No separate `Account`
entity** — one character per login, username + password hash live directly
on the player `Thing` (via `PlayerBehavior`, see
[engine-vs-ruleset.md](engine-vs-ruleset.md)). This drops the OAuth
device-code flow, the character-select step, and an entire repository —
simplest model that still has real auth. Classic small-MUD convention (no
"alts"); revisit if multi-character-per-login is ever actually wanted.

Deferred until networked play needs it — the local CLI stays login-free, per
`SPEC.md`. Telnet currently has a placeholder name-only prompt (no password
check); this replaces that placeholder with the real thing.

## Identity Model

```csharp
public sealed class PlayerBehavior : Behavior
{
    public required string Username { get; init; }
    public required string PasswordHash { get; set; }
    public ISession? Session { get; set; }
    public List<string> Aliases { get; } = [];
}
```

`Username`/`PasswordHash` replace the old `AccountId`/`ExternalAuthProvider`/
`ExternalAuthId` fields. Password hashing via
`Microsoft.AspNetCore.Identity`'s `PasswordHasher<TUser>` — PBKDF2 with a
random salt, versioned hash format (iteration count can be raised later
without invalidating existing hashes), no need to hand-roll crypto. Works
fine referenced outside a full ASP.NET Core host (it's just a class, no
framework dependency beyond the package itself).

Lookup on login: `IPlayerRepository.GetByUsernameAsync(username, ct)` (see
[persistence.md](persistence.md)) — username is unique, checked at account
creation time.

`AccountId` (`src/SharpMud.Engine/Core/AccountId.cs`) is now unused and
should be removed when this is implemented — nothing needs an auth identity
separate from the player `Thing` anymore.

## Terminal Login Flow

Classic MUD login prompt, replacing both the OAuth device-code flow and the
current name-only placeholder:

1. On connect, before any character interaction: `"Username: "`, read a
   line.
2. If the username doesn't exist yet: prompt to create an account —
   `"Create a new character? (y/n)"`, then `"Password: "` /
   `"Confirm password: "` (not echoed — see Open Items on terminal echo
   suppression), hash and store, then proceed into character creation (see
   [character.md](character.md)).
3. If the username exists: `"Password: "` (not echoed), check against
   `PasswordHash` via `PasswordHasher.VerifyHashedPassword`. Wrong password
   → generic `"Login incorrect."` (don't reveal whether the username existed
   — standard practice, also authentic to classic MUD login prompts), allow
   retry (see Open Items for retry/lockout limits).
4. On success, the session attaches to that player `Thing` and play begins
   as today (`SessionLoop.RunAsync`).

Applies uniformly across networked transports (Telnet now, SSH/WebSocket
later) — no transport-specific auth special-casing needed, same as the OAuth
design's intent.

## Open Items

- Terminal echo suppression for password entry — `ISession`/`TelnetSession`
  currently has no concept of "don't echo input back"; needs a mechanism
  (telnet IAC WILL ECHO negotiation server-side, or client-side convention)
  before this is genuinely secure over Telnet rather than just
  password-shaped.
- Failed-login retry/lockout policy (unlimited retries vs. capped, delay
  between attempts) not yet chosen.
- Username validation rules (length, allowed characters, case sensitivity)
  not yet chosen.
- Password strength/length requirements not yet chosen.
- Password reset flow — with no email/OAuth identity backing the account,
  there's no "forgot password" recovery path; not designed. Likely
  admin-assisted reset only (ties into deferred moderation tooling, see
  `SPEC.md`).
