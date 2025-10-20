# Backlog

- [x] VS-0001 Scaffold solution structure (single console project + tests) with vertical-slice friendly wiring and DI bootstrap.
  - Created `LORE-LLM.sln` with projects: `src/LORE-LLM` (console app) and `tests/LORE-LLM.Tests` (xUnit).
  - Tests reference `Shouldly`/`NSubstitute`; core references `CSharpFunctionalExtensions`.
  - Added `Directory.Build.props` enforcing nullable reference types, analyzers, and warnings-as-errors (excluding tests).
  - Implemented composition root via `Microsoft.Extensions.Hosting` with DI extension `AddLoreLlmServices` registering the placeholder CLI application.
