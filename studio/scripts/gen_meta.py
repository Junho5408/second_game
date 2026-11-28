#!/usr/bin/env python3
"""Create missing Unity .meta files. Usage: gen_meta.py <path>...

Importer choice by extension (case-insensitive):
- .cs / .asmdef / text files: the importer section Unity itself writes for them.
- assets with a native Unity importer (models, textures, audio, fonts — NATIVE_EXT): a *minimal* meta
  (fileFormatVersion + guid only). Unity picks the right importer (ModelImporter, TextureImporter, ...) from the
  extension and fills in its default settings on first import, keeping the GUID. Import settings are then fixed
  in code by the game's AssetPostprocessor, not in the meta. (A DefaultImporter section here would stop the
  file from importing as a model/texture.)
- everything else and folders: DefaultImporter.
Existing .meta files are never touched.
"""
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
NATIVE_EXT = {
    # models (ModelImporter)
    ".fbx", ".obj", ".dae", ".3ds", ".dxf",
    # textures (TextureImporter)
    ".png", ".jpg", ".jpeg", ".tga", ".psd", ".tif", ".tiff", ".bmp", ".gif", ".exr", ".hdr",
    # audio (AudioImporter)
    ".wav", ".ogg", ".mp3", ".aif", ".aiff",
    # fonts (TrueTypeFontImporter)
    ".ttf", ".otf",
}
DEFAULT = "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"


def meta_text(p: Path) -> str:
    head = f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n"
    if p.is_dir():
        return head + "folderAsset: yes\n" + DEFAULT
    ext = p.suffix.lower()
    if ext in NATIVE_EXT:
        return head
    body = IMPORTERS.get(ext) or (TEXT if ext in TEXT_EXT else DEFAULT)
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
