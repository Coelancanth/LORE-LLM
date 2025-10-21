# LORE-LLM Handbook

Comprehensive guide for contributors, operators, and reviewers working with the LORE-LLM localization pipeline. It combines onboarding instructions, architectural vision, command guides, and troubleshooting practices into a single reference.

---

## 1. Project Overview

| Concern | Details |
| --- | --- |
| **Purpose** | Deterministic, CLI-driven localization workflow that extracts raw text, enriches it with knowledge-aware metadata (clusters, glossary, context), and prepares machine-assisted translation while preserving audit trails. |
| **Tech Stack** | .NET 8 (C#), `CSharpFunctionalExtensions`, `Microsoft.Extensions.Hosting`, `System.CommandLine`, xUnit + Shouldly + NSubstitute. |
| **Key Artifacts** | Workspace-local JSON/TOML files (`source_text_raw.json`, `clusters_current.json`, `cluster_context.json`, `translation_raw_{lang}.json`, etc.). |
| **CLI Verbs** | `extract`, `crawl-wiki`, `index-*`, `investigate`, `cluster`, `translate`, `validate`, `integrate` (with additional feature verbs enabled via config). |

### Repository Layout

```
LORE-LLM/
├─ docs/                  -> Handbook, backlog, schemas, examples
├─ src/
│  └─ LORE-LLM/
│      ├─ Application/    -> Use cases (extract/investigate/etc.)
│      ├─ Domain/         -> Core records & services
│      ├─ Infrastructure/ -> IO, ingestion, persistence adapters
│      └─ Presentation/   -> CLI commands, option binding
├─ tests/
│  └─ LORE-LLM.Tests/     -> Unit & CLI integration tests
└─ samples/ (optional)    -> Example workspaces & input data
```

We follow a vertical-slice architecture. Each CLI verb lives in its own feature module (Presentation → Application → Domain). Shared services plug in via DI, while infrastructure helpers stay composable and avoid cross-coupling.

---

## 2. Getting Started

1. **Install prerequisites**
   - .NET 8 SDK (`dotnet --list-sdks` should show `8.x`)
   - Git and your IDE of choice (Rider, VS, VS Code)
   - Optional: PowerShell 7+ for repo scripts

2. **Clone & bootstrap**
   ```bash
   git clone https://github.com/<org>/LORE-LLM.git
   cd LORE-LLM
   dotnet build
   dotnet test
   dotnet run --project src/LORE-LLM -- --help
   ```

3. **First-day checklist**
   - Read `docs/backlog.md` for the latest feature status.
   - Skim JSON schemas in `docs/schemas/` to understand artifact contracts.
   - Optional: run `extract`, `crawl-wiki`, and `cluster` on a sample project (see §5).

4. **Workflow expectations**
   - **Branching**: keep `main` clean; open feature branches with linked backlog items.
   - **Testing**: run `dotnet test` before every PR; add tests with changes.
   - **Linting**: nullable & analyzers are warnings-as-errors; keep builds clean.
   - **Functional style**: prefer `Result<T>`/`Maybe<T>`; reserve exceptions for exceptional scenarios.
   - **CLI UX**: commands must return deterministic exit codes and meaningful progress/errors.

---

## 3. Core Architecture & Artifacts

### 3.1 Raw Source (`source_text_raw.json`)
Minimal extraction output containing `id`, `text`, `lineNumber`, and `isEmpty`, plus project metadata (`project`, `projectDisplayName`, `generatedAt`, `inputHash`). This file is the spine for downstream processing.

### 3.2 Cluster Ledger (`clusters_current.json`)
Authoritative record of all clusters produced by the incremental `cluster` workflow.

```json
{
  "project": "pathologic2-marble-nest",
  "projectDisplayName": "Pathologic 2: Marble Nest",
  "generatedAt": "2025-10-20T13:35:00Z",
  "sourceTextHash": "8f4c3d9...",
  "clusters": [
    {
      "clusterId": "scene:executor_farewell",
      "category": "dialogue",
      "sharedContext": [
        "Final exchange with the Executor inside the theatre",
        "Sets up the confrontation with Death"
      ],
      "knowledgeReferences": [
        "wiki:executor",
        "wiki:stone_yard"
      ],
      "members": [
        {"segmentId": "conv:6192355001750781", "ordinal": 0},
        {"segmentId": "conv:6192355001750784", "ordinal": 1}
      ],
      "confidence": 0.71,
      "notes": "LLM-generated synopsis based on chat prompt context and operator-provided segments."
    }
  ]
}
```

The clustering pipeline also writes immutable checkpoints (`clusters_llm_part_*.json`) and `clusters_llm_transcript.md` per batch for auditing and reruns.

### 3.3 Cluster Context Cache (`cluster_context.json`)
Deterministically generated after clustering, this artifact captures reusable wiki snippets and translation notes.

```json
{
  "clusterId": "scene:executor_farewell",
  "segmentIds": [
    "conv:6192355001750781",
    "conv:6192355001750784"
  ],
  "category": "dialogue",
  "knowledgeSnippets": [
    {
      "title": "Executor cloak (Pathologic 2)",
      "slug": "executor-cloak-pathologic-2",
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
      "Executor": "Measured, ritualistic; speaks as guardian of the threshold.",
      "Bachelor": "Rational veneer slipping into incredulity and defiance."
    },
    "culturalAdaptation": [
      "Preserve the theatre motif and stagecraft cadence.",
      "Highlight the ritual nature of death to avoid literal medical phrasing."
    ]
  }
}
```

### 3.4 Knowledge Base (`knowledge/key_concepts.json`)
Structured knowledge entries with aliases, summaries, provenance, and related terms. Generated during investigation, used for glossary building and prompt enrichment.

### 3.5 Investigation Report (`investigation.json`)
Optional per-segment suggestions listing matched tokens and candidate knowledge entries. Useful for QA and when clusters are unavailable.

### 3.6 Glossary Consistency (`glossary_consistency.json`)
Produced during validation to log deterministic glossary enforcement results.

```json
{
  "project": "pathologic2-marble-nest",
  "generatedAt": "2025-10-21T07:12:34Z",
  "terms": [
    {
      "termId": "lore:mother_boddho",
      "sourceTerm": "Mother Boddho",
      "targetTerm": "博多母神",
      "matches": [
        {
          "segmentId": "conv:6192355001877199",
          "clusterId": "scene:boddho_ritual",
          "occurrence": "Mother Boddho",
          "status": "enforced"
        }
      ]
    }
  ],
  "violations": [
    {
      "segmentId": "conv:6192355001963681",
      "clusterId": "scene:condolences",
      "termId": "ritual:condolence",
      "expected": "慰问",
      "actual": "致意",
      "severity": "warning"
    }
  ]
}
```

Enforcement uses an Aho–Corasick matcher to tag source occurrences, propagate expectations into prompts, and validate translations.

---

## 4. End-to-End Workflow

1. **Configure**: Define providers, templates, and locales in `config.yaml` (or equivalent).
2. **Extract**: Run `extract` to normalize raw text into `source_text_raw.json`. Optionally append authored metadata.
3. **Sanitize**: Apply project-specific post processors immediately after extraction (remove artifacts, normalize formatting).
4. **Crawl**: `crawl-wiki` pulls MediaWiki content into `knowledge/raw/`, honoring per-project HTML post processors and tab variants.
5. **Index**: Pluggable `index-*` commands (e.g., `index-wiki`) materialize retrieval stores and record active providers in `knowledge/index.manifest.json`. Keyword dictionaries are the default; vector/graph indexes can plug in.
6. **Investigate (optional)**: `investigate` queries the active retrieval providers to attach per-segment suggestions (`investigation.json`, `knowledge_base.json`). Useful for provenance and glossary seeding, but not required for clustering/translation if caches exist.
7. **Cluster**: The incremental `cluster` command processes unassigned segments in batches, referencing the current ledger and overlap window to extend clusters. Outputs `clusters_current.json`, per-batch checkpoints, transcripts, and updated workspace manifests.
8. **Context Selection**: A deterministic job queries the retrieval index(es) to map clusters to wiki snippets and translation notes, emitting `cluster_context.json`.
9. **Glossary Enforcement**: The glossary manager tags terms, injects required targets into metadata, and preps enforcement hints for prompts.
10. **Enrich & Translate**: Category-aware templates combine cluster metadata, cached snippets, glossary directives, and global cultural guidance in a single LLM pass per cluster. Outputs enriched metadata and `translation_raw_{lang}.json` with deterministic fallbacks.
11. **Validate**: `validate` checks placeholders, glossary coverage, cluster completeness, and writes `glossary_consistency.json` for QA review.
12. **Human Review**: Local markup review today; Paratranz or other integrations planned.
13. **Integrate**: `integrate` packages approved translations for the target engine, triggers regression tests, and feeds notes back into glossary/prompts.

---

## 5. Command Walkthrough

### 5.1 Extraction & Post-Processing

```bash
dotnet run --project src/LORE-LLM -- extract \
  --input raw-input/pathologic2-marble-nest/english.txt \
  --output workspace \
  --project "Pathologic2 Marble Nest"
```

Key options:
- `--post-process` (if exposed) toggles project-specific cleanup.
- Implement new `IPostExtractionProcessor` to normalize unique quirks.

### 5.2 MediaWiki Crawl

```bash
dotnet run --project src/LORE-LLM -- crawl-wiki \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --max-pages 0 \
  --force-refresh
```

**Configuration tips**
- Register projects under `MediaWikiCrawlerOptions` (sanitized project name → `MediaWikiCrawlerProjectOptions`).
- Attach `HtmlPostProcessors` (e.g., `common`, `pathologic-marble-nest`) to strip UI and flatten tab layouts.
- Define `TabOutputs` to emit per-variant markdown with custom file suffixes and headings.
- `EmitBaseDocument = false` yields tab-only exports; base docs still produce redirect stubs.

**Resume semantics**
- Base-document mode skips existing markdown unless `--force-refresh`.
- Tab-only mode currently regenerates variants; backlog item VS-0015 will add skip logic.

### 5.3 Retrieval Indexing

Default keyword index:
```bash
dotnet run --project src/LORE-LLM -- index-wiki \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --force-refresh
```

Outputs:
- `knowledge/index.manifest.json` listing active providers (`keyword`, future `vector`, etc.)
- Provider artifacts (keyword dictionary under `knowledge/wiki_keyword_index.json` containing `title`, `keywords`, `isRedirect`, `redirectTargets`)

Additional indexers (vector/graph) can be registered via configuration and will appear alongside the keyword provider.

### 5.4 Investigation (Optional)

```bash
dotnet run --project src/LORE-LLM -- investigate \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --force-refresh
```

Uses the retrieval providers from the manifest to match segments with knowledge entries. Generates `investigation.json` and `knowledge_base.json` for provenance and glossary cues.

### 5.5 Clustering

Incremental LLM-driven clustering:
```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --provider deepseek \
  --batch-size 800 \
  --overlap 80 \
  --save-transcript
```

Workflow features:
- Ledger-first design avoids heavy reconciliation; overlap window lets batches extend prior clusters.
- Per-batch checkpoints (`clusters_llm_part_*.json`) enable targeted reruns.
- Prompts can use built-in or custom templates (`--prompt-template`) and global context files.
- Providers (`local`, `deepseek`, etc.) register via DI/config; `config/chat.providers.json` controls defaults.

Manual workflow (Cursor/ChatGPT):
1. Run `cluster` with `--provider local --save-transcript`.
2. Copy the latest `# Prompt` section and paste into your chat tool.
3. Request JSON-only output matching the schema.
4. Paste the response under `# Response` in the transcript file.
5. Re-run `cluster` to parse and persist clusters.

### 5.6 Context Selection

```bash
dotnet run --project src/LORE-LLM -- cluster-context \
  --workspace workspace \
  --project "Pathologic2 Marble Nest"
```

Resolves snippets by querying `IRetrievalIndex` providers (keyword, vector, etc.), dedupes shared snippets, and writes `cluster_context.json` with summaries and translation notes.

### 5.7 Translation & Glossary Enforcement

```bash
dotnet run --project src/LORE-LLM -- translate \
  --workspace workspace \
  --project "Pathologic2 Marble Nest" \
  --language ru \
  --with-metadata \
  --with-translation-notes
```

- Category-specific templates tailor prompts for dialogue, UI, lore, etc.
- Glossary matches (via Aho–Corasick) are injected as hard requirements; violations recorded in `glossary_consistency.json`.
- Deterministic fallbacks ensure glossary annotations persist even if LLM calls are retried or replaced.

### 5.8 Validation & Integration

`validate` performs placeholder checks, glossary coverage enforcement, and cluster completeness audits. `integrate` packages approved translations, runs regression hooks, and writes deployment-ready outputs.

---

## 6. Configuration Reference

### Chat Providers (`config/chat.providers.json`)

```json
{
  "defaultProvider": "local",
  "providers": {
    "deepseek": {
      "model": "deepseek-chat",
      "temperature": 0.7,
      "maxTokens": 4096,
      "apiKeyEnvVar": "DEEPSEEK_API_KEY"
    }
  }
}
```

Precedence: CLI flag → environment variable → config file. Missing API keys surface actionable errors (e.g., “set DEEPSEEK_API_KEY”).

### Retrieval Index Manifest (`knowledge/index.manifest.json`)

Example:
```json
{
  "generatedAt": "2025-10-21T05:42:10Z",
  "providers": [
    {
      "name": "keyword",
      "artifact": "knowledge/wiki_keyword_index.json",
      "hash": "d1c4...",
      "config": {
        "tokenizer": "default",
        "minTokenLength": 3
      }
    }
  ]
}
```

Use this manifest to drive context selection, investigation, or any component that needs knowledge lookups.

---

## 7. Roadmap Snapshot

Refer to `docs/backlog.md` for full details. Key upcoming items:

- **VS-0008**: Expand MediaWiki post-processing plugins.
- **VS-0010**: Glossary-aware enrichment drawing from clusters and `cluster_context.json`.
- **VS-0012**: CLI command presets for ergonomic defaults.
- **VS-0013**: Pluggable chat provider configuration (config-driven).
- **VS-0014**: Knowledge-aware clustering prompts leveraging retrieval providers.
- **VS-0015**: Resume semantics for tab-only wiki outputs.
- **VS-0017**: Global context plugins for clustering/translation prompts.

---

## 8. Troubleshooting

| Problem | Suggested Fix |
| --- | --- |
| Analyzer warnings block build | Run `dotnet format` (if configured) or address warnings. |
| CLI cannot find workspace files | Ensure `--workspace`/`--project` point to a folder with `workspace.toml`. |
| `crawl-wiki` slow or failing | Clear cached wiki data for the project and rerun with `--force-refresh`. |
| `cluster` provider missing | Verify provider name in `config/chat.providers.json` and environment key (e.g., `DEEPSEEK_API_KEY`). |
| LLM response cannot be parsed | Inspect `clusters_llm_transcript.md` for malformed JSON; request a re-run or manual edit. |
| Token limits in prompts | Reduce `--batch-size`, trim snippet summaries, or leverage overlap window settings. |
| Glossary violations | Check `glossary_consistency.json` and update glossary entries or translation outputs accordingly. |

If you remain blocked, open a draft PR or reach out in team chat.

---

## 9. Best Practices & Tips

- **Start with local providers** for deterministic dry runs before hitting external APIs.
- **Always enable `--save-transcript`** during clustering to maintain audit trails.
- **Customize prompts deliberately**: keep global context short, reuse templates, and document changes.
- **Commit artifacts thoughtfully**: cluster ledgers, context caches, and translation outputs are part of the review surface.
- **Use per-batch checkpoints** to rerun only the necessary segment sets after prompt updates.
- **Keep glossary authoritative**: reconcile reviewer feedback quickly so future runs stay consistent.
- **Version configs** (`config.yaml`, chat providers, retrieval manifest) alongside code changes.

---

## 10. Appendix: Sample Commands

```bash
# Extraction & cleanup
dotnet run --project src/LORE-LLM -- extract --input raw-input/... --output workspace --project "Pathologic2 Marble Nest"

# Crawl + keyword index
dotnet run --project src/LORE-LLM -- crawl-wiki --workspace workspace --project "Pathologic2 Marble Nest" --force-refresh
dotnet run --project src/LORE-LLM -- index-wiki --workspace workspace --project "Pathologic2 Marble Nest" --force-refresh

# Investigation (optional)
dotnet run --project src/LORE-LLM -- investigate --workspace workspace --project "Pathologic2 Marble Nest"

# Clustering with DeepSeek
dotnet run --project src/LORE-LLM -- cluster --workspace workspace --project "Pathologic2 Marble Nest" --provider deepseek --batch-size 800 --overlap 80 --save-transcript

# Generate cluster context
dotnet run --project src/LORE-LLM -- cluster-context --workspace workspace --project "Pathologic2 Marble Nest"

# Translation with metadata + notes
dotnet run --project src/LORE-LLM -- translate --workspace workspace --project "Pathologic2 Marble Nest" --language ru --with-metadata --with-translation-notes

# Validation
dotnet run --project src/LORE-LLM -- validate --workspace workspace --project "Pathologic2 Marble Nest"
```

Happy shipping! Keep this handbook current and adapt it as the pipeline evolves.
