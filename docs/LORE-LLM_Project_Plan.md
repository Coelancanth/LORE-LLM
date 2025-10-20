# LORE-LLM Project Plan

## 1. Vision & Success Criteria
- Deliver a modular, data-driven localization pipeline that produces context-aware machine translations ready for professional review.
- Prioritize a robust CLI + SDK that can run entirely on developer machines, while keeping the architecture ready for commercial deployment (scaling, auditing, team workflows).
- Enable seamless hand-off to Paratranz (or equivalent) for human-in-the-loop polishing without duplicating effort.

## 2. Core-First Scope
- **Core MVP (v0.1)**: Extraction from raw text dumps (e.g., `english.txt`), glossary lifecycle, preprocessing, prompt assembly, LLM-assisted clustering (chat protocol), metadata/knowledge synthesis, validation, local artifact storage, CLI orchestration.
- **Core Extensions (v0.2+)**: Paratranz sync adapters, reviewer dashboard, automated QA reports, CI integration, optional automated wiki crawl.
- **Commercial-Grade Enhancements (post-core)**: Secrets management, rate limiting, analytics, licensing compliance, build/release packaging, enterprise auth.

## 3. Input Data Model Assumptions

Sample corpus: `english.txt` (Pathologic 2 dialogue export). The extractor treats each line as `{id} {text}`, normalizes it into `source_text_raw.json`, and enriches it with optional authored metadata and knowledge-base lookups.

### 3.1 Source Text (minimal required) — `source_text_raw.json`
```json
{
  "id": "conv:6192355001750784",
  "text": "Well, you are going to die now. Death is awaiting you downstairs. I wonder if you'll manage to give it the proper answer this time."
}
```

- At minimum, the pipeline needs a stable identifier and the source string. No metadata is required at ingest time.
- Additional authored metadata remains optional; if present, it is merged with inferred metadata during augmentation (see §4 step 3).

### 3.2 Optional Authored Metadata — `source_text_metadata.json`
```json
{
  "id": "quest:torvin_rescue:line_03",
  "metadata": {
    "category": "dialogue",
    "quest": "Blooming Dread",
    "speaker": "Torvin",
    "scope": "quest_blooming_dread/dialogue_tree",
    "file": "res://quests/blooming_dread.xml",
    "tags": ["post-boss", "gratitude"]
  },
  "context_hints": [
    "Torvin speaks after being healed by the player.",
    "Tone: relieved, grateful, slightly dazed."
  ]
}
```

- This file is optional and may be empty. When provided, it is appended to the minimal text record before preprocessing.

### 3.3 Glossary (`glossary_initial.csv` → `glossary_translated_{lang}.json`)
```json
{
  "term_id": "lamassu",
  "source_term": "Lamassu",
  "sense": "Behemoth creature created by Machinists.",
  "category": "creature",
  "lore_excerpt": "Machinists forged Lamassu as living siege engines.",
  "disambiguated_token": "Lamassu",
  "target_term": "Ламассу",
  "notes": "Maintain capitalization; mythological origin."
}
```

### 3.4 Processed Text (`preprocessed_text.json`)
```json
{
  "id": "ui:save_button",
  "tokenized_text": "Save Game",
  "disambiguated_tokens": [
    {"placeholder": "{term.save_game_ui}", "source": "Save Game"}
  ],
  "metadata": {
    "category": "ui",
    "javaPackageName": "dot.gui.gamemenu",
    "javaScopeName": "GameMenuPane",
    "javaMethodName": "addButtons"
  },
  "context_hints": [
    "Button label in the main menu for saving progress."
  ]
}
```

### 3.5 Synthetic Metadata (`metadata_inferred.json`)
```json
{
  "id": "conv:6192355001750784",
  "speaker_guess": "Executor",
  "tone": "ominous, fatalistic",
  "scene_context": "Conversation in Pathologic 2 between the Bachelor and the Executor moments before confronting death.",
  "entities": ["Daniil Dankovsky", "Executor", "Death"],
  "confidence": 0.68,
  "notes": "Generated via LLM inference; pending reviewer confirmation."
}
```

### 3.6 Cluster Contexts (`clusters_llm.json`)
```json
{
  "cluster_id": "scene:bachelor_vs_executor",
  "members": [
    "conv:6192355001750784",
    "conv:6192355001750785",
    "conv:6192355001750786"
  ],
  "summary": "Climactic exchange between Daniil Dankovsky and the Executor inside the Theatre. Outcome hinges on confronting Death; tone is fatalistic and confrontational.",
  "glossary_hits": ["Executor", "Daniil Dankovsky"],
  "llm_notes": "LLM-generated synopsis based on chat prompt context and operator-provided segments.",
  "confidence": 0.72,
  "source_conversation": "logs/chat/cluster_001.md"
}
```

- Cluster contexts are synthesized via a chat-based LLM workflow. Operators provide segment batches + glossary cues, and the model responds with structured JSON adhering to our prompt contract. Low-confidence responses fall back to per-string metadata.

### 3.7 Knowledge Base (`knowledge/key_concepts.json`)
```json
{
  "concept_id": "character:daniil_dankovsky",
  "aliases": ["Daniil", "Bachelor"],
  "summary": "A doctor and thanatologist who returns to the Town-on-Gorkhon to cure the Sand Pest.",
  "source": {
    "url": "https://pathologic.fandom.com/wiki/Daniil_Dankovsky",
    "license": "CC-BY-SA 3.0",
    "retrieved": "2024-12-01"
  },
  "related_terms": ["character:executor", "location:stone_yard"],
  "glossary_hint": "Treat as proper noun; keep original capitalization."
}
```

- Knowledge entries seed metadata synthesis and glossary generation. When referenced, attribution is retained for compliance.
### 3.8 Investigation Report (`investigation.json`)
```json
{
  "project": "pathologic2-marble-nest",
  "generated_at": "2025-10-20T13:18:44Z",
  "input_hash": "8f4c3d9...",
  "suggestions": [
    {
      "segment_id": "conv:6192355001750781",
      "tokens": ["Daniil", "death"],
      "candidates": [
        {
          "concept_id": "character:daniil_dankovsky",
          "source": "wiki/pathologic2/daniil_dankovsky",
          "confidence": 0.92,
          "notes": "Name match and thematic alignment."
        },
        {
          "concept_id": "concept:death_personification",
          "source": "wiki/pathologic2/death",
          "confidence": 0.64
        }
      ]
    }
  ]
}
```

- Generated during the investigate stage to highlight which wiki entries or knowledge records should be referenced for each segment prior to metadata synthesis.

## 4. End-to-End Workflow (Holistic Flow)
1. **Configure**: Define providers, templates, locales in `config.yaml`.
2. **Extract**: `SourceExtractor` walks source assets, normalizes text, and emits minimal `source_text_raw.json`; if authored metadata exists, it is stored separately for later merge.
3. **Post-Process**: `PostProcessingPipeline` runs project-specific processors (e.g., Marble Nest cleanup) to mutate extracted artifacts before downstream stages.
4. **Augment Metadata**: `MetadataSynthesizer` merges authored hints with knowledge-base lookups and LLM/heuristic inference to generate per-string metadata, then groups related strings into clusters (`clusters.json`) with confidence scores; low-confidence clusters automatically fall back to per-string context.
5. **Glossary Build**: `GlossaryManager` merges manual lore terms with LLM-assisted discovery, handles disambiguation, and translates the glossary.
6. **Preprocess**: `PreProcessor` replaces ambiguous tokens, performs lemmatization, prepares translation batches, and persists `preprocessed_text`.
7. **Translate**: `AITranslator` assembles prompts (leveraging cluster context, per-string metadata, and knowledge-base excerpts), calls the chosen LLM, enforces glossary usage, and outputs `translation_raw_{lang}.json`.
8. **Validate**: `Validator` runs consistency checks (placeholders, glossary coverage, length budgets) and produces human-readable reports.
9. **Human Review**: Initial MVP supports local markup review; later versions sync with Paratranz for collaborative approval.
10. **Integrate**: `Integrator` converts approved translations into engine-ready packages (e.g., CSV, JSON, binary bundles) and triggers regression tests.
11. **Feedback Loop**: Capture reviewer notes, in-game QA issues, and player feedback to update glossary, prompts, metadata inference rules, post-processing rules, and context hints.

## 5. Architecture Overview
  - **Architectural Style**: Clean Architecture principles applied within a consolidated executable that is organized via Vertical Slice Architecture. Each CLI verb (extract, augment, translate, validate, integrate) resides in an isolated feature folder with its own request/response models, validators, and pipeline wiring; shared contracts are expressed through dedicated namespaces.
  - **Solution Layout**:
    - `LORE-LLM` (.NET 8 console app): Contains presentation (CLI), application services, domain abstractions, and infrastructure adapters separated by namespaces (`Presentation`, `Application`, `Domain`, `Infrastructure`) and internal interfaces.
    - `LORE-LLM.Tests` (xUnit): Exercises domain logic, pipeline orchestration, and CLI integration with test doubles for external systems.
- **Vertical Slices**: Each command registers its own request handler, validators, and pipeline configuration via feature modules (e.g., `ExtractFeature.ConfigureServices`). Shared behaviors (telemetry, exception handling) are implemented as middleware so slices stay independent.
- **Processing Core**: Stateless services operating on versioned JSON artifacts stored in `.lore-llm/{phase}`; deterministic outputs enable caching and diffing. Serialization uses `System.Text.Json` (with custom converters) for performance and source generator support.
- **Post-Processing Pipeline**: `PostProcessingPipeline` coordinates project-specific processors (`IPostExtractionProcessor`), allowing per-lore cleanup (e.g., removing empty segments) immediately after extraction.
- **Fluent Pipeline API**: Developers compose workflows through a fluent interface (e.g., `Pipeline.For(source).AugmentMetadata().BuildGlossary().Translate().Validate().Integrate()`), enabling concise yet expressive orchestration in commercial or scripted deployments.
- **Observability**: Structured logging via `Serilog` (console/file/Seq sinks) and `OpenTelemetry` hooks capture command invocations, LLM call metadata, and clustering confidence for auditing.
- **Coding Practices**: Target .NET 8/C# 12, favor `record` types for immutable models, embrace async streams for large text batches, and enforce analyzers (StyleCop/IDisposable analyzers) via Roslyn rulesets.
- **Error & Testing Toolkit**: Adopt `CSharpFunctionalExtensions` for result/Maybe workflows, `Shouldly` for expressive assertions, and `NSubstitute` for lightweight test doubles.
- **LLM Adapter Layer**: Pluggable providers (Gemini, Azure OpenAI, Claude) implementing a shared interface with retry, rate limiting, telemetry.
- **Knowledge & Investigation Services**: Introduce a wiki ingestion layer capable of crawling `Special:AllPages`, caching article metadata (title, summary, license, last updated), and an investigation pass that cross-references segments with the wiki index to propose relevant entries.
- **Persistence**: Local workspace using structured directories; optional PostgreSQL/SQLite backing for large teams in commercial deployments.
- **Distribution**: Ship as a .NET 8 global tool (self-contained option for Windows/macOS/Linux) with packaging scripts for future MSI/Docker releases.
- **Integration Bridges**: REST adapters for Paratranz and other localization platforms; optional Web UI for quick reviews.

## 6. Phase Roadmap

### Phase 0 – Foundations (Week 1–2)
- Repository setup, coding standards, CI lint/tests.
  - Implement `config.yaml` loader, workspace manifest, logging, diagnostic scaffolding.
  - Scaffold single-project solution (`LORE-LLM` console) plus `LORE-LLM.Tests`, with vertical-slice folders and central dependency injection/composition root.
  - Wire baseline test infrastructure (xUnit, Shouldly, NSubstitute) and add shared test utilities.

### Phase 1 – Core Pipeline (Weeks 3–6)
- Build `SourceExtractor` with plugin points for XML, JSON, plain text, emitting minimal text-first datasets and optional authored metadata appendices.
- Implement `MetadataSynthesizer` capable of inferring speaker/tone/scene context from raw text via heuristics and glossary signals.
- Stand up the chat-based clustering workflow:
  - Format Markdown prompts that batch segments + glossary context.
  - Call a user-selected LLM provider (Cursor, OpenAI, Claude, etc.) via a pluggable chat protocol.
  - Persist `clusters_llm.json` alongside raw conversation transcripts for auditing.
- Implement glossary ingestion, disambiguation helpers, lemmatization (spaCy via Python interop or ONNX model).
  - Create preprocessing pipeline and artifact persistence.
  - Ship initial `AITranslator` with prompt templates, batching, and glossary enforcement heuristics.
  - Deliver first vertical slices (`extract`, `cluster`, `augment`, `translate`) as independent CLI features wired to shared services/namespaces.
  - Add `Validator` for placeholder/tag checks and glossary coverage reports.
  - Establish functional error handling patterns across services using `CSharpFunctionalExtensions` (`Result`, `Maybe`, `UnitResult`).

### Phase 2 – Stability & Developer Experience (Weeks 7–9)
- Harden CLI UX (progress reporting, resumable runs, dry runs).
- Add structured logging, metrics hooks, and error recovery (retry/backoff, partial reruns).
- Provide sample project, documentation, and automated tests across modules, highlighting fluent pipeline composition patterns.
- Introduce QA tooling for cluster confidence review, glossary coverage gaps, and LLM transcript inspection.
- Add CLI utilities to review clusters, split/merge assignments, and persist overrides back into the workspace manifest.
- Add cross-cutting behaviors (caching, telemetry) via pipeline middleware so vertical slices stay isolated.

### Phase 3 – Collaboration & Paratranz Integration (Weeks 10–12)
- Implement Paratranz sync (upload sources, glossary, import MT suggestions, download approvals).
- Introduce local review UI fallback (minimal Blazor/React app).
- Extend QA reports with diff views and release notes.

### Phase 4 – Commercial Readiness (Weeks 13+)
- Secrets management abstraction, provider credential vault support.
- Role-based access (API tokens, audit logs), deployment packaging (Docker, MSI, dotnet tool).
- Performance profiling, horizontal scaling options (job queue, background workers).
- Legal & compliance checklist (model licensing, data privacy, localization IP tracking).

## 7. Data Governance & Versioning
- All artifacts include schema version, source commit hash, and locale metadata.
- `workspace.toml` tracks phase completion status, timestamps, upstream dependencies, and manual overrides (cluster splits/merges, metadata edits).
- Implement change detection to only retranslate impacted segments when source text updates.

## 8. Quality Assurance Strategy
- Automated tests: unit tests per module, integration tests on mock project, snapshot tests for prompts.
- QA reports: glossary coverage %, untranslated tokens, placeholder mismatches, diff summaries, cluster confidence warnings, knowledge attribution gaps.
- Manual checkpoints before release: linguist review, in-engine smoke tests, telemetry hooks for runtime locale selection.

## 9. Commercial Considerations
- **Licensing**: Dual-license (e.g., MPL + commercial terms) or permissive OSS + paid support; clarify third-party model usage rights and ensure wiki-derived knowledge complies with CC-BY-SA attribution requirements.
- **Security**: Encrypt credentials, scrub PII from logs, provide self-hosted model option for sensitive text.
- **Scalability**: Design translation jobs to run in containers/agents; support cloud storage (S3/Azure Blob) for artifacts.
- **Supportability**: Publish SLA-ready documentation, API references, troubleshooting guides, and contact channels.

## 10. Next Immediate Actions
1. Finalize schemas for `source_text_raw.json`, optional `source_text_metadata.json`, `metadata_inferred.json`, `clusters_llm.json`, `glossary_translated.json`, `preprocessed_text.json`, and `translation_raw.json`.
2. Prototype the chat-based clustering prompt/response flow on a small corpus and check it into `docs/examples/cluster_prompt.md` + `clusters_llm.example.json`.
3. Wire the glossary ingestion pipeline to surface glossary cues inside the clustering prompts.
4. Draft prompt templates for metadata synthesis, clustering, and translation, plus glossary enforcement logic; run a small translation spike using the updated context.
5. Establish repository structure (Domain/Application/Infrastructure/Cli projects), fluent pipeline skeleton, vertical-slice command modules, dependency-injected services, and CI (lint + unit tests) to anchor contributions.











