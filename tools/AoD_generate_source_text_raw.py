#!/usr/bin/env python3
import argparse
import hashlib
import json
import os
import re
from datetime import datetime, timezone


def sanitize_project(name: str) -> str:
    slug = name.strip().lower()
    slug = re.sub(r"[^a-z0-9\s\-_:]", "", slug)
    slug = re.sub(r"[\s_]+", "-", slug)
    return slug


def compute_input_hash(file_paths):
    sha = hashlib.sha256()
    for path in sorted(file_paths):
        with open(path, "rb") as f:
            while True:
                chunk = f.read(8192)
                if not chunk:
                    break
                sha.update(chunk)
    return sha.hexdigest()


def find_source_files(root: str):
    candidates = []
    for dirpath, _, filenames in os.walk(root):
        for fn in filenames:
            if fn.lower().endswith(".json"):
                candidates.append(os.path.join(dirpath, fn))
    return sorted(candidates)


def relpath_norm(path: str, root: str) -> str:
    rel = os.path.relpath(path, root)
    return rel.replace("\\", "/")


def infer_category(rel_path: str) -> str:
    path_lower = rel_path.lower()
    # Common AoD locations: scripts/data/text/dialogues|journal|slides|text
    if "/dialogues" in path_lower:
        return "dialogue"
    if "/slides" in path_lower:
        return "slide"
    if "/journal" in path_lower:
        return "journal"
    if "/text" in path_lower:
        return "text"
    return "misc"


def file_base_without_multi_ext(path: str) -> str:
    base = os.path.basename(path)
    # Strip multiple extensions like .xml.json or .english.cs.json
    while True:
        root, ext = os.path.splitext(base)
        if not ext:
            return base
        base = root


def load_segments(file_paths, input_root: str):
    segments = []
    files_index = []
    for path in file_paths:
        with open(path, "r", encoding="utf-8") as f:
            try:
                data = json.load(f)
            except Exception:
                # Non-JSON or malformed; skip
                continue
        if not isinstance(data, list):
            continue

        rel = relpath_norm(path, input_root)
        category = infer_category(rel)
        file_base = file_base_without_multi_ext(path)
        local_index = 0

        for entry in data:
            if not isinstance(entry, dict):
                continue
            key = entry.get("key") or entry.get("id")
            text = entry.get("original") or entry.get("text") or ""
            if not key:
                continue
            local_index += 1
            seg_id = f"{category}:{file_base}:{key}"
            metadata = {
                "category": category,
                "sourceKey": str(key),
                "sourceRelPath": rel,
                "fileBase": file_base
            }
            segments.append({
                "id": seg_id,
                "text": text,
                "isEmpty": (len(text.strip()) == 0),
                "lineNumber": local_index,
                "metadata": metadata
            })

        files_index.append({
            "sourceRelPath": rel,
            "category": category,
            "fileBase": file_base,
            "count": local_index
        })

    return segments, files_index


def main():
    parser = argparse.ArgumentParser(description="Generate canonical source_text_raw.json for Age of Decadence.")
    parser.add_argument("--input", required=True, help="Path to raw-input/age-of-decadence")
    parser.add_argument("--output", required=True, help="Workspace directory")
    parser.add_argument("--project", required=True, help="Project display name")
    args = parser.parse_args()

    input_root = os.path.abspath(args.input)
    output_root = os.path.abspath(args.output)
    project_display = args.project
    project_sanitized = sanitize_project(project_display)

    files = find_source_files(input_root)
    if not files:
        raise SystemExit(f"No source files found under {input_root}")

    segments, files_index = load_segments(files, input_root)
    if not segments:
        raise SystemExit("No segments extracted.")

    input_hash = compute_input_hash(files)
    document = {
        "sourcePath": input_root,
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "project": project_sanitized,
        "projectDisplayName": project_display,
        "inputHash": input_hash,
        "segments": segments
    }

    project_dir = os.path.join(output_root, project_sanitized)
    os.makedirs(project_dir, exist_ok=True)
    out_path = os.path.join(project_dir, "source_text_raw.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(document, f, ensure_ascii=False, indent=2)

    # Write analysis: files index and suggested enrichment rules
    index_path = os.path.join(project_dir, "source_files_index.json")
    with open(index_path, "w", encoding="utf-8") as f:
        json.dump({"files": files_index}, f, ensure_ascii=False, indent=2)

    # Build suggested enrichment config
    categories = sorted({fi["category"] for fi in files_index})
    id_prefix_rules = {f"{cat}:": {"category": cat} for cat in categories if cat}
    path_rules = []
    for cat in categories:
        if not cat:
            continue
        if cat == "dialogue":
            path_rules.append({"contains": "dialogue", "metadata": {"category": "dialogue"}})
        elif cat == "slide":
            path_rules.append({"contains": "slides", "metadata": {"category": "slide"}})
        elif cat == "journal":
            path_rules.append({"contains": "journal", "metadata": {"category": "journal"}})
        elif cat == "text":
            path_rules.append({"contains": "text", "metadata": {"category": "text"}})
        else:
            path_rules.append({"contains": cat, "metadata": {"category": cat}})

    suggested = {
        "idPrefixRules": id_prefix_rules,
        "pathPatternRules": path_rules
    }
    suggested_path = os.path.join(project_dir, "metadata.enrichment.suggested.json")
    with open(suggested_path, "w", encoding="utf-8") as f:
        json.dump(suggested, f, ensure_ascii=False, indent=2)

    print(out_path)


if __name__ == "__main__":
    main()


