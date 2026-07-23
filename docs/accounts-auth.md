# Accounts & Auth

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md)/[engine-vs-ruleset.md](engine-vs-ruleset.md)
for `Player`/`PlayerBehavior`, and [persistence.md](persistence.md) for the
lookup this flow depends on.

**Implemented and verified** — see Verified below.

## Decision (revised — supersedes the earlier OAuth design)

Traditional username/password, not external OAuth. **No separate `Account`
entity** — one character per login, username + password hash live directly
on the player `Thing` (via `PlayerBehavior`, see
[engine-vs-ruleset.md](engine-vs-ruleset.md)). This drops the OAuth
device-code flow, the character-select step, and an entire repository —
simplest model that still has real auth. Classic small-MUD convention (no
"alts"); revisit if multi-character-per-login is ever actually wanted.

The local CLI stays login-free, per `SPEC.md` — `LoginFlow` is only used by
`TelnetTransportBackgroundService`'s Telnet path.

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
`ExternalAuthId` fields; `AccountId` (`src/SharpMud.Engine/Core/AccountId.cs`)
has been deleted. `Thing.Name` (the display name shown to other players) is
set equal to `Username` at character creation — there's no separate
"character name" concept, matching the "no alts" simplification above.

Password hashing: `PasswordHashing` (`src/SharpMud.Hosting/PasswordHashing.cs`)
wraps `Microsoft.Extensions.Identity.Core`'s `PasswordHasher<TUser>` — PBKDF2
with a random salt, versioned hash format. Lives in `SharpMud.Hosting`, not
Engine — this is login-flow infrastructure, not game logic, and Engine
shouldn't pick up an Identity package dependency for something only the
networked login flow needs. `TUser` is unused by the default hasher's
algorithm; `object` is passed as a harmless stand-in rather than inventing an
unused marker type.

`Username` uniqueness is enforced both in the login flow (checked before
character creation) and at the DB level (`PlayerBehaviorConfiguration` puts
a unique index on it, scoped correctly even inside the shared TPH
`Behaviors` table since non-`PlayerBehavior` rows just have `NULL` there,
which unique indexes don't conflict on).

Lookup on login: `IThingRepository.FindPlayerByUsernameAsync(username, ct)`
— matches on `PlayerBehavior.Username`, not `Thing.Name` (they're equal
today, but this is the semantically correct field to search regardless of
whether that ever changes).

## Terminal Login Flow

Implemented in `src/SharpMud.Hosting/LoginFlow.cs`, called from
`TelnetTransportBackgroundService.HandleConnectionAsync` (replacing the old
name-only placeholder):

1. `"Username: "`, read a line. Empty input disconnects.
2. Look up the username — first among currently-live/online players in
   `World` (covers reconnecting within the same server run), then via
   `IThingRepository.FindPlayerByUsernameAsync` (covers a genuinely fresh
   process that loaded the persisted world but this particular player hasn't
   reconnected yet). A match found via the repository is reattached into the
   *live* room `Thing` (looked up by ID in `World`), not the freshly
   reconstructed standalone room object the repository call returned — see
   [persistence.md](persistence.md) for why that distinction matters.
3. **Username exists**: `"Password: "` with echo suppressed (see
   `ISession.SetEchoAsync` below), verified via `PasswordHashing.Verify`.
   Wrong password → generic `"Login incorrect."` (doesn't reveal whether the
   username existed), retry up to 3 attempts (not a considered final policy
   — see Open Items), then loop back to the username prompt rather than
   dropping the connection outright. Correct password, and:
   - the character's `PlayerBehavior.IsBanned` (ADR-0005) → `"This account
     has been banned."`, connection dropped, before any of the checks
     below — a banned account never reaches the already-logged-in/Linkdead
     branches.
   - the character is still actively `Playing` with a live, connected
     session → `"That character is already logged in."`, connection
     dropped (unchanged by ADR-0004 — see
     [networking.md](networking.md#reconnect--session-resumption--adr-0004)).
   - the character is `Linkdead` (disconnected, not `quit`, within
     `ReconnectPolicy.GraceWindow`) → resumes the same character in place,
     `"Welcome back."`, per ADR-0004.
4. **Username doesn't exist**: `"Create a new character? (y/n)"`. `n` (or
   anything but `y`) loops back to the username prompt. `y` → `"Password: "`
   / `"Confirm password: "`, both echo-suppressed; mismatch or empty →
   `"Passwords didn't match."`, loop back to username. Match → hash, create
   the character via `IPlayerFactory.CreatePlayer` (the sample's
   `ClassicPlayerFactory` wraps `HubWorldBuilder.CreatePlayer`), and
   `IThingRepository.SaveTreeAsync` it immediately (not waiting for the
   eventual disconnect-triggered save) so a crash right after creation
   doesn't lose the new login.
5. On success, `SessionLoop.RunAsync` takes over exactly as before.

Applies uniformly across networked transports (Telnet now, SSH/WebSocket
later) — no transport-specific auth special-casing needed in the flow itself
(though the actual `SetEchoAsync` implementation is transport-specific, see
below).

## Echo Suppression

`ISession` gained `SetEchoAsync(bool enabled, CancellationToken ct)`.
`TelnetSession` sends real telnet protocol bytes — `IAC WILL ECHO` (bytes
`255, 251, 1`) when `enabled=false` to ask an RFC-1116-compliant client to
stop echoing locally (and since the server never echoes it either, typed
input doesn't appear at all), `IAC WONT ECHO` (`255, 252, 1`) to restore
normal echo afterward. `ConsoleSession` no-ops it — local CLI never logs in,
so it's never exercised. This is **not a security boundary by itself** — a
raw/non-compliant client can simply ignore the negotiation and echo anyway —
it only suppresses display for normal telnet clients; the password itself is
still transmitted in the clear (Telnet has no built-in transport
encryption — see [networking.md](networking.md) for the plaintext-transport
tradeoff this project already accepted for v1).

## Verified

Live, over real TCP against `TelnetTransportBackgroundService`'s Telnet
listener, not just unit tests: a brand-new username creates a character (confirmed the `IAC
WILL/WONT ECHO` bytes are actually sent, visible as raw bytes to a
non-compliant test client); 3 consecutive wrong-password attempts are all
rejected with the generic message and the flow loops back to the username
prompt rather than dropping the connection; the correct password then logs
in successfully; declining character creation for a nonexistent username
loops back to the username prompt within the same connection rather than
disconnecting; mismatched passwords during creation are rejected. Also
confirmed the password hash itself survives a real process restart against
the same SQLite file — not just an in-memory check — by creating an account,
restarting the server, and logging in again with the same credentials.

`PasswordHashingTests` (`SharpMud.Hosting.Tests`) covers: correct password
verifies, wrong password fails, and two hashes of the same password are
never byte-identical (confirms the random salt is actually being used, not
silently skipped).

## Open Items

- Failed-login retry/lockout policy — implemented as a flat 3 attempts then
  return to the username prompt; not a considered final policy (no delay
  between attempts, no per-IP/per-username lockout tracking across
  connections).
- Username validation rules (length, allowed characters, case sensitivity)
  not yet chosen — currently anything non-empty after trimming is accepted.
- Password strength/length requirements not yet chosen — currently only
  "non-empty" is enforced.
- Password reset flow — with no email/OAuth identity backing the account,
  there's no "forgot password" recovery path; not designed. Likely
  admin-assisted reset only (ties into the moderation tooling in
  [ADR-0005](adr/0005-security-role-model-and-moderation-commands.md)/
  [PLAN-0005](plans/0005-security-role-model-and-moderation-commands.md)).
- The password itself travels in cleartext over Telnet (only the on-screen
  *display* is suppressed) — no transport encryption exists yet; SSH (see
  [networking.md](networking.md)) would be the natural place this gets
  solved properly, not something to bolt onto raw Telnet.
- `PasswordHasher.VerifyHashedPassword`'s `SuccessRehashNeeded` result
  (returned when the hash was created with older/weaker parameters than the
  hasher's current defaults) is currently treated the same as plain
  `Success` — the stored hash is never opportunistically upgraded on a
  successful login.
