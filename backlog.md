# Backlog

- [ ] VS-0001 Scaffold solution structure (Domain/Application/Infrastructure/CLI) with vertical-slice friendly wiring and DI bootstrap.
  - Create `AILocalizer.sln` containing projects: `AILocalizer.Domain` (class library), `AILocalizer.Application` (class library), `AILocalizer.Infrastructure` (class library), `AILocalizer.Cli` (console app).
  - Add test projects: `AILocalizer.Domain.Tests`, `AILocalizer.Application.Tests`, `AILocalizer.Cli.Tests` (xUnit) wired to cover domain logic, application services, and CLI commands.
  - Configure shared `Directory.Build.props` and analyzers (StyleCop, nullable enabled, warnings-as-errors for core layers).
  - Implement composition root in `AILocalizer.Cli` using `Microsoft.Extensions.Hosting` with feature-based service registration for vertical slices.
