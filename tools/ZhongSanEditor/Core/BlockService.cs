using System.Text;
using System.Text.Json.Nodes;

namespace ZhongSanEditor.Core;

internal sealed class LeaderConflictException : Exception
{
    public List<string> Conflicts { get; }
    public LeaderConflictException(List<string> conflicts)
        : base("有被屏蔽的武将是势力君主/部队主将/城池太守，直接写入可能导致游戏异常。")
    {
        Conflicts = conflicts;
    }
}

internal sealed class ApplyReport
{
    public readonly List<string> Changes = new();
    public readonly List<string> Warnings = new();
    public int Requested;
    public int PersonsChanged;
    public readonly Dictionary<string, int> CleanedRefs = new();

    public void Clean(string what, int count = 1)
    {
        if (count <= 0) return;
        CleanedRefs.TryGetValue(what, out var c);
        CleanedRefs[what] = c + count;
    }

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==== 应用结果 ====");
        sb.AppendLine($"请求屏蔽：{Requested} 人，实际改动：{PersonsChanged} 人");
        sb.AppendLine();
        sb.AppendLine("---- 引用清理 ----");
        if (CleanedRefs.Count == 0) sb.AppendLine("（无）");
        foreach (var kv in CleanedRefs) sb.AppendLine($"  {kv.Key}：{kv.Value} 处");
        sb.AppendLine();
        if (Warnings.Count > 0)
        {
            sb.AppendLine("---- 告警 ----");
            foreach (var w in Warnings) sb.AppendLine("  ! " + w);
            sb.AppendLine();
        }
        sb.AppendLine("---- 明细 ----");
        foreach (var c in Changes) sb.AppendLine("  " + c);
        return sb.ToString();
    }
}

/// <summary>把一批武将"彻底下架"：置死 + 清理所有跨表引用。</summary>
internal static class BlockService
{
    private static readonly string[] ArchPersonStrings =
    {
        "PersonsString", "MovingPersonsString", "NoFactionPersonsString",
        "NoFactionMovingPersonsString", "feiziliebiaoString", "CaptivesString",
    };

    public static ApplyReport Apply(GameDocument doc, IEnumerable<int> ids, bool force = false)
    {
        var set = new HashSet<int>(ids);
        var rep = new ApplyReport { Requested = set.Count };
        if (set.Count == 0) return rep;

        var names = new Dictionary<int, string>();
        foreach (var p in doc.Persons())
        {
            int id = J.Int(p, "ID");
            if (set.Contains(id)) names[id] = J.Str(p, "SurName") + J.Str(p, "GivenName");
        }

        // ---- 冲突检查：君主/主将/太守 ----
        var conflicts = new List<string>();
        foreach (var f in doc.FactionsArray)
        {
            if (f is not JsonObject fo) continue;
            int l = J.Int(fo, "LeaderID", -1);
            if (set.Contains(l)) conflicts.Add($"势力「{J.Str(fo, "Name")}」的君主：{Name(names, l)}");
            int pr = J.Int(fo, "PrinceID", -1);
            if (set.Contains(pr)) conflicts.Add($"势力「{J.Str(fo, "Name")}」的储君：{Name(names, pr)}");
        }
        foreach (var a in doc.ArchitecturesArray)
        {
            if (a is not JsonObject ao) continue;
            int m = J.Int(ao, "MayorID", -1);
            if (set.Contains(m)) conflicts.Add($"城池「{J.Str(ao, "Name")}」的太守：{Name(names, m)}");
        }
        foreach (var t in doc.TroopsArray)
        {
            if (t is not JsonObject to) continue;
            int l = J.Int(to, "LeaderIDString", -1);
            if (set.Contains(l)) conflicts.Add($"部队「{J.Str(to, "Name")}」的主将：{Name(names, l)}");
        }
        if (conflicts.Count > 0 && !force)
            throw new LeaderConflictException(conflicts);

        // 强制继续时：势力不能留一个"君主是死人"的状态，按势力内现存成员改立继承人。
        if (conflicts.Count > 0)
        {
            var stats = new Dictionary<int, (bool Alive, bool Avail, int Abil)>();
            foreach (var p in doc.Persons())
            {
                int pid = J.Int(p, "ID");
                stats[pid] = (J.Bool(p, "Alive"), J.Bool(p, "Available"),
                    J.Int(p, "BaseCommand") + J.Int(p, "BaseStrength") + J.Int(p, "BaseIntelligence")
                    + J.Int(p, "BasePolitics") + J.Int(p, "BaseGlamour"));
            }

            var cityById = new Dictionary<int, JsonObject>();
            foreach (var a in doc.ArchitecturesArray)
                if (a is JsonObject ao) cityById[J.Int(ao, "ID")] = ao;

            int PickSuccessor(JsonObject faction, HashSet<int> exclude)
            {
                var best = -1;
                var bestAbil = -1;
                foreach (var aid in J.Ids(J.Str(faction, "ArchitecturesString")))
                {
                    if (!cityById.TryGetValue(aid, out var ao)) continue;
                    foreach (var key in new[] { "PersonsString", "MovingPersonsString" })
                        foreach (var pid in J.Ids(J.Str(ao, key)))
                        {
                            if (set.Contains(pid) || exclude.Contains(pid)) continue;
                            if (!stats.TryGetValue(pid, out var st) || !st.Alive || !st.Avail) continue;
                            // 优先储君，其次五维最高
                            int score = st.Abil + (pid == J.Int(faction, "PrinceID", -1) ? 1000 : 0);
                            if (score > bestAbil) { bestAbil = score; best = pid; }
                        }
                }
                return best;
            }

            foreach (var f in doc.FactionsArray)
            {
                if (f is not JsonObject fo) continue;
                int leader = J.Int(fo, "LeaderID", -1);
                if (set.Contains(leader))
                {
                    int succ = PickSuccessor(fo, new HashSet<int>());
                    J.SetIfExists(fo, "LeaderID", succ);
                    rep.Warnings.Add(succ > 0
                        ? $"势力「{J.Str(fo, "Name")}」的君主 {Name(names, leader)} 被屏蔽，已改立 {Name(names, succ)} 为君主"
                        : $"势力「{J.Str(fo, "Name")}」的君主 {Name(names, leader)} 被屏蔽，且势力内没有可继位的人，LeaderID 置 -1（进游戏后请确认）");
                }

                int prince = J.Int(fo, "PrinceID", -1);
                if (set.Contains(prince))
                {
                    int succ2 = PickSuccessor(fo, new HashSet<int> { J.Int(fo, "LeaderID", -1) });
                    J.SetIfExists(fo, "PrinceID", succ2);
                    rep.Warnings.Add(succ2 > 0
                        ? $"势力「{J.Str(fo, "Name")}」的储君 {Name(names, prince)} 被屏蔽，已改立 {Name(names, succ2)} 为储君"
                        : $"势力「{J.Str(fo, "Name")}」的储君 {Name(names, prince)} 被屏蔽，PrinceID 置 -1");
                }
            }

            foreach (var c in conflicts.Where(c => c.Contains("太守") || c.Contains("主将")))
                rep.Warnings.Add(c + "（已按要求继续，太守/主将字段已清空，请进游戏确认）");
        }

        // ---- 人物本体 ----
        var seen = new HashSet<int>();
        foreach (var p in doc.Persons())
        {
            int id = J.Int(p, "ID");
            if (set.Contains(id))
            {
                seen.Add(id);
                bool aliveBefore = J.Bool(p, "Alive");
                bool availBefore = J.Bool(p, "Available");
                string label = $"「{names.GetValueOrDefault(id, id.ToString())}」(ID {id})";
                if (aliveBefore)
                {
                    rep.Changes.Add(availBefore
                        ? $"{label} 已登场 → 置死（Alive true → false，Available true → false）"
                        : $"{label} 未登场 → 置死（Alive true → false）");
                    rep.PersonsChanged++;
                }
                else if (availBefore)
                {
                    rep.Changes.Add($"{label} 本已死亡，清掉残留的登场标记（Available true → false）");
                    rep.PersonsChanged++;
                }
                else
                {
                    rep.Changes.Add($"{label} 本已死亡且未登场，跳过人物本体");
                }

                J.SetIfExists(p, "Alive", false);
                J.SetIfExists(p, "Available", false);
            }

            // 他的"亲信/部曲"名单里不要留着被屏蔽者
            if (p.ContainsKey("preferredTroopPersonsString"))
            {
                var raw = J.Str(p, "preferredTroopPersonsString");
                if (J.RemoveIds(raw, set, out var now) && now != raw)
                {
                    J.SetIfExists(p, "preferredTroopPersonsString", now);
                    rep.Clean("人物·部曲名单(preferredTroopPersonsString)");
                }
            }
        }

        foreach (var id in set)
            if (!seen.Contains(id))
                rep.Warnings.Add($"ID {id} 在本文件里找不到对应人物（可能 ID 写错，或该段人物在本剧本/存档中不存在）");

        // ---- 城池 ----
        foreach (var a in doc.ArchitecturesArray)
        {
            if (a is not JsonObject ao) continue;
            foreach (var key in ArchPersonStrings)
            {
                if (!ao.ContainsKey(key)) continue;
                var raw = J.Str(ao, key);
                if (!J.RemoveIds(raw, set, out var now) || now == raw) continue;
                J.SetIfExists(ao, key, now);
                rep.Clean("城池·" + key);
            }

            int mayor = J.Int(ao, "MayorID", -1);
            if (set.Contains(mayor))
            {
                J.SetIfExists(ao, "MayorID", -1);
                rep.Changes.Add($"城池「{J.Str(ao, "Name")}」太守清空（原 {Name(names, mayor)}）");
            }
        }

        // ---- 部队 ----
        foreach (var t in doc.TroopsArray)
        {
            if (t is not JsonObject to) continue;
            foreach (var key in new[] { "PersonsString", "CaptivesString" })
            {
                if (!to.ContainsKey(key)) continue;
                var raw = J.Str(to, key);
                if (!J.RemoveIds(raw, set, out var now) || now == raw) continue;
                J.SetIfExists(to, key, now);
                rep.Clean("部队·" + key);
            }

            int leader = J.Int(to, "LeaderIDString", -1);
            if (set.Contains(leader))
            {
                J.SetIfExists(to, "LeaderIDString", "");
                rep.Changes.Add($"部队「{J.Str(to, "Name")}」主将清空（原 {Name(names, leader)}）");
            }
        }

        // ---- 俘虏 ----
        if (doc.CaptiveDataArray is JsonArray captives)
        {
            for (int i = captives.Count - 1; i >= 0; i--)
            {
                if (captives[i] is not JsonObject co) continue;
                int pid = J.Int(co, "CaptivePersonID", -1);
                if (!set.Contains(pid)) continue;
                captives.RemoveAt(i);
                rep.Clean("俘虏记录(captiveData)");
            }
        }

        // ---- 宝物 ----
        // 注意：Treasure.HidePlaceIDString 存的是"藏在哪座城"的 Architecture ID（GameScenario.cs:3486/4611），
        // 不是人物 ID，绝不能动；只有 BelongedPersonIDString 才是人物（单值，不存在时用 -1 表示）。
        foreach (var t in doc.TreasuresArray)
        {
            if (t is not JsonObject to || !to.ContainsKey("BelongedPersonIDString")) continue;
            var owner = J.Int(to, "BelongedPersonIDString", -1);
            if (!set.Contains(owner)) continue;
            J.SetIfExists(to, "BelongedPersonIDString", -1);
            rep.Clean("宝物·BelongedPersonIDString");
        }

        // ---- 部队事件 ----
        foreach (var e in doc.TroopEventsArray)
        {
            if (e is not JsonObject eo) continue;
            foreach (var key in new[] { "LaunchPersonString", "EffectPersonsString", "TargetPersonsString" })
            {
                if (!eo.ContainsKey(key)) continue;
                var raw = J.Str(eo, key);
                if (!J.RemoveIds(raw, set, out var now) || now == raw) continue;
                J.SetIfExists(eo, key, now);
                rep.Clean("部队事件·" + key);
            }
        }

        // ---- 人际关系对 ----
        if (doc.PersonRelationsArray is JsonArray rels)
        {
            for (int i = rels.Count - 1; i >= 0; i--)
            {
                if (rels[i] is not JsonObject ro) continue;
                if (set.Contains(J.Int(ro, "PersonID1", -1)) || set.Contains(J.Int(ro, "PersonID2", -1)))
                {
                    rels.RemoveAt(i);
                    rep.Clean("人际关系(PersonRelationIds)");
                }
            }
        }

        // ---- 亲属/配偶/仇敌 等 ID 映射表 ----
        foreach (var key in GameDocument.IdMapKeys)
        {
            if (doc.Root[key] is not JsonArray arr) continue;
            for (int i = arr.Count - 1; i >= 0; i--)
            {
                if (arr[i] is not JsonObject eo) continue;
                int k = J.Int(eo, "Key", int.MinValue);
                if (k != int.MinValue && set.Contains(k))
                {
                    arr.RemoveAt(i);
                    rep.Clean(key + "·删除本人条目");
                    continue;
                }

                var v = eo["Value"];
                if (v is JsonArray va)
                {
                    bool changed = false;
                    for (int j = va.Count - 1; j >= 0; j--)
                    {
                        if (int.TryParse(va[j]?.ToJsonString(), out var vid) && set.Contains(vid))
                        {
                            va.RemoveAt(j);
                            changed = true;
                        }
                    }
                    if (changed) rep.Clean(key + "·移除引用");
                }
                else if (v is JsonValue jv && jv.TryGetValue<int>(out var iv) && set.Contains(iv))
                {
                    eo["Value"] = JsonValue.Create(-1);
                    rep.Clean(key + "·置为 -1");
                }
            }
        }

        // ---- 人物自身的单值引用 ----
        foreach (var p in doc.Persons())
        {
            foreach (var key in new[] { "princessTakerID", "waitForFeiziId", "suoshurenwu", "ConvincingPersonID" })
            {
                if (!p.ContainsKey(key)) continue;
                int v = J.Int(p, key, -1);
                if (!set.Contains(v)) continue;
                J.SetIfExists(p, key, -1);
                rep.Clean("人物·" + key);
            }
        }

        return rep;
    }

    private static string Name(Dictionary<int, string> names, int id)
        => names.TryGetValue(id, out var n) && !string.IsNullOrEmpty(n) ? $"{n}(ID {id})" : $"ID {id}";
}
