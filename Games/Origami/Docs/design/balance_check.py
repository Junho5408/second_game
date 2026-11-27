#!/usr/bin/env python3
"""Origami v2 기획 수치 검증 스크립트 (game-designer, ORIGAMI-007).

게임 코드가 아니다. 기획 문서(GAME_RULES / CONTENT_SPECS)의 수치가 서로 맞는지 확인하는 참고용 계산기다.
  python3 Games/Origami/Docs/design/balance_check.py          # 경제 + 판정 기준값 (판정 부분은 수 분 걸림)
  python3 Games/Origami/Docs/design/balance_check.py econ     # 경제만 (빠름)
판정(IoU)은 격자 샘플링 근사(격자 0.004)라 정확값과 약 ±0.003 차이가 날 수 있다.
"""
import itertools
import math
import sys

# ---------------------------------------------------------------- 경제 (GAME_RULES 기본 수치)
MODELS = ["plane", "house", "tulip", "dog", "glider"]
BASE = dict(plane=6, house=8, tulip=12, dog=18, glider=26)
PAGE = dict(plane=0, house=0, tulip=20, dog=50, glider=150)
POPULAR = ["plane", "house", "tulip", "house", "dog", "tulip", "glider"]
START, PAPER, SHEETS, DAYS = 20, 2, 5, 7
GRADE_PCT = {3: 150, 2: 100, 1: 60}
TYCOON = 600


def price(m, grade, day):
    return BASE[m] * GRADE_PCT[grade] * (150 if POPULAR[day - 1] == m else 100) // 10000


def sim(buy_day, grade, log=None):
    """모든 종이를 같은 등급으로 완성, 그날 가장 비싸게 팔리는 보유 작품만 접는다(종이값이 모두 같으므로 최적)."""
    money, owned = START, {"plane", "house"}
    for day in range(1, DAYS + 1):
        for m, d in buy_day.items():
            if d == day:
                if money < PAGE[m]:
                    return None
                money -= PAGE[m]
                owned.add(m)
        if day > 1 and money < PAPER:
            return None
        sales = 0
        for s in range(SHEETS):
            free = day == 1 and s == 0
            if not free:
                if money < PAPER:
                    break
                money -= PAPER
            m = "plane" if free else max(owned, key=lambda k: (price(k, grade, day), -MODELS.index(k)))
            sales += price(m, grade, day)
        money += sales
        if log is not None:
            log.append((day, money))
    return money


def best(grade):
    pages = ("tulip", "dog", "glider")
    top = None
    for days in itertools.product([None] + list(range(1, 8)), repeat=3):
        r = sim(dict(zip(pages, days)), grade)
        if r is not None and (top is None or r > top[0]):
            top = (r, days)
    return top


def econ():
    print("판매가 표 [★3,★2,★1 일반 | ★3,★2,★1 인기]")
    for m in MODELS:
        print(" ", m, [BASE[m] * g * p // 10000 for p in (100, 150) for g in (150, 100, 60)])
    res = {}
    for g in (3, 2, 1):
        r = best(g)
        log = []
        sim(dict(zip(("tulip", "dog", "glider"), r[1])), g, log)
        res[g] = r[0]
        print(f"모두 ★{g}: 최대 최종 돈 {r[0]} (책 구입일 tulip/dog/glider = {r[1]}) 일별 {log}")
    print("책 구입 없이 모두 ★3:", sim({}, 3))
    assert res[3] == 967 and res[2] == 562 and res[1] == 242
    assert res[2] < TYCOON <= res[3], "재벌은 ★3 위주 플레이에서만"
    assert sim({}, 3) == 437 < TYCOON
    # 최단 파산: 모든 종이 실패 → 1일 끝 12, 2일 끝 2, 3일 1장 후 0, 4일 아침 파산
    m = START - 4 * PAPER
    m -= 5 * PAPER
    m -= PAPER
    assert m == 0 < PAPER
    print("경제 검증 OK")


# ---------------------------------------------------------------- 평면 접기 (Fold3DSpike FoldPlanner와 같은 규칙: 움직이는 쪽 모든 겹이 180° 접힘)
S2 = math.sqrt(2)
K = 1 - S2
X = 2 * math.tan(math.pi / 8)


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1]


def _area(p):
    return 0.5 * sum(p[i][0] * p[(i + 1) % len(p)][1] - p[i][1] * p[(i + 1) % len(p)][0] for i in range(len(p)))


def fold(facets, grab, drop):
    pt = ((grab[0] + drop[0]) / 2, (grab[1] + drop[1]) / 2)
    n = _sub(drop, grab)
    ln = math.hypot(*n)
    n = (n[0] / ln, n[1] / ln)
    side = lambda p: _dot(_sub(p, pt), n)
    refl = lambda p: (p[0] - 2 * side(p) * n[0], p[1] - 2 * side(p) * n[1])
    out, moved, stat = [], False, False
    for f in facets:
        s = [0 if abs(side(p)) < 1e-9 else side(p) for p in f]
        mv, st = [], []
        for i in range(len(f)):
            j = (i + 1) % len(f)
            if s[i] <= 0:
                mv.append(f[i])
            if s[i] >= 0:
                st.append(f[i])
            if (s[i] < 0 < s[j]) or (s[i] > 0 > s[j]):
                t = s[i] / (s[i] - s[j])
                w = (f[i][0] + (f[j][0] - f[i][0]) * t, f[i][1] + (f[j][1] - f[i][1]) * t)
                mv.append(w)
                st.append(w)
        if len(mv) >= 3 and abs(_area(mv)) > 1e-7 and any(v < 0 for v in s):
            out.append([refl(p) for p in reversed(mv)])
            moved = True
        if len(st) >= 3 and abs(_area(st)) > 1e-7 and any(v > 0 for v in s):
            out.append(st)
            stat = True
    assert moved and stat, "invalid fold"
    return out


def run(steps):
    f = [[(-1, -1), (1, -1), (1, 1), (-1, 1)]]
    for g, d in steps:
        f = fold(f, g, d)
    return f


# CONTENT_SPECS 기준 접기 순서 (잡는 점 → 놓는 점, 종이 좌표: 한 변 2 = 20cm, 중심 원점, +Y = 먼 쪽)
REF = {
    "plane": [((-1, 1), (0, 0)), ((1, 1), (0, 0)), ((-1, 0), (0, K)), ((1, 0), (0, K)), ((-X, -1), (X, -1))],
    "house": [((-1, 1), (-1, -1)), ((-1, 0), (-0.6, -0.8)), ((1, 0), (0.6, -0.8))],
    "tulip": [((1, 1), (-1, -1)), ((-1, 1), (0.2, 0.6)), ((1, -1), (0.6, 0.2))],
    "dog": [((1, 1), (-1, -1)), ((-1, 1), (-1.3, 0.1)), ((1, -1), (0.1, -1.3)), ((-1, -1), (K, K))],  # 4단계 턱: (-0.4142, -0.4142)
    "glider": [((-1, 1), (0, 0)), ((1, 1), (0, 0)), ((0, 1), (0, 0)), ((-0.5, 0.5), (0, 0)), ((0.5, 0.5), (0, 0)),
               ((-0.3, -1), (0.3, -1))],
}

try:
    import numpy as np
except ImportError:  # pragma: no cover
    np = None

R = 0.004


def _grid():
    g = np.arange(-2.2, 2.2, R) + R / 2
    return np.meshgrid(g, g)


def mask(facets, XX, YY, tf=None):
    m = np.zeros(XX.shape, bool)
    for f in facets:
        if tf:
            f = [tf(p) for p in f]
        if _area(f) < 0:
            f = list(reversed(f))
        inside = np.ones(XX.shape, bool)
        for i in range(len(f)):
            a, b = f[i], f[(i + 1) % len(f)]
            inside &= ((b[0] - a[0]) * (YY - a[1]) - (b[1] - a[1]) * (XX - a[0])) >= -1e-12
        m |= inside
    return m


D4 = [lambda p: (p[0], p[1]), lambda p: (-p[1], p[0]), lambda p: (-p[0], -p[1]), lambda p: (p[1], -p[0]),
      lambda p: (-p[0], p[1]), lambda p: (p[1], p[0]), lambda p: (p[0], -p[1]), lambda p: (-p[1], -p[0])]


def match(fa, fb, XX, YY):
    def norm(fs):
        mk = mask(fs, XX, YY)
        c = (XX[mk].mean(), YY[mk].mean())
        return [[(p[0] - c[0], p[1] - c[1]) for p in f] for f in fs]
    a, b = norm(fa), norm(fb)
    ma = mask(a, XX, YY)
    return max(((ma & mask(b, XX, YY, g)).sum() / (ma | mask(b, XX, YY, g)).sum()) for g in D4)


def recognition():
    if np is None:
        print("numpy 없음: 판정 검증 생략")
        return
    XX, YY = _grid()
    fin = {m: run(REF[m]) for m in MODELS}
    H, P = REF["house"], REF["plane"]
    cases = [
        # (이름, 접기 순서, 보유 작품, 기대 결과 작품 또는 None(실패작), 기대 일치도, 허용 오차)
        ("house ref", H, MODELS, "house", 1.000, 0.005),
        ("house 아래에서 위로(대칭)", [((-1, -1), (-1, 1)), ((-1, 0), (-0.6, 0.8)), ((1, 0), (0.6, 0.8))], MODELS, "house", 1.0, 0.005),
        ("plane 오른쪽 먼저(대칭)", [P[1], P[0], P[3], P[2], ((X, -1), (-X, -1))], MODELS, "plane", 1.0, 0.005),
        ("house 2·3단계 놓는 점 +0.03y", [H[0], ((-1, 0), (-0.6, -0.77)), ((1, 0), (0.6, -0.77))], MODELS, "house", 0.973, 0.006),
        ("house 2·3단계 놓는 점 +0.1y", [H[0], ((-1, 0), (-0.6, -0.7)), ((1, 0), (0.6, -0.7))], MODELS, "house", 0.922, 0.006),
        ("house 2·3단계 놓는 점 +0.2y", [H[0], ((-1, 0), (-0.6, -0.6)), ((1, 0), (0.6, -0.6))], MODELS, "house", 0.868, 0.006),
        ("plane 3·4단계 놓는 점 y=-0.30", P[:2] + [((-1, 0), (0, -0.3)), ((1, 0), (0, -0.3)), P[4]], MODELS, None, 0.804, 0.006),
        ("house 2단계까지", H[:2], MODELS, None, 0.783, 0.006),
        ("반으로 한 번", [H[0]], MODELS, None, 0.728, 0.006),
        ("dog 턱 없이", REF["dog"][:3], MODELS, None, 0.811, 0.006),
        ("glider 5단계까지", REF["glider"][:5], MODELS, None, 0.708, 0.006),
        ("dog (책 없음: plane·house만 보유)", REF["dog"], ["plane", "house"], None, 0.482, 0.006),
    ]
    for m in MODELS:
        cases.append((f"{m} ref", REF[m], MODELS, m, 1.0, 0.005))
    ok = True
    for name, steps, owned, exp, val, tol in cases:
        f = run(steps)
        sc = {m: match(f, fin[m], XX, YY) for m in owned}
        b = max(owned, key=lambda m: (sc[m], -MODELS.index(m)))
        got = b if sc[b] >= 0.85 else None
        good = got == exp and abs(sc[b] - val) <= tol
        ok &= good
        print(f"{'OK ' if good else 'BAD'} {name}: 최고 {b} {sc[b]:.3f} → {got or '실패작'}")
    print("작품 사이 일치도(대각선 제외 최대값은 0.85 미만이어야 함)")
    worst = 0
    for a in MODELS:
        row = [match(fin[a], fin[b], XX, YY) for b in MODELS]
        worst = max(worst, max(v for b, v in zip(MODELS, row) if b != a))
        print(" ", a, " ".join(f"{v:.3f}" for v in row))
    ok &= worst < 0.85
    print("판정 검증", "OK" if ok else "실패")
    if not ok:
        sys.exit(1)


if __name__ == "__main__":
    econ()
    if len(sys.argv) < 2 or sys.argv[1] != "econ":
        recognition()
