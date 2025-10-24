You are building an overview of a project’s raw localization assets.  
All work happens under `raw-input/<project>` and the goal is to produce a refined `analysis/raw_input_overview.json` that future automation can trust.

## Required Output (`raw_input_overview.json`)
Return a JSON object with the following sections:

```json
{
  "directories": [...],
  "filePatterns": [
    { "directory": "relative/path", "extensions": { ".xml": count, ".json": count, ... } },
    ...
  ],
  "stringIdPatterns": {
    "commonKeys": [...],            // keys like "id", "line", "original"
    "filenamePatterns": [...],      // glob-like patterns or regex fragments discovered in filenames
    "contentMarkers": [...],        // tokens inside files that signal IDs/blocks (e.g., "translate english", "<string id=\"")
    "notes": [...]                  // optional clarifications per pattern
  },
  "recommendedGrouping": [
    {
      "group": "high-level bucket (e.g., ui_and_system)",
      "reason": "why this grouping exists (cite directories/patterns)",
      "sourceDirectories": [...],
      "filenamePatterns": [...],
      "stringMarkers": [...]
    },
    ...
  ],
  "scanAt": "ISO-8601 timestamp"
}
```

### Field requirements
- `directories`: every relative directory (dot for root) discovered under `raw-input/<project>`, sorted lexicographically.
- `filePatterns`: emit **one entry per directory**. For each, count every file extension present (ignore case) and list them in descending count order.
- `stringIdPatterns`: derive keys/markers by actually scanning files. Populate `commonKeys`, `filenamePatterns`, `contentMarkers`; leave arrays empty only after confirming nothing exists. Add clarifying notes when appropriate.
- `recommendedGrouping`: algorithmically derive high-level buckets using the data above. Each entry must reference the evidence (directories, filename patterns, markers) that justify the grouping. Do not hard-code manual categories; infer them from what you observed. Groups should be sufficient to seed downstream automation (e.g., “quests_and_events” derived from `scripts/data/slides` + filenames containing `quest`).
- `scanAt`: timestamp when the scan ran (UTC).

## Tasks
1. Traverse `raw-input/<project>` recursively.
2. Collect the data needed to populate each field exactly as specified.
3. Write the refined JSON to `workspace/<project>/analysis/raw_input_overview.json` (create directories if missing). Overwrite existing content.
4. Print a human-readable summary at the end (directories counted, unique extensions, number of recommended groups).

## Constraints
- Be idempotent: re-running must produce identical output when the source tree hasn’t changed.
- Use UTF-8 without BOM.
- Fail fast with clear errors if the input tree is missing.
- Do not attempt any further processing (e.g., structured extraction or translation); this task is only about producing the refined overview JSON.
