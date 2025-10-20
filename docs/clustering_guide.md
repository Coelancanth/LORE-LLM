# LLM-Assisted Clustering Guide

## Overview

The `cluster` command enables LLM-assisted grouping of related dialogue segments with pluggable chat providers. It batches segments, formats structured prompts, invokes an LLM, parses the JSON response, and persists cluster metadata alongside audit transcripts.

## Quick Start

### 1. Offline Testing (No API Required)

```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project pathologic2-marble-nest \
  --provider local \
  --batch-size 25 \
  --save-transcript
```

**Output:**
- `workspace/pathologic2-marble-nest/clusters_llm.json`
- `workspace/pathologic2-marble-nest/clusters_llm_transcript.md`
- Updated `workspace/pathologic2-marble-nest/workspace.json` (adds `clustersLlm` artifact)

### 2. Manual Workflow with Cursor or Browser Chat

**Step-by-step:**

1. Generate the prompt using the local provider:
   ```bash
   dotnet run --project src/LORE-LLM -- cluster \
     --workspace workspace \
     --project pathologic2-marble-nest \
     --provider local \
     --batch-size 10 \
     --save-transcript
   ```

2. Open `workspace/pathologic2-marble-nest/clusters_llm_transcript.md`

3. Copy the **last Prompt section** (everything from `# Prompt` to the next heading)

4. Paste into Cursor Chat (⌘+L / Ctrl+L) or any browser chat (ChatGPT, Claude, etc.)

5. Ask the LLM to return **only JSON** in this shape:
   ```json
   [
     {
       "clusterId": "scene:example",
       "memberIds": ["seg-1", "seg-2"],
       "sharedContext": ["Description of shared context"],
       "knowledgeReferences": ["concept:character_name"],
       "confidence": 0.85,
       "notes": "Optional notes about this cluster"
     }
   ]
   ```

6. Copy the LLM's JSON response and **replace the content under `# Response`** in the transcript file

7. Re-run the same command—it will parse the transcript and persist clusters to `clusters_llm.json`

Tip: Improve prompts by injecting relevant concepts from the wiki keyword index (`knowledge/wiki_keyword_index.json`) and `knowledge_base.json` (from the `investigate` stage). Keep total prompt tokens reasonable and prefer bullet lists of concept IDs (e.g., `wiki:executor`) with one-line summaries.

### 3. DeepSeek API (Automatic)

**Setup:**
```bash
# Windows (PowerShell)
$env:DEEPSEEK_API_KEY="sk-your-deepseek-api-key"

# macOS/Linux
export DEEPSEEK_API_KEY="sk-your-deepseek-api-key"
```

**Run:**
```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project pathologic2-marble-nest \
  --provider deepseek \
  --batch-size 25 \
  --save-transcript
```

The workflow automatically:
- Calls DeepSeek API with the formatted prompt
- Parses the JSON response
- Persists `clusters_llm.json` and transcript
- Updates workspace manifest

## Custom Prompt Templates

Create a file `my_prompt.txt`:
```text
You are a dialogue clustering expert for {{projectDisplayName}}.
Analyze the segments below and group related ones into thematic clusters.
Return ONLY valid JSON as an array of cluster objects.

Each cluster must have:
- clusterId: unique identifier (e.g., "scene:opening", "quest:rescue_mission")
- memberIds: array of segment IDs
- sharedContext: array of strings describing the common theme
- knowledgeReferences: array of relevant concept IDs (optional)
- confidence: float 0.0-1.0
- notes: any observations (optional)
```

Then run:
```bash
dotnet run --project src/LORE-LLM -- cluster \
  --workspace workspace \
  --project pathologic2-marble-nest \
  --provider deepseek \
  --prompt-template my_prompt.txt \
  --save-transcript
```

## Output Schema

### clusters_llm.json
```json
{
  "project": "pathologic2-marble-nest",
  "projectDisplayName": "Pathologic 2: Marble Nest",
  "generatedAt": "2025-10-20T15:30:00Z",
  "sourceTextHash": "abc123...",
  "clusters": [
    {
      "clusterId": "scene:executor_farewell",
      "memberIds": ["conv:6192355001750781", "conv:6192355001750784"],
      "sharedContext": [
        "Final exchange with the Executor",
        "Sets up confrontation with Death"
      ],
      "knowledgeReferences": ["character:executor"],
      "confidence": 0.71,
      "notes": "High-stakes dialogue sequence"
    }
  ]
}
```

### clusters_llm_transcript.md
```markdown
# Prompt
You are an assistant that clusters related dialogue lines for Pathologic 2: Marble Nest.
...

Segments:
- id: conv:6192355001750781
  text: "Executor, stay your blade."
- id: conv:6192355001750784
  text: "Well, you are going to die now..."

# Response
[
  {
    "clusterId": "scene:executor_farewell",
    "memberIds": ["conv:6192355001750781", "conv:6192355001750784"],
    ...
  }
]
```

## Command Options

| Option | Description | Default |
|--------|-------------|---------|
| `--workspace`, `-w` | Workspace directory | *required* |
| `--project`, `-p` | Project display name | "default" |
| `--provider` | Chat provider (local, deepseek) | "local" |
| `--batch-size` | Segments per batch | 25 |
| `--max-segments` | Upper limit on segments processed (testing) | 0 (no cap) |
| `--include-empty` | Include empty segments | false |
| `--prompt-template` | Custom prompt file path | *(built-in)* |
| `--save-transcript` | Save prompt/response audit log | false |

## Adding New Providers

Implement `IChatProvider`:

```csharp
public sealed class MyCustomProvider : IChatProvider
{
    public string Name => "mycustom";

    public async Task<Result<string>> CompleteAsync(string prompt, CancellationToken cancellationToken)
    {
        // Call your LLM API
        // Return JSON response as string
    }
}
```

Register in `ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IChatProvider, MyCustomProvider>();
```

## Troubleshooting

**"Chat provider not found: deepseek"**
- Ensure `DEEPSEEK_API_KEY` environment variable is set before running
- Restart terminal/IDE after setting the variable

**"Failed to parse clusters from LLM response"**
- Check `clusters_llm_transcript.md` for malformed JSON
- Ensure LLM returned a bare array `[{...}]` or wrapped in `{"clusters": [{...}]}`
- Validate JSON manually at jsonlint.com

**"DeepSeek API error: 401"**
- Verify API key is correct
- Check key hasn't expired at platform.deepseek.com

**"DeepSeek API error: 429"**
- Rate limit exceeded; reduce `--batch-size` or wait before retrying
- Consider using `--save-transcript` for manual workflow

## Best Practices

1. **Start small:** Test with `--batch-size 5` first to verify prompt quality
2. **Always audit:** Use `--save-transcript` to review LLM responses
3. **Iterate prompts:** Refine `--prompt-template` based on transcript quality
4. **Manual review:** Check `confidence` scores and adjust low-quality clusters
5. **Version control:** Commit `clusters_llm.json` and transcripts for traceability

## Next Steps

After clustering, use the generated clusters in downstream stages:
- **Augmentation:** Merge cluster context into segment metadata (VS-0010)
- **Translation:** Leverage shared context for coherent translations
- **Validation:** Check cluster coverage and flag orphaned segments

