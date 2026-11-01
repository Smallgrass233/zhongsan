import json, re, sys
from collections import Counter

ORIG = r"D:\re\zhongsan\backup\test_save.bak.20261101-134045.json"
MOD  = r"D:\re\zhongsan\mod\test\test_save.json"
BLOCKED = {24, 9372, 9017, 25051}

def load(p):
    with open(p, "r", encoding="utf-8-sig") as f:
        t = f.read()
    return json.loads(t.replace("\r", "").replace("\n", ""))

a = load(ORIG); b = load(MOD)

diff = []
def walk(x, y, path=""):
    if type(x) is not type(y):
        diff.append((path, "TYPE", str(x)[:50], str(y)[:50])); return
    if isinstance(x, dict):
        if set(x) != set(y):
            diff.append((path, "KEYS", str(set(x) ^ set(y))[:60], "")); return
        for k in x: walk(x[k], y[k], path + "/" + k)
    elif isinstance(x, list):
        if len(x) != len(y):
            diff.append((path, "LEN", len(x), len(y))); return
        for i, (p, q) in enumerate(zip(x, y)): walk(p, q, f"{path}[{i}]")
    elif x != y:
        diff.append((path, "VAL", x, y))

walk(a, b)
print("changed leaves:", len(diff))
c = Counter("/".join(p.split("/")[:4]) for p, *_ in diff)
for k, v in c.most_common(): print(f"  {v:4d}  {k}")

# person field audit
allowed = {"Alive", "Available", "preferredTroopPersonsString",
           "princessTakerID", "waitForFeiziId", "suoshurenwu", "ConvincingPersonID"}
pe = Counter(); bad = []
for p, kind, x, y in diff:
    parts = p.strip("/").split("/")
    if len(parts) >= 4 and parts[0] == "Persons" and parts[1] == "GameObjects":
        field = parts[3]
        pe[field] += 1
        if field not in allowed: bad.append((p, x, y))
print("person fields changed:", dict(pe))
print("UNEXPECTED person changes:", bad[:5] if bad else "none")

# state of blocked persons
blocked_idx = {}
for i, p in enumerate(b["Persons"]["GameObjects"]):
    if p["ID"] in BLOCKED:
        blocked_idx[p["ID"]] = i
        print(f"ID {p['ID']} {p.get('SurName','')}{p.get('GivenName','')}: Alive={p['Alive']} Available={p['Available']}")
print("blocked all present:", len(blocked_idx) == len(BLOCKED), blocked_idx)

# scan person-bearing strings only
PERSON_STR_KEYS = {
    "PersonsString", "MovingPersonsString", "NoFactionPersonsString",
    "NoFactionMovingPersonsString", "feiziliebiaoString", "CaptivesString",
    "preferredTroopPersonsString",
}
hits = []
def scan(o, path):
    if isinstance(o, dict):
        for k, v in o.items():
            if k in PERSON_STR_KEYS and isinstance(v, str):
                ids = {int(t) for t in re.findall(r"-?\d+", v)}
                for h in BLOCKED & ids: hits.append((path + "/" + k, h, v[:40]))
            else:
                scan(v, path + "/" + k)
    elif isinstance(o, list):
        for i, v in enumerate(o): scan(v, f"{path}[{i}]")

scan(b, "")
print("remaining person-ID refs in string fields:", hits if hits else "none")

# single-int person refs
single = []
for key in ("LeaderIDString", "BelongedPersonIDString"):
    pass
for obj in b["Troops"]["GameObjects"]:
    if isinstance(obj.get("LeaderIDString"), str):
        ids = {int(t) for t in re.findall(r"-?\d+", obj["LeaderIDString"])}
        if ids & BLOCKED: single.append(("Troop.LeaderIDString", obj["ID"]))
    elif isinstance(obj.get("LeaderIDString"), int) and obj["LeaderIDString"] in BLOCKED:
        single.append(("Troop.LeaderIDString", obj["ID"]))
for obj in b["Treasures"]["GameObjects"]:
    if obj.get("BelongedPersonIDString") in BLOCKED: single.append(("Treasure.owner", obj["ID"]))
for obj in b["Architectures"]["GameObjects"]:
    for k in ("MayorID",):
        if obj.get(k) in BLOCKED: single.append((k, obj["ID"]))
print("remaining single refs:", single if single else "none")

print("counts:", {k: (len(a[k]["GameObjects"]) if isinstance(a.get(k), dict) else len(a[k]),
                      len(b[k]["GameObjects"]) if isinstance(b.get(k), dict) else len(b[k]))
                  for k in ("Persons", "Architectures", "Troops", "Treasures", "captiveData")})
print("relations:", len(a["PersonRelationIds"]), "->", len(b["PersonRelationIds"]))
