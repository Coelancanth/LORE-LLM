# Backlog

- [x] VS-0001 Scaffold solution structure (single console project + tests) with vertical-slice friendly wiring and DI bootstrap.
  - Created `LORE-LLM.sln` with projects: `src/LORE-LLM` (console app) and `tests/LORE-LLM.Tests` (xUnit).
  - Tests reference `Shouldly`/`NSubstitute`; core references `CSharpFunctionalExtensions`.
  - Added `Directory.Build.props` enforcing nullable reference types, analyzers, and warnings-as-errors (excluding tests).
  - Implemented composition root via `Microsoft.Extensions.Hosting` with DI extension `AddLoreLlmServices` registering the placeholder CLI application.
- [x] VS-0002 Define CLI surface (System.CommandLine) and register initial verbs: `extract`, `augment`, `translate`, `validate`, `integrate`.
  - Each command lives in its own feature module under `Presentation/Commands/<Verb>`.
  - Wire binding models, help text, and minimal handlers returning `Result<int>` via `CSharpFunctionalExtensions`.
  - Root command built with System.CommandLine 2.0 `Parse().InvokeAsync()` flow; stub handlers log intent while returning exit codes.
  - Add unit tests covering command invocation and option parsing.
- [x] VS-0003 Implement raw text extraction pipeline.
  - Added `RawTextExtractor` service + `SourceSegment`/`SourceTextRawDocument` models; command now writes project-scoped `source_text_raw.json` and `workspace.json` with SHA-256 manifest data.
  - Handles ID-only lines by emitting empty segments and skips blank rows; fails gracefully on missing input/no segments.
  - Added integration tests for CLI extract verb plus dedicated unit tests validating JSON/manifest output.
- [x] VS-0004 Define schemas for metadata, clusters, knowledge, and investigation.
  - Add strongly typed records for `SegmentMetadata`, `ClusterContext`, `KnowledgeEntry`, and `InvestigationSuggestion` (linkage between segments and indexed wiki articles).
  - Document JSON schemas in `docs/schemas/` and provide examples in `docs/examples/` for metadata, clusters, wiki knowledge entries, and investigation reports.
  - Build wiki ingestion scaffold using the MediaWiki API (e.g., `action=query&list=allpages`, `prop=extracts|info`) to cache article metadata (title, summary, license, last updated) and produce an investigation report that cross-references segments with wiki suggestions.
  - Ensure serialization tests cover round-trip validation for the new models and their artifacts.
- [x] VS-0005 Introduce post-processing pipeline.
  - Define `IPostExtractionProcessor` (or similar) and register per-project processors via DI/config.
  - Implement Marble Nest processor that removes empty segments or other quest-specific quirks after extraction.
  - Add CLI wiring (e.g., optional `--post-process` flag or default run) and tests verifying processors mutate workspace artifacts correctly.
  - Document processor extension points and add an example config entry in the docs.
- [x] VS-0006 Implement investigation stage.
  - Added `investigate` CLI command that reads `source_text_raw.json`, generates `investigation.json`, and refreshes the workspace manifest.
  - Implemented token heuristics and candidate matching backed by a MediaWiki ingestion service (opensearch + parse) with on-disk caching and knowledge base emission.
  - Investigation workflow now persists `knowledge_base.json`, updates manifest artifacts, and surfaces wiki-backed candidates per segment.
  - Expanded unit and CLI coverage around the pipeline and refreshed docs/examples for the new artifacts.
- [x] VS-0007 Crawl wiki pages to Markdown via API.
  - Implemented `crawl-wiki` CLI command with pluggable `IMediaWikiCrawler`, caching of `allpages`, markdown conversion, throttling, and deterministic tests.
  - Stored outputs under `knowledge/raw/*.md`; seeded Daniil Dankovsky/Bachelor entries as verification.
  - Added HTML post-processing pipeline with project-specific sanitizers that strip wiki UI artifacts and flatten multi-tab layouts into Markdown sections.
- [] VS-0008 MediaWiki crawler post-processing plugins.
  - Introduced configuration-driven HTML pipeline backed by `MediaWikiCrawlerOptions`, letting each sanitized project map to its API base and ordered post-processor list.
  - Added Pathologic-specific processor that strips infoboxes, tab chrome, galleries, and other decorative elements while flattening tabs into Markdown headings.
  - Documented extension guidance (`docs/wiki_crawler.md`) so new fandoms can register processors through DI without touching core crawler logic.
  - Tab-aware exports now emit only per-variant Markdown when configured (Pathologic disables the combined document via `EmitBaseDocument = false`); follow-up work will slot these into project-specific subfolders and add a CLI indexing command so plugins can emit searchable catalogs.
- [x] VS-0009 LLM-assisted clustering (chat protocol).
  - Introduced `cluster` CLI command with pluggable `IChatProvider` abstraction, `ChatProviderResolver`, and `ClusterWorkflow`.
  - Implemented `local` provider (offline deterministic clustering for testing) and registered via DI.
  - Workflow batches segments, formats Markdown prompts (with optional custom templates via `--prompt-template`), invokes provider, parses JSON response (supports both full `ClusterDocument` and bare array shapes), and persists `clusters_llm.json`.
  - Optional `--save-transcript` flag emits `clusters_llm_transcript.md` capturing full prompt/response conversation for auditing.
  - Updates `workspace.json` manifest with `clustersLlm` artifact reference.
  - Added CLI integration tests with stub provider validating end-to-end artifact generation and manifest updates.
  - Documented usage in onboarding with practical workflows for Cursor, browser-based chat, and future API providers.
- [ ] VS-0010 Glossary-aware enrichment from clusters.
  - Use LLM-generated clusters to detect glossary terms, flag gaps, and push cluster summaries back onto member segments for the augmentation/translation pipeline.
  - Extend augmentation to consume `clusters_llm.json`, merging cluster synopses and glossary highlights into segment metadata.
  - Provide CLI toggles for enabling the enrichment path and update docs/tests to reflect glossary + cluster interplay.

- [x] VS-0011 Wiki keyword indexing + redirect detection.
  - Added `index-wiki` CLI command to generate `knowledge/wiki_keyword_index.json` from crawled markdown.
  - Index entries include `title`, tokenized `keywords`, and `isRedirect` flag; redirect-only pages can be skipped downstream.
  - Updated crawler docs with resume semantics and indexing flow.

- [ ] VS-0012 CLI command presets (config-driven arguments).
  - Introduce `config/cli.presets.json` (or TOML) so commands like `crawl-wiki` can load default options (pages, throttling, project) without long flag lists.
  - Support `--preset <name>` plus implicit defaults per sanitized project; allow overrides on the command line.
  - Update docs/backlog to explain preset workflow and add tests covering config loading + precedence (CLI overrides > preset > hardcoded defaults).

- [ ] VS-0013 Pluggable chat provider configuration (config folder).
  - Introduce `config/chat.providers.json` to declare providers (DeepSeek/OpenAI/Claude/etc.), default model, temperature, maxTokens, and API key env var mapping.
  - Precedence: CLI > environment variables > config file defaults.
  - Validate configuration against the official provider API specs (DeepSeek Chat Completions, OpenAI responses, Anthropic Messages) and surface actionable errors on misconfig. (Focus only on DeepSeek for now)
  - Wire DI to construct providers from config; keep `DEEPSEEK_API_KEY` compatibility.

- [ ] VS-0014 Knowledge-aware clustering prompt enrichment.
  - Add `--with-knowledge` (and caps like `--knowledge-max`) to inject top-N relevant concepts from `knowledge/wiki_keyword_index.json` and/or `knowledge_base.json` into clustering prompts.
  - Keep prompts within token budgets; document guidance and add tests.

- [ ] VS-0015 Crawler resume for tab-only variant outputs.
  - When `EmitBaseDocument = false` with configured `TabOutputs`, skip pages where all expected variant files already exist (unless `--force-refresh`).
  - Add deterministic tests and doc updates.

- [ ] VS-0016 Cluster CLI options: temperature/model overrides.
  - Add `--temperature` and `--model` to the `cluster` command; flow through to selected provider.
  - Document safe ranges per provider and defaults sourced from config.

- [ ] VS-0017 Global context plugin/prompt.
  - Provide a project-level, pluggable global context (e.g., surreal stage-play tone for Pathologic) injected into clustering/translation prompts.
  - Support per-project files (e.g., `config/<project>/global.context.md`) or preset keys; allow CLI override `--global-context <file>`.
  - Document guidance for concise, stable system prompts and add tests verifying presence in transcripts.


