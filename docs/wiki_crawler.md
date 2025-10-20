# MediaWiki Crawler Configuration

The `crawl-wiki` command fetches raw wiki pages as Markdown so downstream investigation and translation stages have rich context. This document explains how to configure the crawler per project and extend the HTML clean-up pipeline when onboarding a new fandom.

---

## Project Registration

Projects are keyed by the sanitized project name (see `ProjectNameSanitizer`). Register each project under `MediaWikiCrawlerOptions` during DI composition:

```csharp
services.Configure<MediaWikiCrawlerOptions>(options =>
{
    var project = new MediaWikiCrawlerProjectOptions
    {
        ApiBase = "https://pathologic.fandom.com/api.php",
        EmitBaseDocument = false
    };

    project.HtmlPostProcessors.Add(MediaWikiHtmlPostProcessorIds.Common);
    project.HtmlPostProcessors.Add(MediaWikiHtmlPostProcessorIds.PathologicMarbleNest);

    options.Projects["pathologic2-marble-nest"] = project;
});
```

* `ApiBase` – MediaWiki API endpoint (usually `<host>/api.php`).
* `HtmlPostProcessors` – ordered list of post-processing plugins (see below). Leave empty to run every plugin whose `CanProcess` returns `true`.
* `EmitBaseDocument` – defaults to `true`. Set to `false` when you only want per-tab outputs (pages without matching tabs still emit a combined document so redirects remain preserved).

---

## HTML Post-Processing Pipeline

Before converting HTML to Markdown we pass each page through a pipeline of `IMediaWikiHtmlPostProcessor` implementations. Processors can remove decorative UI, flatten tabbed layouts, or perform project-specific fixes.

| Processor ID | Description |
| --- | --- |
| `common` | Removes comments, scripts, styles, noscript blocks, and edit controls that should never reach Markdown. |
| `pathologic-marble-nest` | Pathologic-specific cleanup that strips infoboxes, tab UI chrome, galleries, and ensures tabbed sections become standalone Markdown headings. |

### Creating a New Processor

1. Implement `IMediaWikiHtmlPostProcessor` and choose a unique `Id`.
2. Register it with DI (e.g., `services.AddSingleton<IMediaWikiHtmlPostProcessor, MyNewProcessor>();`).
3. Add the processor `Id` to the relevant project’s `HtmlPostProcessors` list.

Processors receive the sanitized project name and page title alongside an editable HtmlAgilityPack `HtmlDocument`. Use DOM mutations (remove nodes, rewrite elements, etc.) to produce clean Markdown input.

---

## Multi-Tab Output

Some fandom pages surface multiple content tabs (e.g., “Pathologic 2” vs “Pathologic”). Configure `TabOutputs` to export each tab to its own Markdown file in addition to the combined output:

```csharp
project.TabOutputs.Add(new MediaWikiTabOutputOptions
{
    TabName = "Pathologic 2",
    TabSlug = "pathologic-2",
    FileSuffix = "-pathologic-2",
    TitleFormat = "{title} (Pathologic 2)"
});

project.TabOutputs.Add(new MediaWikiTabOutputOptions
{
    TabName = "Pathologic",
    TabSlug = "pathologic",
    FileSuffix = "-pathologic",
    TitleFormat = "{title} (Pathologic)"
});
```

- `TabName` or `TabSlug` selects a sanitized tab (names match the tab label emitted by the processor).
- `FileSuffix` determines the filename (defaults to `-{tabSlug}` if omitted).
- `TitleFormat` (optional) customizes the top-level heading; `{title}` and `{tab}` placeholders resolve to the page title and tab label.

When `EmitBaseDocument` is `true`, the crawler writes the base `<slug>.md` with every tab collapsed into headings, and then emits additional files such as `<slug>-pathologic-2.md`. Set it to `false` to skip the combined output whenever matching tabs are found—redirect-only pages still fall back to the base document so no links are lost. Each variant replicates the metadata header (`Source`, `License`, `Retrieved`) and adds a `Variant:` line for clarity.

---

## Resume/Skip Semantics

- Base-document mode (EmitBaseDocument = true or no TabOutputs): if the base markdown already exists and `--force-refresh` is not set, the crawler skips that page and continues. This enables safe, resumable runs.
- Tab-only mode (EmitBaseDocument = false with TabOutputs): currently always fetches and regenerates tab variants. If you want resume behavior here, we can add a pre-check to skip when all expected variants already exist.

Examples:

```bash
# Resume a previous full crawl (skips existing base documents)
dotnet run --project src/LORE-LLM -- crawl-wiki --workspace <workspace> --project <name>

# Force re-fetch and rebuild all markdown
dotnet run --project src/LORE-LLM -- crawl-wiki --workspace <workspace> --project <name> --force-refresh
```

---

## Indexing Crawled Markdown

After crawling, generate the keyword index used by investigation and other stages:

```bash
dotnet run --project src/LORE-LLM -- index-wiki \
  --workspace <workspace> \
  --project <name> \
  --force-refresh
```

- Output: `<workspace>/<project>/knowledge/wiki_keyword_index.json`
- Each entry includes:
  - `title`: normalized file title
  - `keywords`: tokenized search keys
  - `isRedirect`: true for redirect-only markdown
  - `redirectTargets`: optional list of `{ title, slug }` targets parsed from the redirect list

Example (Backbone → The Town):
```json
{
  "title": "backbone",
  "keywords": ["backbone"],
  "isRedirect": true,
  "redirectTargets": [ { "title": "The Town", "slug": "the-town" } ]
}
```

---

## Tips for New Fandoms

- Capture a few representative pages, inspect the raw HTML, and note repeated UI fragments to target.
- Favor additive processors per project instead of modifying existing ones—this keeps the pipeline composable.
- Tests under `tests/LORE-LLM.Tests/Wiki` show how to stub HTTP responses and assert on the Markdown output.
