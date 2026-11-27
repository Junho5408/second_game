#!/usr/bin/env python3
"""Create missing Unity .meta files. Usage: gen_meta.py <path>..."""
import sys
import uuid
from pathlib import Path

from meta_common import assets_items, is_ignored

IMPORTERS = {
    ".cs": "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n"
           "  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n"
           "  assetBundleVariant: \n",
    ".asmdef": "AssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n"
               "  assetBundleName: \n  assetBundleVariant: \n",
}
TEXT_EXT = {".json", ".txt", ".md"}
TEXT = "TextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
DEFAULT = "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def meta_text(p: Path) -> str:
    head = f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n"
    if p.is_dir():
        return head + "folderAsset: yes\n" + DEFAULT
    body = IMPORTERS.get(p.suffix) or (TEXT if p.suffix in TEXT_EXT else DEFAULT)
    return head + body


def _under_assets(p: Path) -> bool:
    return "Assets" in p.parts[:-1]


def ensure_meta(path: Path) -> list:
    path = Path(path)
    items = []
    if path.is_dir():
        if _under_assets(path) and not is_ignored(path):
            items.append(path)
        items.extend(assets_items(path))
    elif _under_assets(path):
        items.append(path)
    created = []
    for item in items:
        if not _under_assets(item) or item.name.endswith(".meta"):
            continue
        meta = item.with_name(item.name + ".meta")
        if not meta.exists():
            meta.write_text(meta_text(item), encoding="utf-8", newline="\n")
            created.append(meta)
    return created


def main():
    if len(sys.argv) < 2:
        print(__doc__, file=sys.stderr); sys.exit(2)
    for arg in sys.argv[1:]:
        for m in ensure_meta(Path(arg)):
            print(m)


if __name__ == "__main__":
    main()
