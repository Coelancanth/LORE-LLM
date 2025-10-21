# End-to-End Example: Pathologic 2 – Marble Nest

This walkthrough runs the full pipeline on a concrete project, showing commands, inputs, and outputs at each step.

---

## 0) Prerequisites

- .NET 8 SDK installed
- Project workspace folder exists: `workspace/pathologic2-marble-nest`
- Optional: Set DeepSeek API key for LLM providers
  - Windows (PowerShell):
    ```powershell
    $env:DEEPSEEK_API_KEY="sk-your-key"
    ```
  - macOS/Linux:
    ```bash
    export DEEPSEEK_API_KEY="sk-your-key"
    ```

---

## 1) Extract raw source

```bash
dotnet run --project src/LORE-LLM -- extract \
  --input raw-input/pathologic2-marble-nest/english.txt \
  --output workspace \
  --project "Pathologic2 Marble Nest"
```

- Outputs (under `workspace/pathologic2-marble-nest/`):
  - `source_text_raw.json` – normalized segments
  - `workspace.json` – manifest

---

## 2) Crawl wiki pages to Markdown

Full crawl (resumable in base-doc mode):
```bash
dotnet run --project src/LORE-LLM -- crawl-wiki \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --max-pages 0
```

Specific pages:
```bash
dotnet run --project src/LORE-LLM -- crawl-wiki \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --page "Daniil Dankovsky" \
  --page "Executor"
```

- Outputs: `knowledge/raw/*.md` with headers:
  - `> Source: ...`
  - `> License: CC-BY-SA 3.0`
  - `> Retrieved: <ISO date>`
  - Optional redirect-only docs start with `Redirect to:` list

---

## 3) Build retrieval index(es)

```bash
dotnet run --project src/LORE-LLM -- index-wiki \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --force-refresh
```

- Output: `knowledge/index.manifest.json` plus provider-specific artifacts (default keyword dictionary under `knowledge/wiki_keyword_index.json`)
  - Manifest lists active retrieval providers (e.g., `keyword`, `vector`) and their configuration hashes
  - Keyword provider sample entry: `{ title, keywords: [...], isRedirect: bool, redirectTargets?: [{title, slug}] }`
  - Additional providers (vector, graph) can be registered via config and will materialize their own cache files

---

## 4) Investigate and seed knowledge base

```bash
dotnet run --project src/LORE-LLM -- investigate \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --force-refresh
```

- Outputs:
  - `investigation.json` – token matches and wiki candidates per segment
  - `knowledge_base.json` – curated knowledge entries (with provenance)
  - Updates `workspace.json` manifest
- Uses the active retrieval providers declared in `knowledge/index.manifest.json`; keyword lookup remains the default fallback

---

## 5) Cluster segments with an LLM

```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --provider deepseek \
  --batch-size 800 \
  --overlap 80 \
  --save-transcript
```

- The command processes unassigned segments in batches, appending a summary of existing clusters (from `clusters_current.json`) plus the last `--overlap` segments so the LLM can extend prior groupings. Resume runs reuse the persisted ledger.
- Outputs:
  - `clusters_current.json` – authoritative ledger listing every cluster, category, shared context, and ordered members
  - `clusters_llm_part_*.json` – immutable per-batch snapshots for rollback/auditing
  - `clusters_llm_transcript.md` – concatenated prompts/responses for review

Ledger excerpt:
```json
{
  "clusterId": "scene:executor_farewell",
  "category": "dialogue",
  "sharedContext": [
    "Final exchange with the Executor inside the theatre",
    "Sets up the confrontation with Death"
  ],
  "knowledgeReferences": ["wiki:executor"],
  "members": [
    {"segmentId": "conv:6192355001750781", "ordinal": 0},
    {"segmentId": "conv:6192355001750784", "ordinal": 1}
  ],
  "confidence": 0.71
}
```

---

## 6) Build cluster context & translation notes

```bash
dotnet run --project src/LORE-LLM -- cluster-context \
  --workspace workspace \
  --project "Pathologic2 Marble Nest"
```

- Deterministically resolves wiki Markdown via `knowledge/wiki_keyword_index.json`, trims reusable snippets, and records them in `cluster_context.json` alongside optional translation notes/cultural guidance.
- Multiple clusters can point to the same snippet; prompts deduplicate at runtime.

Context excerpt:
```json
{
  "clusterId": "scene:executor_farewell",
  "knowledgeSnippets": [
    {
      "title": "Executor cloak (Pathologic 2)",
      "summary": [
        "Executor escorts the Bachelor toward the theatre exit.",
        "Tone: fatalistic, ritualistic."
      ],
      "sourcePath": "knowledge/raw/executor-cloak-pathologic-2.md"
    }
  ],
  "translationNotes": {
    "tone": "Surreal stage-play gravitas with fatalistic inevitability.",
    "speakerVoices": {
      "Executor": "Measured, ritualistic; guardian of the threshold.",
      "Bachelor": "Rational veneer cracking into incredulity."
    }
  }
}
```

---

## 7) Enrich metadata & translate

```bash
dotnet run --project src/LORE-LLM -- translate \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --language ru \
  --with-metadata \
  --with-translation-notes
```

- Category-specific prompt templates load the cluster ledger, context snippets, translation notes, global cultural guidance, and per-segment source strings.
- Glossary terms tagged via deterministic dictionary matching are injected as hard requirements; the LLM returns enriched metadata (speaker, entities, tone) plus translations in `translation_raw_ru.json`.
- Deterministic fallbacks ensure critical annotations (e.g., glossary matches) persist even if the LLM call is retried offline, with violations written to `glossary_consistency.json`.

---

## 8) Troubleshooting quick hits

- Missing wiki config: ensure `MediaWikiCrawlerOptions` registers your project
- Rate limits: lower `--batch-size`, use `--save-transcript`, and rerun only the failed `clusters_llm_part_*`
- Parse errors: validate JSON in the offending part snapshot; append a corrected rerun instead of rerunning everything
- Redirect loops: index shows `isRedirect: true` so downstream can skip

---

This flow demonstrates how to go from raw text to crawled context, keyword index, clustered scenes, deterministic wiki snippets, and category-aware translation with rich metadata.
