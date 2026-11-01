# -*- coding: utf-8 -*-
"""五维成长曲线模拟 v2（按“每日经验预算”建模，便于横向比较三种改造方案）
- 现状: 属性 = (exp/1000)^0.9，经验产出正比于当前属性（强者恒强），硬上限 400
- 方案A: 经验增量 × clamp((400-属性)/150, 0.15, 2)
- 方案B: 属性 = 400*(1-exp(-exp/60000))（饱和曲线）
- 方案C: 方案A + 按官爵软上限（无爵250/县令270/太守300/州牧330/公360/王400），超过软上限后经验增量 ×0.2
用法: python sim_growth.py [--md 输出.md]
"""
import math, random, io, os, sys

random.seed(20261101)
CAP = 400.0
MAXEXP = 300000.0
DAYS = 360

SCHEMES = ['现状', '方案A', '方案B', '方案C', '方案D']
PROFILES = [('种田', 1.5), ('均衡', 5.0), ('征战', 15.0)]
STARTS = [40, 70, 100]
SOFT = 250.0

def conv(exp, scheme):
    if scheme in ('现状', '方案A', '方案C'):
        return int((exp / 1000.0) ** 0.9)
    return int(min(CAP, CAP * (1 - math.exp(-exp / 60000.0))))

def rand_val(a, b):
    n, r = divmod(int(a), int(b))
    return n + (1 if r and random.randrange(int(b)) < r else 0)

def factor(attr, scheme, soft=SOFT):
    f = 1.0
    if scheme == '方案D':
        return max(0.05, 2.2 * (CAP - attr) / CAP)
    if scheme in ('方案A', '方案C'):
        f = max(0.15, min(2.0, (CAP - attr) / 150.0))
    if scheme == '方案C' and attr > soft:
        f *= 0.2
    return f

def sim(start, daily, scheme, years):
    e = 0.0
    out = []
    for y in range(1, years + 1):
        for _ in range(DAYS):
            attr = min(CAP, start + conv(e, scheme))
            # 经验产出正比于当前属性：attr=100 时每天约 daily 点
            gain = daily if scheme == '方案D' else daily * (max(attr, 20) / 100.0)
            e = min(MAXEXP, e + gain * factor(attr, scheme))
        out.append((y, min(CAP, start + conv(e, scheme))))
    return out

def table(years):
    rows = []
    for scheme in SCHEMES:
        for pname, daily in PROFILES:
            for s in STARTS:
                c = dict(sim(s, daily, scheme, years))
                rows.append((scheme, pname, s, c.get(5), c.get(10), c.get(years)))
    return rows

if __name__ == '__main__':
    md = '--md' in sys.argv
    lines = []
    def p(s=''):
        print(s); lines.append(s)
    p('# 五维成长模拟结果')
    p()
    p('模型：经验产出正比于当前属性；"种田/均衡/征战"= 每天折算 1.5/5/15 点经验（attr=100 时）。')
    for years in (10, 20):
        p()
        p(f'## {years} 年后属性（起始 40/70/100）')
        p()
        p('| 方案 | 画像 | 起始40 | 起始70 | 起始100 |')
        p('|---|---|---|---|---|')
        for scheme, pname, s, v5, v10, vN in table(years):
            if s != 40:
                continue
            # 需要三个起始值，重算
            a = dict(sim(40, dict(PROFILES)[pname], scheme, years))[years]
            b = dict(sim(70, dict(PROFILES)[pname], scheme, years))[years]
            c = dict(sim(100, dict(PROFILES)[pname], scheme, years))[years]
            p(f'| {scheme} | {pname} | {a} | {b} | {c} |')
    p()
    p('## 关键对照：低属性追赶（征战画像，20 年）')
    p()
    for scheme in SCHEMES:
        a = dict(sim(40, 15.0, scheme, 20))[20] - 40
        b = dict(sim(100, 15.0, scheme, 20))[20] - 100
        p(f'- {scheme}: 起始40 → +{a:.0f}；起始100 → +{b:.0f}；差距变化 {"缩小" if a > b else "扩大"}（{a-b:+.0f}）')
    if md:
        io.open('sim_growth_out.md', 'w', encoding='utf-8').write('\n'.join(lines))
        print('\n已写 sim_growth_out.md')
