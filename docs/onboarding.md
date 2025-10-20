# LORE-LLM Onboarding Guide

Welcome aboard! This guide gives new contributors the minimum context and repeatable steps to get productive with LORE‑LLM. Keep it handy for the first few days and update it whenever you discover gaps.

---

## 1. Project at a Glance

| Concern | Quick Summary |
| --- | --- |
| **Purpose** | CLI-driven localization pipeline that extracts raw text, enriches it with context (metadata, clusters, knowledge), and prepares machine-translation workflows. |
| **Tech stack** | .NET 8 (C#), `CSharpFunctionalExtensions`, `Microsoft.Extensions.Hosting`, `System.CommandLine`, xUnit + Shouldly + NSubstitute. |
| **Artifacts** | JSON/TOML files under a per-project workspace (`source_text_raw.json`, `knowledge_base.json`, `investigation.json`, etc.). |
| **CLI verbs** | `extract`, `augment`, `translate`, `validate`, `integrate`, `investigate`, `crawl-wiki`, `cluster`. Each lives in `src/LORE-LLM/Presentation/Commands/<Verb>`. |

---

## 2. Get Set Up

1. **Install prerequisites**
   - .NET 8 SDK (`dotnet --list-sdks` should show `8.x`).
   - Git, your editor of choice (Rider, VS, VS Code).
   - Optional: PowerShell 7+ for repo scripts.

2. **Clone the repository**
   ```bash
   git clone https://github.com/<org>/LORE-LLM.git
   cd LORE-LLM
   ```

3. **Restore & build**
   ```bash
   dotnet build
   dotnet test
   ```
   You should see zero warnings (nullable + analyzers are treated as errors).

4. **Run the CLI help**
   ```bash
   dotnet run --project src/LORE-LLM -- --help
   ```
   Review each verb’s help (`--help`) to understand the surface area.

---

## 3. Repository Layout Cheatsheet

```
LORE-LLM/
├─ docs/                  -> Plans, backlogs, schemas, onboarding
├─ src/
│  └─ LORE-LLM/
│      ├─ Application/    -> Use cases (extract/investigate/etc.)
│      ├─ Domain/         -> Core records & services
│      ├─ Infrastructure/ -> IO, ingestion, persistence adapters
│      └─ Presentation/   -> CLI commands, option binding
├─ tests/
│  └─ LORE-LLM.Tests/     -> Unit & CLI integration tests
└─ samples/ (if present)  -> Example workspaces & input data
```

Stick to vertical slices: Domain → Application → Presentation. Infrastructure helpers slot in as needed but avoid cross-coupling.

---

## 4. Day-One Tasks

1. **Read the backlog and project plan**  
   - `docs/backlog.md` shows completed vertical slices and current priorities.  
   - `docs/LORE-LLM_Project_Plan.md` covers long-term vision, phases, and data assumptions.

2. **Run an extraction sample (if available)**  
   - Follow `docs/examples/` or sample workspace instructions.  
   - `dotnet run --project src/LORE-LLM -- extract --project ./samples/pathologic2`

3. **Skim the schemas**  
   - Understand JSON contracts in `docs/schemas/`.  
   - Knowing field names speeds up debugging later.

4. **Investigate wiki integration**  
   - Run `dotnet run --project src/LORE-LLM -- investigate --workspace <workspace> --project <name> --force-refresh` once to seed the wiki cache and keyword index.  
   - Subsequent runs can supply `--offline` to exercise the pipeline without network calls (fails if caches are missing or stale).

5. **(Optional) Cache wiki markdown**  
   - Use `dotnet run --project src/LORE-LLM -- crawl-wiki --workspace <workspace> --project <name> --page "Page Title"` to save MediaWiki content as markdown under `knowledge/raw/`.  
   - Advanced projects can register crawl post-processing plugins to strip UI fragments (infoboxes, galleries) without touching shared crawler code—see `docs/wiki_crawler.md` for configuration details.

6. **LLM-assisted clustering**  
   The `cluster` command batches segments and formats structured prompts for LLM-based clustering. Multiple workflows are supported:

   **a) Offline/Local Testing (deterministic, no API calls)**
   ```bash
   dotnet run --project src/LORE-LLM -- cluster \
     --workspace <workspace> \
     --project <name> \
     --provider local \
     --batch-size 25 \
     --save-transcript
   ```
   - Uses the built-in `local` provider that groups each batch into a single cluster (for testing/validation).
   - Outputs `clusters_llm.json` and `clusters_llm_transcript.md` under the project folder.
   - Updates `workspace.json` manifest with `clustersLlm` artifact reference.

   **b) Manual Workflow with Cursor or Browser-Based Chat (ChatGPT, Claude, etc.)**
   1. Run the cluster command with `--save-transcript` to generate the formatted prompt:
      ```bash
      dotnet run --project src/LORE-LLM -- cluster \
        --workspace <workspace> \
        --project <name> \
        --provider local \
        --batch-size 10 \
        --save-transcript
      ```
   2. Open the generated `<workspace>/<project>/clusters_llm_transcript.md` file.
   3. Copy the **last Prompt section** (everything under `# Prompt` until the next heading).
   4. Paste it into **Cursor Chat** (⌘+L / Ctrl+L), **ChatGPT**, **Claude**, or any other chat interface.
   5. Ask the LLM to return **only JSON** in the shape shown in the prompt template (array of cluster objects with `clusterId`, `memberIds`, optional `sharedContext`, `knowledgeReferences`, `confidence`, `notes`).
   6. Copy the LLM's JSON response and replace the content under `# Response` in the transcript file.
   7. Re-run the cluster command with the same arguments—it will parse the updated transcript and persist the clusters to `clusters_llm.json`.

   **c) API-Based Workflow (DeepSeek, OpenAI, Claude, etc.)**
   - **DeepSeek** is included out-of-the-box. Set the `DEEPSEEK_API_KEY` environment variable:
     ```bash
     # Windows (PowerShell)
     $env:DEEPSEEK_API_KEY="your-api-key-here"
     
     # macOS/Linux
     export DEEPSEEK_API_KEY="your-api-key-here"
     ```
   - Other providers (OpenAI, Claude) require custom implementations registered in `ServiceCollectionExtensions.cs`.
   - Run:
     ```bash
     dotnet run --project src/LORE-LLM -- cluster \
       --workspace <workspace> \
       --project <name> \
       --provider deepseek \
       --batch-size 25 \
       --save-transcript
     ```
   - The workflow automatically calls the API, parses the response, and persists artifacts.

   **d) Custom Prompt Templates**
   - Create a custom prompt template file (e.g., `my_cluster_prompt.txt`) with your instructions. Use `{{projectDisplayName}}` as a placeholder.
   - Supply it via `--prompt-template`:
     ```bash
     dotnet run --project src/LORE-LLM -- cluster \
       --workspace <workspace> \
       --project <name> \
       --provider deepseek \
       --prompt-template my_cluster_prompt.txt \
       --save-transcript
     ```

   **Notes:**
   - The `--provider` flag is pluggable; `local` is included for offline testing.
   - External providers (OpenAI, Claude, DeepSeek) require API credentials and must be registered in DI.
   - The workflow batches segments to stay within token limits; adjust `--batch-size` as needed.
   - Always use `--save-transcript` for auditing and manual review workflows.

7. **Build the wiki keyword index**  
   - After crawling, generate the index used by investigation and clustering prompts:  
   ```bash
   dotnet run --project src/LORE-LLM -- index-wiki \
     --workspace <workspace> \
     --project <name> \
     --force-refresh
   ```
   - This writes `<workspace>/<project>/knowledge/wiki_keyword_index.json` with `title`, `keywords`, and `isRedirect` flags.

---

## 5. Development Workflow Expectations

- **Branching**: `main` stays clean. Branch off (`feature/<short-desc>`) and open PRs with linked backlog items.
- **Testing**: Run `dotnet test` before every PR. Add or update tests alongside code changes.
- **Linting**: Analyzers + nullable are enforced. Fix warnings locally before pushing.
- **Functional style**: We favor `Result<T>` / `Maybe<T>` across layers. Keep exceptions for truly exceptional cases.
- **CLI UX**: Every command should return a deterministic exit code and log meaningful progress/error messages.

---

## 6. Troubleshooting & Help

| Problem | Try this first |
| --- | --- |
| Build fails with analyzer warnings | Run `dotnet format` (if configured) or fix the warnings; they block compilation. |
| CLI command can’t find workspace files | Confirm you’re passing `--project`/`--workspace` pointing to a folder containing `workspace.toml`. |
| Investigation command slow or failing | Delete cached wiki data under the project workspace and rerun with `--force-refresh`. |
| Unsure where a service lives | Search under `src/LORE-LLM/Application` for the verb; follow the DI registrations in `Program.cs` / `DependencyInjection` extensions. |

If you get stuck, drop questions in the team’s chat channel or open a draft PR and tag a maintainer.

---

## 7. Next Updates

- Keep this document current: add new CLI verbs, change prerequisites, or note common pitfalls as the project evolves.
- Consider automating setup (scripts, sample workspace) once the onboarding steps stabilize.

Welcome again—happy shipping! 🎉

