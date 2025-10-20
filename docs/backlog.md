# Backlog

- [x] VS-0001 Scaffold solution structure (single console project + tests) with vertical-slice friendly wiring and DI bootstrap.
  - Created `LORE-LLM.sln` with projects: `src/LORE-LLM` (console app) and `tests/LORE-LLM.Tests` (xUnit).
  - Tests reference `Shouldly`/`NSubstitute`; core references `CSharpFunctionalExtensions`.
  - Added `Directory.Build.props` enforcing nullable reference types, analyzers, and warnings-as-errors (excluding tests).
  - Implemented composition root via `Microsoft.Extensions.Hosting` with DI extension `AddLoreLlmServices` registering the placeholder CLI application.
- [ ] VS-0002 Define CLI surface (System.CommandLine) and register initial verbs: `extract`, `augment`, `translate`, `validate`, `integrate`.
  - Each command lives in its own feature module under `Presentation/Commands/<Verb>`.
  - Wire binding models, help text, and minimal handlers returning `Result<int>` via `CSharpFunctionalExtensions`.
  - Add unit tests covering command invocation and option parsing.
- [ ] VS-0003 Implement raw text extraction pipeline.
  - Parse `raw-input/english.txt` line-by-line into normalized segment records and emit `workspace/source_text_raw.json`.
  - Persist workspace manifest entry noting extraction timestamp and input hash.
  - Cover with tests (including golden snapshot) and docs outlining expected JSON schema.
- [ ] VS-0004 Define domain models and schemas for metadata, clusters, and knowledge base.
  - Add strongly typed records for `SourceSegment`, `SegmentMetadata`, `ClusterContext`, `KnowledgeEntry`.
  - Document schemas in `docs/schemas/` and ensure serialization contracts via unit tests.
  - Seed example JSON files under `docs/examples/` for manual review.
