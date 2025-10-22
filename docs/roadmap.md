# LORE-LLM Roadmap

Strategic view of the platform’s evolution. Use this alongside the backlog (execution detail) and the handbook (operational guide). Timelines assume sequential development but individual slices may progress in parallel where feasible.

---

## Vision & Success Criteria

- Ship a deterministic localization pipeline that enriches raw text with knowledge-aware metadata and feeds auditable, context-grounded translations.
- Keep everything runnable on developer laptops while remaining ready for commercial deployment (scaling, auditing, multi-team workflows).
- Enable professional review hand-offs (e.g., Paratranz) without duplicating effort and maintain clear provenance for all machine-assisted output.

---

## Product Scope

| Horizon | Focus |
| --- | --- |
| **Core MVP (v0.1)** | Extraction, pluggable MediaWiki crawl, retrieval indexing, LLM-assisted clustering, glossary enforcement, CLI orchestration, workspace manifests. |
| **Core Extensions (v0.2+)** | Advanced HTML processors, Paratranz sync adapters, reviewer dashboard, automated QA, CI integration. |
| **Commercial Enhancements** | Secrets management, rate limiting, analytics, licensing/compliance, packaged releases (dotnet tool/MSI/Docker), enterprise auth. |

---

## Phase Roadmap

### Phase 0 – Foundations
- Scaffold solution, tests, and DI composition root.
- Establish coding standards, analyzers, CI lint/tests.
- Ship sample workspace(s) and smoke-test scripts.

### Phase 1 – Core Pipeline
- **Extract & Sanitize** – Normalize raw text and apply project-specific cleanups right after ingestion.
- **Crawl** – Download MediaWiki HTML, run the post-processing pipeline, and emit Markdown (including per-tab variants).
- **Index (Pluggable Retrieval)** – Run `index-*` commands to build keyword/vector/graph indices; record them in `knowledge/index.manifest.json`.
- **Investigate (Optional)** – Query active retrieval providers to produce per-segment lore candidates (`investigation.json`, `knowledge_base.json`).
- **Cluster** – Execute the incremental LLM workflow (`clusters_current.json`, batch checkpoints, transcripts).
- **Context Selection** – Resolve cluster snippets and translation notes from the retrieval providers into `cluster_context.json`.
- **Glossary Enforcement** – Tag terms deterministically (Aho–Corasick), propagate expectations into prompts, and prep validation.
- **Translate** – Apply category-specific prompt templates that blend cluster metadata, snippets, glossary directives, and cultural guidance; emit translations plus enriched metadata.

### Phase 2 – Stability & Developer Experience
- Resume semantics for tab-only crawls and cluster reruns.
- Command presets/config-driven defaults.
- Advanced telemetry, logging, and error recovery.
- QA tooling: cluster review UI, glossary coverage dashboards, transcript inspection.
- CLI utilities for manual cluster edits (split/merge) and overrides.

### Phase 3 – Collaboration & Review
- Paratranz (or equivalent) bidirectional sync.
- Lightweight local review UI (Blazor/React).
- Diff-based QA reports and release notes.
- Vector/graph retrieval providers for richer context selection.

### Phase 4 – Commercial Readiness
- Secrets management abstraction and credential vaults.
- Role-based access, audit logging, packaging (dotnet tool, Docker, MSI).
- Performance profiling, job queue support, horizontal scaling options.
- Compliance checklist (licensing, data privacy, localization IP tracking).

---

## Data Governance & Quality

- All artifacts carry schema version, source hash, and timestamps.
- `workspace.toml` records pipeline phases, overrides, and dependencies.
- Change detection ensures only impacted segments retrigger translation.
- Automated tests: unit, integration, CLI, and prompt snapshots.
- QA reports surface glossary coverage, placeholder checks, cluster confidence, and knowledge attribution gaps.
- Manual checkpoints: linguist review, in-engine smoke tests, telemetry validation.

---

## Next Immediate Actions

1. Finalize schemas for core artifacts (`source_text_raw`, `clusters_current`, `cluster_context`, `glossary_consistency`, etc.).
2. Complete MediaWiki post-processing plugin framework and add Pathologic-specific sanitizers.
3. Harden the pluggable retrieval layer with manifest-driven provider discovery and tests.
4. Prototype clustering prompt templates on sample corpora; capture transcripts/examples.
5. Build glossary-aware augmentation and validation loop with deterministic fallbacks.
6. Land category-aware translation templates with reusable cultural guidance.
7. Expand CLI presets and error telemetry to smooth operator experience.

---

## Related Documents

- `docs/backlog.md` – executable task list (vertical slices & details).
- `docs/LORE-LLM_Handbook.md` – operational guide covering setup, commands, and troubleshooting.




---

## Pipeline Flow With Pluggable Retrieval

| Step | Description |
| --- | --- |
| **1. Extract & Sanitize** | Run `extract` to produce `source_text_raw.json`, then immediately apply project-specific post processors to normalize formatting quirks. |
| **2. Crawl** | Execute `crawl-wiki` to fetch MediaWiki HTML, apply the configured post-processing pipeline, and write Markdown (base + tab variants) into `knowledge/raw/`. |
| **3. Index (Pluggable)** | Invoke `index-*` commands (starting with `index-wiki`) to build retrieval providers. Each provider persists its cache (keyword dictionary, vector store, graph index, etc.) and registers in `knowledge/index.manifest.json` via `IRetrievalIndex`. |
| **4. Investigate (Optional)** | Query the registered retrieval providers to emit per-segment lore candidates (`investigation.json`, `knowledge_base.json`) for provenance and glossary seeding. |
| **5. Cluster** | Run the incremental LLM workflow that references the current ledger plus an overlap window, updating `clusters_current.json` while emitting batch checkpoints/transcripts. Providers (`local`, `deepseek`, …) plug into `IChatProvider`. |
| **6. Context Selection** | Deterministically resolve wiki snippets and translation notes per cluster by querying `IRetrievalIndex` providers; persist results in `cluster_context.json` (shared snippets deduped). |
| **7. Glossary Enforcement** | Tag glossary terms with Aho–Corasick, propagate required targets into metadata/prompts, and stage validation hooks. |
| **8. Translate** | Apply category-aware prompt templates that blend cluster metadata, snippets, glossary directives, and cultural guidance; capture translations plus enriched metadata. |
| **9. Validate → Review → Integrate** | Run placeholder/glossary checks (`glossary_consistency.json`), perform human review (local or Paratranz), then package approved outputs with regression checks and feedback loops. |

**Key Points About the Pluggable Retrieval Flow**
- *Single manifest of truth* – every provider registers in `knowledge/index.manifest.json`, so downstream stages discover capabilities without hard-coded paths.
- *Shared abstraction* – `IRetrievalIndex` lets investigation, context selection, and future features query providers in priority order (vector first, keyword fallback, etc.).
- *Deterministic outputs* – even with vector stores, results are persisted into `cluster_context.json`, keeping translation reproducible.
- *Extensibility* – new providers (graph, hybrid BM25+vector) only require implementing the interface and wiring configuration; clustering and translation logic remain untouched.

This keeps the entire pipeline grounded: raw text → wiki cache → manifest-driven retrieval → clustering → context selection → translation, with glossary consistency enforced throughout.
