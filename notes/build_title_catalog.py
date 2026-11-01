# -*- coding: utf-8 -*-
"""称号成就候选目录 v3

v3 相对 v2：
- 修正 v2 生成的页面在部分环境下筛选失效/乱码的问题：行改为服务端直出，过滤全部基于 data-* 属性；
- 建议条件按 Lv 分级（Lv 越高要求越高），不再出现 Lv24 只要统率 55 这种；
- 标出建议删除的恶搞/穿越/幻想称号（默认红字+删除线），可一键导出删除清单；
- 现有条件只显示真实条件（900/901/902 这类占位单独标注为“无法习得/无法出生”）。
"""
import json, re, html, collections

GAME = r"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release"
COMMON = GAME + r"\Content\Data\Common\CommonData.json"
OUT = r"D:\re\zhongsan\notes\称号成就候选目录.html"
CSV = r"D:\re\zhongsan\notes\称号成就候选目录.csv"

def load(p):
    return json.loads(open(p, encoding="utf-8").read().replace("\r", "").replace("\n", ""))

common = load(COMMON)
titles = [e["Value"] for e in common["AllTitles"]["Titles"]]
influ = {e["Value"]["ID"]: e["Value"] for e in common["AllInfluences"]["Influences"]}
conds = {e["Value"]["ID"]: e["Value"]["Name"] for e in common["AllConditions"]["Conditions"]}

# ---------- 建议条件：按 Lv 分级 ----------
ATTR = {
    "武勇": {60: 149, 75: 150, 85: 151, 90: 152, 95: 153, 100: 154},
    "统率": {60: 155, 70: 157, 75: 158, 80: 160, 85: 161, 90: 162, 95: 163, 100: 164},
    "智谋": {70: 165, 75: 166, 80: 170, 85: 171, 90: 172, 95: 173, 100: 174},
    "政治": {70: 179, 80: 180, 85: 181, 90: 182, 95: 183, 100: 184},
    "魅力": {70: 195, 80: 190, 85: 191, 90: 192, 95: 193, 100: 194},
}
JIBAI = {10: 550, 20: 551, 30: 552, 40: 553, 50: 554, 60: 555}
GONGJI = {1000: 7300, 2000: 7301, 3000: 7302, 4000: 7303, 5000: 7304, 8000: 7307, 10000: 7309, 15000: 7310}
MINGSHENG = {1000: 300, 2000: 301, 3000: 302, 4000: 303, 5000: 304, 6000: 305}

def nearest(table, target):
    ok = [k for k in table if k >= target]
    if ok:
        return table[min(ok, key=lambda k: (k - target, k))]
    return table[max(table)]

def reqs(lv):
    if lv <= 3:   return 70,  0,  1000, 1000, 20
    if lv <= 6:   return 75, 10,  2000, 1000, 60
    if lv <= 9:   return 80, 20,  3000, 2000, 120
    if lv <= 12:  return 85, 30,  5000, 3000, 200
    if lv <= 16:  return 90, 40,  8000, 5000, 300
    if lv <= 20:  return 95, 50, 10000, 6000, 400
    return 100, 60, 15000, 8000, 500

# ---------- 类型判定 ----------
MIL = ["攻", "防", "战意", "混乱", "伤", "兵", "骑", "弓", "枪", "士气", "连击", "奇袭", "伏兵", "攻城", "守城", "击破", "武勇"]
CIV = ["农业", "商业", "技术", "内政", "民心", "治安", "城", "金", "粮", "人口", "建设", "指导", "政治"]
INT = ["计略", "谋略", "智力", "外交", "离间", "伪报", "侦察", "看破", "连环", "智谋"]

def ftype(inf_objs):
    s = "".join(i["Name"] + (i.get("Description") or "") for i in inf_objs)
    mil = sum(s.count(k) for k in MIL)
    civ = sum(s.count(k) for k in CIV)
    inte = sum(s.count(k) for k in INT)
    if mil >= civ and mil >= inte and mil:
        return "战功"
    if inte > civ and inte:
        return "谋略"
    return "内政"

def power_of(inf_objs, lv):
    p = len(inf_objs)
    for i in inf_objs:
        d = i.get("Description", "") or ""
        nums = [int(x) for x in re.findall(r"\d+", d)]
        if nums:
            p += 1 + min(3, max(nums) / 40)
        if "战法" in d or "『" in d:
            p += 0.5
    return round(p + lv * 0.6, 1)

# ---------- 建议删除名单（恶搞/穿越/幻想；鬼神之勇是正常称号，保留） ----------
DELETE_IDS = {
    12002: "琅琊孔萌（谐音梗）", 12013: "天下一枝梅（恶搞）", 12014: "九天玄女（幻想）",
    12669: "大妖术（幻想）", 12736: "恶魔军神（幻想）", 12763: "洞悉恶魔（幻想）",
    13006: "盗梦（科幻）", 13008: "诸葛再世（穿越）", 13011: "天下一枝梅（恶搞）",
    13016: "天才的世界（现代）", 13022: "九天玄女（幻想）", 13024: "校草（现代）",
    13025: "小三（现代）", 13030: "英明神武（恶搞）", 13031: "疯狂的人生（现代）",
    13035: "稻荷大神（日本神）", 13043: "木鱼法身（恶搞）", 13045: "乌江不自刎（恶搞）",
    13054: "天下一枝梅（恶搞）", 13058: "水魔（恶搞）", 13060: "破铜烂铁（恶搞）",
    10032: "妖后降临（幻想·Lv12）", 10999: "妖王降臨（幻想·Lv24）",
    11668: "恶魔天降（幻想）", 11940: "消兵大妖法（幻想）", 11946: "攻心大妖法（幻想）",
    11949: "夺兵大妖法（幻想）", 11952: "夺伤大妖法（幻想）", 11984: "混乱大妖法（幻想）",
}
FANTASY_KW = ["妖王", "妖后", "妖术", "妖法", "大妖", "恶魔", "九天玄女", "盗梦", "魔军", "魔天"]
JOKE_KW = ["校草", "小三", "天才的世界", "疯狂的人生", "水魔", "破铜烂铁", "木鱼法身",
           "稻荷大神", "天下一枝梅", "乌江不自刎", "诸葛再世", "英明神武", "琅琊孔萌"]

def parse_ids(s):
    return [int(x) for x in str(s or "").replace("\xa0", " ").split() if x.isdigit()]

rows = []
for t in titles:
    cids = parse_ids(t.get("ConditionTableString"))
    if 900 not in cids:
        continue
    inf_ids = parse_ids(t.get("InfluencesString"))
    objs = [influ[i] for i in inf_ids if i in influ]
    lv = int(t.get("Level") or 1)
    kind = ftype(objs)
    real = [c for c in cids if c not in (900, 901, 902)]
    real_text = "、".join(conds.get(c, str(c)) for c in real) or "（无真实条件）"
    attr_t, jb, gj, ms, auto = reqs(lv)
    ci = []
    if kind == "战功":
        main = "武勇" if any("武勇" in (o["Name"] + (o.get("Description") or "")) for o in objs) else "统率"
        ci.append(nearest(ATTR[main], attr_t))
        if main != "统率":
            ci.append(nearest(ATTR["统率"], max(60, attr_t - 10)))
        if jb:
            ci.append(nearest(JIBAI, jb))
    elif kind == "谋略":
        ci.append(nearest(ATTR["智谋"], attr_t))
        ci.append(nearest(ATTR["政治"], max(70, attr_t - 10)))
    else:
        ci.append(nearest(ATTR["政治"], attr_t))
        ci.append(nearest(ATTR["魅力"], max(70, attr_t - 10)))
    ci.append(nearest(GONGJI, gj))
    ci.append(nearest(MINGSHENG, ms))
    ci = list(dict.fromkeys(ci))
    rows.append(dict(
        id=t["ID"], name=t["Name"], lv=lv, kind=kind, power=power_of(objs, lv),
        effects=[f'{o["Name"]}：{o.get("Description") or ""}'.strip("：") for o in objs],
        cur=real_text, has_real=bool(real),
        cond_ids=ci, cond_text="、".join(conds.get(c, str(c)) for c in ci), auto=auto,
        delete=DELETE_IDS.get(t["ID"], ""),
        variant="" ,
    ))

name_count = collections.Counter(r["name"] for r in rows)
for r in rows:
    if name_count[r["name"]] > 1:
        r["variant"] = "同名×%d" % name_count[r["name"]]

# 档位按强度
ranked = sorted(rows, key=lambda r: -(r["power"] + r["lv"] * 1.5))
n = len(ranked)
cut = {"传说": 0.08, "史诗": 0.23, "金": 0.48, "银": 0.78}
for i, r in enumerate(ranked):
    q = i / n
    r["tier"] = "传说" if q < cut["传说"] else "史诗" if q < cut["史诗"] else "金" if q < cut["金"] else "银" if q < cut["银"] else "铜"

# 档位还要受 Lv 约束：Lv2 的称号不可能靠“效果强”就变成传说
ORDER = ["传说", "史诗", "金", "银", "铜"]
for r in rows:
    lv_cap = "传说" if r["lv"] >= 12 else "史诗" if r["lv"] >= 9 else "金" if r["lv"] >= 6 else "银" if r["lv"] >= 4 else "铜"
    r["tier"] = ORDER[max(ORDER.index(r["tier"]), ORDER.index(lv_cap))]

rows.sort(key=lambda r: (0 if r["delete"] else 1, {"传说": 0, "史诗": 1, "金": 2, "银": 3, "铜": 4}[r["tier"]], -r["power"], r["name"]))
tier_cnt = collections.Counter(r["tier"] for r in rows)
del_cnt = sum(1 for r in rows if r["delete"])

def esc(s):
    return html.escape(str(s))

body = []
for r in rows:
    eff_full = "<br>".join(esc(e) for e in r["effects"]) or "（无效果条目）"
    cls = "tier-" + r["tier"] + (" todel" if r["delete"] else "")
    tags = []
    if r["delete"]:
        tags.append(f'<span class=del>建议删除：{esc(r["delete"])}</span>')
    else:
        tags.append('<span class=keep>保留改造</span>')
    if r["variant"]:
        tags.append(f'<span class=fl>{esc(r["variant"])}</span>')
    if not r["has_real"]:
        tags.append('<span class=fl>现在无真实条件</span>')
    body.append(
        f'<tr class="{cls}" data-tier="{esc(r["tier"])}" data-kind="{esc(r["kind"])}" data-del="{1 if r["delete"] else 0}" '
        f'data-name="{esc(r["name"])}" data-id="{r["id"]}" data-lv="{r["lv"]}">'
        f'<td><input type=checkbox class=pick data-id="{r["id"]}" data-del="{1 if r["delete"] else 0}"'
        f'{" checked" if r["delete"] else ""}></td>'
        f'<td>{r["id"]}</td><td class=nm>{esc(r["name"])}</td><td>{r["lv"]}</td>'
        f'<td class=tt>{esc(r["tier"])}</td><td>{esc(r["kind"])}</td><td class=pw>{r["power"]}</td>'
        f'<td class=eff><details><summary>{len(r["effects"])} 条</summary>{eff_full}</details></td>'
        f'<td class=cur>{esc(r["cur"])}</td><td class=cond><b>{esc(r["cond_text"])}</b></td><td>{r["auto"]}</td>'
        f'<td>{" ".join(tags)}</td></tr>')

legend = [
    ("传说", "效果最强的 8%：Lv21+ 需主属性 100 + 击破 60 + 功绩 15000 + 名声 8000；没有真实条件的一律先按删除处理"),
    ("史诗", "前 23%：主属性 85–100 + 击破 30–50 + 功绩 5000–10000 + 名声 3000–6000"),
    ("金", "前 48%：主属性 80–90 + 击破 20–40 + 功绩 3000–8000 + 名声 2000–5000"),
    ("银", "前 78%：主属性 75–85 + 击破 10–30 + 功绩 2000–5000 + 名声 1000–3000"),
    ("铜", "其余：主属性 70 + 功绩 1000 + 名声 1000"),
]

page = f"""<!doctype html><html lang=zh><head><meta charset=utf-8>
<title>称号成就候选目录 v3.1（{len(rows)} 个候选）</title>
<style>
body{{font-family:"Microsoft YaHei",system-ui;margin:0;background:#f6f4ef;color:#20201d;font-size:13px}}
header{{padding:16px 22px;background:#3a2f24;color:#f6f4ef}} h1{{margin:0 0 6px;font-size:19px}}
.sub{{opacity:.8;font-size:12px;line-height:1.7}}
.bar{{position:sticky;top:0;background:#fffdf8;border-bottom:1px solid #ddd6c8;padding:8px 22px;display:flex;gap:8px;flex-wrap:wrap;align-items:center;z-index:5}}
input[type=search]{{padding:5px 9px;border:1px solid #cfc7b6;border-radius:6px;width:200px}}
select,button{{padding:5px 10px;border:1px solid #b9a98c;background:#efe7d6;border-radius:6px;cursor:pointer}}
button:hover{{background:#e3d7bf}}
table{{border-collapse:collapse;width:100%;background:#fffdf8}}
th,td{{border-bottom:1px solid #eee6d7;padding:5px 7px;text-align:left;vertical-align:top}}
th{{position:sticky;top:52px;background:#efe9db;z-index:4}}
tr:hover td{{background:#fbf6ea}}
.nm{{font-weight:600}} .pw{{color:#6b6255}} .cur{{color:#8a6a12;max-width:230px}} .cond{{color:#7a4f16;max-width:300px}}
.eff{{max-width:380px;color:#5c5347}} .eff summary{{cursor:pointer;color:#7a4f16}}
.tier-传说 .tt{{color:#8a2c0f;font-weight:700}} .tier-史诗 .tt{{color:#a1440f;font-weight:600}}
.tier-金 .tt{{color:#8a6a12}} .tier-银 .tt{{color:#5b6a72}} .tier-铜 .tt{{color:#7d7466}}
.todel td{{background:#fff0ef}} .todel .nm{{color:#a12f22;text-decoration:line-through}}
.fl{{display:inline-block;background:#ffe8d8;border:1px solid #e8b48c;color:#8a3a10;border-radius:4px;padding:0 5px;margin:1px;font-size:11px}}
.del{{display:inline-block;background:#fde2df;border:1px solid #e2a19a;color:#a12f22;border-radius:4px;padding:0 5px;margin:1px;font-size:11px}}
.keep{{display:inline-block;background:#e6f2e2;border:1px solid #a9cfa2;color:#3d6b34;border-radius:4px;padding:0 5px;margin:1px;font-size:11px}}
.count{{margin-left:auto;color:#5c5347;font-weight:600}}
ul.legend{{margin:6px 22px;padding-left:18px;color:#5c5347;line-height:1.7}}
</style></head><body>
<header><h1>称号成就候选目录 v3.1</h1>
<div class=sub>已于 2026-11-01 删除 29 个恶搞/穿越/幻想称号（校草、小三、妖王降臨、大妖法系列等），本目录不再列出；现存共 {len(rows)} 个条件含 900“无法习得”的称号。档位=效果强度；<b>建议条件按 Lv 分级</b>，Lv 越高要求越高；
<b>红字删除线</b>=建议直接删除的恶搞/穿越/幻想称号（{del_cnt} 个，默认勾选）。勾选后点“导出已选”即可。</div></header>
<div class=bar>
<input type=search id=q placeholder="搜称号名 / ID">
<select id=ft><option value="">全部档位</option>{''.join(f'<option>{t}</option>' for t,_ in legend)}</select>
<select id=fk><option value="">全部类型</option><option>战功</option><option>谋略</option><option>内政</option></select>
<select id=fd><option value="">全部处理</option><option value="0">保留改造</option><option value="1">建议删除</option></select>
<label><input type=checkbox id=only> 只看已勾选</label>
<button id=all>全选当前结果</button><button id=none>清空</button>
<button id=exp>导出已选</button><button id=expdel>导出删除清单</button>
<span class=count id=count></span>
</div>
<ul class=legend>{''.join(f'<li><b>{t}</b> — {d}</li>' for t,d in legend)}</ul>
<table><thead><tr><th></th><th>ID</th><th>称号</th><th>Lv</th><th>档位</th><th>类型</th><th>强度</th><th>效果（点击展开）</th><th>现有真实条件</th><th>建议条件</th><th>AutoLearn<br>1/N 天</th><th>建议</th></tr></thead>
<tbody id=tb>
{''.join(body)}
</tbody></table>
<script>
const tb=document.getElementById('tb'),q=document.getElementById('q'),ft=document.getElementById('ft'),
 fk=document.getElementById('fk'),fd=document.getElementById('fd'),only=document.getElementById('only'),count=document.getElementById('count');
function vis(){{
  let n=0;
  for(const tr of tb.rows){{
    const ok=(!q.value||tr.dataset.name.includes(q.value)||tr.dataset.id===q.value)
      &&(!ft.value||tr.dataset.tier===ft.value)
      &&(!fk.value||tr.dataset.kind===fk.value)
      &&(!fd.value||tr.dataset.del===fd.value)
      &&(!only.checked||tr.querySelector('.pick').checked);
    tr.style.display=ok?'':'none'; if(ok)n++;
  }}
  count.textContent='当前显示 '+n+' 条';
}}
[q,ft,fk,fd,only].forEach(e=>{{e.oninput=vis;e.onchange=vis;}});
vis();
document.getElementById('all').onclick=()=>{{for(const tr of tb.rows)if(tr.style.display!=='none')tr.querySelector('.pick').checked=true;vis();}};
document.getElementById('none').onclick=()=>{{for(const c of document.querySelectorAll('.pick'))c.checked=false;vis();}};
function dl(ids,name){{
  if(!ids.length){{alert('没有勾选');return}}
  const a=document.createElement('a');
  a.href=URL.createObjectURL(new Blob([JSON.stringify(ids)],{{type:'application/json'}}));
  a.download=name;a.click();
}}
document.getElementById('exp').onclick=()=>dl([...document.querySelectorAll('.pick:checked')].map(c=>+c.dataset.id),'称号成就已选.json');
document.getElementById('expdel').onclick=()=>dl([...document.querySelectorAll('.pick[data-del="1"]')].map(c=>+c.dataset.id),'称号删除清单.json');
</script></body></html>"""

open(OUT, "w", encoding="utf-8").write(page)
print("rows", len(rows), dict(tier_cnt), "delete", del_cnt, "bytes", len(page.encode("utf-8")))

# 同时导出一份 CSV：HTML 里筛选不方便时，用 Excel 打开按列筛
import csv
with open(CSV, "w", encoding="utf-8-sig", newline="") as f:
    w = csv.writer(f)
    w.writerow(["ID", "称号", "Lv", "档位", "类型", "强度", "现有真实条件", "建议条件", "AutoLearn", "效果全文", "备注"])
    for r in rows:
        w.writerow([r["id"], r["name"], r["lv"], r["tier"], r["kind"], r["power"], r["cur"],
                    r["cond_text"], r["auto"], " | ".join(r["effects"]), r["variant"] or r["delete"]])
print("csv:", CSV)
