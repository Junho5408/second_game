# Review ORIGAMI-015: v2 design doc cleanup + parity generator

- Reviewer: code-reviewer
- Commit: 22ac41b (base 7190c81)
- Result: **APPROVED_WITH_WARNINGS**

## Verification
| Command | Result |
|---|---|
| `check_scope.py Origami ORIGAMI-015` | OK |
| `gen_parity.py > tmp; cmp tmp PythonParityData.cs` | byte-identical (lines 1627, snaps 518 on stderr) |
| `gen_parity.py tmp_file.cs; cmp` | byte-identical |
| `gen_parity.py` run from another cwd (scratch dir), piped to `cmp` | byte-identical (import uses the script's own location) |
| `gen_parity.py a b` | usage message, exit 1 |
| `git status --porcelain` after the runs | clean (nothing written to the repo when no path is given) |
| `git diff 7190c81 22ac41b -- .../balance_check.py` | empty (unmodified) |
| `balance_check.py econ` | "경제 검증 OK", exit 0 |
| `balance_check.py steps` (house) | step 3 line `(1.0, -0.5)–(-1.0, -0.5)`, same as the new CONTENT §2 row |
| `verify_core.sh Origami` | PASS, 595/595 |
| `check_meta.py Games/Origami/Unity` | OK |

## Checklist
1. Acceptance criteria
   - AC1: generator is in `Docs/design/tools/gen_parity.py`, `sys.path` comes from `Path(__file__).resolve().parent.parent`, it writes to stdout or to a path you give it, the usage text is in the module docstring, and the output is byte-identical. Met.
   - AC2: CONTENT §2 house step 3 now matches the script's order, and the rule note says the script output is the reference for endpoint order. Met.
   - AC3: the GAME_DESIGN W-5 text now says only what was checked. J-14/J-15 cover the same-position swap for both models, the exhaustive check up to 5 folds is tulip-only, and it states that the cicada was not searched exhaustively. This matches GAME_RULES R-011 (line 160) and CONTENT §4.2. The header now reads R-001~R-021. F-006 AC-04 now does one Fold before Unfold, and it correctly cites R-007 (empty undo history means rejection, GAME_RULES l.124). Met.
   - AC4: in the doc diff, only wording changed. No numbers, thresholds or rules changed (the house step-3 endpoints only swapped order, the same segment). econ is OK. Met.
2. Conventions/boundaries: only `Docs/design/` changed. Nothing changed under `Unity/Assets/`, so no `.meta` files were needed.
3-5. No runtime code. The generator's output is deterministic because the seeds are fixed.
6-8. No regressions: Core tests pass, and the parity data file is unchanged.

## Warnings (non-blocking)
- W-1 `Docs/design/tools/gen_parity.py` `CS_HEADER` (line ~205): the generated C# header still says "by a scratch script (gen_parity.py ...)", but the script now lives in the repo. It has to stay that way for byte identity with the current test file. Impact: a misleading provenance comment in the test data. Fix: in a later task that owns `PythonParityData.cs`, change the header text to point to `Docs/design/tools/gen_parity.py` in both the generator and the test file together.
- W-2 `gen_parity.py` `candidates(st, rng)`: the `rng` parameter is unused. This is cosmetic. Leave it alone unless the script is regenerated anyway, because changing the RNG call order would change the output.
