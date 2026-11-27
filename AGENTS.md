# AGENTS.md — Unity Game Studio Agent Guidelines

Every agent (and the Orchestrator) reads this file before starting any task.

## 1. Project Overview

This repository contains Unity indie games developed by an autonomous Claude Code agent team.
Each game lives in `Games/<GameName>/`. Team rules, tools and templates are shared at the repo root.
All agents follow the game's design specs (`Docs/design/`) and technical architecture (`Docs/tech/`).

Team: Orchestrator (main session, see `CLAUDE.md`) + `game-designer`, `architect`, `developer`,
`content-designer`, `code-reviewer`, `qa-engineer` (`.claude/agents/`).

## 2. Mandatory Workflow

Every task follows: **Scan → Plan → Implement → Verify → Review → Fix → Complete.**
Never skip verification.

## 3. Documentation

Before starting any task:
- Read this file.
- Read your task file `Games/<G>/Tasks/<ID>.json` (scope, acceptance criteria, verification).
- Read the relevant design docs (`Docs/design/`) and technical docs (`Docs/tech/`).
- Check existing implementations.

Update relevant documentation when requirements or interfaces change.

## 4. Game Layout (per game)

| Path (under `Games/<G>/`) | What | Owner |
|---|---|---|
| `Docs/design/` | GAME_DESIGN, GAME_RULES, FEATURE_SPECS, CONTENT_SPECS | game-designer |
| `Docs/tech/` | ARCHITECTURE, INTERFACE_CONTRACTS, DATA_STRUCTURE, CODING_CONVENTION | architect |
| `Docs/DECISIONS.md`, `Docs/PROGRESS.md`, `Tasks/` | decisions, progress, task files | Orchestrator |
| `Unity/Assets/Game/Core/` | engine-free game logic (no `UnityEngine`) | developer (per task scope) |
| `Unity/Assets/Game/Scripts/{Gameplay,Systems,UI}/` | Unity-side code (MonoBehaviours, UI Toolkit) | developer (per task scope) |
| `Unity/Assets/Game/Data/`, `Art/`, `Audio/`, `Prefabs/` | content | content-designer |
| `Unity/Assets/Game/Tests/EditMode/Core/` | engine-free Core tests (run by dotnet AND Unity) | developer / qa-engineer |
| `Unity/Assets/Game/Tests/{EditMode,PlayMode}/` | Unity tests | developer / qa-engineer |
| `Unity/Assets/Game/Editor/` | editor/build scripts | architect |
| `Unity/Packages/`, `Unity/ProjectSettings/` | packages, settings | architect only |
| `Reports/review/` | review reports | code-reviewer |
| `Reports/qa/`, `Reports/build/` | QA and CI verification reports | qa-engineer |

## 5. Unity Development Rules (code-first — nobody opens the Unity Editor)

- Unity version is fixed: **6000.3.29f1**. Never change `ProjectVersion.txt`.
- Core: C# 9, netstandard2.1 APIs only, **no `using UnityEngine`**, namespace `<G>.Core`.
  Put game rules and state here so they are testable without Unity.
- Runtime (`Scripts/`): presentation, input, effects only. Calls Core. Namespace `<G>.Runtime`.
- Create game objects from code (entry point `Scripts/Bootstrap.cs`). Avoid hand-written scene/prefab YAML.
- UI uses UI Toolkit (UXML/USS text files) or code-built UI.
- Game data: JSON (or code definitions) by default; ScriptableObjects only if the architect decides so.
- **Every new file or folder under `Unity/Assets/` needs a `.meta`**: run `python3 studio/scripts/gen_meta.py <path>`.
  Never edit or copy an existing `.meta` GUID.
- Do not introduce packages without an assigned architect task.
- Never overwrite another agent's work.

## 6. File Ownership

Every development task defines its allowed paths in `file_scope`. You may modify only:
- paths matched by your task's `file_scope` (a trailing `/` means the whole folder) and their `.meta` files;
- your own task file — and in it **only `status`, `result` and `notes`** (via `task.py set`).

Shared files require explicit ownership assignment by the Orchestrator.
`python3 studio/scripts/check_scope.py <G> <ID>` lists any file you changed outside your scope — run it before reporting.

## 7. Git Rules

- Work on the branch or worktree the Orchestrator gave you.
- Do not commit unrelated changes. Keep commits small and task-specific; message starts with the task ID.
- Never force-push. Do not merge unreviewed changes (merging is the Orchestrator's job).

## 8. Tools

Run from the repository root.

| Command | Purpose |
|---|---|
| `python3 studio/scripts/task.py list <G> [--status S]` | list tasks |
| `python3 studio/scripts/task.py set <G> <ID> --status S [--result KEY=VALUE] [--note TEXT]` | update your task |
| `python3 studio/scripts/check_scope.py <G> <ID>` | files changed outside the task scope |
| `python3 studio/scripts/gen_meta.py <path>...` | create missing `.meta` files |
| `python3 studio/scripts/check_meta.py Games/<G>/Unity` | missing/orphan `.meta`, duplicate GUIDs |
| `bash studio/scripts/verify_core.sh <G>` | `dotnet test` of Core (no Unity needed) |

Task states: `BACKLOG → ASSIGNED → IN_PROGRESS → REVIEW → QA → COMPLETED`;
`QA → AWAITING_UNITY → COMPLETED` when Unity verification is pending in CI;
`REVIEW|QA|AWAITING_UNITY → FIX_REQUIRED → IN_PROGRESS`; any open state → `BLOCKED|CANCELLED`.

Verification items: `core-tests` (dotnet, here), `review`, `unity-compile`, `unity-editmode`,
`unity-playmode`, `unity-build` (GitHub Actions `unity` workflow only).

## 9. Quality Requirements

Every implementation must:
- match the feature specification and acceptance criteria;
- compile (Core: `verify_core.sh` passes);
- include appropriate tests (Core logic → `Tests/EditMode/Core/`);
- handle expected edge cases;
- avoid unrelated modifications;
- include a completion report (below).

## 10. Agent Communication

Use the task file and reports — never rely only on conversational context for dependencies or status.

Completion report (your final message to the Orchestrator):
```
Task: <ID>   Status: <new status>
Changed files: <list>
Verification: <command> → <result>   (one line per command actually run)
Scope check: <check_scope output>
Assumptions / open issues: <list or "none">
```

## 11. Failure Handling

- Record failures with reproducible evidence (command + output excerpt).
- Never hide or skip failing tests; never weaken a test to make it pass.
- Never mark incomplete work as done. Never claim Unity behavior that CI has not verified.
- Retry only within limits (2 retries per task with the same agent; `task.py` enforces it).
- Escalate unresolved technical blockers to the Orchestrator.

## 12. Autonomous Operation & Safety

Routine implementation and verification need no user approval.

| Action | Allowed |
|---|---|
| Code/doc changes, tests, commits on the task branch | yes, within scope |
| Git merge | Orchestrator only, after review + QA pass |
| Packages / ProjectSettings changes | architect task only |
| File deletion | only when the task says so |
| External deployment / publishing | never without the user |
| Reading or writing secrets (licenses, passwords, tokens) | never |
| Shipping an unverified build | never |

## 13. Completion

A task is complete only when: implementation is committed, review is APPROVED (or APPROVED_WITH_WARNINGS),
required verification passed (including CI for Unity items), documentation is updated,
and the Orchestrator verified the result.
