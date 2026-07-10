# Testing

Use the `dotnet-unit-testing-patterns` skill for the mechanics
(AutoFixture SpecimenBuilders, NSubstitute, AwesomeAssertions). This file
covers project-level conventions only:

- xUnit v3 on Microsoft Testing Platform v2 (`xunit.v3.mtp-v2` package —
  see the comment in `Directory.Packages.props` for why not plain
  `xunit.v3`).
- One test project per src project, same name + `.Tests` suffix, and it
  stays that way — don't consolidate tests across projects.
- Global usings for `Xunit`, `AutoFixture`, `AutoFixture.Xunit3`,
  `NSubstitute`, `AwesomeAssertions` are set per test csproj — copy that
  block when creating a new test project.
- Per-project custom `[XxxAutoData]` attributes wrap `AutoDataAttribute`
  via a shared `BaseFixtureFactory` — add a new one for a new test project
  rather than reusing another project's.
- Arrange-Act-Assert with blank-line separation between the three sections,
  no exceptions.
