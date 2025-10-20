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

## 3) Build the wiki keyword index

```bash
dotnet run --project src/LORE-LLM -- index-wiki \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --force-refresh
```

- Output: `knowledge/wiki_keyword_index.json`
  - Each entry: `{ title, keywords: [..], isRedirect: bool, redirectTargets?: [{title, slug}] }`
  - Redirect-only pages are flagged and include `redirectTargets` for downstream skipping/resolution

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

---

## 5) Cluster segments with an LLM

### a) Offline/local provider
```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --provider local \
  --batch-size 25 \
  --save-transcript
```

### b) DeepSeek provider
```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --provider deepseek \
  --batch-size 25 \
  --max-segments 100 \
  --save-transcript
```

- Outputs:
  - `clusters_llm.json` – `ClusterDocument` with `clusters` array
  - `clusters_llm_transcript.md` – full prompt/response

Transcript prompt excerpt:
```text
You are an assistant that clusters related dialogue lines for Pathologic 2: Marble Nest.
Return ONLY JSON as an array under key 'clusters' or a bare array of objects with: clusterId, memberIds, sharedContext (optional array), knowledgeReferences (optional array), confidence (0..1), notes (optional).

Segments:
- id: conv:6192355001750781
  text: "Executor, stay your blade."
- id: conv:6192355001750784
  text: "Well, you are going to die now..."
```

Expected JSON (bare array) excerpt:
```json
[
  {
    "clusterId": "scene:executor_farewell",
    "memberIds": ["conv:6192355001750781", "conv:6192355001750784"],
    "sharedContext": ["Final exchange in theatre"],
    "knowledgeReferences": ["wiki:executor"],
    "confidence": 0.71
  }
]
```

---

## 6) Next steps (enrichment & translation)

- Enrich segment metadata from clusters (planned VS-0010):
  - Push `sharedContext`, `knowledgeReferences`, and synopsis back into per-segment metadata
- Translate (sample):
```bash
dotnet run --project src/LORE-LLM -- translate \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --language ru
```

---

## 7) Troubleshooting quick hits

- Missing wiki config: ensure `MediaWikiCrawlerOptions` registers your project
- Rate limits: lower `--batch-size`, use `--save-transcript` and manual Cursor flow
- Parse errors: validate JSON in transcript; re-run clustering
- Redirect loops: index shows `isRedirect: true` so downstream can skip

---

This flow demonstrates how to go from raw text to crawled context, keyword index, knowledge base, and clustered segments ready for enrichment and translation.
