#!/usr/bin/env python3
"""Create a new game from studio/templates/game.

Usage: new_game.py <Name> [--mode 2d|3d]
"""
import argparse
import shutil
import sys
from pathlib import Path

from gen_meta import ensure_meta
from studio_common import GAME_NAME_RE, game_dir

TEMPLATE = Path(__file__).resolve().parent.parent / "templates" / "game"

MODE_PACKAGES = {
    "2d": '    "com.unity.2d.sprite": "1.0.0",\n    "com.unity.modules.physics2d": "1.0.0",',
    "3d": '    "com.unity.modules.physics": "1.0.0",',
}


def substitutions(name, mode):
    return {
        "{{GAME}}": name,
        "{{GAME_UPPER}}": name.upper(),
        "{{MODE}}": mode,
        "{{ORTHO}}": "true" if mode == "2d" else "false",
        "{{MODE_PACKAGES}}": MODE_PACKAGES[mode],
    }


def apply(text, subs):
    for k, v in subs.items():
        text = text.replace(k, v)
    return text


def main():
    p = argparse.ArgumentParser()
    p.add_argument("name")
    p.add_argument("--mode", choices=["2d", "3d"], default="2d")
    a = p.parse_args()
    if not GAME_NAME_RE.match(a.name):
        print(f"invalid game name {a.name!r}: must match {GAME_NAME_RE.pattern}", file=sys.stderr)
        sys.exit(1)
    dest = game_dir(a.name)
    if dest.exists():
        print(f"{dest} already exists", file=sys.stderr)
        sys.exit(1)
    subs = substitutions(a.name, a.mode)
    try:
        for src in sorted(TEMPLATE.rglob("*")):
            rel = Path(apply(src.relative_to(TEMPLATE).as_posix(), subs))
            out = dest / rel
            if src.is_dir():
                out.mkdir(parents=True, exist_ok=True)
                continue
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_text(apply(src.read_text(encoding="utf-8"), subs),
                           encoding="utf-8", newline="\n")
        ensure_meta(dest / "Unity" / "Assets")
    except Exception:
        shutil.rmtree(dest, ignore_errors=True)
        raise
    print(f"created Games/{a.name}")


if __name__ == "__main__":
    main()
