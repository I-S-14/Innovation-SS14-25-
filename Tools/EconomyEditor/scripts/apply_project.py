#!/usr/bin/env python3
"""Apply a reviewed economy project without rewriting unrelated game components.

Requires PyYAML. Default mode prints a unified diff and changes no files.
"""
from __future__ import annotations

import argparse
import difflib
import json
import os
from pathlib import Path
import re
import tempfile

import yaml

ID = re.compile(r"^[A-Za-z][A-Za-z0-9_-]{0,150}$")
KEY = re.compile(r"^[a-z][a-z0-9-]{0,150}$")


def validate(project):
    if project.get("schemaVersion") != 1 or not isinstance(project.get("vendors"), list) or not 0 < len(project["vendors"]) <= 100:
        raise ValueError("Unsupported or empty project")
    seen, labels = set(), {}
    for vendor in project["vendors"]:
        vid = vendor["id"]
        if not ID.fullmatch(vid) or vid in seen:
            raise ValueError(f"Duplicate or invalid vendor: {vid}")
        seen.add(vid)
        if not 0 <= vendor["taxPercent"] <= 1:
            raise ValueError(f"Invalid tax: {vid}")
        for tab in vendor["tabs"]:
            key = tab["name"]
            if not KEY.fullmatch(key):
                raise ValueError(f"Tab name must be a localization key: {key}")
            pair = (tab["labelRu"], tab["labelEn"])
            if any(not s.strip() or len(s) > 150 or any(c in s for c in "\n\r{}") for s in pair):
                raise ValueError(f"Invalid tab label: {key}")
            if key in labels and labels[key] != pair:
                raise ValueError(f"Conflicting tab translations: {key}")
            labels[key] = pair
        for entries in [t["inventory"] for t in vendor["tabs"]] + [vendor["contrabandInventory"]]:
            ids = set()
            for entry in entries:
                item = entry["itemId"]
                if not ID.fullmatch(item) or item in ids:
                    raise ValueError(f"Duplicate or invalid item in {vid}: {item}")
                ids.add(item)
                for field, maximum in [("price", 100_000_000), ("stock", 1_000_000)]:
                    if type(entry[field]) is not int or not 0 <= entry[field] <= maximum:
                        raise ValueError(f"Invalid {field}: {vid}/{item}")
    return labels


def plan(project, repo):
    """Return path -> (old text or None, new text); do not mutate the repository."""
    labels = validate(project)
    requested = {v["id"]: v for v in project["vendors"]}
    sources, entities = {}, set()
    accounts, accesses = set(), set()
    for path in sorted((repo / "Resources/Prototypes").rglob("*.yml")):
        text = path.read_text(encoding="utf-8-sig")
        blocks = re.split(r"(?m)(?=^- type: )", text)
        for index, block in enumerate(blocks):
            match = re.search(r"(?m)^  id: ([^\s#]+)", block)
            if not match:
                continue
            pid = match[1]
            if block.startswith("- type: entity\n"):
                entities.add(pid)
                if pid in requested:
                    if pid in sources:
                        raise ValueError(f"Ambiguous entity ID: {pid}")
                    sources[pid] = (path, index)
            elif block.startswith("- type: accessLevel\n"):
                accesses.add(pid)
            elif block.startswith("- type: stationBankAccount\n"):
                accounts.add(pid)
    changes = {}
    for vid, vendor in requested.items():
        if vid not in sources:
            raise ValueError(f"Vendor not found: {vid}")
        if vendor["revenueAccount"] and vendor["revenueAccount"] not in accounts:
            raise ValueError(f"Unknown revenue account: {vendor['revenueAccount']}")
        for tab in vendor["tabs"]:
            if set(tab["access"]) - accesses:
                raise ValueError(f"Unknown access in {vid}: {set(tab['access']) - accesses}")
        for entries in [t["inventory"] for t in vendor["tabs"]] + [vendor["contrabandInventory"]]:
            for entry in entries:
                if entry["itemId"] not in entities:
                    raise ValueError(f"Unknown entity in {vid}: {entry['itemId']}")
        path, index = sources[vid]
        old = path.read_text(encoding="utf-8-sig")
        text = changes.get(path, (old, old))[1]
        blocks = re.split(r"(?m)(?=^- type: )", text)
        block = blocks[index]
        match = re.search(r"(?m)^  - type: IS14VendingMachine\s*(?:#.*)?$", block)
        if not match:
            raise ValueError(f"{vid} has no own IS14VendingMachine component; add an explicit component before applying")
        end = re.search(r"(?m)^  - type: ", block[match.end():])
        end_pos = match.end() + end.start() if end else len(block)
        fragment = block[match.start():end_pos]
        lines = fragment.splitlines(keepends=True)
        tail = []
        while lines and (not lines[-1].strip() or lines[-1].lstrip().startswith("#")):
            tail.insert(0, lines.pop())
        node = yaml.compose("".join(lines))
        if not isinstance(node, yaml.SequenceNode) or not isinstance(node.value[0], yaml.MappingNode):
            raise ValueError(f"Invalid component fragment: {vid}")
        desired = {
            "revenueAccount": vendor["revenueAccount"] or None,
            "taxPercent": vendor["taxPercent"],
            "tabs": [{"name": t["name"], "access": t["access"], "inventory": t["inventory"]} for t in vendor["tabs"]],
            "contrabandInventory": vendor["contrabandInventory"],
        }
        update = yaml.compose(yaml.safe_dump(desired, sort_keys=False, allow_unicode=False))
        component = node.value[0]
        replace = {k.value: (k, v) for k, v in update.value}
        component.value = [replace.pop(k.value, (k, v)) for k, v in component.value]
        component.value.extend(replace.values())
        serialized = yaml.serialize(node, allow_unicode=False)
        serialized = "\n".join("  " + line if line else line for line in serialized.rstrip().splitlines()) + "\n"
        blocks[index] = block[:match.start()] + serialized + "".join(tail) + block[end_pos:]
        changes[path] = (old, "".join(blocks))
    for locale, label_index in [("ru-RU", 0), ("en-US", 1)]:
        locale_root = repo / "Resources/Locale" / locale
        located = {}
        # Editor-owned tab keys live in the IS14 namespace. Avoid touching or
        # requiring read access to unrelated upstream localization packages.
        search_root = locale_root / "_IS14" if all(key.startswith("is14-vending-tab-") for key in labels) else locale_root
        for path in sorted(search_root.rglob("*.ftl")):
            text = path.read_text(encoding="utf-8-sig")
            for key in labels:
                if re.search(r"(?m)^" + re.escape(key) + r"\s*=", text):
                    if key in located:
                        raise ValueError(f"Duplicate localization: {locale}/{key}")
                    located[key] = path
        new_path = locale_root / "_IS14/economy-editor.ftl"
        for key, pair in sorted(labels.items()):
            path = located.get(key, new_path)
            old = path.read_text(encoding="utf-8-sig") if path.exists() else None
            text = changes.get(path, (old, old or ""))[1]
            line = f"{key} = {pair[label_index]}"
            pattern = r"(?m)^" + re.escape(key) + r"\s*=[^\n]*(?:\n[ \t]+[^\n]*)*"
            if key in located:
                text = re.sub(pattern, lambda m: line, text)
            else:
                text = text.rstrip() + ("\n\n" if text.strip() else "") + line + "\n"
            changes[path] = (old, text)
    return {p: pair for p, pair in changes.items() if pair[0] != pair[1]}


def apply(changes):
    # Check all source versions before writing anything.
    for path, (old, _) in changes.items():
        current = path.read_text(encoding="utf-8-sig") if path.exists() else None
        if current != old:
            raise RuntimeError(f"File changed during planning: {path}")
    done = []
    try:
        for path, (old, text) in changes.items():
            path.parent.mkdir(parents=True, exist_ok=True)
            with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False, suffix=".tmp") as handle:
                handle.write(text)
                temporary = Path(handle.name)
            try:
                os.replace(temporary, path)
            finally:
                temporary.unlink(missing_ok=True)
            done.append((path, old))
    except Exception:
        for path, old in reversed(done):
            if old is None:
                path.unlink(missing_ok=True)
            else:
                path.write_text(old, encoding="utf-8")
        raise


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("project", type=Path)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[3])
    parser.add_argument("--apply", action="store_true", help="Write the reviewed changes; otherwise print a diff")
    args = parser.parse_args()
    project = json.loads(args.project.read_text(encoding="utf-8-sig"))
    changes = plan(project, args.repo.resolve())
    for path, (old, new) in changes.items():
        name = str(path.relative_to(args.repo.resolve()))
        print("".join(difflib.unified_diff((old or "").splitlines(True), new.splitlines(True), fromfile=name, tofile=name)), end="")
    if args.apply:
        apply(changes)
    print(f"\n{'Updated' if args.apply else 'Would update'} {len(changes)} files.")


if __name__ == "__main__":
    main()
