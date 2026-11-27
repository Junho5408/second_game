"""Helpers shared by gen_meta and check_meta."""
from pathlib import Path


def is_ignored(p: Path) -> bool:
    return p.name.startswith(".") or p.name.endswith("~")


def assets_items(assets: Path):
    """Yield every non-meta file/folder under assets (not assets itself), skipping ignored ones."""
    stack = [assets]
    while stack:
        d = stack.pop()
        for child in sorted(d.iterdir()):
            if is_ignored(child) or child.name.endswith(".meta"):
                continue
            yield child
            if child.is_dir():
                stack.append(child)
