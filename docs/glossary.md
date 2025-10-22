# LORE-LLM Glossary

Shared terminology for the localization pipeline. Keep this file close to the code so engineering, operations, and reviewers speak the same language.

## Core Concepts

- **Artifact** – Deterministic file produced by a pipeline stage (e.g., `source_text_raw.json`, `clusters_current.json`, `knowledge/raw/*.md`). Artifacts are the hand-off between stages and carry the data that downstream steps consume.
- **Manifest** – Canonical index that lists artifacts, their hashes, and where to find them. Examples include `workspace.json` (workspace-wide ledger) and `knowledge/index.manifest.json` (retrieval providers). Manifests let later stages discover outputs without hard-coded paths.
- **Ledger** – Authoritative record of the current state for a stage. `clusters_current.json` is the clustering ledger: it tracks every cluster id, membership list, notes, and prompts so reruns can resume deterministically.
- **Cluster** – Group of related segments that share translation context. Clusters drive prompt construction, glossary expectations, and context retrieval.
- **Segment** – Smallest unit of extracted source text, carrying an id, original text, and metadata (line number, emptiness flag, etc.). Segments roll up into clusters.
- **Workspace** – Folder containing all artifacts for a project run (`workspace.json`, `knowledge/`, `translation_*`, etc.).
- **Provider** – Pluggable component registered in a manifest (e.g., retrieval provider `keyword`, vector provider `vector:qdrant`, chat provider `local`). Providers expose common interfaces so features can swap implementations without changing orchestration code.
- **Pipeline** – Ordered set of CLI verbs (`extract`, `crawl-wiki`, `index-wiki`, `investigate`, `cluster`, `translate`, `validate`, `integrate`) that produce and mutate artifacts inside a workspace.
- **Knowledge Base Curation** – Process of turning unstructured lore sources (tables, wikis, bespoke docs) into structured Markdown with extracted keywords and optional LLM-enriched summaries. Output feeds the retrieval indexes.

## Retrieval Terminology

- **Knowledge Base** – Cached wiki metadata and snippets produced during investigation, used as provenance for clusters and translation context.
- **Keyword Index** – Deterministic token → title map built from markdown filenames (`knowledge/wiki_keyword_index.json`).
- **Vector Index** – Qdrant collection seeded with deterministic embeddings so cluster summaries can retrieve relevant wiki articles. Payloads carry markdown slugs and keyword tokens for deterministic lookups.
- **Snippet** – Short excerpt from a knowledge artifact (markdown section, tab variant) used to provide context in prompts or reports.
- **Slug** – URL- and filesystem-friendly identifier derived from a title (e.g., `Bachelor (Pathologic 2)` → `bachelor-pathologic-2`), used to name markdown files and link retrieval results back to artifacts.
- **Upsert** – “Update or insert.” Operation used when sending vectors to Qdrant so existing points are replaced and new ones are added in a single call.

## Glossary Maintenance

- Keep entries concise: 2–3 sentences describing intent and how the term fits in the pipeline.
- Prefer linking back to source files or artifacts where the term lives (e.g., `clusters_current.json`, `knowledge/index.manifest.json`).
- Update this glossary when introducing new pipeline stages, manifests, or provider concepts.
