# Accounts & Auth

See [README.md](README.md) for how this doc relates to `SPEC.md` and the other
subsystem docs. See [character.md](character.md) for the identity fields on
`Player`, and [persistence.md](persistence.md) for the lookup this flow
depends on.

## Decision

External OAuth (Google/GitHub/Discord) rather than engine-managed
username/password. No password storage/reset burden; less classic-MUD-feeling
at the login prompt, but appropriate for a modern, publicly-run service.
Deferred until networked play lands — v1 local CLI has no login at all.

## Identity Model

Auth identity lives on a separate `Account` entity, not on `Player` directly
— an account can own multiple characters (see below).

```csharp
public sealed class Account
{
    public AccountId Id { get; init; }
    public string ExternalAuthProvider { get; init; } = ""; // "google" | "github" | "discord"
    public string ExternalAuthId { get; init; } = "";
    public List<PlayerId> CharacterIds { get; set; } = [];
}
```

Lookup on login: `IAccountRepository.GetByExternalAuthIdAsync(provider,
externalId, ct)` (new repository alongside `IPlayerRepository`/
`IRoomRepository`, see [persistence.md](persistence.md)) — first login for a
given provider/external-id pair creates a new `Account`; subsequent logins
resume the existing one and present a character-select step.

## Multiple Characters Per Account

An `Account` may own several `Player`s (classic MUD "alts" convention). After
OAuth resolves to an `Account`, the login flow lists `CharacterIds` (fetched
via `IPlayerRepository`) and prompts the player to pick one, or create a new
one (running character creation — see [character.md](character.md)) if under
whatever per-account character cap is chosen (see Open Items).

## Terminal OAuth Flow (Device-Code)

Telnet/SSH/WebSocket sessions have no browser redirect built in, so login
uses a device-code flow, the same pattern used by browserless devices
(smart TVs, CLI tools):

1. On connect, before any character interaction, the session displays a
   login URL and a short one-time code.
2. Player opens the URL on any device (phone, laptop) and authorizes via
   their chosen provider (Google/GitHub/Discord), entering the code if
   prompted.
3. The server polls the provider's device-code endpoint until authorization
   completes (or the code expires).
4. On success, `IAccountRepository.GetByExternalAuthIdAsync` resolves/creates
   the `Account`, and the session proceeds to character-select as above.

This flow applies uniformly across all networked transports (Telnet, SSH,
WebSocket) — no transport-specific auth special-casing needed. Local CLI
(v1) has no login step at all, per SPEC.md.

## Open Items

- Per-account character cap (unlimited alts vs. a fixed max) not yet chosen.
- Device-code polling interval and expiry duration not yet specified.
- Which providers ship in v1 of networked play vs. added later.
- Character deletion/retirement flow not yet designed.
