"""Python 기준 대조(parity) 데이터 생성기 — ORIGAMI-009 테스트용 (ORIGAMI-015에서 저장소로 보관).

Docs/design/balance_check.py(수정하지 않음)의 엔진 함수(act/apply, landmarks, mark_pieces, snap)와
반올림 없는 line_on_paper로 기준 데이터를 만들고, C# 소스 파일
Unity/Assets/Game/Tests/EditMode/Core/Folding/PythonParityData.cs 의 전체 내용(머리 주석 + 래퍼 포함)을 출력한다.
시드는 "origami-009-parity" / "origami-009-snap"으로 고정되어 있어 출력은 결정적이다.

사용법 (저장소 루트에서):
  python3 Games/Origami/Docs/design/tools/gen_parity.py            # 표준출력으로 출력
  python3 Games/Origami/Docs/design/tools/gen_parity.py OUT.cs     # 지정 파일에 기록
  # 현재 테스트 데이터와 비교:
  python3 Games/Origami/Docs/design/tools/gen_parity.py | diff - \
      Games/Origami/Unity/Assets/Game/Tests/EditMode/Core/Folding/PythonParityData.cs
요약 정보(줄 수, snap 개수)는 표준오류로 출력된다.
balance_check.py의 규칙이 바뀌면 이 스크립트로 PythonParityData.cs를 다시 생성한다
(테스트 파일 갱신은 해당 테스트 파일 소유 작업에서 수행).
"""
import math
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))  # Docs/design/
import balance_check as bc  # noqa: E402


def r(v):
    return repr(float(v))


def pt(q):
    return r(q[0]) + " " + r(q[1])


def enc_action(s):
    k = s[0]
    if k == "F":
        m = s[3] if len(s) > 3 else False
        l = s[4] if len(s) > 4 else 0
        return f"F {pt(s[1])} {pt(s[2])} {1 if m else 0} {l}"
    if k == "C":
        l = s[3] if len(s) > 3 else 0
        return f"C {pt(s[1])} {pt(s[2])} {l}"
    return "X"


def o(v):
    t = f"{float(v):.10f}".rstrip("0").rstrip(".")
    return "0" if t in ("-0", "") else t


def opt(q):
    return o(q[0]) + " " + o(q[1])


def enc_state(st):
    fs = []
    for f in st.f:
        fs.append(f"{f.layer} {1 if f.front else 0} " + " ".join(opt(q) for q in f.w) + " / " + " ".join(opt(q) for q in f.p))
    cs = [opt(a) + " " + opt(b) for a, b in st.c]
    return "; ".join(fs) + " # " + "; ".join(cs)


ERR = {"조금 더 끌어 주세요": "DragTooShort", "잡은 곳에 종이가 없어요": "NothingGrabbed",
       "접히는 부분이 없어요": "NothingToFold", "접는 선이 종이 밖이에요": "LineOutsidePaper"}


def line_unrounded(st, grab, drop):
    L = bc.Line(grab, drop)
    d = (-L.n[1], L.n[0])
    ts = []
    for f in st.f:
        w = f.w if bc._area(f.w) > 0 else list(reversed(f.w))
        t0, t1, ok = -10.0, 10.0, True
        for i in range(len(w)):
            P, Q = w[i], w[(i + 1) % len(w)]
            e = bc._sub(Q, P)
            num, den = bc._cross(e, bc._sub(L.pt, P)), bc._cross(e, d)
            if abs(den) < 1e-12:
                if num < -1e-9:
                    ok = False
                    break
                continue
            t = -num / den
            if den > 0:
                t0 = max(t0, t)
            else:
                t1 = min(t1, t)
        if ok and t1 > t0 + 1e-9:
            ts += [t0, t1]
    if not ts:
        return None
    a, b = min(ts), max(ts)
    return (L.pt[0] + d[0] * a, L.pt[1] + d[1] * a), (L.pt[0] + d[0] * b, L.pt[1] + d[1] * b)


def candidates(st, rng):
    pts = [q for f in st.f for q in f.w]
    lms = bc.landmarks(st)
    return pts, lms


def random_action(st, rng):
    pts, lms = candidates(st, rng)
    u = rng.random()
    if u < 0.1:
        return ("X",)
    if rng.random() < 0.05:
        g = (rng.uniform(-1.6, 1.6), rng.uniform(-1.6, 1.6))
    elif rng.random() < 0.6:
        g = rng.choice(pts)
    else:
        f = rng.choice(st.f)
        i = rng.randrange(len(f.w))
        a, b = f.w[i], f.w[(i + 1) % len(f.w)]
        t = rng.random()
        g = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
    if rng.random() < 0.5:
        d = rng.choice(lms)
    else:
        d = (rng.uniform(-1.6, 1.6), rng.uniform(-1.6, 1.6))
    if rng.random() < 0.1:
        d = (g[0] + rng.uniform(-0.02, 0.02), g[1] + rng.uniform(-0.02, 0.02))
    layers = rng.choice([0, 0, 0, 1, 2, 3, 9])
    if u < 0.3:
        return ("C", g, d, layers)
    return ("F", g, d, rng.random() < 0.3, layers)


def main(out):
    lines = []
    # ---- reference sequences, state after each step
    for m in bc.MODELS:
        lines.append(f"REF {m}")
        st = bc.State()
        lines.append("S " + enc_state(st))
        for s in bc.REF[m]:
            lines.append("A " + enc_action(s))
            st = bc.act(st, s)
            lines.append("S " + enc_state(st))
    # ---- random sequences
    rng = random.Random("origami-009-parity")
    nseq = 0
    while nseq < 40:
        st = bc.State()
        seq = []
        try:
            for _ in range(rng.randrange(3, 7)):
                s = random_action(st, rng)
                try:
                    nxt = bc.act(st, s)
                    seq.append(("A " + enc_action(s), "S " + enc_state(nxt)))
                    st = nxt
                except bc.FoldError as e:
                    seq.append(("A " + enc_action(s), "E " + ERR[str(e)]))
        except ZeroDivisionError:
            continue
        lines.append(f"RND {nseq}")
        for a, b in seq:
            lines.append(a)
            lines.append(b)
        nseq += 1
    # ---- snap / landmark / line parity on reference states + random states
    rng = random.Random("origami-009-snap")
    states = []
    for m in bc.MODELS:
        st = bc.State()
        seq = []
        states.append((list(seq), st))
        for s in bc.REF[m]:
            st = bc.act(st, s)
            seq.append(s)
            states.append((list(seq), st))
    count = 0
    for seq, st in states:
        lines.append("SNAPSTATE " + " | ".join(enc_action(s) for s in seq))
        lms = bc.landmarks(st)
        lines.append("LM " + "; ".join(opt(q) for q in lms))
        mp = bc.mark_pieces(st)
        lines.append("MP " + "; ".join(opt(a) + " " + opt(b) + " " + str(st.f.index(f)) for a, b, f in mp))
        pts = [q for f in st.f for q in f.w]
        for _ in range(14):
            g = rng.choice(pts) if rng.random() < 0.7 else rng.choice(lms)
            base = rng.choice(lms)
            kind = rng.random()
            if kind < 0.4:
                d = (base[0] + rng.uniform(-0.07, 0.07), base[1] + rng.uniform(-0.07, 0.07))
            elif kind < 0.8:
                d = (rng.uniform(-1.5, 1.5), rng.uniform(-1.5, 1.5))
            elif kind < 0.9:
                d = g
            else:
                d = base
            try:
                q, k = bc.snap(st, g, d)
            except ZeroDivisionError:
                continue
            lines.append(f"SN {pt(g)} {pt(d)} {k} {opt(q)}")
            lp = line_unrounded(st, g, d) if math.dist(g, d) >= 1e-9 else None
            lines.append("LN " + ("none" if lp is None else opt(lp[0]) + " " + opt(lp[1])))
            count += 1
    text = CS_HEADER + "\n" + "\n".join(lines) + "\n" + CS_FOOTER
    if out is None:
        sys.stdout.write(text)
    else:
        with open(out, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
    print("lines", len(lines), "snaps", count, file=sys.stderr)


CS_HEADER = """// ORIGAMI-009: reference data generated from Docs/design/balance_check.py (engine functions act/apply, landmarks,
// mark_pieces, snap; line_on_paper without the _r6 rounding) by a scratch script (gen_parity.py, seeds
// "origami-009-parity" / "origami-009-snap"). Do not edit by hand — regenerate from balance_check.py.
// Format (one record per line):
//   REF <model> | RND <n>      start of a sequence (initial state follows as the first S line for REF)
//   A F gx gy dx dy mountain(0/1) layers | A C gx gy dx dy layers | A X   one action (inputs are exact Python repr)
//   S <facets> # <creases>    state after the action; facet = "layer front wx wy ... / px py ...", "; " separated (10 decimals)
//   E <FoldError>             the action was rejected by Python plan()
//   SNAPSTATE <actions | ...>  state for the following LM / MP / SN / LN lines (actions applied from the initial sheet)
//   LM <x y; ...>             landmarks()   MP <ax ay bx by facetIndex; ...>  mark_pieces()
//   SN gx gy dx dy kind qx qy snap() (kind P/L/M/-)   LN ax ay bx by | none   unrounded line_on_paper()
namespace Origami.Tests.Folding
{
    internal static class PythonParityData
    {
        public const string Text = @\""""
CS_FOOTER = """";
    }
}
"""


if __name__ == "__main__":
    if len(sys.argv) > 2:
        sys.exit("usage: gen_parity.py [OUT.cs]   (default: stdout)")
    main(sys.argv[1] if len(sys.argv) == 2 else None)
