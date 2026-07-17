# Security standards

- Passwords: `PasswordHasher<TUser>` from `Microsoft.Extensions.Identity.Core`
  (PBKDF2) — never hand-roll hashing, never store plaintext, never
  downgrade to a faster/weaker hash for convenience.
- Config: `appsettings.json` is allowed for **non-secret** configuration
  (log levels, feature toggles, tunable defaults) — see
  [ADR-0003](../../../../docs/adr/0003-allow-appsettingsjson-for-non-secret-config.md).
  `HostOptions.Parse`'s existing env-var parsing (`SHARPMUD_MODE`,
  `SHARPMUD_TELNET_PORT`, `SHARPMUD_DB_PATH`) is unaffected by this — it's
  not yet migrated to the `IOptions<T>`/`appsettings.json` pattern.
  **Secrets never go in `appsettings.json` or any other committed file**
  with a real value — connection strings with credentials, API keys, and
  anything else that would be a real compromise if leaked stay on
  environment variables only. This is the one rule that didn't change: if
  a secret needs a default, put a placeholder in code and require the real
  value via an env var, never commit it.
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
