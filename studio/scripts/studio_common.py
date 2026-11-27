"""Shared constants and helpers for studio scripts."""
import os
import re
from pathlib import Path

STATUSES = ["BACKLOG", "ASSIGNED", "IN_PROGRESS", "REVIEW", "QA", "AWAITING_UNITY",
            "COMPLETED", "FIX_REQUIRED", "BLOCKED", "CANCELLED"]
AGENTS = ["orchestrator", "game-designer", "architect", "developer",
          "content-designer", "code-reviewer", "qa-engineer"]
VERIFICATIONS = ["core-tests", "review", "unity-compile", "unity-editmode",
                 "unity-playmode", "unity-build"]
PRIORITIES = ["critical", "high", "medium", "low"]
GAME_NAME_RE = re.compile(r"^[A-Z][A-Za-z0-9]{1,31}$")
MAX_RETRIES = 2


def repo_root() -> Path:
    env = os.environ.get("STUDIO_ROOT")
    if env:
        return Path(env).resolve()
    return Path(__file__).resolve().parent.parent.parent


def game_dir(name: str) -> Path:
    return repo_root() / "Games" / name
