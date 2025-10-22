#!/usr/bin/env python3
import argparse
import ast
import hashlib
import json
import os
import re
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Dict, List, Optional, Tuple


LOCATION_COMMENT_RE = re.compile(r"^(?:game|renpy)/.+?:\d+$")
TRANSLATE_LINE_RE = re.compile(r"^translate\s+(?P<language>\w+)\s+(?P<label>[^\s:]+)")


def sanitize_project(name: str) -> str:
    slug = name.strip().lower()
    slug = re.sub(r"[^a-z0-9\s\-_:]", "", slug)
    slug = re.sub(r"[\s_]+", "-", slug)
    return slug


def compute_input_hash(file_paths: List[str]) -> str:
    sha = hashlib.sha256()
    for path in sorted(file_paths):
        with open(path, "rb") as f:
            while True:
                chunk = f.read(8192)
                if not chunk:
                    break
                sha.update(chunk)
    return sha.hexdigest()


def find_rpy_files(root: str) -> List[str]:
    files = []
    for dirpath, _, filenames in os.walk(root):
        for name in filenames:
            if not name.lower().endswith(".rpy"):
                continue
            files.append(os.path.join(dirpath, name))
    return sorted(files)


def relpath_norm(path: str, root: str) -> str:
    rel = os.path.relpath(path, root)
    return rel.replace("\\", "/")


def normalize_for_id(rel: str) -> str:
    return rel.replace("/", ":")


def file_base_without_multi_ext(path: str) -> str:
    base = os.path.basename(path)
    while True:
        root, ext = os.path.splitext(base)
        if not ext:
            return base
        base = root


def parse_string_literal(token: str, *, path: str, line: int) -> str:
    stripped = token.strip()
    try:
        return ast.literal_eval(stripped)
    except Exception as exc:  # pragma: no cover - fail fast to reveal malformed inputs
        raise ValueError(f"Unable to parse string literal at {path}:{line}: {stripped}") from exc


def split_literal_and_suffix(text: str) -> Tuple[str, Optional[str]]:
    candidate = text.lstrip()
    if not candidate or candidate[0] not in ("'", '"'):
        raise ValueError("Text does not start with a string literal.")
    quote = candidate[0]
    escaped = False
    for idx in range(1, len(candidate)):
        char = candidate[idx]
        if char == quote and not escaped:
            literal = candidate[: idx + 1]
            remainder = candidate[idx + 1 :].strip()
            return literal, remainder if remainder else None
        if char == "\\" and not escaped:
            escaped = True
        else:
            escaped = False
    raise ValueError("Unterminated string literal.")


@dataclass
class Segment:
    id: str
    text: str
    is_empty: bool
    line_number: int
    metadata: Dict[str, object]


@dataclass
class UnhandledLine:
    source_rel_path: str
    block_label: Optional[str]
    line_number: int
    content: str


def segment_to_dict(segment: Segment) -> Dict[str, object]:
    return {
        "id": segment.id,
        "text": segment.text,
        "lineNumber": segment.line_number,
        "metadata": segment.metadata,
    }


def extract_segments_from_rpy(path: str, input_root: str, *, strict: bool) -> Tuple[List[Segment], Dict[str, object], List[UnhandledLine]]:
    rel = relpath_norm(path, input_root)
    normalized_rel = normalize_for_id(rel)
    segments: List[Segment] = []
    local_index = 0

    block_counters: Dict[str, int] = defaultdict(int)
    pending_reference: Optional[str] = None

    with open(path, "r", encoding="utf-8-sig") as f:
        lines = f.readlines()

    inside_block = False
    block_indent = 0
    block_label: Optional[str] = None
    block_instance_id: Optional[str] = None
    current_reference: Optional[str] = None
    comment_buffer: List[str] = []
    pending_old: Optional[str] = None

    unhandled_lines: List[UnhandledLine] = []

    def register_unhandled(line_no: int, text: str, block: Optional[str]) -> None:
        unhandled_lines.append(
            UnhandledLine(
                source_rel_path=rel,
                block_label=block,
                line_number=line_no,
                content=text,
            )
        )

    for line_no, raw_line in enumerate(lines, start=1):
        stripped = raw_line.strip()
        indent = len(raw_line) - len(raw_line.lstrip(" \t"))

        if not inside_block and stripped.startswith("#"):
            comment_text = stripped[1:].strip()
            if LOCATION_COMMENT_RE.match(comment_text):
                pending_reference = comment_text
            continue

        if inside_block:
            if indent <= block_indent and stripped and not stripped.startswith("#"):
                inside_block = False
                handled_transition = stripped.startswith("translate ")
                block_label = None
                block_instance_id = None
                comment_buffer = []
                pending_old = None
                current_reference = None
                # Re-evaluate this line in non-block context
                if handled_transition:
                    # fall through to outer logic
                    inside_block = False
                else:
                    if not stripped.startswith("#"):
                        continue

        if not inside_block:
            match = TRANSLATE_LINE_RE.match(stripped)
            if match and match.group("language").lower() == "english" and stripped.endswith(":"):
                block_label = match.group("label")
                block_counters[block_label] += 1
                block_instance_id = f"{block_label}:{block_counters[block_label]}"
                inside_block = True
                block_indent = indent
                current_reference = pending_reference
                pending_reference = None
                comment_buffer = []
                pending_old = None
            continue

        # Inside translation block
        if stripped == "":
            # Blank lines are benign
            continue

        if stripped.startswith("#"):
            comment_text = stripped[1:].strip()
            if LOCATION_COMMENT_RE.match(comment_text):
                if indent <= block_indent:
                    inside_block = False
                    block_label = None
                    block_instance_id = None
                    comment_buffer = []
                    pending_old = None
                    current_reference = None
                    pending_reference = comment_text
                    continue
                current_reference = comment_text
            else:
                comment_buffer.append(comment_text)
            continue

        if stripped.startswith("old "):
            literal = stripped[4:]
            pending_old = parse_string_literal(literal, path=path, line=line_no)
            continue

        if stripped.startswith("new "):
            literal = stripped[4:]
            new_text = parse_string_literal(literal, path=path, line=line_no)
            text_value = new_text if new_text != "" else (pending_old or "")
            local_index += 1
            seg_id = f"{normalized_rel}:{block_instance_id}:{line_no}"
            metadata: Dict[str, object] = {
                "sourceRelPath": rel,
                "translationBlock": block_label,
                "blockInstance": block_instance_id,
                "sourceReference": current_reference,
                "entryType": "old_new",
                "hasNewTranslation": bool(new_text),
            }
            if pending_old is not None:
                metadata["oldText"] = pending_old
            if comment_buffer:
                metadata["comments"] = list(comment_buffer)
            segment = Segment(
                id=seg_id,
                text=text_value,
                is_empty=(text_value.strip() == ""),
                line_number=local_index,
                metadata=metadata,
            )
            segments.append(segment)
            comment_buffer = []
            pending_old = None
            continue

        if stripped in {"pass", "return", "nvl clear"}:
            # Drop comments that mirror the command but preserve others (e.g., original text lines).
            lower_cmd = stripped.lower()
            comment_buffer = [c for c in comment_buffer if c.lower() != lower_cmd]
            pending_old = None
            continue

        if stripped.startswith(("while ", "if ", "elif ", "else:", "python:", "menu:", "label ", "scene ", "show ", "hide ", "play ", "stop ")):
            comment_buffer = []
            pending_old = None
            continue

        if stripped.startswith(('"', "'")):
            literal, suffix = split_literal_and_suffix(stripped)
            text_value = parse_string_literal(literal, path=path, line=line_no)
            local_index += 1
            seg_id = f"{normalized_rel}:{block_instance_id}:{line_no}"
            metadata = {
                "sourceRelPath": rel,
                "translationBlock": block_label,
                "blockInstance": block_instance_id,
                "sourceReference": current_reference,
                "entryType": "string_literal",
            }
            if pending_old is not None:
                metadata["oldText"] = pending_old
            if comment_buffer:
                metadata["comments"] = list(comment_buffer)
            if suffix:
                metadata["trailingTokens"] = suffix
            segment = Segment(
                id=seg_id,
                text=text_value,
                is_empty=(text_value.strip() == ""),
                line_number=local_index,
                metadata=metadata,
            )
            segments.append(segment)
            comment_buffer = []
            pending_old = None
            continue

        parts = stripped.split(None, 1)
        if len(parts) == 2 and parts[1].startswith(('"', "'")):
            speaker = parts[0]
            literal, suffix = split_literal_and_suffix(parts[1])
            text_value = parse_string_literal(literal, path=path, line=line_no)
            local_index += 1
            seg_id = f"{normalized_rel}:{block_instance_id}:{line_no}"
            metadata = {
                "sourceRelPath": rel,
                "translationBlock": block_label,
                "blockInstance": block_instance_id,
                "sourceReference": current_reference,
                "entryType": "character_line",
                "speaker": speaker,
            }
            if pending_old is not None:
                metadata["oldText"] = pending_old
            if comment_buffer:
                metadata["comments"] = list(comment_buffer)
            if suffix:
                metadata["trailingTokens"] = suffix
            segment = Segment(
                id=seg_id,
                text=text_value,
                is_empty=(text_value.strip() == ""),
                line_number=local_index,
                metadata=metadata,
            )
            segments.append(segment)
            comment_buffer = []
            pending_old = None
            continue

        # If we reach here, we encountered content we do not extract.
        register_unhandled(line_no, stripped, block_label)
        comment_buffer = []
        pending_old = None

    file_index = {
        "sourceRelPath": rel,
        "blockCount": dict(block_counters),
        "segmentCount": local_index,
    }

    if strict and unhandled_lines:
        example = unhandled_lines[0]
        raise ValueError(
            f"Unhandled translation content at {example.source_rel_path}:{example.line_number} "
            f"in block '{example.block_label}': {example.content}"
        )

    return segments, file_index, unhandled_lines


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate canonical source_text_raw.json for Heads Will Roll.")
    parser.add_argument("--input", required=True, help="Path to raw-input/head-will-roll directory containing .rpy files.")
    parser.add_argument("--output", required=True, help="Workspace directory for emitted artifacts.")
    parser.add_argument("--project", required=True, help="Project display name.")
    parser.add_argument(
        "--strict-unhandled",
        action="store_true",
        help="Fail if any unrecognised lines are encountered inside translation blocks.",
    )
    args = parser.parse_args()

    input_root = os.path.abspath(args.input)
    output_root = os.path.abspath(args.output)
    project_display = args.project
    project_sanitized = sanitize_project(project_display)

    rpy_files = find_rpy_files(input_root)
    if not rpy_files:
        raise SystemExit(f"No .rpy files found under {input_root}")

    all_segments: List[Segment] = []
    files_index: List[Dict[str, object]] = []
    all_unhandled: List[UnhandledLine] = []

    per_file_records: List[Dict[str, object]] = []

    for file_path in rpy_files:
        file_segments, file_index, unhandled = extract_segments_from_rpy(file_path, input_root, strict=args.strict_unhandled)
        all_segments.extend(file_segments)
        files_index.append(file_index)
        all_unhandled.extend(unhandled)
        rel_path = file_index["sourceRelPath"]
        file_hash = compute_input_hash([file_path])
        per_file_records.append(
            {
                "relPath": rel_path,
                "filePath": file_path,
                "fileHash": file_hash,
                "segments": list(file_segments),
                "fileIndex": file_index,
            }
        )

    if not all_segments:
        raise SystemExit("No segments extracted from the provided .rpy files.")

    input_hash = compute_input_hash(rpy_files)
    generated_at = datetime.now(timezone.utc).isoformat()
    document = {
        "sourcePath": input_root,
        "generatedAt": generated_at,
        "project": project_sanitized,
        "projectDisplayName": project_display,
        "inputHash": input_hash,
        "segments": [segment_to_dict(segment) for segment in all_segments],
    }

    project_dir = os.path.join(output_root, project_sanitized)
    os.makedirs(project_dir, exist_ok=True)

    out_path = os.path.join(project_dir, "source_text_raw.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(document, f, ensure_ascii=False, indent=2)

    used_names: set[str] = set()
    for record in per_file_records:
        rel_path = record["relPath"]
        file_path = record["filePath"]
        file_hash = record["fileHash"]
        file_segments = record["segments"]
        file_index = record["fileIndex"]

        base_name = file_base_without_multi_ext(rel_path)
        candidate = f"source_text_raw_{base_name}.json"
        if candidate in used_names:
            sanitized = re.sub(r"[^A-Za-z0-9]+", "_", rel_path)
            candidate = f"source_text_raw_{sanitized}.json"
        used_names.add(candidate)

        file_document = {
            "sourcePath": os.path.normpath(file_path),
            "sourceRelPath": rel_path,
            "generatedAt": generated_at,
            "project": project_sanitized,
            "projectDisplayName": project_display,
            "inputHash": file_hash,
            "blockCount": file_index.get("blockCount", {}),
            "segmentCount": file_index.get("segmentCount", 0),
            "segments": [segment_to_dict(segment) for segment in file_segments],
        }

        per_file_path = os.path.join(project_dir, candidate)
        with open(per_file_path, "w", encoding="utf-8") as pf:
            json.dump(file_document, pf, ensure_ascii=False, indent=2)

    index_path = os.path.join(project_dir, "source_files_index.json")
    with open(index_path, "w", encoding="utf-8") as f:
        json.dump({"files": files_index}, f, ensure_ascii=False, indent=2)

    suggested_path = os.path.join(project_dir, "metadata.enrichment.suggested.json")
    suggested = {
        "pathPatternRules": [
            {
                "pattern": "script*.rpy",
                "metadata": {"category": "dialogue"},
            },
            {
                "pattern": "common.rpy",
                "metadata": {"category": "engine-ui"},
            },
        ],
        "fileRegexRules": [],
        "prefixRules": [],
    }
    with open(suggested_path, "w", encoding="utf-8") as f:
        json.dump(suggested, f, ensure_ascii=False, indent=2)

    if all_unhandled:
        print("Warning: Unhandled translation lines detected:", len(all_unhandled))
        for entry in all_unhandled[:10]:
            print(
                f"  {entry.source_rel_path}:{entry.line_number} "
                f"(block={entry.block_label}) -> {entry.content}"
            )
        if len(all_unhandled) > 10:
            print(f"  ... and {len(all_unhandled) - 10} more")

    print(out_path)


if __name__ == "__main__":
    main()
