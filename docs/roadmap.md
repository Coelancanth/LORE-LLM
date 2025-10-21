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
- Extraction + project-specific post processors.
- MediaWiki crawl with configurable HTML pipeline and tab exports.
- Pluggable retrieval indices (`index-*`) writing to `knowledge/index.manifest.json` (keyword dictionary first, vector/graph later).
- Investigation (optional) leveraging the retrieval providers.
- Incremental clustering workflow with checkpoints/transcripts.
- Deterministic cluster context selection and translation-note cache.
- Glossary tagging (Aho–Corasick), prompt injection, and consistency reporting.
- Baseline translation CLI with category-aware templates.

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
