# Backlog

- [x] VS-0001 Scaffold solution structure (single console project + tests) with vertical-slice friendly wiring and DI bootstrap.
  - Created LORE-LLM.sln with projects: src/LORE-LLM (console app) and 	ests/LORE-LLM.Tests (xUnit).
  - Tests reference Shouldly/NSubstitute; core references CSharpFunctionalExtensions.
  - Added Directory.Build.props enforcing nullable reference types, analyzers, and warnings-as-errors (excluding tests).
  - Implemented composition root via Microsoft.Extensions.Hosting with DI extension AddLoreLlmServices registering the placeholder CLI application.
- [x] VS-0002 Define CLI surface (System.CommandLine) and register initial verbs: xtract, ugment, 	ranslate, alidate, integrate.
  - Each command lives in its own feature module under Presentation/Commands/<Verb>.
  - Wire binding models, help text, and minimal handlers returning Result<int> via CSharpFunctionalExtensions.
  - Root command built with System.CommandLine 2.0 Parse().InvokeAsync() flow; stub handlers log intent while returning exit codes.
  - Add unit tests covering command invocation and option parsing.
- [x] VS-0003 Implement raw text extraction pipeline.
  - Added RawTextExtractor service + SourceSegment/SourceTextRawDocument models; command now writes source_text_raw.json and workspace.json with SHA-256 manifest data.
  - Handles ID-only lines by emitting empty segments and skips blank rows; fails gracefully on missing input/no segments.
  - Added integration tests for CLI extract verb plus dedicated unit tests validating JSON/manifest output.
- [ ] VS-0004 Define domain models and schemas for metadata, clusters, and knowledge base.
  - Add strongly typed records for SourceSegment, SegmentMetadata, ClusterContext, KnowledgeEntry.
  - Document schemas in docs/schemas/ and ensure serialization contracts via unit tests.
  - Seed example JSON files under docs/examples/ for manual review.
