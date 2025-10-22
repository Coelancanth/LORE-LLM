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
- **Extract & Sanitize** – Normalize raw text and apply project-specific cleanups right after ingestion. Projects may ship external adapters (Python/other) that emit the canonical `source_text_raw.json`; the pipeline validates/enriches the artifact but does not mandate a single in-process extractor.
- **Curate Knowledge Base** – Harvest glossary sources (tables, MediaWiki pages, bespoke docs), extract canonical keywords, and generate rich Markdown summaries—falling back to LLM-assisted notes when source content is sparse.
- **Index (Pluggable Retrieval)** – Run `index-*` commands to build retrieval providers and record them in `knowledge/index.manifest.json`. Implemented: keyword index plus vector (Qdrant) baseline with deterministic embeddings and artifact hashing.
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
- Vector/graph retrieval providers for richer context selection (vector-based cluster context selection shipped, see `cluster-context`).

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
3. Implement knowledge base curation that converts raw tables/pages into indexed Markdown, extracting canonical keywords and optionally enriching sparse entries via LLM suggestions.
4. Enrich the Qdrant vector index payload with markdown slugs and normalized keyword metadata for deterministic lookups.
5. Add a Qdrant search abstraction that supports keyword filters and powers a shared retrieval orchestration layer.
6. Land Aho–Corasick glossary tagging and feed the normalized tokens into retrieval and prompt construction flows.
7. Deliver deterministic, config-driven metadata enrichment (string-ID adapters: enrichment pipeline, layered config, AoD profiles) so segments carry quest/category context before clustering without rebuilding binaries.
8. Build the cluster context resolver that queries Qdrant (vector + keyword filters) and persists curated snippets into `cluster_context.json`.
9. Land category-aware translation templates with reusable cultural guidance.

---

## Related Documents

- `docs/backlog.md` – executable task list (vertical slices & details).
- `docs/LORE-LLM_Handbook.md` – operational guide covering setup, commands, and troubleshooting.
- `docs/glossary.md` – ubiquitous language reference shared across engineering and operations.




---

## Pipeline Flow With Pluggable Retrieval

| Step | Description |
| --- | --- |
| **1. Extract & Sanitize** | Run `extract` to produce `source_text_raw.json`, then immediately apply project-specific post processors to normalize formatting quirks. |
| **2. Curate Knowledge Base** | Execute `crawl-wiki` (or future adapters) to transform raw glossary sources into curated Markdown, extracting keywords and optionally enriching thin content with LLM suggestions. |
| **3. Index (Pluggable)** | Invoke `index-*` commands (starting with `index-wiki`) to build retrieval providers. Each provider persists its cache (keyword dictionary, vector store, graph index, etc.) and registers in `knowledge/index.manifest.json` via `IRetrievalIndex`. |
| **4. Investigate (Optional)** | Query the registered retrieval providers to emit per-segment lore candidates (`investigation.json`, `knowledge_base.json`) for provenance and glossary seeding. |
| **5. Cluster** | Run the incremental LLM workflow that references the current ledger plus an overlap window, updating `clusters_current.json` while emitting batch checkpoints/transcripts. Providers (`local`, `deepseek`, …) plug into `IChatProvider`. |
| **6. Context Selection** | Deterministically resolve wiki snippets and translation notes per cluster by querying `IRetrievalIndex` providers; persist results in `cluster_context.json` (shared snippets deduped). |
| **7. Glossary Enforcement** | Tag glossary terms with Aho–Corasick, propagate required targets into metadata/prompts, and stage validation hooks. |
| **8. Translate** | Apply category-aware prompt templates that blend cluster metadata, snippets, glossary directives, and cultural guidance; capture translations plus enriched metadata. |
| **9. Validate → Review → Integrate** | Run placeholder/glossary checks (`glossary_consistency.json`), perform human review (local or Paratranz), then package approved outputs with regression checks and feedback loops. |

**Key Points About the Pluggable Retrieval Flow**
- *Single manifest of truth* – every provider registers in `knowledge/index.manifest.json`, so downstream stages discover capabilities without hard-coded paths.
- *Rich payloads* – vector providers persist markdown slugs and keyword tokens so search hits map deterministically to knowledge artifacts and support filtered queries.
- *Shared abstraction* – `IRetrievalIndex` lets investigation, context selection, and future features query providers in priority order (vector first, keyword fallback, etc.).
- *Deterministic outputs* – even with vector stores, results are persisted into `cluster_context.json`, keeping translation reproducible.
- *Extensibility* – new providers (graph, hybrid BM25+vector) only require implementing the interface and wiring configuration; clustering and translation logic remain untouched.

This keeps the entire pipeline grounded: raw text → wiki cache → manifest-driven retrieval → clustering → context selection → translation, with glossary consistency enforced throughout.
