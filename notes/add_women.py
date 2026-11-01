# -*- coding: utf-8 -*-
"""按史料/演义补 20 位女性角色（ID 9700-9719）。

- 以文件中现存的女性人物为模板深拷贝，保证 118 个字段齐全、类型一致。
- 只新增 Persons 记录 + AllBiographies 列传，不改动既有数据。
- 全部为「未登场」（Alive=True, Available=False），按 YearAvailable 在游戏中自然出现。
- 数值刻意压低：五维 <= 300，技能 <= 4，特技 0，称号最多 1 个 Lv<=3 的身份称号。

用法：python add_women.py --dry | --apply
"""
import copy
import glob
import json
import os
import shutil
import sys
import time

GAME = r"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release"
SCEN = os.path.join(GAME, r"Content\Data\Scenario")
SAVE = r"D:\Documents\WorldOfTheThreeKingdoms\Save"
BACKUP = r"D:\re\zhongsan\backup\addwomen"
BASE_ID = 9700

# (ID, 姓, 名, 统, 武, 智, 政, 魅, 出生, 出场, 死亡, 称号ID, 技能ID, 列传)
WOMEN = [
    (9700, "董", "贵人", 10, 5, 58, 52, 84, 176, 196, 200, 0, "4 54 96",
     "董承之女，汉献帝贵人。建安五年董承衣带诏事泄，曹操诛董承三族，董贵人虽怀有身孕仍被缢杀，献帝求情而不得。"),
    (9701, "王", "荣", 8, 5, 62, 50, 86, 168, 186, 181, 0, "4 95",
     "汉献帝生母，初为美人。因何皇后妒忌，产子后被毒杀，献帝即位后追尊为灵怀皇后。"),
    (9702, "何", "太后", 20, 10, 56, 66, 80, 160, 180, 189, 0, "3 13 54",
     "汉灵帝皇后，大将军何进之妹。灵帝崩后与何进拥立少帝，何进被宦官所杀，董卓入京后废少帝，何太后被鸩杀。"),
    (9703, "唐", "姬", 10, 6, 55, 46, 82, 175, 189, 0, 0, "0 54",
     "汉少帝（弘农王）之妃。董卓废少帝后，李儒奉命进鸩，少帝与唐姬诀别，唐姬悲歌起舞，后归乡里守节不嫁。"),
    (9704, "刘", "夫人", 16, 6, 63, 62, 76, 165, 185, 205, 0, "1 3",
     "袁绍之妻，袁尚之母。袁绍死后偏爱幼子袁尚，与审配等矫绍遗命立尚，致使袁谭、袁尚兄弟相攻，河北遂衰。"),
    (9705, "崔", "氏", 8, 5, 66, 54, 90, 190, 210, 0, 51, "2 12 54",
     "崔琰之女，曹植之妻。因衣着华丽违曹操俭约之令，被曹操赐死，曹植亦因此失宠。"),
    (9706, "毛", "皇后", 20, 10, 60, 58, 88, 200, 222, 237, 0, "3 4 54",
     "魏明帝曹叡皇后，河内人。初为平原王王妃，明帝即位立为后。后因郭夫人得宠而生怨，被明帝赐死。"),
    (9707, "夏侯", "徽", 30, 16, 76, 66, 80, 211, 228, 234, 40, "2 12 100",
     "夏侯尚之女，司马师之妻。聪慧有识度，与司马师育五女。青龙二年暴亡，后世多传为司马氏所鸩。"),
    (9708, "潘", "淑", 15, 10, 58, 56, 92, 210, 232, 252, 0, "4 1 96",
     "吴大帝孙权皇后，会稽句章人。以美貌得宠，生子孙亮。孙权病重时因询问吕后旧事触怒宫人，被缢杀。"),
    (9709, "谢", "氏", 16, 8, 60, 56, 82, 185, 200, 0, 0, "4 1",
     "吴大帝孙权元妃，会稽山阴人，谢承之姊。早年为孙权正妻，后因孙权纳徐氏而失宠，郁郁早卒。"),
    (9710, "袁", "氏", 12, 6, 56, 52, 80, 190, 208, 0, 0, "1 4",
     "袁术之女。袁术败亡后入吴，为孙权夫人。性恭谨而不妒，常以谦让自处。"),
    (9711, "赵", "夫人", 10, 6, 66, 42, 86, 195, 214, 0, 0, "2 12 76",
     "吴大帝孙权夫人，善画。相传能于方帛之上绣作五岳列国地形，号称「针绝」「丝绝」「机绝」。"),
    (9712, "孙", "寒华", 10, 14, 70, 36, 84, 220, 240, 0, 62, "110 12 92",
     "吴地传说中修道成仙的女子，孙氏之女。于衡山修行，后世道教典籍多有记载，谓其白日飞升。"),
    (9713, "张", "星彩", 45, 56, 50, 44, 82, 205, 222, 237, 0, "80 96 85",
     "张飞之女，蜀汉后主刘禅的敬哀皇后。章武年间入宫为太子妃，刘禅即位后立为皇后，谥敬哀。"),
    (9714, "关", "凤", 50, 66, 48, 38, 80, 200, 217, 0, 0, "51 80 96",
     "关羽之女，民间传说中的人物。孙权曾遣使为其子求娶，关羽以「虎女焉能嫁犬子」拒之，后世演义遂衍其名。"),
    (9715, "曹", "婴", 56, 64, 62, 42, 76, 215, 234, 0, 0, "54 82 96",
     "曹操孙女，民间传说与后世游戏中的形象。少有胆略，善骑射，常随军出行，事迹不见于正史。"),
    (9716, "羊", "献容", 18, 8, 62, 58, 88, 250, 270, 322, 0, "4 54 96",
     "晋惠帝皇后，泰山南城人。八王之乱中五废五立，洛阳陷落后被刘曜所纳，立为前赵皇后。"),
    (9717, "贾", "南风", 30, 20, 74, 70, 56, 257, 275, 300, 0, "3 13 54 100",
     "贾充之女，晋惠帝皇后。性妒而多权诈，专擅朝政，构陷太子司马遹，终引发八王之乱，为赵王司马伦所杀。"),
    (9718, "李", "婉", 12, 6, 68, 56, 80, 240, 260, 0, 81, "2 12 54",
     "贾充元配，李丰之女。父被诛后坐罪流徙，作《女训》行于世。后虽得赦归，贾充已另娶郭槐，遂不复相迎。"),
    (9719, "阮", "氏", 12, 6, 72, 52, 78, 220, 240, 0, 54, "54 100 4",
     "阮共之女，许允之妻。新婚之夜以言辞折服许允，后许允被诛，她教子有方，全其家门。"),
]


def load(p):
    t = open(p, encoding="utf-8").read().strip()
    if t[:2] == "\x1f\x8b":
        return None
    return json.loads(t.replace("\r", "").replace("\n", ""))


def persons_of(d):
    v = d.get("Persons")
    if isinstance(v, dict):
        return v.get("GameObjects") or []
    return v or []


def add_to_file(path, apply):
    d = load(path)
    if not isinstance(d, dict) or "Persons" not in d:
        return None
    ps = persons_of(d)
    used = {p.get("ID") for p in ps}
    todo = [w for w in WOMEN if w[0] not in used]
    if not todo:
        return None
    # 模板：优先 505 马云騄 / 424 貂蝉 / 任意女性
    tpl = None
    for pref in (505, 424, 9372):
        tpl = next((p for p in ps if p.get("ID") == pref), None)
        if tpl:
            break
    if tpl is None:
        tpl = next(p for p in ps if p.get("Sex"))
    pics = [p.get("PictureIndex") for p in ps if p.get("Sex") and isinstance(p.get("PictureIndex"), int) and p.get("PictureIndex", 0) > 0]
    pics = sorted(set(pics)) or [tpl.get("PictureIndex", 0)]

    bios = None
    ab = d.get("AllBiographies")
    if isinstance(ab, dict) and isinstance(ab.get("Biographys"), list):
        bios = ab["Biographys"]
    btpl = None
    if bios:
        btpl = next((b["Value"] for b in bios if b.get("Key") == tpl.get("ID")), bios[0]["Value"])

    added = []
    for idx, (wid, sur, given, cmd, mil, intel, pol, glam, born, yav, dead, title, skills, bio) in enumerate(todo):
        p = copy.deepcopy(tpl)
        p.update({
            "__type": tpl.get("__type", "Person:#GameObjects"),
            "ID": wid,
            "Name": None,
            "SurName": sur,
            "GivenName": given,
            "CalledName": "",
            "Sex": True,
            "Alive": True,
            "Available": False,
            "YearAvailable": yav,
            "YearBorn": born,
            "YearDead": dead,
            "BaseCommand": cmd,
            "BaseStrength": mil,
            "BaseIntelligence": intel,
            "BasePolitics": pol,
            "BaseGlamour": glam,
            "PictureIndex": pics[idx % len(pics)],
            "RealTitlesString": (str(title) + " ") if title else "",
            "SkillsString": (skills + " ") if skills else "",
            "StuntsString": "",
            "PersonalTitleString": 0,
            "UniqueTitlesString": "",
            "JoinFactionID": [],
            "ProhibitedFactionID": [],
            "preferredTroopPersonsString": "",
            "princessTakerID": -1,
            "waitForFeiziId": -1,
            "suoshurenwu": -1,
            "ConvincingPersonID": -1,
            "Tags": "",
            "Generation": tpl.get("Generation", 2),
            "NvGuan": True,
        })
        ps.append(p)
        added.append(wid)
        if bios is not None and btpl is not None:
            b = copy.deepcopy(btpl)
            b["ID"] = wid
            b["Name"] = sur + given
            b["Brief"] = bio
            b["History"] = bio
            b["InGame"] = ""
            b["Romance"] = ""
            bios.append({"Key": wid, "Value": b})

    if apply:
        os.makedirs(BACKUP, exist_ok=True)
        stamp = time.strftime("%Y%m%d-%H%M%S")
        shutil.copy2(path, os.path.join(BACKUP, "%s.%s.json" % (os.path.splitext(os.path.basename(path))[0], stamp)))
        tmp = path + ".tmp"
        with open(tmp, "w", encoding="utf-8", newline="\n") as f:
            json.dump(d, f, ensure_ascii=False, separators=(",", ":"))
        os.replace(tmp, path)
    return added


def main():
    apply = "--apply" in sys.argv
    lines = []
    for p in sorted(glob.glob(os.path.join(SCEN, "*.json"))) + sorted(glob.glob(os.path.join(SAVE, "*.json"))):
        r = add_to_file(p, apply)
        if r:
            lines.append("%s: +%d 人 %s" % (os.path.basename(p), len(r), r[0] if len(r) == 1 else "%d..%d" % (r[0], r[-1])))
    out = "\n".join(lines)
    print(out)
    print("\n文件数：%d" % len(lines))
    rp = r"D:\re\zhongsan\notes\addwomen_report_%s.txt" % time.strftime("%Y%m%d-%H%M%S")
    open(rp, "w", encoding="utf-8").write(out)
    print("报告：" + rp)


if __name__ == "__main__":
    main()
