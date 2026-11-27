# QA ORIGAMI-015: v2 design doc cleanup + parity generator

- QA: qa-engineer
- Commits tested: 22ac41b (implementation), edd3557 (review). Base: 7190c81.
- Result: **PASS**. Status set to COMPLETED. The only verification item is `review`, and it was APPROVED_WITH_WARNINGS.

## Acceptance criteria
| # | Criterion | Result | Evidence |
|---|---|---|---|
| AC1 | `Docs/design/tools/gen_parity.py` stored, uses repo-relative paths, output is byte-identical, usage in the header | PASS | `gen_parity.py > qa15/out.cs; cmp` gave IDENTICAL. `gen_parity.py qa15/out2.cs; cmp` gave IDENTICAL. Running it from the scratch dir with stdout piped to `cmp -` against `PythonParityData.cs` gave IDENTICAL. stderr printed `lines 1627 snaps 518`. Line 23 sets `sys.path.insert(0, str(Path(__file__).resolve().parent.parent))`. The module docstring documents stdout mode, file mode and the diff command. |
| AC2 | CONTENT §2 house step 3 endpoint order matches the script | PASS | `balance_check.py steps` step 3 prints `(1.0, -0.5)–(-1.0, -0.5)`. The CONTENT §2 row now reads `(1,−0.5)–(−1,−0.5)`. The rule note also says the script output is the reference for endpoint order. |
| AC3 | GAME_DESIGN cicada wording, R-021 range, F-006 AC-04 | PASS | The GAME_DESIGN text now says three things: J-14/J-15 cover both models; the exhaustive search up to 5 folds is tulip-only; the cicada was not searched exhaustively. This matches GAME_RULES l.160 and CONTENT §4.2 / l.216. The header now reads `R-001~R-021`, and GAME_RULES does go up to R-021. FEATURE_SPECS F-006 AC-04 now does one `Fold` before `Unfold` and cites R-007. |
| AC4 | No numeric or rule changes, econ OK | PASS | `git diff 7190c81 22ac41b --stat`: 3 doc files, 4 lines changed each way, all wording. The house endpoints were only reordered, so the segment is the same. `balance_check.py` is unchanged. `balance_check.py econ` printed "경제 검증 OK", exit 0. |

## Bugs
None.

## Tests added
None. This is a docs and tool task. The generator was tested by byte comparison (above).

## Untested / notes
- Review W-1 is open: the generated C# header still says the file came from a "scratch script". It has to stay that way to keep the output byte-identical. Fix it in a later task that owns `PythonParityData.cs`.
- Review W-2 is open: `rng` is unused in `candidates`. This is cosmetic.
- I did not run the generator with other Python versions. I tested it only with the local `python3`.
