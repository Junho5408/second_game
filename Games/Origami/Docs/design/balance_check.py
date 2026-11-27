#!/usr/bin/env python3
"""Origami v2 기획 수치 검증 스크립트 (game-designer, ORIGAMI-007).

게임 코드가 아니다. 기획 문서(GAME_RULES / FEATURE_SPECS / CONTENT_SPECS)의 수치를 다시 만들어 내는 참고용 계산기다.
문서의 모든 수치(경제, 판정 기준값 J-xx, 작품 사이 일치도, 오차 표, 지름길 탐색, 책 쪽 번호, 플레이 시간)는 이 스크립트 출력에서 나온다.

  python3 Games/Origami/Docs/design/balance_check.py            # 전부 (약 5~10분, 4코어)
  python3 Games/Origami/Docs/design/balance_check.py econ       # 경제·책·플레이 시간 (빠름)
  python3 Games/Origami/Docs/design/balance_check.py recog      # 판정 기준값 J-01~J-18, 불변성, 작품 사이 일치도, 자석 검사
  python3 Games/Origami/Docs/design/balance_check.py steps      # 기준 순서의 접는 선 위 점(도해용) 출력
  python3 Games/Origami/Docs/design/balance_check.py errors     # 사람 오차 모의실험(시드 고정) + 기대 경제
  python3 Games/Origami/Docs/design/balance_check.py shortcut   # 지름길 검사(책 순서의 모든 부분 순서 전수 + 빔 탐색 + 기법 필요성)

판정(IoU)은 격자 샘플링 근사다(격자 0.004, 무게중심 정렬 후 D4 8가지). 정확값과 약 ±0.003 차이가 날 수 있다.
접기 규칙은 GAME_RULES R-007(전체 접기 = Fold3DSpike FoldPlanner, 위 N겹 접기, 접었다 펴기), 자석은 R-021을 그대로 옮겼다.
"""
import itertools
import math
import random
import sys

try:
    import numpy as np
except ImportError:  # pragma: no cover
    np = None

# =============================================================== 경제 (GAME_RULES 기본 수치, CONTENT_SPECS §5)
MODELS = ["plane", "house", "tulip", "dog", "cicada"]
NAMES = dict(plane="종이비행기", house="집", tulip="튤립", dog="강아지 얼굴", cicada="매미")
BASE = dict(plane=6, house=8, tulip=12, dog=18, cicada=26)
PAGE = dict(plane=0, house=0, tulip=20, dog=50, cicada=150)
POPULAR = ["plane", "house", "tulip", "house", "dog", "tulip", "cicada"]
START, PAPER, SHEETS, DAYS = 20, 2, 5, 7
GRADE_PCT = {3: 150, 2: 100, 1: 60}
TYCOON = 600
BUYABLE = ("tulip", "dog", "cicada")


def price(m, grade, day):
    if grade == 0:
        return 0
    return BASE[m] * GRADE_PCT[grade] * (150 if POPULAR[day - 1] == m else 100) // 10000


def exp_price(m, day, probs):
    """probs[m] = {3: p, 2: p, 1: p} (나머지는 구겨진 종이 0원). probs None = 항상 ★3."""
    if probs is None:
        return price(m, 3, day)
    return sum(p * price(m, g, day) for g, p in probs[m].items())


def sim(buy_day, grade=3, log=None, probs=None, only=None, first="plane"):
    """모든 종이를 같은 등급(또는 기대 등급 probs)으로 완성, 그날 가장 비싸게 팔리는 보유 작품만 접는다.
    only = 이 작품만 접기(첫 무료 종이는 first). 페이지는 아침에 산다."""
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
            if free:
                m = first
            elif only is not None and only in owned:
                m = only
            else:
                m = max(owned, key=lambda k: (exp_price(k, day, probs), -MODELS.index(k)))
            sales += exp_price(m, day, probs) if probs is not None else price(m, grade, day)
        money += sales
        if log is not None:
            log.append((day, round(money, 1)))
    return money


def best(grade=3, probs=None):
    top = None
    for days in itertools.product([None] + list(range(1, 8)), repeat=3):
        r = sim(dict(zip(BUYABLE, days)), grade, probs=probs)
        if r is not None and (top is None or r > top[0]):
            top = (r, days)
    return top


def tulip_only_one_star():
    """N-1: 튤립만 ★1로 접는 런. 1일차 = 무료 plane ★1 + plane ★1 4장(튤립 페이지 살 돈 모으기), 2일차 아침 튤립 구입, 2~7일 튤립만 ★1."""
    probs = {m: {1: 1.0} for m in MODELS}
    return sim({"tulip": 2}, probs=probs, only="tulip")


# 책 쪽 (R-010, F-007): [0 목차][1 접기 기호] + 작품마다 [표지, 1단계 … N단계, 완성]
def book_pages():
    pages, start = 2, {}
    for m in MODELS:
        start[m] = pages
        pages += len(REF[m]) + 2
    return pages, start


def turn_page(p, total):
    p = max(0, min(total - 1, p))
    return p - p % 2


# 플레이 시간 가정 (W-4): 동작 1번(책 확인 + 끌기) 느림 15초 / 보통 10초 / 빠름 6초, 종이 1장 고정 20/15/10초(꺼내기·완성·결과),
# 하루 고정 90/60/40초(걷기·판매대·책장·잠·정산).
PLAY = {"느림": (15, 20, 90), "보통": (10, 15, 60), "빠름": (6, 10, 40)}


def play_time(plan_days):
    """최적 ★3 계획에서 접는 작품 목록으로 분 단위 시간."""
    log = []
    _plan_sheets(plan_days, log)
    out = {}
    for k, (act_s, sheet_s, day_s) in PLAY.items():
        sec = DAYS * day_s + sum(sheet_s + act_s * len(REF[m]) for m in log)
        out[k] = round(sec / 60)
    return out, log


def _plan_sheets(buy_day, log):
    money, owned = START, {"plane", "house"}
    for day in range(1, DAYS + 1):
        for m, d in buy_day.items():
            if d == day:
                money -= PAGE[m]
                owned.add(m)
        sales = 0
        for s in range(SHEETS):
            free = day == 1 and s == 0
            if not free:
                money -= PAPER
            m = "plane" if free else max(owned, key=lambda k: (price(k, 3, day), -MODELS.index(k)))
            log.append(m)
            sales += price(m, 3, day)
        money += sales


def econ():
    print("판매가 표 [★3,★2,★1 일반 | ★3,★2,★1 인기]")
    for m in MODELS:
        print(" ", m, [BASE[m] * g * p // 10000 for p in (100, 150) for g in (150, 100, 60)])
    res = {}
    for g in (3, 2, 1):
        r = best(g)
        log = []
        sim(dict(zip(BUYABLE, r[1])), g, log)
        res[g] = r
        print(f"모두 ★{g}: 최대 최종 돈 {r[0]} (책 구입일 tulip/dog/cicada = {r[1]}) 일별 {log}")
    print("책 구입 없이 모두 ★3:", sim({}, 3))
    t1 = tulip_only_one_star()
    print("튤립만 ★1 (1일 plane ★1, 2일 아침 튤립 구입):", t1, "/ 튤립 완성 수 30")
    assert res[3][0] == 967 and res[2][0] == 562 and res[1][0] == 242
    assert res[2][0] < TYCOON <= res[3][0], "재벌은 ★3 위주 플레이에서만"
    assert sim({}, 3) == 437 < TYCOON
    assert t1 == 197 and t1 > 20
    # 최단 파산: 모든 종이 실패 → 1일 끝 12, 2일 끝 2, 3일 1장 후 0, 4일 아침 파산
    m = START - 4 * PAPER
    m -= 5 * PAPER
    m -= PAPER
    assert m == 0 < PAPER
    total, start = book_pages()
    print("책:", total, "쪽, 작품 표지 쪽", start)
    assert total == 44 and start == dict(plane=2, house=10, tulip=17, dog=24, cicada=32)
    assert [turn_page(p, total) for p in (-1, 0, 17, 31, 43, 99)] == [0, 0, 16, 30, 42, 42]
    pt, sheets = play_time(dict(zip(BUYABLE, res[3][1])))
    print("플레이 시간(분, ★3 최적 계획 35장):", pt, "작품별 장수", {m: sheets.count(m) for m in MODELS})
    print("경제 검증 OK")


# =============================================================== 접기 엔진 (R-007: Fold3DSpike FoldPlanner + 위 N겹 + 접었다 펴기)
EPS = 1e-9
MIN_AREA = 1e-7
MIN_DRAG = 0.03


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1]


def _cross(a, b):
    return a[0] * b[1] - a[1] * b[0]


def _area(p):
    return 0.5 * sum(_cross(p[i], p[(i + 1) % len(p)]) for i in range(len(p)))


class Facet:
    """평평한 볼록 조각. w = 테이블 위 위치, p = 펼친 종이 위 같은 점(종이 좌표), layer = 전체 쌓임 순서(0 = 맨 아래)."""
    __slots__ = ("w", "p", "layer", "front")

    def __init__(self, w, p, layer, front):
        self.w, self.p, self.layer, self.front = list(w), list(p), layer, front

    def contains(self, q, eps=1e-6):
        n = len(self.w)
        return all(_cross(_sub(self.w[(i + 1) % n], self.w[i]), _sub(q, self.w[i])) >= -eps for i in range(n))

    def strictly(self, q, eps=1e-6):
        n = len(self.w)
        return all(_cross(_sub(self.w[(i + 1) % n], self.w[i]), _sub(q, self.w[i]))
                   / math.dist(self.w[i], self.w[(i + 1) % n]) > eps for i in range(n))

    def to_world(self, q):
        a, b, c = self.p[0], self.p[1], self.p[2]
        A, B, C = self.w[0], self.w[1], self.w[2]
        e1, e2 = _sub(b, a), _sub(c, a)
        det = _cross(e1, e2)
        d = _sub(q, a)
        s, t = _cross(d, e2) / det, _cross(e1, d) / det
        return (A[0] + s * (B[0] - A[0]) + t * (C[0] - A[0]), A[1] + s * (B[1] - A[1]) + t * (C[1] - A[1]))


SQUARE = [(-1, -1), (1, -1), (1, 1), (-1, 1)]


class State:
    """종이 상태 = 조각들 + 접은 자국(원래 종이 좌표의 선분 목록) + 접은 횟수."""

    def __init__(self, facets=None, creases=None, folds=0):
        self.f = facets if facets is not None else [Facet(SQUARE, SQUARE, 0, False)]
        self.c = creases if creases is not None else []
        self.folds = folds


class Line:
    def __init__(self, grab, drop):
        self.pt = ((grab[0] + drop[0]) / 2, (grab[1] + drop[1]) / 2)
        n = _sub(drop, grab)
        ln = math.hypot(*n)
        self.n = (n[0] / ln, n[1] / ln)

    def side(self, q):
        v = _dot(_sub(q, self.pt), self.n)
        return 0.0 if abs(v) < EPS else v

    def refl(self, q):
        s = _dot(_sub(q, self.pt), self.n)
        return (q[0] - 2 * s * self.n[0], q[1] - 2 * s * self.n[1])


class FoldError(Exception):
    pass


def _split(f, L):
    s = [L.side(q) for q in f.w]
    if not any(v < 0 for v in s):
        return None, f
    if not any(v > 0 for v in s):
        return f, None
    mw, mp, sw, sp = [], [], [], []
    n = len(f.w)
    for i in range(n):
        j = (i + 1) % n
        if s[i] <= 0:
            mw.append(f.w[i]); mp.append(f.p[i])
        if s[i] >= 0:
            sw.append(f.w[i]); sp.append(f.p[i])
        if (s[i] < 0 < s[j]) or (s[i] > 0 > s[j]):
            t = s[i] / (s[i] - s[j])
            mw.append((f.w[i][0] + (f.w[j][0] - f.w[i][0]) * t, f.w[i][1] + (f.w[j][1] - f.w[i][1]) * t))
            mp.append((f.p[i][0] + (f.p[j][0] - f.p[i][0]) * t, f.p[i][1] + (f.p[j][1] - f.p[i][1]) * t))
            sw.append(mw[-1]); sp.append(mp[-1])
    m = Facet(mw, mp, f.layer, f.front) if len(mw) >= 3 and abs(_area(mw)) > MIN_AREA else None
    st = Facet(sw, sp, f.layer, f.front) if len(sw) >= 3 and abs(_area(sw)) > MIN_AREA else None
    if m is None:
        return None, f
    if st is None:
        return f, None
    return m, st


def _overlap_len(a, b, c, d):
    u = _sub(b, a)
    lu = math.hypot(*u)
    if lu < 1e-9 or abs(_cross(u, _sub(c, a))) / lu > 1e-6 or abs(_cross(u, _sub(d, a))) / lu > 1e-6:
        return 0
    t1, t2 = _dot(_sub(c, a), u) / lu, _dot(_sub(d, a), u) / lu
    return min(lu, max(t1, t2)) - max(0, min(t1, t2))


HINGE_MIN = 0.02


def _hinged(a, b, L):
    """R-007 연결: 두 조각이 원래 종이에서 변을 HINGE_MIN(2mm) 이상 공유하고, 그 변이 이번 접는 선 위에 있지 않다."""
    na, nb = len(a.p), len(b.p)
    for i in range(na):
        if L.side(a.w[i]) == 0 and L.side(a.w[(i + 1) % na]) == 0:
            continue
        for j in range(nb):
            if _overlap_len(a.p[i], a.p[(i + 1) % na], b.p[j], b.p[(j + 1) % nb]) >= HINGE_MIN:
                return True
    return False


def _clip(poly, a, b):
    out, n = [], len(poly)
    for i in range(n):
        P, Q = poly[i], poly[(i + 1) % n]
        sp, sq = _cross(_sub(b, a), _sub(P, a)), _cross(_sub(b, a), _sub(Q, a))
        if sp >= 0:
            out.append(P)
        if (sp > 0 > sq) or (sp < 0 < sq):
            t = sp / (sp - sq)
            out.append((P[0] + (Q[0] - P[0]) * t, P[1] + (Q[1] - P[1]) * t))
    return out


def _overlap_area(a, b):
    poly = list(a.w) if _area(a.w) > 0 else list(reversed(a.w))
    bw = b.w if _area(b.w) > 0 else list(reversed(b.w))
    for i in range(len(bw)):
        poly = _clip(poly, bw[i], bw[(i + 1) % len(bw)])
        if len(poly) < 3:
            return 0
    return abs(_area(poly))


def plan(st, grab, drop, layers=0):
    """R-007 움직이는 부분 정하기. layers = 0 전체, n ≥ 1 위 n겹. 반환 (움직이는 조각, 남는 조각, 선, 일부만 접힘 여부)."""
    if math.dist(grab, drop) < MIN_DRAG:
        raise FoldError("조금 더 끌어 주세요")
    L = Line(grab, drop)
    pieces = [(i,) + _split(f, L) for i, f in enumerate(st.f)]
    grab_side = [i for i, m, s in pieces if m is not None]
    mv = set(grab_side)
    if layers:
        stack = sorted([i for i, f in enumerate(st.f) if f.contains(grab)], key=lambda i: -st.f[i].layer)
        if not stack:
            raise FoldError("잡은 곳에 종이가 없어요")
        if layers < len(stack):
            mv = {i for i in stack[:layers] if pieces[i][1] is not None}
            changed = True
            while changed:
                changed = False
                for j in grab_side:
                    if j in mv:
                        continue
                    mj = pieces[j][1]
                    for i in list(mv):
                        mi = pieces[i][1]
                        if _hinged(mi, mj, L) or (mj.layer > mi.layer and _overlap_area(mi, mj) > MIN_AREA):
                            mv.add(j)
                            changed = True
                            break
    if not mv:
        raise FoldError("접히는 부분이 없어요")
    moving, static = [], []
    for i, m, s in pieces:
        if i in mv:
            moving.append(m)
            if s is not None:
                static.append(s)
        else:
            static.append(st.f[i])
    if not static:
        raise FoldError("접는 선이 종이 밖이에요")
    return moving, static, L, len(mv) < len(grab_side)


def fold(st, grab, drop, mountain=False, layers=0):
    moving, static, L, partial = plan(st, grab, drop, layers)
    lay = [f.layer for f in moving + static]
    max_a, min_a = max(lay), min(lay)
    max_m, min_m = max(f.layer for f in moving), min(f.layer for f in moving)
    out = [Facet(f.w, f.p, f.layer, f.front) for f in static]
    if mountain and partial:
        for f in out:
            if f.layer >= min_m:
                f.layer += max_m - min_m + 1
    for f in moving:
        if not mountain:
            layer = max_a + 1 + (max_m - f.layer)
        elif partial:
            layer = min_m + (max_m - f.layer)
        else:
            layer = min_a - 1 - (f.layer - min_m)
        out.append(Facet([L.refl(q) for q in reversed(f.w)], list(reversed(f.p)), layer, not f.front))
    rank = {v: i for i, v in enumerate(sorted(set(f.layer for f in out)))}
    for f in out:
        f.layer = rank[f.layer]
    return State(out, list(st.c), st.folds + 1)


def crease(st, grab, drop, layers=0):
    """접었다 펴기: 종이 모양은 그대로, 움직였을 조각들의 접는 선 부분이 자국(원래 종이 좌표)으로 남는다."""
    moving, _, L, _ = plan(st, grab, drop, layers)
    marks = []
    for f in moving:
        n = len(f.w)
        for i in range(n):
            j = (i + 1) % n
            if L.side(f.w[i]) == 0 and L.side(f.w[j]) == 0:
                marks.append((f.p[i], f.p[j]))
    return State([Facet(f.w, f.p, f.layer, f.front) for f in st.f], list(st.c) + marks, st.folds)


def flip(st):
    xs = [q[0] for f in st.f for q in f.w]
    cx = 0.5 * (min(xs) + max(xs))
    mx = max(f.layer for f in st.f)
    return State([Facet([(2 * cx - q[0], q[1]) for q in reversed(f.w)], list(reversed(f.p)), mx - f.layer, not f.front)
                  for f in st.f], list(st.c), st.folds)


def transform(st, tf):
    """판정 불변성 검사용: 모든 조각 위치에 변환 tf를 적용(뒤집힘이면 꼭짓점 순서를 바꿔 반시계 유지)."""
    out = []
    for f in st.f:
        w = [tf(q) for q in f.w]
        p = list(f.p)
        if _area(w) < 0:
            w.reverse(); p.reverse()
        out.append(Facet(w, p, f.layer, f.front))
    return State(out, list(st.c), st.folds)


def act(st, s):
    """s = ("F", grab, drop, mountain, layers) · ("C", grab, drop, layers) · ("X",) 뒤집기."""
    k = s[0]
    if k == "F":
        return fold(st, s[1], s[2], s[3] if len(s) > 3 else False, s[4] if len(s) > 4 else 0)
    if k == "C":
        return crease(st, s[1], s[2], s[3] if len(s) > 3 else 0)
    if k == "X":
        return flip(st)
    raise ValueError(k)


def apply(steps, st=None):
    st = st or State()
    for s in steps:
        st = act(st, s)
    return st


# --------------------------------------------------------------- 자국과 기준점 (R-021)
def mark_pieces(st):
    """접은 자국을 각 조각에 잘라 테이블 위치로 옮긴 선분 (a, b, 조각)."""
    out = []
    for f in st.f:
        pp = f.p if _area(f.p) > 0 else list(reversed(f.p))
        for a, b in st.c:
            t0, t1, d, ok = 0.0, 1.0, _sub(b, a), True
            for i in range(len(pp)):
                P, Q = pp[i], pp[(i + 1) % len(pp)]
                e = _sub(Q, P)
                num, den = _cross(e, _sub(a, P)), _cross(e, d)
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
            if ok and t1 - t0 > 1e-6:
                out.append((f.to_world((a[0] + d[0] * t0, a[1] + d[1] * t0)),
                            f.to_world((a[0] + d[0] * t1, a[1] + d[1] * t1)), f))
    return out


def visible(st, q, f):
    return not any(g is not f and g.layer > f.layer and g.strictly(q) for g in st.f)


def landmarks(st):
    """기준점 = 모든 조각 꼭짓점(시험판 자석과 같음) + 보이는 자국 점(자국 조각 끝점, 자국끼리 교차점)."""
    pts = [q for f in st.f for q in f.w]
    mp = mark_pieces(st)
    for a, b, f in mp:
        pts.extend(q for q in (a, b) if visible(st, q, f))
    for i in range(len(mp)):
        for j in range(i + 1, len(mp)):
            a, b, f = mp[i]
            c, d, g = mp[j]
            den = _cross(_sub(b, a), _sub(d, c))
            if abs(den) < 1e-9:
                continue
            t, u = _cross(_sub(c, a), _sub(d, c)) / den, _cross(_sub(c, a), _sub(b, a)) / den
            if -1e-9 <= t <= 1 + 1e-9 and -1e-9 <= u <= 1 + 1e-9:
                q = (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)
                if visible(st, q, f) or visible(st, q, g):
                    pts.append(q)
    res = []
    for q in pts:
        if all(math.dist(q, r) > 1e-6 for r in res):
            res.append(q)
    return res


SNAP_POINT, SNAP_LINE, SNAP_MARK, LINE_PAIR_MIN = 0.06, 0.04, 0.04, 0.2


def _refl_ab(p, a, b):
    d = _sub(b, a)
    ln = math.hypot(*d)
    d = (d[0] / ln, d[1] / ln)
    t = _dot(_sub(p, a), d)
    f = (a[0] + d[0] * t, a[1] + d[1] * t)
    return (2 * f[0] - p[0], 2 * f[1] - p[1])


def _key(q):
    """동점 처리용 좌표 키: x, 그다음 y (1e-9 단위로 반올림)."""
    return (round(q[0], 9), round(q[1], 9))


def snap(st, grab, drop):
    """R-021 놓기 자석. 반환 (놓는 점, 종류): P 기준점, L 접는 선 맞춤, M 자국 선, - 없음.
    동점 순서(리뷰 N-8, 결정적): 거리(L은 거리 합)를 1e-9로 반올림해 작은 것 → 점 좌표 x → y가 작은 것.
    L의 쌍은 두 점을 (x, y) 순으로 정렬한 (앞 점, 뒤 점)으로 비교한다."""
    lms = [q for q in landmarks(st) if math.dist(q, grab) > 1e-6]
    near = [(round(math.dist(q, drop), 9),) + _key(q) + (q,) for q in lms if math.dist(q, drop) <= SNAP_POINT]
    if near:
        return min(near)[-1], "P"
    if math.dist(grab, drop) < 1e-9:
        return drop, "-"
    L = Line(grab, drop)
    cand = [(abs(_dot(_sub(q, L.pt), L.n)), q) for q in lms]
    cand = [c for c in cand if c[0] <= SNAP_LINE]
    pairs = []
    for (da, a), (db, b) in itertools.combinations(cand, 2):
        if math.dist(a, b) >= LINE_PAIR_MIN:
            a, b = sorted((a, b), key=_key)
            pairs.append((round(da + db, 9), _key(a), _key(b), a, b))
    if pairs:
        pair = min(pairs)
        return _refl_ab(grab, pair[3], pair[4]), "L"
    marks = []
    for a, b, f in mark_pieces(st):
        ab = _sub(b, a)
        t = max(0, min(1, _dot(_sub(drop, a), ab) / _dot(ab, ab)))
        q = (a[0] + ab[0] * t, a[1] + ab[1] * t)
        d = math.dist(q, drop)
        if d <= SNAP_MARK and visible(st, q, f):
            marks.append((round(d, 9),) + _key(q) + (q,))
    if marks:
        return min(marks)[-1], "M"
    return drop, "-"


# =============================================================== 작품 기준 순서 (CONTENT_SPECS §2)
S2 = math.sqrt(2)
K = 1 - S2                       # ≈ -0.4142
X0 = 2 * math.tan(math.pi / 8)   # ≈ 0.8284
C = (-1, -1)


def _r6(p):
    return (round(p[0], 6), round(p[1], 6))


def _tip(q, side):
    """다이아몬드 기본형에서 덮개 끝 C를 '대각선 위 (-1+q, -1+q)와 옆 꼭짓점 side를 잇는 선'으로 접을 때 놓는 점."""
    return _r6(_refl_ab(C, (-1 + q, -1 + q), side))



REF = {
    "plane": [("C", (-1, 1), (1, 1)), ("F", (-1, 1), (0, 0)), ("F", (1, 1), (0, 0)),
              ("F", (-1, 0), (0, K)), ("F", (1, 0), (0, K)), ("F", (-X0, -1), (X0, -1))],
    "house": [("F", (-1, 1), (-1, -1)), ("C", (-1, 0), (1, 0)), ("C", (-1, -1), (-1, 0)),
              ("F", (-1, 0), (-0.6, -0.8)), ("F", (1, 0), (0.6, -0.8))],
    "tulip": [("F", (1, 1), C), ("F", (-1, 1), C), ("F", (1, -1), C),
              ("F", C, _tip(0.7, (0, -1)), False, 2), ("F", C, _tip(0.7, (-1, 0)), False, 2)],
    "dog": [("F", (1, 1), C), ("C", (-1, 1), (1, -1)), ("C", C, (0, 0)),
            ("F", (-1, 1), (-1.4, 0.0)), ("F", (1, -1), (0.0, -1.4)), ("F", C, (-0.5, -0.5))],
    # 매미 (수정 2차, 리뷰 R-1): 위 1겹 머리 단계 삭제. 날개 = 위 2겹(이음선 때문에 위 1겹을 골라도 같음), 머리 = 남은 바닥 2겹 전체.
    # 날개 선 = 아래 꼭짓점 (0,-1)과 대각선 자국 위 (-0.38,-0.38)을 지나는 선(q = 0.62, 손으로 맞춤). 머리 = C를 두 날개 선이 만난 점으로.
    # 옆 = 옆 꼭짓점을 두 자국(대각선·세로)이 만나는 점 (-0.5,-0.5)에 맞춰 뒤로(산접기).
    "cicada": [("F", (1, 1), C), ("F", (-1, 1), C), ("F", (1, -1), C), ("C", (-1, 0), (0, -1)), ("C", (-1, 0), (0, 0)),
               ("F", C, _tip(0.62, (0, -1)), False, 2), ("F", C, _tip(0.62, (-1, 0)), False, 2),
               ("F", C, (-0.38, -0.38)),
               ("F", (-1, 0), (-0.5, -0.5), True), ("F", (0, -1), (-0.5, -0.5), True)],
}
FOLDS = {m: sum(1 for s in REF[m] if s[0] == "F") for m in MODELS}


def describe(s):
    if s[0] == "C":
        return f"접었다 펴기 {s[1]}→{s[2]}" + (f" 위 {s[3]}겹" if len(s) > 3 and s[3] else "")
    if s[0] == "X":
        return "뒤집기"
    return (("산접기" if len(s) > 3 and s[3] else "골접기") + f" {s[1]}→{s[2]}"
            + (f" 위 {s[4]}겹" if len(s) > 4 and s[4] else " 전체"))


def line_on_paper(st, s):
    """접는 선이 현재 종이 윤곽 위에 그려지는 양 끝점."""
    L = Line(s[1], s[2])
    d = (-L.n[1], L.n[0])
    ts = []
    for f in st.f:
        w = f.w if _area(f.w) > 0 else list(reversed(f.w))
        t0, t1, ok = -10.0, 10.0, True
        for i in range(len(w)):
            P, Q = w[i], w[(i + 1) % len(w)]
            e = _sub(Q, P)
            num, den = _cross(e, _sub(L.pt, P)), _cross(e, d)
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
    a, b = min(ts), max(ts)
    return _r6((L.pt[0] + d[0] * a, L.pt[1] + d[1] * a)), _r6((L.pt[0] + d[0] * b, L.pt[1] + d[1] * b))


def steps():
    for m in MODELS:
        st = State()
        print(f"{m} ({NAMES[m]}) 동작 {len(REF[m])} / 접기 {FOLDS[m]}")
        for i, s in enumerate(REF[m], 1):
            d2, kind = snap(st, s[1], s[2])
            mv, _, _, partial = plan(st, s[1], s[2], (s[4] if len(s) > 4 else 0) if s[0] == "F" else (s[3] if len(s) > 3 else 0))
            a, b = line_on_paper(st, s)
            print(f"  {i}. {describe(s)}  선 {a}–{b}  자석 {kind}  움직이는 조각 {len(mv)}{' (일부 겹)' if partial else ''}")
            st = act(st, s)
        print(f"  완성: 조각 {len(st.f)}, 자국 {len(st.c)}")


# =============================================================== 판정 (R-011~R-013)
R_FINE = 0.004


def grid(r):
    n = int(round(4.4 / r))
    g = (np.arange(n) - (n - 1) / 2) * r
    return np.meshgrid(g, g), g


def rmask(polys, G, shift=(0, 0)):
    (XX, YY), g = G
    r = g[1] - g[0]
    m = np.zeros(XX.shape, bool)
    for f in polys:
        f = [(q[0] - shift[0], q[1] - shift[1]) for q in f]
        if _area(f) < 0:
            f = list(reversed(f))
        xs, ys = [q[0] for q in f], [q[1] for q in f]
        i0, i1 = np.searchsorted(g, min(xs) - r), np.searchsorted(g, max(xs) + r)
        j0, j1 = np.searchsorted(g, min(ys) - r), np.searchsorted(g, max(ys) + r)
        X, Y = XX[j0:j1, i0:i1], YY[j0:j1, i0:i1]
        inside = np.ones(X.shape, bool)
        for i in range(len(f)):
            a, b = f[i], f[(i + 1) % len(f)]
            inside &= ((b[0] - a[0]) * (Y - a[1]) - (b[1] - a[1]) * (X - a[0])) >= -1e-12
        m[j0:j1, i0:i1] |= inside
    return m


def cmask(st, G):
    """윤곽(모든 조각의 합집합)을 넓이 무게중심이 원점에 오게 옮긴 격자 마스크."""
    polys = [f.w for f in st.f]
    (XX, YY), _ = G
    m0 = rmask(polys, G)
    return rmask(polys, G, (XX[m0].mean(), YY[m0].mean()))


def d4(m):
    out = []
    for k in range(4):
        r = np.rot90(m, k)
        out += [r, np.fliplr(r)]
    return out


def iou(ma, mbs):
    return max((ma & mb).sum() / (ma | mb).sum() for mb in mbs)


def judge(st, owned, refm, G):
    """R-012: (작품 또는 None, 최고 후보, 일치도). 동점은 카탈로그 순서."""
    ma = cmask(st, G)
    sc = {m: iou(ma, refm[m]) for m in owned}
    b = max(owned, key=lambda m: (sc[m], -MODELS.index(m)))
    return (b if sc[b] >= 0.85 else None), b, sc[b]


GRADE3_MIN, GRADE2_MIN, MATCH_MIN = 97, 93, 85   # GAME_RULES 기본 수치 (W-3 재조정: 95/90 → 97/93)
MARGIN = 0.02        # 문서가 기대는 판정 결과는 경계(0.85/0.93/0.97)에서 이만큼 떨어져야 한다(DECISIONS 지름길 정책, 리뷰 R-3)
GRID_ERR = 0.002     # 격자 0.004 근사 오차 여유. ★2 구간(0.93~0.97, 폭 0.04)은 가운데 0.95 ± GRID_ERR만 허용


def margin_ok(s):
    if GRADE2_MIN / 100 <= s < GRADE3_MIN / 100:
        return abs(s - 0.95) <= GRID_ERR
    return all(abs(s - b / 100) >= MARGIN for b in (GRADE3_MIN, GRADE2_MIN, MATCH_MIN))


def grade(s):
    p = math.floor(s * 100 + 1e-9)
    return 3 if p >= GRADE3_MIN else 2 if p >= GRADE2_MIN else 1 if p >= MATCH_MIN else 0


def recognition():
    G = grid(R_FINE)
    fin = {m: apply(REF[m]) for m in MODELS}
    refm = {m: d4(cmask(fin[m], G)) for m in MODELS}
    H, P, T, Dg, Ci = REF["house"], REF["plane"], REF["tulip"], REF["dog"], REF["cicada"]
    hf = [s for s in H if s[0] == "F"]
    var = lambda dy: [hf[0], ("F", (-1, 0), (-0.6, -0.8 + dy)), ("F", (1, 0), (0.6, -0.8 + dy))]
    cases = [
        # (ID, 이름, 순서, 보유, 기대 작품(None = 구겨진 종이), 기대 일치도, 허용 오차)
        ("J-02", "house 아래에서 위로(대칭)", [("F", (-1, -1), (-1, 1)), ("F", (-1, 0), (-0.6, 0.8)), ("F", (1, 0), (0.6, 0.8))], MODELS, "house", 1.0, 0.005),
        ("J-03", "plane 오른쪽 먼저, 자국 없이", [P[2], P[1], P[4], P[3], ("F", (X0, -1), (-X0, -1))], MODELS, "plane", 1.0, 0.005),
        ("J-04", "house Var(0.01)", var(0.01), MODELS, "house", 0.992, 0.006),
        ("J-05", "house Var(0.06)", var(0.06), MODELS, "house", 0.951, 0.006),
        ("J-06", "house Var(0.15)", var(0.15), MODELS, "house", 0.892, 0.006),
        ("J-07", "plane 3·4단계 놓는 점 y=-0.30", P[:3] + [("F", (-1, 0), (0, -0.3)), ("F", (1, 0), (0, -0.3)), P[5]], MODELS, None, 0.804, 0.006),
        ("J-08", "house 지붕 한쪽만", hf[:2], MODELS, None, 0.783, 0.006),
        ("J-09", "반으로 한 번", hf[:1], MODELS, None, 0.728, 0.006),
        ("J-10", "dog 귀 없이(턱만)", Dg[:3] + Dg[5:], MODELS, None, None, None),
        ("J-11", "cicada 옆 접기 없이(1~8단계)", Ci[:8], MODELS, None, None, None),
        ("J-12", "dog (plane·house만 보유)", Dg, ["plane", "house"], None, None, None),
        ("J-14", "tulip 꽃잎을 전체 겹으로", [s[:4] for s in T], MODELS, None, None, None),
        ("J-15", "cicada 1~5단계 + 6단계를 전체 겹으로 + 9·10단계(C의 모든 겹이 넘어가 7·8단계는 할 수 없다)",
         Ci[:5] + [Ci[5][:4]] + Ci[8:], MODELS, None, None, None),
        ("J-16", "house 자국 없이(접기만)", hf, MODELS, "house", 1.0, 0.005),
        ("J-17", "cicada 날개(6·7단계)를 위 1겹으로(이음선 때문에 덮개 2겹이 같이 접힘)",
         Ci[:5] + [s[:4] + (1,) for s in Ci[5:7]] + Ci[7:], MODELS, "cicada", 1.0, 0.005),
        ("J-18", "cicada 머리(8단계) 없이", Ci[:7] + Ci[8:], MODELS, None, None, None),
    ]
    for m in MODELS:
        cases.insert(0, ("J-01", f"{m} 기준 순서", REF[m], MODELS, m, 1.0, 0.005))
        cases.append(("J-13", f"{m} 기준 + 뒤집기", REF[m] + [("X",)], MODELS, m, 1.0, 0.01))
    ok = True
    for cid, name, seq, owned, exp, val, tol in cases:
        got, b, s = judge(apply(seq), owned, refm, G)
        good = got == exp and (val is None or abs(s - val) <= tol)
        good = good and margin_ok(s)  # 등급 경계에서 0.02 이상(★2 구간은 가운데) — DECISIONS 지름길 정책
        if exp is None:  # 구겨진 종이여야 한다(0.85 − 0.02 이하)
            good = good and s <= MATCH_MIN / 100 - MARGIN
        ok &= good
        res = f"{NAMES[got]} ★{grade(s)}" if got else "구겨진 종이"
        print(f"{'OK ' if good else 'BAD'} {cid} {name}: 최고 {b} {s:.3f} → {res}")
    # F-009 AC-03 / N-2: 평행이동·회전·반전 불변
    tfs = {"이동(+0.3,-0.2)": lambda q: (q[0] + 0.3, q[1] - 0.2), "90° 회전": lambda q: (-q[1], q[0]),
           "좌우 반전": lambda q: (-q[0], q[1])}
    for m in MODELS:
        vals = [iou(cmask(transform(fin[m], tf), G), refm[m]) for tf in tfs.values()]
        good = all(v >= 0.99 for v in vals)
        ok &= good
        print(f"{'OK ' if good else 'BAD'} 불변성 {m}: " + ", ".join(f"{k} {v:.3f}" for k, v in zip(tfs, vals)))
    print("작품 사이 일치도(대각선 제외 최대값은 0.75 이하 목표, 0.85 미만 필수)")
    worst = 0
    masks = {m: cmask(fin[m], G) for m in MODELS}
    for a in MODELS:
        row = [iou(masks[a], refm[b]) for b in MODELS]
        worst = max(worst, max(v for b, v in zip(MODELS, row) if b != a))
        print(" ", a.ljust(6), " ".join(f"{v:.3f}" for v in row), f" 윤곽 넓이 {masks[a].sum() * R_FINE ** 2:.3f}")
    ok &= worst <= 0.75
    # R-021: 정확한 입력이면 자석이 기준 순서를 바꾸지 않는다
    for m in MODELS:
        st, kinds, allsame = State(), [], True
        for s in REF[m]:
            d2, k = snap(st, s[1], s[2])
            L1, L2 = Line(s[1], s[2]), Line(s[1], d2)
            same = abs(abs(_dot(L1.n, L2.n)) - 1) < 1e-9 and abs(_dot(_sub(L2.pt, L1.pt), L1.n)) < 1e-5
            ok &= same
            allsame &= same
            kinds.append(k if same else k + "!")
            st = act(st, s)
        print(f"{'OK ' if allsame else 'BAD'} 자석 {m}: {' '.join(kinds)} (P 기준점, L 접는 선, M 자국 선, - 자유)")
    print("판정 검증", "OK" if ok else "실패")
    if not ok:
        sys.exit(1)


# =============================================================== 사람 오차 모의실험 (W-3)
SIGMAS = [0.005, 0.01, 0.02, 0.03]   # 축마다 표준편차: 0.5 / 1 / 2 / 3 mm
TRIALS = 100
R_MC = 0.01


def _moving_area(st, s):
    layers = (s[4] if len(s) > 4 else 0) if s[0] == "F" else (s[3] if len(s) > 3 else 0)
    try:
        return sum(abs(_area(f.w)) for f in plan(st, s[1], s[2], layers)[0])
    except (FoldError, ZeroDivisionError):
        return None


def run_human(seq, sigma, rng, retries=5):
    """사람 모델: 잡기는 정확(잡기 자석 0.16), 겹 수는 책대로. 놓는 점 = 기준 + 가우스(축마다 sigma), 그 뒤 R-021 자석.
    미리보기에서 움직이는 넓이가 의도의 2/3~3/2 밖이면 다시 놓는다(최대 retries번)."""
    st = State()
    for s in seq:
        if s[0] in "FC":
            want = _moving_area(st, s)
            for _ in range(retries + 1):
                d = (s[2][0] + rng.gauss(0, sigma), s[2][1] + rng.gauss(0, sigma))
                d2, _k = snap(st, s[1], d)
                s2 = (s[0], s[1], d2) + tuple(s[3:])
                got = _moving_area(st, s2)
                if got is not None and want and 2 / 3 <= got / want <= 1.5:
                    break
            s = s2
        try:
            st = act(st, s)
        except (FoldError, ZeroDivisionError):
            return None
    return st


def _mc_task(args):
    m, sigma = args
    G = grid(R_MC)
    fin = {k: d4(cmask(apply(REF[k]), G)) for k in MODELS}
    rng = random.Random(f"origami-{m}-{sigma}")
    cnt = {3: 0, 2: 0, 1: 0, 0: 0}
    vals = []
    for _ in range(TRIALS):
        st = run_human(REF[m], sigma, rng)
        if st is None:
            cnt[0] += 1
            vals.append(0.0)
            continue
        got, b, s = judge(st, MODELS, fin, G)
        g = grade(s) if got == m else 0
        cnt[g] += 1
        vals.append(s if got == m else 0.0)
    return m, sigma, cnt, vals


def errors():
    from multiprocessing import Pool
    tasks = [(m, s) for s in SIGMAS for m in MODELS]
    with Pool(4) as p:
        res = p.map(_mc_task, tasks)
    table = {(m, s): (cnt, sorted(v)[len(v) // 2]) for m, s, cnt, v in res}
    raw = {(m, s): v for m, s, _, v in res}
    print(f"사람 오차 모의실험 (작품·σ마다 {TRIALS}회, 시드 'origami-<id>-<σ>'): ★3% / ★2 이상% / 인정% / 중앙값")
    for s in SIGMAS:
        row = []
        for m in MODELS:
            cnt, med = table[(m, s)]
            n = sum(cnt.values())
            row.append(f"{m} {100 * cnt[3] // n}/{100 * (cnt[3] + cnt[2]) // n}/{100 * (n - cnt[0]) // n}/{med:.2f}")
        print(f"  σ={s * 100:.1f}mm  " + "  ".join(row))
    for s in SIGMAS:
        probs = {}
        for m in MODELS:
            cnt, _ = table[(m, s)]
            n = sum(cnt.values())
            probs[m] = {g: cnt[g] / n for g in (3, 2, 1)}
        r = best(probs=probs)
        print(f"  기대 최종 돈 σ={s * 100:.1f}mm: {r[0]:.0f} (구입일 {r[1]})")
    print("  등급 기준 후보별 ★3% (σ=1/2/3mm): 기준 = (★3 최소, ★2 최소, ★1 최소)")
    for th in ((95, 90, 85), (97, 93, 85), (98, 94, 85)):
        cells = []
        for m in MODELS:
            cells.append(m + " " + "/".join(str(sum(1 for v in raw[(m, s)] if math.floor(v * 100 + 1e-9) >= th[0]))
                                            for s in SIGMAS[1:]))
        print(f"    {th}: " + "  ".join(cells))
    return table


# =============================================================== 지름길 탐색 (W-1)
R_SEARCH = 0.03
SEARCH_DEPTH = dict(plane=4, house=2, tulip=4, dog=3, cicada=4)
BEAM = 25
GRID_PTS = [(x * 0.25, y * 0.25) for x in range(-6, 7) for y in range(-6, 7)]
_SG = None
_MT = None


def _fmask(st):
    (XX, YY), g = _SG
    m = rmask([f.w for f in st.f], _SG)
    ys, xs = np.nonzero(m)
    if len(ys) == 0 or m[0].any() or m[-1].any() or m[:, 0].any() or m[:, -1].any():
        return None
    n = len(g)
    cy, cx = int(round(ys.mean() - (n - 1) / 2)), int(round(xs.mean() - (n - 1) / 2))
    return np.roll(np.roll(m, -cy, 0), -cx, 1)


def _cand_points(st):
    out = []
    for f in st.f:
        n = len(f.w)
        for i in range(n):
            mid = ((f.w[i][0] + f.w[(i + 1) % n][0]) / 2, (f.w[i][1] + f.w[(i + 1) % n][1]) / 2)
            for q in (f.w[i], mid):
                if all(math.dist(q, r) > 1e-6 for r in out):
                    out.append(q)
    return out


def _sc_init(mt):
    global _SG, _MT
    _SG = grid(R_SEARCH)
    _MT = mt


def _expand(args):
    st, path, sel = args
    V = _cand_points(st)
    drops = V + [g for g in GRID_PTS if all(math.dist(g, v) > 1e-6 for v in V)]
    out = []
    for gp in V:
        for d in drops:
            if math.dist(gp, d) < MIN_DRAG:
                continue
            for lay in ((0, 1, 2) if sel else (0,)):
                try:
                    ns = fold(st, gp, d, False, lay)
                except (FoldError, ZeroDivisionError):
                    continue
                m = _fmask(ns)
                if m is not None:
                    out.append((iou(m, _MT), hash(m.tobytes()), ns, path + [(gp, d, lay)]))
    return out


def beam(target, depth, sel=False):
    """빔 탐색: 매 단계 잡기 = 조각 꼭짓점·변 가운데, 놓기 = 그 점들 + 0.25 격자(-1.5~1.5), 전체(또는 sel이면 위 1·2겹도) 골접기.
    목표 윤곽과의 일치도 상위 BEAM개만 다음 단계로. 증명이 아니라 '이 범위에서 못 찾음'을 기록한다."""
    from multiprocessing import Pool
    global _SG
    _SG = grid(R_SEARCH)
    mt = d4(_fmask(target))
    frontier, seen, best_per_depth = [(0, State(), [])], set(), []
    with Pool(4, initializer=_sc_init, initargs=(mt,)) as p:
        for _ in range(depth):
            cand = []
            for lst in p.imap(_expand, [(st, path, sel) for _, st, path in frontier]):
                for sc, k, ns, path in lst:
                    if k not in seen:
                        seen.add(k)
                        cand.append((sc, ns, path))
            cand.sort(key=lambda c: -c[0])
            frontier = cand[:BEAM]
            best_per_depth.append(frontier[0])
    return best_per_depth


OLD_GLIDER = [("F", (-1, 1), (0, 0)), ("F", (1, 1), (0, 0)), ("F", (0, 1), (0, 0)), ("F", (-0.5, 0.5), (0, 0)),
              ("F", (0.5, 0.5), (0, 0)), ("F", (-0.3, -1), (0.3, -1))]


SHORTCUT_MAX = GRADE2_MIN / 100 - MARGIN   # 0.91: 유료 작품은 책보다 적은 접기로 이 값 이상이면 실패(★2 경계 0.93 − 여유 0.02)
TECH_DEPTH = dict(tulip=5)                  # 기법 필요성: 전체 겹만으로 책 접기 수만큼 탐색


def subsequences():
    """리뷰 R-2: 각 책 순서에서 접기를 1개 이상 뺀 모든 부분 순서(남은 동작 순서 유지)를 정확히 다시 잰다.
    접었다 펴기는 모양을 바꾸지 않으므로 그대로 둔다(빼도 같은 상태). 무효가 되는 순서(잡을 곳 없음 등)는 건너뛴다.
    각 부분 순서의 상태를 5종 기준 모두와 비교한다: 그 작품보다 접기 수가 적은데 유료 작품 m과 0.91 이상이면 실패."""
    G = grid(R_FINE)
    refm = {m: d4(cmask(apply(REF[m]), G)) for m in MODELS}
    ok, found = True, {m: [] for m in MODELS}
    for src in MODELS:
        seq = REF[src]
        fidx = [i for i, s in enumerate(seq) if s[0] == "F"]
        n_valid = n_all = 0
        for k in range(1, len(fidx) + 1):
            for drop in itertools.combinations(fidx, k):
                n_all += 1
                sub = [s for i, s in enumerate(seq) if i not in drop]
                try:
                    st = apply(sub)
                except (FoldError, ZeroDivisionError):
                    continue
                if st.folds == 0:
                    continue
                n_valid += 1
                ma = cmask(st, G)
                for m in MODELS:
                    if st.folds >= FOLDS[m]:
                        continue
                    found[m].append((iou(ma, refm[m]), src, [d + 1 for d in drop], st.folds))
        print(f"  {src}: 부분 순서 {n_all}개 중 유효 {n_valid}개 검사")
    for m in MODELS:
        top3 = sorted(found[m], key=lambda r: -r[0])[:3]
        v = top3[0][0]
        paid = PAGE[m] > 0
        good = v < SHORTCUT_MAX or not paid
        ok &= good
        print(f"  {'OK ' if good else 'BAD'}{'' if paid else '(무료, 기록만)'} 부분 순서 최고 {m} (책 {FOLDS[m]}접기), 상위 3개:")
        for v, src, drop, nf in top3:
            print(f"      {v:.3f} ← {src} 순서에서 {drop}단계 뺌({nf}접기) → "
                  f"{NAMES[m] + ' ★' + str(grade(v)) if v >= 0.85 else '구겨진 종이'}")
    return ok


def shortcut():
    G = grid(R_FINE)
    ok = True
    print(f"지름길 검사 (DECISIONS 지름길 정책): 유료 작품은 책보다 적은 접기로 {SHORTCUT_MAX:.2f}(★2 경계 0.93 − 여유 0.02) 이상이면 실패. "
          "★1까지는 허용. 무료 plane·house는 기록만")
    print("[1] 책 순서의 모든 부분 순서 (전수)")
    ok &= subsequences()
    print(f"[2] 빔 탐색 (빔 {BEAM}, 격자 {R_SEARCH}로 고르고 격자 {R_FINE}로 다시 잼)")
    # 탐색기가 실제 지름길을 찾는지 먼저 확인: v2 초안 글라이더(리뷰 W-1)는 3번 안에 0.85를 넘어야 한다
    tgt = apply(OLD_GLIDER)
    ref = d4(cmask(tgt, G))
    found = max(iou(cmask(apply([("F",) + x[:2] + (False, x[2]) for x in b[2]]), G), ref) for b in beam(tgt, 3))
    print(f"  점검: 초안 글라이더 6접기 → 3접기 안에서 찾은 최고 {found:.3f} (0.85 이상이어야 탐색기가 유효)")
    ok &= found >= 0.85
    for m in MODELS:
        tgt = apply(REF[m])
        ref = d4(cmask(tgt, G))
        for sel in (False, True):
            depth = SEARCH_DEPTH[m] if not sel else min(SEARCH_DEPTH[m], 3)
            res = beam(tgt, depth, sel)
            fine = [iou(cmask(apply([("F",) + x[:2] + (False, x[2]) for x in b[2]]), G), ref) for b in res]
            top = max(fine)
            paid = PAGE[m] > 0
            good = top < SHORTCUT_MAX or not paid
            ok &= good
            path = res[fine.index(top)][2]
            print(f"  {'OK ' if good else 'BAD'}{'' if paid else '(무료, 기록만)'} {m} (책 {FOLDS[m]}접기) {'위 1·2겹 포함' if sel else '전체 접기만'} 깊이 {depth}: "
                  f"깊이별 최고 {' '.join(f'{v:.3f}' for v in fine)} / 최고 경로 {[(_r6(a), _r6(b), l) for a, b, l in path]}")
    print("[3] 기법 필요성: 전체 겹 접기만으로 책 접기 수까지 탐색해도 ★2 미만(0.91 미만)이어야 '위 N겹이 ★2 이상에 필요'라고 쓴다")
    for m, depth in TECH_DEPTH.items():
        tgt = apply(REF[m])
        ref = d4(cmask(tgt, G))
        res = beam(tgt, depth, False)
        fine = [iou(cmask(apply([("F",) + x[:2] + (False, x[2]) for x in b[2]]), G), ref) for b in res]
        good = max(fine) < SHORTCUT_MAX
        ok &= good
        print(f"  {'OK ' if good else 'BAD'} {m} 전체 겹만 깊이 {depth}: 깊이별 최고 {' '.join(f'{v:.3f}' for v in fine)}")
    print("지름길 검증", "OK" if ok else "실패")
    if not ok:
        sys.exit(1)


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "all"
    if mode in ("all", "econ"):
        econ()
    if np is None and mode != "econ":
        print("numpy 없음: 판정 검증 생략")
        sys.exit(0)
    if mode in ("all", "steps"):
        steps()
    if mode in ("all", "recog"):
        recognition()
    if mode in ("all", "errors"):
        errors()
    if mode in ("all", "shortcut"):
        shortcut()
