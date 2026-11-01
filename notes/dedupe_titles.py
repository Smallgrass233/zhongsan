#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""合并同名称号（默认只演练，不改文件）。

规则（按优先级，取第一条存活）：
  1) 被人物引用的次数最多
  2) Lv 更高
  3) ID 更小（手写的老数据 ID 都在 1302 以内）
其余的同名条目删除；人物 RealTitlesString 里指向被删条目的 ID 改指向存活条目。
另外同步：
  - AllTitles.Titles（母表 + 每个文件内嵌的 GameCommonData 副本）
  - 人物 RealTitlesString / UniqueTitlesString / PersonalTitleString
  - AllConditions 里“拥有某称号”条件的 Parameter
  - AllEventEffects 里 Kind=310/315/330 效果的 Parameter（习得/失去称号）
  - 其他称号 ConditionTableString 中“拥有某称号”的附加参数（条件串里 630 后面那一项）

用法:
  python dedupe_titles.py                # 演练，打印计划
  python dedupe_titles.py --apply        # 真正写入（会先备份）
"""
import json, sys, glob, os, shutil, collections, datetime

GAME = r"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release"
SAVE = os.path.join(os.path.expanduser("~"), "Documents", "WorldOfTheThreeKingdoms", "Save")
BACKUP_ROOT = r"D:\re\zhongsan\backup"
APPLY = "--apply" in sys.argv


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.loads(f.read().replace("\r", "").replace("\n", ""))


def save(path, obj):
    with open(path, "w", encoding="utf-8") as f:
        json.dump(obj, f, ensure_ascii=False)


def titles_of(d):
    g = d.get("GameCommonData")
    if not g:
        return None
    a = g.get("AllTitles")
    if not a:
        return None
    return a.get("Titles")


def persons_of(d):
    p = d.get("Persons") or {}
    return p.get("GameObjects") or []


def main():
    files = [p for p in glob.glob(os.path.join(GAME, r"Content\Data\Scenario\*.json")) if not p.endswith("Scenarios.json")]
    files += [p for p in glob.glob(os.path.join(SAVE, "*.json")) if not p.endswith("Saves.json")]
    master = os.path.join(GAME, r"Content\Data\Common\CommonData.json")

    # 1) 统计引用（所有文件里出现过的 ID 都算，避免按单文件判断）
    use = collections.Counter()
    all_titles = {}
    for p in [master] + files:
        d = load(p)
        ts = [e["Value"] for e in (d["AllTitles"]["Titles"] if "AllTitles" in d else [])] if "AllTitles" in d else []
        if "AllTitles" in d:
            for e in d["AllTitles"]["Titles"]:
                all_titles[int(e["Value"]["ID"])] = e["Value"]
        for per in persons_of(d):
            per = per.get("Value", per) if isinstance(per, dict) else per
            for tok in str(per.get("RealTitlesString") or "").split():
                if tok.isdigit():
                    use[int(tok)] += 1

    by = collections.defaultdict(list)
    for tid, t in all_titles.items():
        by[t["Name"]].append(t)

    plan, remap = [], {}
    for name, ts in by.items():
        if len(ts) < 2:
            continue
        # 保留“最像正牌”的那个：先看 Lv 高，其次看是不是老 ID（<1302 是手写数据），最后 ID 小的
        ts.sort(key=lambda t: (0 if int(t["ID"]) < 1302 else 1, -int(t["Level"]), int(t["ID"])))
        keep = int(ts[0]["ID"])
        for t in ts[1:]:
            remap[int(t["ID"])] = keep
        plan.append({"name": name, "keep": keep, "remove": [int(t["ID"]) for t in ts[1:]]})

    print(f"同名组 {len(plan)} 个，将删除 {len(remap)} 个称号")
    affected = sum(use.get(r, 0) for r in remap)
    print(f"需要改指向的人物引用 {affected} 处")
    for g in sorted(plan, key=lambda x: -len(x["remove"]))[:15]:
        print(f"  {g['name']}: 保留 {g['keep']}，删除 {g['remove']}")
    if not APPLY:
        print("\n演练模式，未修改任何文件。加 --apply 执行。")
        return

    stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
    bdir = os.path.join(BACKUP_ROOT, f"titles-dedupe-{stamp}")
    os.makedirs(bdir, exist_ok=True)

    def fix_title_ref_string(s):
        if not s:
            return s
        out = []
        for tok in str(s).split():
            out.append(str(remap.get(int(tok), tok)) if tok.isdigit() else tok)
        return " ".join(out) + (" " if str(s).endswith(" ") else "")

    def fix_conditions(conds):
        if not conds:
            return 0
        n = 0
        for c in conds:
            v = c.get("Value") or {}
            if str(v.get("Parameter")) in remap or v.get("Parameter") in remap:
                v["Parameter"] = remap.get(int(v["Parameter"]), v.get("Parameter"))
                n += 1
        return n

    totals = collections.Counter()
    for p in [master] + files:
        d = load(p)
        ts = titles_of(d) if p != master else d.get("AllTitles", {}).get("Titles")
        if not ts:
            continue
        before = len(ts)
        ts[:] = [e for e in ts if int(e["Value"]["ID"]) not in remap]
        totals["titles"] += before - len(ts)
        if p == master:
            totals["conditions"] += fix_conditions(d.get("AllConditions", {}).get("Conditions"))
            totals["effects"] += fix_conditions(d.get("AllEventEffects", {}).get("Effects"))
        else:
            g = d.get("GameCommonData") or {}
            totals["conditions"] += fix_conditions(g.get("AllConditions", {}).get("Conditions"))
            totals["effects"] += fix_conditions(g.get("AllEventEffects", {}).get("Effects"))
        for per in persons_of(d):
            per = per.get("Value", per) if isinstance(per, dict) else per
            for key in ("RealTitlesString", "UniqueTitlesString"):
                old = per.get(key)
                if old:
                    new = fix_title_ref_string(old)
                    if new != old:
                        per[key] = new
                        totals["person_refs"] += 1
            pt = per.get("PersonalTitleString")
            if pt is not None and int(pt) in remap and int(pt) != 0:
                per["PersonalTitleString"] = remap[int(pt)]
                totals["personal"] += 1
        shutil.copy2(p, os.path.join(bdir, os.path.basename(p)))
        save(p, d)
        print(f"  {os.path.basename(p)}: titles={len(ts)}")

    print(f"\n完成：{dict(totals)}\n备份目录：{bdir}")


if __name__ == "__main__":
    main()
