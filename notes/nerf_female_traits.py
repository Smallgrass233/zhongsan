# -*- coding: utf-8 -*-
"""女性武将特性/称号清理，按“现实合理性”规则：

- 悬空引用（ID 不在 CommonData 的称号/技能/特技表里）→ 全部删除（男女都清）。
- 仅对女性（Sex == true）：
  * 称号：删除 Lv >= 8 的幻想类称号；删除「名将」这类凭军功获得的高阶称号（女性一律不留）；
    只保留 Lv <= 3 的身份类称号，最多 2 个。
  * 技能：裁到最多 8 个（保留原有顺序）。
  * 特技：裁到最多 2 个（保留原有顺序）。
  * 五维：总和压到 <= 330，魅力 <= 95，等比缩放。

用法：
  python nerf_female_traits.py --dry        # 只统计不写
  python nerf_female_traits.py --apply      # 备份后写回
"""
import glob
import json
import os
import shutil
import sys
import time

GAME = r"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release"
COMMON = os.path.join(GAME, r"Content\Data\Common\CommonData.json")
SCEN_DIR = os.path.join(GAME, r"Content\Data\Scenario")
SAVE_DIR = r"D:\Documents\WorldOfTheThreeKingdoms\Save"
BACKUP = r"D:\re\zhongsan\backup\traits"

CAP_ABIL = 330
CAP_GLAMOUR = 95
MAX_SKILLS = 8
MAX_STUNTS = 2
MAX_TITLES = 2
TITLE_LV_KEEP = 3
BANNED_TITLE_NAMES = {"名将"}


def load(p):
    t = open(p, encoding="utf-8").read().strip()
    if t[:2] == "\x1f\x8b":
        return None
    t = t.replace("\r", "").replace("\n", "")
    return json.loads(t)


def build_tables():
    c = json.load(open(COMMON, encoding="utf-8"))
    titles = {}
    for e in c["AllTitles"]["Titles"]:
        t = e["Value"]
        titles[t["ID"]] = (t.get("Name", ""), t.get("Level", 0))
    skills = {e["Value"]["ID"]: e["Value"].get("Name", "") for e in c["AllSkills"]["Skills"]}
    stunts = {e["Value"]["ID"]: e["Value"].get("Name", "") for e in c["AllStunts"]["Stunts"]}
    infl = {e["Value"]["ID"]: e["Value"] for e in c["AllInfluences"]["Influences"]}
    combat = {}
    for e in c["AllTitles"]["Titles"]:
        t = e["Value"]
        n = 0
        for tok in (t.get("InfluencesString") or "").split():
            v = infl.get(int(tok))
            if v and (v.get("Kind") or {}).get("Combat"):
                n += 1
        combat[t["ID"]] = n
    return titles, skills, stunts, combat


def ids(s):
    out = []
    for tok in (s or "").replace("\n", " ").replace("\r", " ").replace("\t", " ").split(" "):
        tok = tok.strip()
        if tok:
            try:
                out.append(int(tok))
            except ValueError:
                pass
    return out


def join_ids(v):
    return " ".join(str(i) for i in v) + (" " if v else "")


def nerf_person(p, titles, skills, stunts, combat, stats):
    changed = []

    # 1) 悬空引用：所有人物都清
    for field, table, kind in (("RealTitlesString", titles, "title"),
                               ("SkillsString", skills, "skill"),
                               ("StuntsString", stunts, "stunt")):
        cur = ids(p.get(field))
        keep = [i for i in cur if i in table]
        if len(keep) != len(cur):
            stats["dangling_" + kind] += len(cur) - len(keep)
            p[field] = join_ids(keep)
            changed.append("清除悬空" + kind)

    if not p.get("Sex"):
        return changed

    # 2) 称号：压掉幻想级（Lv>=8）、没有军功支撑的「名将」、以及高阶战斗称号
    cmd = int(p.get("BaseCommand", 0) or 0)
    mil = cmd + int(p.get("BaseStrength", 0) or 0)
    tids = ids(p.get("RealTitlesString"))
    keep = []
    for tid in tids:
        name, lv = titles[tid]
        if lv >= 8:
            stats["drop_title_lv8"] += 1
            continue
        if name in BANNED_TITLE_NAMES and not (cmd >= 80 and int(p.get("BaseStrength", 0) or 0) >= 75):
            stats["drop_title_named"] += 1
            continue
        if lv >= 6 and combat.get(tid, 0) > 0 and mil < 150:
            stats["drop_title_mid"] += 1
            continue
        keep.append(tid)
    if len(keep) > MAX_TITLES:
        stats["drop_title_count"] += len(keep) - MAX_TITLES
        keep = sorted(keep, key=lambda i: -titles[i][1])[:MAX_TITLES]
    if keep != tids:
        p["RealTitlesString"] = join_ids(keep)
        changed.append("称号→%s" % (keep or "无"))

    # 3) 技能
    sids = ids(p.get("SkillsString"))
    if len(sids) > MAX_SKILLS:
        stats["trim_skills"] += len(sids) - MAX_SKILLS
        sids = sids[:MAX_SKILLS]
        p["SkillsString"] = join_ids(sids)
        changed.append("技能→%d" % len(sids))

    # 4) 特技
    uids = ids(p.get("StuntsString"))
    if len(uids) > MAX_STUNTS:
        stats["trim_stunts"] += len(uids) - MAX_STUNTS
        uids = uids[:MAX_STUNTS]
        p["StuntsString"] = join_ids(uids)
        changed.append("特技→%d" % len(uids))

    # 5) 五维
    fields = ["BaseCommand", "BaseStrength", "BaseIntelligence", "BasePolitics", "BaseGlamour"]
    dims = [int(p.get(f, 0) or 0) for f in fields]
    if dims[4] > CAP_GLAMOUR:
        dims[4] = CAP_GLAMOUR
        stats["cap_glamour"] += 1
    total = sum(dims)
    if total > CAP_ABIL:
        f = CAP_ABIL / float(total)
        dims = [max(5, int(d * f)) for d in dims]
        while sum(dims) > CAP_ABIL:
            dims[dims.index(max(dims))] -= 1
        stats["cap_abil"] += 1
    for f, v in zip(fields, dims):
        if int(p.get(f, 0) or 0) != v:
            p[f] = v
            changed.append("%s→%d" % (f, v))

    return changed


def iter_files():
    for p in sorted(glob.glob(os.path.join(SCEN_DIR, "*.json"))):
        yield p, "剧本"
    for p in sorted(glob.glob(os.path.join(SAVE_DIR, "*.json"))):
        yield p, "存档"


def main():
    apply = "--apply" in sys.argv
    titles, skills, stunts, combat = build_tables()
    stats = dict.fromkeys(["files", "persons_female", "touched", "dangling_title", "dangling_skill",
                           "dangling_stunt", "drop_title_lv8", "drop_title_named", "drop_title_mid",
                           "drop_title_count", "trim_skills", "trim_stunts", "cap_glamour", "cap_abil"], 0)
    os.makedirs(BACKUP, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    lines = []
    for path, kind in iter_files():
        data = load(path)
        if data is None:
            lines.append("跳过(gzip) %s" % path)
            continue
        if not isinstance(data, dict) or "Persons" not in data:
            lines.append("跳过(非剧本/存档) %s" % os.path.basename(path))
            continue
        stats["files"] += 1
        persons = data.get("Persons") or []
        if isinstance(persons, dict):
            persons = persons.get("GameObjects") or []
        stats["persons_female"] += sum(1 for p in persons if p.get("Sex"))
        touched = []
        for p in persons:
            ch = nerf_person(p, titles, skills, stunts, combat, stats)
            if ch:
                name = (p.get("SurName") or "") + (p.get("GivenName") or "")
                touched.append("%s(%s): %s" % (name, p.get("ID"), "，".join(ch)))
        if touched:
            stats["touched"] += 1
            lines.append("[%s] %s 改动 %d 人" % (kind, os.path.basename(path), len(touched)))
            lines.extend("    " + t for t in touched[:60])
            if len(touched) > 60:
                lines.append("    …另有 %d 人" % (len(touched) - 60))
        if apply and touched:
            bak = os.path.join(BACKUP, "%s.%s.json" % (os.path.splitext(os.path.basename(path))[0], stamp))
            shutil.copy2(path, bak)
            tmp = path + ".tmp"
            with open(tmp, "w", encoding="utf-8", newline="\n") as f:
                json.dump(data, f, ensure_ascii=False, separators=(",", ":"))
            os.replace(tmp, path)
    lines.append("")
    lines.append("统计: " + json.dumps(stats, ensure_ascii=False))
    out = "\n".join(lines)
    print(out)
    rp = r"D:\re\zhongsan\notes\nerf_report_%s.txt" % stamp
    open(rp, "w", encoding="utf-8").write(out)
    print("\n报告: " + rp)


if __name__ == "__main__":
    main()
