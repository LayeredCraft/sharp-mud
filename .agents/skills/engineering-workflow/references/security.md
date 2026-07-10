# Security standards

- Passwords: `PasswordHasher<TUser>` from `Microsoft.Extensions.Identity.Core`
  (PBKDF2) — never hand-roll hashing, never store plaintext, never
  downgrade to a faster/weaker hash for convenience.
- Config/secrets: environment variables (`SHARPMUD_MODE`,
  `SHARPMUD_TELNET_PORT`, `SHARPMUD_DB_PATH`, ...) parsed in
  `HostOptions.Parse`. No `appsettings.json`, and no secrets committed to
  the repo — if a new setting needs a default, put the default in code, not
  in a checked-in config file with a real value.
- Telnet is an untrusted input surface (arbitrary remote clients). Any new
  Telnet-facing code must treat input as adversarial: no assumption about
  line length, encoding, or IAC sequence well-formedness. `SetEchoAsync`
  (real IAC WILL/WONT ECHO) is the existing model for protocol-correct
  handling — match that rigor for new protocol features, don't fake it
  client-side only.
- SQL injection surface is currently zero (EF Core/LINQ only) — keep it
  that way; if you ever reach for raw SQL, that's a signal to stop and
  reconsider the query via LINQ/EF instead.
- New auth/session work should read `docs/accounts-auth.md` first — it
  documents a revision history (OAuth → username/password) with the
  reasoning; don't re-litigate that decision without discussing it.
