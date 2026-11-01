# -*- coding: utf-8 -*-
"""五维成长曲线模拟：现状 / 方案A(成长抑制) / 方案B(饱和曲线) / 实战加成
用法: python sim_growth.py
输出: 控制台表格 + growth_curves.svg + 结果写回 notes/成长曲线模拟.md 的数据段
"""
import math, random, json, io, os

random.seed(20261101)
CAP = 400.0
MAXEXP = 300000.0
DAYS_PER_YEAR = 360

def conv(exp, scheme):
    if scheme in ('vanilla', 'A', 'combat_vanilla', 'combat_A'):
        return int((exp / 1000.0) ** 0.9)
    return int(CAP * (1 - math.exp(-exp / 60000.0)))

def rand_val(a, b):
    b = int(b)
    if b == 0:
        return 0
    n, r = divmod(int(a), b)
    if r > 0 and random.randrange(b) < r:
        n += 1
    return n

def growth_factor(attr, scheme):
    if scheme in ('A', 'AB', 'combat_A'):
        return max(0.15, min(2.0, (CAP - attr) / 150.0))
    return 1.0

def sim(base_p, base_g, scheme, years=10, rate=1.0, combat_exp=0.0, combat_target=None):
    """返回 [(year, politics, glamour, cmd_or_None)]"""
    ep = eg = 0.0
    ec = 0.0
    out = [(0, base_p, base_g, None)]
    combat_target = combat_target or 0.0
    for y in range(1, years + 1):
        for _ in range(DAYS_PER_YEAR):
            p = base_p + conv(ep, scheme)
            g = base_g + conv(eg, scheme)
            ability = 2 * (p + g)
            val = rand_val(ability * rate, 500)
            inc = val * 2 * growth_factor(p, scheme)
            ep = min(MAXEXP, ep + inc)
            eg = min(MAXEXP, eg + inc)
            if combat_exp > 0:
                inc_c = combat_exp * growth_factor(combat_target + conv(ec, scheme), scheme)
                ec = min(MAXEXP, ec + inc_c)
        out.append((y, base_p + conv(ep, scheme), base_g + conv(eg, scheme),
                    (combat_target + conv(ec, scheme)) if combat_exp > 0 else None))
    return out

def days_to_gain(base, scheme, target_gain, rate=1.0, limit=40000):
    e = 0.0
    for d in range(1, limit + 1):
        p = base + conv(e, scheme)
        val = rand_val(2 * (p + base) * rate, 500)
        e = min(MAXEXP, e + val * 2 * growth_factor(p, scheme))
        if conv(e, scheme) >= target_gain:
            return d
    return None

SCHEMES = [('vanilla', '现状'), ('A', '方案A 抑制'), ('B', '方案B 饱和'), ('AB', 'A+B')]
STARTS = [40, 70, 100]

rows = []
for key, name in SCHEMES:
    for s in STARTS:
        curve = sim(s, s, key, years=10)
        pts = {y: p for y, p, g, c in curve}
        rows.append({'scheme': key, 'name': name, 'start': s,
                     'y1': pts[1], 'y3': pts[3], 'y5': pts[5], 'y10': pts[10]})

# 实战加成对比（统率成长，假定常年带兵：每天 +8 统率经验）
combat_rows = []
for key, name in [('vanilla', '现状'), ('A', '方案A 抑制')]:
    for s in [40, 70, 100]:
        curve = sim(s, s, 'combat_A' if key == 'A' else 'combat_vanilla', years=10, combat_exp=8.0, combat_target=s)
        pts = {y: c for y, p, g, c in curve}
        combat_rows.append({'name': name, 'start': s, 'y1': pts[1], 'y5': pts[5], 'y10': pts[10]})

# 不同起始值达到 +50 政治所需天数
days_rows = []
for key, name in [('vanilla', '现状'), ('A', '方案A')]:
    for s in [30, 50, 70, 90]:
        days_rows.append({'name': name, 'start': s, 'days': days_to_gain(s, key, 50)})

print('=== 内政成长（政治，单城，rate=1） ===')
print(f"{'方案':<12}{'起始':>5}{'1年':>7}{'3年':>7}{'5年':>7}{'10年':>7}")
for r in rows:
    print(f"{r['name']:<12}{r['start']:>5}{r['y1']:>7}{r['y3']:>7}{r['y5']:>7}{r['y10']:>7}")

print('\n=== 实战成长（统率，常年带兵 +8 经验/天） ===')
for r in combat_rows:
    print(f"{r['name']:<10}{r['start']:>5}{r['y1']:>7}{r['y5']:>7}{r['y10']:>7}")

print('\n=== 从起始值涨 50 点政治所需天数 ===')
for r in days_rows:
    print(f"{r['name']:<8}{r['start']:>5}  {r['days']} 天")

json.dump({'internal': rows, 'combat': combat_rows, 'days': days_rows},
          io.open(os.path.join(os.path.dirname(__file__), 'sim_growth.json'), 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)

# ---- SVG 折线图：政治 vs 年份 ----
def svg_curves(path):
    W, H, L, R, T, B = 900, 520, 70, 30, 40, 60
    series = []
    colors = {'vanilla': '#888', 'A': '#1f77b4', 'B': '#d62728', 'AB': '#2ca02c'}
    labels = {'vanilla': '现状', 'A': '方案A 抑制', 'B': '方案B 饱和', 'AB': 'A+B'}
    for key, _ in SCHEMES:
        for s in [40, 100]:
            curve = sim(s, s, key, years=10)
            pts = [(y, p) for y, p, g, c in curve]
            series.append((f'{labels[key]} · 起始{s}', pts, colors[key], '-' if s == 40 else '--'))
    maxy = 420
    def X(y): return L + (W - L - R) * (y / 10.0)
    def Y(v): return H - B - (H - T - B) * (v / maxy)
    parts = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" font-family="Microsoft YaHei,sans-serif" font-size="12">',
             f'<rect width="{W}" height="{H}" fill="white"/>']
    for gv in range(0, maxy + 1, 50):
        parts.append(f'<line x1="{L}" y1="{Y(gv):.1f}" x2="{W-R}" y2="{Y(gv):.1f}" stroke="#eee"/>')
        parts.append(f'<text x="{L-10}" y="{Y(gv)+4:.1f}" text-anchor="end" fill="#666">{gv}</text>')
    for yr in range(0, 11):
        parts.append(f'<text x="{X(yr):.1f}" y="{H-B+20}" text-anchor="middle" fill="#666">{yr}</text>')
    for i, (name, pts, color, dash) in enumerate(series):
        d = ' '.join(('M' if i2 == 0 else 'L') + f'{X(y):.1f},{Y(v):.1f}' for i2, (y, v) in enumerate(pts))
        parts.append(f'<path d="{d}" fill="none" stroke="{color}" stroke-width="2" stroke-dasharray="{dash}"/>')
        ly = T + 8 + 20 * i
        parts.append(f'<text x="{W-R-300}" y="{ly}" fill="{color}">{name}</text>')
    parts.append(f'<text x="{(L+W-R)/2}" y="{H-15}" text-anchor="middle">年数</text>')
    parts.append(f'<text x="18" y="{(T+H-B)/2}" transform="rotate(-90 18 {(T+H-B)/2})" text-anchor="middle">政治</text>')
    parts.append('</svg>')
    io.open(path, 'w', encoding='utf-8').write('\n'.join(parts))

svg_curves(os.path.join(os.path.dirname(__file__), 'growth_curves.svg'))
print('\n已输出 growth_curves.svg / sim_growth.json')
