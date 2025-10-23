# Heads Will Roll – Metadata Design Reference

This note captures how the `tools/HeadsWillRoll_generate_source_text_raw.py` adapter interprets the Ren’Py dumps for *Heads Will Roll Reforged* and what each emitted field means. Keep it alongside the raw assets so future extraction, clustering, and translation passes stay reproducible.

---

## Extraction Workflow

```pwsh
python tools/HeadsWillRoll_generate_source_text_raw.py `
    --input raw-input/head-will-roll `
    --output workspace `
    --project "Heads Will Roll Reforged"
```

Outputs land in `workspace/heads-will-roll-reforged/`:

- `source_text_raw.json` – aggregate canonical artifact.  
- `source_text_raw_<file>.json` – per-`.rpy` slices (e.g., `source_text_raw_script.json`).  
- `source_files_index.json` – block/segment counts per file.  
- `metadata.enrichment.suggested.json` – starter enrichment rules inferred from file names.

The adapter walks every `.rpy`, tracks handled lines, and supports `--strict-unhandled` to fail fast if new translation constructs appear. Default runs still surface warnings with file/line context.

---

## Segment Identity

Each segment ID follows:

```
<sourceRelPath>:<translationBlock>:<blockInstanceIndex>:<sourceLine>
```

Example: `script.rpy:player_decides_to_go_to_rennes_multipath_cdc966dd:1:98775`

- `sourceRelPath` is the `.rpy` relative path (`script.rpy`).  
- `translationBlock` is the Ren’Py `translate english ...` label.  
- `blockInstanceIndex` disambiguates repeated labels by appending an ordinal (`:1`, `:2`, ...).  
- `sourceLine` is the 1-based line number in our raw dump where the string literal or `new` entry appeared.

This gives deterministic anchors for enrichment, clustering, testing, and downstream translation hand-offs.

---

## Metadata Fields

Every segment carries a deterministic `metadata` bag. Key meanings:

| Key | Description |
| --- | --- |
| `sourceRelPath` | Relative path of the input `.rpy`. |
| `translationBlock` | Ren’Py translation block label. |
| `blockInstance` | `<translationBlock>:<ordinal>` – unique per block instance. |
| `sourceReference` | The Ren’Py locator comment (e.g., `game/script.rpy:140862`) when present. |
| `entryType` | Parser path: `old_new`, `string_literal`, or `character_line`. |
| `speaker` | Present for `character_line`; the prefix token (`h`, `e`, etc.). |
| `hasNewTranslation` | For `old_new` entries, whether the `new` string was non-empty. |
| `oldText` | Original-language string from the `old` clause (helpful when `new` differs or is empty). |
| `comments` | Translator comments, original-language lines, or other annotations that preceded the string. |
| `trailingTokens` | Ren’Py tokens appended after the string literal (e.g., `nointeract`). |

We intentionally dropped the `isEmpty` flag to keep payloads leaner. If a localized string must fall back to `old`, `hasNewTranslation` is `false` and `text == oldText`.

---

## Parsing Rules & Edge Cases

- **Control-only blocks** (`nvl clear`, `pass`, etc.) are skipped unless they contain strings.  
- **Locator comments between blocks** (`# game/script.rpy:NNN`) close the current block and seed the next, ensuring `sourceReference` attaches to the correct segment.  
- **Speaker-prefixed lines** (e.g., `h "..."`) are emitted as `entryType: "character_line"` with `speaker`.  
- **Trailing directives** like `"..." nointeract` are captured in `trailingTokens`.  
- Unknown statements raise warnings or fail in strict mode—check output after every extraction.

---

## Deterministic Clustering Guidance

The metadata provides all signals needed to pre-cluster before hitting the LLM:

1. **Primary buckets**: group by (`sourceRelPath`, `translationBlock`, `blockInstance`).  
2. **Split by entry type**: separate `old_new` UI tables from narrative `string_literal` and `character_line`.  
3. **Speaker sub-groups**: for character lines, keep one cluster per dominant speaker.  
4. **Ordering**: respect `lineNumber` (per file) for natural dialog flow.  
5. **Merge heuristics**: optionally merge adjacent buckets if they share the same `sourceReference` range or recurring prefixes (e.g., item stat tables).

Planned output: `clusters_precomputed.json`, feeding the LLM cluster command as seeds or final clusters.

---

## NPC Persona & Knowledge Hints

After deterministic clustering, we intend to:

- Detect clusters dominated by a single `speaker`.  
- Summarize their lines into tone/personality guidance via an LLM (with human review).  
- Emit profiles (e.g., `knowledge/npc_profiles.json`) containing personality notes, sample quotes, preferred terminology, and translation hints.  
- Reference those profiles in translation prompts and glossary validation.

Keeping original-language comments (`metadata.comments`) is critical—they give the LLM and reviewers the source phrasing needed to infer character traits reliably.

---

## Regeneration Checklist

1. Update raw Ren’Py dumps if the upstream game patches.  
2. Run the adapter command (above).  
3. Inspect warnings (or use `--strict-unhandled`).  
4. Validate workspace artifacts (`dotnet run -- validate-source`).  
5. Review `source_files_index.json` for unexpected block counts.  
6. Commit `workspace/heads-will-roll-reforged/` artifacts so changes stay traceable.

Keep this document current as new heuristics, metadata fields, or knowledge pipelines are introduced.***
