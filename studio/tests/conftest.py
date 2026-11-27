import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parent.parent / "scripts"
sys.path.insert(0, str(SCRIPTS))


def run_script(name, *args, cwd=None, env_root=None):
    env = dict(os.environ)
    if env_root is not None:
        env["STUDIO_ROOT"] = str(env_root)
    return subprocess.run(
        [sys.executable, str(SCRIPTS / name), *map(str, args)],
        capture_output=True, text=True, cwd=cwd, env=env,
    )


def git(root, *args):
    return subprocess.run(["git", *args], cwd=root, check=True,
                          capture_output=True, text=True).stdout.strip()


@pytest.fixture
def studio_repo(tmp_path):
    root = tmp_path / "repo"
    (root / "Games" / "Demo" / "Tasks").mkdir(parents=True)
    git(root, "init", "-q")
    git(root, "config", "user.email", "t@example.com")
    git(root, "config", "user.name", "t")
    (root / "README.md").write_text("x\n")
    git(root, "add", "-A")
    git(root, "commit", "-qm", "init")
    return root


@pytest.fixture
def task_cli(studio_repo):
    def call(*args):
        return run_script("task.py", *args, cwd=studio_repo, env_root=studio_repo)
    return call


def read_task(root, game, task_id):
    return json.loads((root / "Games" / game / "Tasks" / f"{task_id}.json").read_text())
