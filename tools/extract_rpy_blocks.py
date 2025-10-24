#!/usr/bin/env python3
"""
Utility to extract Ren'Py `translate english` blocks into a structured JSON file
for manual/LLM-assisted analysis.
"""

import argparse
import json
import os
import re
from datetime import datetime, timezone
from typing import Dict, List, Optional

BLOCK_PATTERN = re.compile(r"^\s*translate\s+english\s+([^\s:]+)\s*:\s*$")
DIALOGUE_SPEAKER_PATTERN = re.compile(r'^\s*([A-Za-z0-9_]+)\s+"')


def extract_blocks(path: str) -> List[Dict[str, object]]:
    blocks: List[Dict[str, object]] = []
    current: Optional[Dict[str, object]] = None
    with open(path, "r", encoding="utf-8-sig") as handle:
        for line_number, raw_line in enumerate(handle, start=1):
            line = raw_line.rstrip("\n")
            match = BLOCK_PATTERN.match(line)
            if match:
                if current is not None:
                    current["endLine"] = line_number - 1
                    current["lineCount"] = len(current["lines"])
                    current["speakers"] = sorted(current["speakers"])
                    blocks.append(current)
                label = match.group(1)
                current = {
                    "label": label,
                    "startLine": line_number,
                    "endLine": line_number,
                    "lineCount": 0,
                    "lines": [],
                    "speakers": set(),
                }
                continue

            if current is not None:
                current["lines"].append(
                    {
                        "lineNumber": line_number,
                        "text": line,
                    }
                )
                speaker_match = DIALOGUE_SPEAKER_PATTERN.match(line)
                if speaker_match:
                    current["speakers"].add(speaker_match.group(1))
    if current is not None:
        current["endLine"] = current["lines"][-1]["lineNumber"] if current["lines"] else current["startLine"]
        current["lineCount"] = len(current["lines"])
        current["speakers"] = sorted(current["speakers"])
        blocks.append(current)
    return blocks


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Extract Ren'Py translate blocks for offline analysis."
    )
    parser.add_argument("--input", required=True, help="Path to the .rpy source file.")
    parser.add_argument(
        "--output",
        required=True,
        help="Path to the JSON file to write (directories will be created as needed).",
    )
    args = parser.parse_args()

    input_path = os.path.abspath(args.input)
    if not os.path.exists(input_path):
        raise SystemExit(f"Input file not found: {input_path}")

    blocks = extract_blocks(input_path)
    output_dir = os.path.dirname(os.path.abspath(args.output))
    if output_dir:
        os.makedirs(output_dir, exist_ok=True)

    payload = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "sourcePath": input_path,
        "blockCount": len(blocks),
        "blocks": blocks,
    }

    with open(args.output, "w", encoding="utf-8") as handle:
        json.dump(payload, handle, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()

