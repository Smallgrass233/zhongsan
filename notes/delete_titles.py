# -*- coding: utf-8 -*-
"""删除恶搞/穿越/幻想称号（v3 目录里标红的那批）。

处理范围：Content\\Data\\Common\\CommonData.json（母表）+ 26 个剧本 + 20 个存档
（每个数据文件里都内嵌了一份 GameCommonData，必须逐份删，否则进游戏还在）。

对每个人物：从 RealTitlesString / PersonalTitleString / UniqueTitlesString 里剔除被删称号 ID。
已核对：条件表、事件效果表都没有引用这批 ID。

用法：
  python delete_titles.py --dry
  python delete_titles.py --apply
"""
import json, glob, os, shutil, sys, time

GAME = r"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release"
COMMON = GAME + r"\Content\Data\Common\CommonData.json"
SCEN = GAME + r"\Content\Data\Scenario"
SAVE = r"D:\Documents\WorldOfTheThreeKingdoms\Save"
BACKUP = r"D:\re\zhongsan\backup"

DELETE = {
    12002: "琅琊孔萌", 12013: "天下一枝梅", 12014: "九天玄女", 12669: "大妖术",
    12736: "恶魔军神", 12763: "洞悉恶魔", 13006: "盗梦", 13008: "诸葛再世",
    13011: "天下一枝梅", 13016: "天才的世界", 13022: "九天玄女", 13024: "校草",
    13025: "小三", 13030: "英明神武", 13031: "疯狂的人生", 13035: "稻荷大神",
    13043: "木鱼法身", 13045: "乌江不自刎", 13054: "天下一枝梅", 13058: "水魔",
    13060: "破铜烂铁", 10032: "妖后降临", 10999: "妖王降臨", 11668: "恶魔天降",
    11940: "消兵大妖法", 11946: "攻心大妖法", 11949: "夺兵大妖法",
    11952: "夺伤大妖法", 11984: "混乱大妖法",
}
TITLE_FIELDS = ("RealTitlesString", "PersonalTitleString", "UniqueTitlesString")
D = set(DELETE)


def load(p):
    return json.loads(open(p, encoding="utf-8").read().replace("\r", "").replace("\n", ""))


def dump(p, obj):
    with open(p, "w", encoding="utf-8", newline="") as f:
        json.dump(obj, f, ensure_ascii=False, separators=(",", ":"))


def files():
    out = [COMMON]
    for pat in (os.path.join(SCEN, "*.json"), os.path.join(SAVE, "*.json")):
        for p in sorted(glob.glob(pat)):
            if os.path.basename(p) not in ("Scenarios.json", "Saves.json"):
                out.append(p)
    return out


def process(path, apply):
    doc = load(path)
    common = doc if "AllTitles" in doc else (doc.get("GameCommonData") or {})
    titles = common.get("AllTitles", {}).get("Titles", [])
    removed = [t["Value"]["ID"] for t in titles if t["Value"]["ID"] in D]
    if apply and removed:
        common["AllTitles"]["Titles"] = [t for t in titles if t["Value"]["ID"] not in D]

    n_person = 0
    for e in doc.get("Persons", {}).get("GameObjects", []):
        v = e.get("Value", e)
        for fld in TITLE_FIELDS:
            s = str(v.get(fld) or "")
            if not s.strip():
                continue
            toks = [x for x in s.replace("\xa0", " ").split() if x]
            keep = [x for x in toks if not (x.isdigit() and int(x) in D)]
            if len(keep) != len(toks):
                n_person += 1
                if apply:
                    v[fld] = (" ".join(keep) + " ") if keep else ""
    return len(removed), n_person, doc


def main(apply):
    ts = time.strftime("%Y%m%d-%H%M%S")
    bdir = os.path.join(BACKUP, "titles-delete-" + ts)
    total_t = total_p = 0
    touched = []
    for p in files():
        nt, np_, _ = process(p, False)
        if apply and (nt or np_):
            os.makedirs(bdir, exist_ok=True)
            shutil.copy2(p, os.path.join(bdir, os.path.basename(p)))
            nt, np_, doc = process(p, True)
            dump(p, doc)
        if nt or np_:
            touched.append((os.path.basename(p), nt, np_))
        total_t += nt
        total_p += np_
    print(f"files={len(files())} titles={total_t} person_refs={total_p} touched={len(touched)}")
    print("backup:", bdir if apply else "(dry run)")
    for name, nt, np_ in touched:
        print(f"  {name}: titles={nt} persons={np_}")


if __name__ == "__main__":
    main("--apply" in sys.argv)
