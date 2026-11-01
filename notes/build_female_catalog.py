# -*- coding: utf-8 -*-
"""生成《中华三国志》女性武将特性总目录（HTML），供人工圈选“超标特性”。
数据源：所有剧本 JSON + 所有存档 JSON；特性定义来自 CommonData.json。"""
import json, glob, os, re, sys, html, collections

GAME = r"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release"
COMMON = os.path.join(GAME, r"Content\Data\Common\CommonData.json")
SCEN_DIR = os.path.join(GAME, r"Content\Data\Scenario")
SAVE_DIR = r"D:\Documents\WorldOfTheThreeKingdoms\Save"
OUT = r"D:\re\zhongsan\notes\女性武将特性目录.html"

C = json.load(open(COMMON, encoding="utf-8-sig"))
INF = {e["Value"]["ID"]: e["Value"] for e in C["AllInfluences"]["Influences"]}
TITLES = {e["Value"]["ID"]: e["Value"] for e in C["AllTitles"]["Titles"]}
SKILLS = {e["Value"]["ID"]: e["Value"] for e in C["AllSkills"]["Skills"]}
STUNTS = {e["Value"]["ID"]: e["Value"] for e in C["AllStunts"]["Stunts"]}


def gx(s):
    if isinstance(s, int):
        return [s]
    if not s:
        return []
    return [int(x) for x in re.split(r"[ ,\t]+", str(s).strip()) if x.strip().lstrip("-").isdigit()]


def eff(v):
    out = []
    for i in gx(v.get("InfluencesString", "")):
        e = INF.get(i)
        if e:
            out.append(e.get("Description") or e.get("Name") or "")
    return out


def load(path):
    t = open(path, encoding="utf-8-sig", errors="replace").read()
    t = t.replace("\r", "").replace("\n", "")
    return json.loads(t)


def safe_name(p):
    return (p.get("SurName") or "") + (p.get("GivenName") or "")


def person_row(p, src):
    K = ["BaseCommand", "BaseStrength", "BaseIntelligence", "BasePolitics", "BaseGlamour"]
    st = [int(p.get(k, 0) or 0) for k in K]
    tlist = []
    for tid in gx(p.get("RealTitlesString", "")):
        v = TITLES.get(tid)
        if v:
            tlist.append({"id": tid, "name": v["Name"], "lv": v.get("Level", 0), "e": eff(v)})
        else:
            tlist.append({"id": tid, "name": "ID%d(不存在)" % tid, "lv": 0, "e": []})
    slist = []
    for sid in gx(p.get("SkillsString", "")):
        v = SKILLS.get(sid)
        if v:
            slist.append({"id": sid, "name": v["Name"], "lv": v.get("Level", 0), "e": eff(v)})
        else:
            slist.append({"id": sid, "name": "ID%d(不存在)" % sid, "lv": 0, "e": []})
    stl = []
    for sid in gx(p.get("StuntsString", "")):
        v = STUNTS.get(sid)
        if v:
            stl.append({"id": sid, "name": v["Name"], "lv": v.get("Level", 0), "e": eff(v)})
        else:
            stl.append({"id": sid, "name": "ID%d(不存在)" % sid, "lv": 0, "e": []})
    return {
        "src": src,
        "id": p.get("ID"),
        "name": safe_name(p),
        "st": st,
        "ab": sum(st),
        "alive": bool(p.get("Alive")),
        "avail": bool(p.get("Available")),
        "yav": p.get("YearAvailable", 0),
        "born": p.get("YearBorn", 0),
        "dead": p.get("YearDead", 0),
        "gen": p.get("Generation", 0),
        "t": tlist,
        "s": slist,
        "u": stl,
        "bio": (p.get("BiographyString") or p.get("CalledName") or ""),
    }


files = []
for f in sorted(glob.glob(os.path.join(SCEN_DIR, "*.json"))):
    if os.path.basename(f).lower() == "scenarios.json":
        continue
    files.append(("剧本/" + os.path.basename(f), f))
for f in sorted(glob.glob(os.path.join(SAVE_DIR, "*.json"))):
    b = os.path.basename(f).lower()
    if b in ("saves.json",):
        continue
    files.append(("存档/" + os.path.basename(f), f))

rows = []
all_counts = collections.Counter()
all_counts_type = {}
dict_titles, dict_skills, dict_stunts = {}, {}, {}
seen_person = set()

for src, path in files:
    try:
        d = load(path)
    except Exception as e:
        print("SKIP", path, e, file=sys.stderr)
        continue
    P = d.get("Persons", {}).get("GameObjects", []) or []
    nfem = 0
    for p in P:
        if not p.get("Sex"):
            continue
        nfem += 1
        r = person_row(p, src)
        rows.append(r)
        key = (r["id"], r["name"])
        if key in seen_person:
            continue
        seen_person.add(key)
        for t in r["t"]:
            dict_titles.setdefault(t["id"], {"name": t["name"], "lv": t["lv"], "e": t["e"], "n": 0, "holders": []})
            dict_titles[t["id"]]["n"] += 1
            dict_titles[t["id"]]["holders"].append(r["name"])
            all_counts[("T", t["id"])] += 1
        for t in r["s"]:
            dict_skills.setdefault(t["id"], {"name": t["name"], "lv": t["lv"], "e": t["e"], "n": 0, "holders": []})
            dict_skills[t["id"]]["n"] += 1
            dict_skills[t["id"]]["holders"].append(r["name"])
            all_counts[("S", t["id"])] += 1
        for t in r["u"]:
            dict_stunts.setdefault(t["id"], {"name": t["name"], "lv": t["lv"], "e": t["e"], "n": 0, "holders": []})
            dict_stunts[t["id"]]["n"] += 1
            dict_stunts[t["id"]]["holders"].append(r["name"])
            all_counts[("U", t["id"])] += 1
    print("scanned", src, "persons", len(P), "females", nfem, file=sys.stderr)


def score(r):
    tlv = max([t["lv"] for t in r["t"]] or [0])
    s = r["ab"]
    s += 8 * max(0, tlv - 6)
    s += 4 * max(0, len(r["s"]) - 8)
    s += 6 * max(0, len(r["u"]) - 2)
    s += 3 * max(0, r["st"][4] - 90)
    return s


for r in rows:
    r["sc"] = score(r)
    tlv = max([t["lv"] for t in r["t"]] or [0])
    flags = []
    if tlv >= 8:
        flags.append("称号Lv%d" % tlv)
    if r["ab"] >= 390:
        flags.append("五维%d" % r["ab"])
    if len(r["s"]) >= 12:
        flags.append("技能%d" % len(r["s"]))
    if len(r["u"]) >= 3:
        flags.append("特技%d" % len(r["u"]))
    if r["st"][4] >= 95:
        flags.append("魅力%d" % r["st"][4])
    r["flags"] = flags

rows.sort(key=lambda r: -r["sc"])

def esc(x):
    return html.escape(str(x), quote=True)


payload = {
    "rows": rows,
    "titles": [dict(tid=k, **v) for k, v in dict_titles.items()],
    "skills": [dict(tid=k, **v) for k, v in dict_skills.items()],
    "stunts": [dict(tid=k, **v) for k, v in dict_stunts.items()],
    "files": [s for s, _ in files],
}

HTML = """<!doctype html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>中三 · 女性武将特性目录</title>
<style>
body{font:14px/1.5 "Microsoft YaHei",sans-serif;margin:0;background:#f6f7f9;color:#222}
header{background:#2c3e50;color:#fff;padding:14px 20px}
header h1{margin:0;font-size:18px}
header .sub{opacity:.75;font-size:12px;margin-top:4px}
.wrap{padding:14px 20px}
.bar{position:sticky;top:0;background:#fff;border-bottom:1px solid #ddd;padding:8px 20px;z-index:5;display:flex;gap:12px;flex-wrap:wrap;align-items:center}
input[type=text]{padding:4px 8px;border:1px solid #bbb;border-radius:4px;width:220px}
select{padding:4px;border:1px solid #bbb;border-radius:4px}
table{border-collapse:collapse;width:100%;background:#fff;font-size:13px}
th,td{border-bottom:1px solid #e6e6e6;padding:5px 8px;text-align:left;vertical-align:top}
th{background:#eef1f4;position:sticky;top:46px;cursor:pointer;white-space:nowrap}
tr:hover td{background:#fafcff}
tr.detail td{background:#fbfbf3;font-size:12px}
.tag{display:inline-block;padding:1px 6px;border-radius:10px;background:#e8eef7;margin:1px 2px;white-space:nowrap}
.tag.hot{background:#ffe0e0;color:#a00}
.tag.mid{background:#fff2cc;color:#8a6d00}
.tag.ok{background:#e2f0d9;color:#2e6b1f}
.eff{color:#555}
.mono{font-family:Consolas,monospace}
.red{color:#c00;font-weight:600}
.num{text-align:right;font-variant-numeric:tabular-nums}
h2{margin:22px 0 8px;font-size:16px}
.hint{color:#666;font-size:12px;margin:6px 0 10px}
.count{color:#888}
</style></head>
<body>
<header><h1>中三 · 女性武将特性总目录</h1>
<div class="sub">数据源：全部剧本 + 全部存档（按文件分开列）。点击表格行展开称号/技能/特技的完整效果；表头可排序。</div></header>
<div class="bar">
  <input id="q" type="text" placeholder="搜索姓名 / ID / 特性名">
  <select id="src"><option value="">全部来源</option></select>
  <label><input type="checkbox" id="onlyFlag"> 只看超标候选</label>
  <label><input type="checkbox" id="onlyFemale" checked disabled> 仅女性</label>
  <span class="count" id="cnt"></span>
</div>
<div class="wrap">
<h2>一、女性武将明细（按“超标指数”排序）</h2>
<div class="hint">超标指数 = 五维总和 + 8×(称号最高等级−6) + 4×(技能数−8) + 6×(特技数−2) + 3×(魅力−90)，只用于排序，不是游戏内数值。红旗标签=该维度明显高于常规。</div>
<table id="tbl"><thead><tr>
<th data-k="sc">指数</th><th data-k="name">姓名</th><th data-k="id">ID</th><th data-k="src">来源</th>
<th data-k="ab">五维</th><th data-k="st">统/武/智/政/魅</th><th data-k="t">称号</th><th data-k="s">技能</th><th data-k="u">特技</th><th>状态</th><th>标签</th>
</tr></thead><tbody></tbody></table>

<h2>二、女性持有的称号（按等级排序）</h2>
<div class="hint">这里列出所有女性角色实际持有的称号及效果全文；等级越高、效果条数越多通常越强。可对照“张角 天公将军 Lv10”。</div>
<table id="ttbl"><thead><tr><th>Lv</th><th>称号</th><th>ID</th><th>持有女性数</th><th>持有者</th><th>效果</th></tr></thead><tbody></tbody></table>

<h2>三、女性持有的技能</h2>
<table id="stbl"><thead><tr><th>Lv</th><th>技能</th><th>ID</th><th>持有女性数</th><th>持有者</th><th>效果</th></tr></thead><tbody></tbody></table>

<h2>四、女性持有的战斗特技</h2>
<table id="utbl"><thead><tr><th>特技</th><th>ID</th><th>持有女性数</th><th>持有者</th><th>效果</th></tr></thead><tbody></tbody></table>
</div>
<script>
const DATA = __DATA__;
function el(t,c,txt){const e=document.createElement(t);if(c)e.className=c;if(txt!==undefined)e.textContent=txt;return e;}
function tags(list,cls){return list.map(t=>`<span class="tag ${cls(t)}">${t.name}${t.lv?" Lv"+t.lv:""}</span>`).join("");}
const q=document.getElementById('q'),srcSel=document.getElementById('src'),onlyFlag=document.getElementById('onlyFlag');
DATA.files.forEach(f=>{const o=document.createElement('option');o.value=f;o.textContent=f;srcSel.appendChild(o);});
let sortKey='sc',asc=false,openRow=null;
function match(r){
  const s=(q.value||'').trim().toLowerCase();
  if(srcSel.value && r.src!==srcSel.value) return false;
  if(onlyFlag.checked && r.flags.length===0) return false;
  if(!s) return true;
  const hay=(r.name+' '+r.id+' '+r.t.map(t=>t.name).join(' ')+' '+r.s.map(t=>t.name).join(' ')+' '+r.u.map(t=>t.name).join(' ')).toLowerCase();
  return hay.includes(s);
}
function cmp(a,b){
  const k=sortKey;
  let x=a[k],y=b[k];
  if(k==='t'){x=Math.max(...a.t.map(t=>t.lv),0);y=Math.max(...b.t.map(t=>t.lv),0);}
  if(k==='s'){x=a.s.length;y=b.s.length;}
  if(k==='u'){x=a.u.length;y=b.u.length;}
  if(k==='st'){x=a.ab;y=b.ab;}
  if(typeof x==='string') return asc?x.localeCompare(y):y.localeCompare(x);
  return asc?x-y:y-x;
}
function render(){
  const tb=document.querySelector('#tbl tbody');tb.innerHTML='';
  const list=DATA.rows.filter(match).sort(cmp);
  document.getElementById('cnt').textContent='显示 '+list.length+' / '+DATA.rows.length+' 条';
  list.slice(0,400).forEach((r,i)=>{
    const tr=el('tr');
    tr.innerHTML=`<td class="num">${r.sc}</td><td><b>${r.name}</b></td><td class="mono">${r.id}</td>
      <td>${r.src}</td><td class="num">${r.ab}</td><td class="mono">${r.st.join(' ')}</td>
      <td>${tags(r.t,t=>t.lv>=8?'hot':(t.lv>=5?'mid':'ok'))}</td>
      <td class="num">${r.s.length}</td><td class="num">${r.u.length}</td>
      <td>${r.alive?(r.avail?'已登场':'未登场'):'已死'}</td>
      <td>${r.flags.map(f=>`<span class="tag hot">${f}</span>`).join('')}</td>`;
    tr.onclick=()=>{
      if(openRow){openRow.remove();openRow=null;}
      const d=el('tr','detail');const td=el('td');td.colSpan=11;
      td.innerHTML=detail(r);d.appendChild(td);tr.after(d);openRow=d;
    };
    tb.appendChild(tr);
  });
}
function detail(r){
  const sec=(title,arr)=>{
    if(!arr.length) return '';
    return `<div><b>${title}</b><ul style="margin:4px 0 8px 18px;padding:0">`+
      arr.map(t=>`<li><b>${t.name}</b>${t.lv?' Lv'+t.lv:''} <span class="count">#${t.id}</span><div class="eff">${t.e.map(x=>'· '+x).join('<br>')||'（无直接效果）'}</div></li>`).join('')+`</ul></div>`;
  };
  return `<div style="padding:4px 0 8px">
    <div class="count">来源 ${r.src} · ID ${r.id} · 生 ${r.born} 卒 ${r.dead} · 出场年 ${r.yav} · 世代 ${r.gen}</div>
    ${sec('称号',r.t)}${sec('技能',r.s)}${sec('战斗特技',r.u)}</div>`;
}
function fill(id,arr,cols){
  const tb=document.querySelector('#'+id+' tbody');tb.innerHTML='';
  arr.sort((a,b)=>(b.lv||0)-(a.lv||0)||((b.n||0)-(a.n||0))).forEach(v=>{
    const tr=el('tr');
    tr.innerHTML=`<td class="num">${v.lv||''}</td><td><b>${v.name}</b></td><td class="mono">${v.tid}</td>
      <td class="num">${v.n}</td><td>${[...new Set(v.holders)].slice(0,12).join('、')}</td>
      <td class="eff">${v.e.map(x=>'· '+x).join('<br>')||'（无直接效果）'}</td>`;
    tb.appendChild(tr);
  });
}
document.querySelectorAll('#tbl th').forEach(th=>th.onclick=()=>{
  const k=th.dataset.k;if(!k)return;
  if(sortKey===k) asc=!asc; else {sortKey=k;asc=false;}
  render();
});
[q,srcSel,onlyFlag].forEach(x=>x.addEventListener('input',render));
render();
fill('ttbl',DATA.titles);
fill('stbl',DATA.skills);
fill('utbl',DATA.stunts);
</script></body></html>"""

out = HTML.replace("__DATA__", json.dumps(payload, ensure_ascii=False, separators=(",", ":")))
open(OUT, "w", encoding="utf-8").write(out)
print("rows", len(rows), "titles", len(dict_titles), "skills", len(dict_skills), "stunts", len(dict_stunts))
print("written", OUT, os.path.getsize(OUT))
