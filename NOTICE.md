# Notice

sharp-mud is licensed under the [MIT License](LICENSE).

## Design inspiration

sharp-mud's engine design (the `Thing`/`Behavior` composition model, the
generic event system, session/transport abstractions, and several other
architectural decisions) is informed by a close reading of
[WheelMUD](https://github.com/DavidRieman/WheelMUD)'s source code and
architecture. See [docs/research/wheelmud-findings.md](docs/research/wheelmud-findings.md)
for the full source-dive record of what was adopted, what was deliberately
changed, and why.

sharp-mud is a clean-room reimplementation, not a fork or derivative of
WheelMUD's source — no WheelMUD code is included in this repository.
WheelMUD itself is licensed under the
[Microsoft Public License (MS-PL)](https://github.com/DavidRieman/WheelMUD/blob/main/src/LICENSE.txt),
which does not apply to sharp-mud's own, independently-written code (see
[ADR-0006](docs/adr/0006-nuget-package-distribution.md)'s License and
naming section for the full reasoning).
