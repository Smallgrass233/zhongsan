using System.Text;
using System.Text.Json.Nodes;

namespace ZhongSanEditor.Core;

/// <summary>无界面的自检：--selftest &lt;file&gt; [--log &lt;path&gt;]。用真实文件跑一遍读取、筛选、屏蔽、回读校验。</summary>
internal static class SelfTest
{
    public static int Run(string file, string logPath)
    {
        var sb = new StringBuilder();
        int fails = 0;

        void Check(string name, bool ok, string detail = "")
        {
            sb.AppendLine((ok ? "PASS  " : "FAIL  ") + name + (detail.Length > 0 ? "  |  " + detail : ""));
            if (!ok) fails++;
        }

        try
        {
            var doc = GameDocument.Load(file);
            var rows = doc.Persons().Select(p => new PersonRow(doc, p)).ToList();
            var index = GameIndex.Build(doc);
            foreach (var r in rows)
            {
                var place = index.Get(r.Id);
                r.Where = place?.Where ?? "";
                r.Faction = place?.Faction ?? "";
            }

            var female = rows.Where(r => r.IsFemale).ToList();
            var active = rows.Where(r => r.IsActive).ToList();
            Check("读取文件", rows.Count > 100, $"kind={doc.Kind}，人物 {rows.Count}，女性 {female.Count}，已登场 {active.Count}，文件 {new FileInfo(file).Length / 1048576.0:0.0} MB");
            Check("五维计算", rows.All(r => r.Abil == r.Command + r.Strength + r.Intelligence + r.Politics + r.Glamour),
                "示例：" + string.Join("、", rows.OrderByDescending(r => r.Abil).Take(3).Select(r => $"{r.DisplayName}={r.Abil}")));

            var strongF = female.Where(r => r.Abil >= 300).ToList();
            Check("筛选：女性且五维≥300", strongF.Count > 0, $"{strongF.Count} 人：" + string.Join("、", strongF.OrderByDescending(r => r.Abil).Take(6).Select(r => $"{r.DisplayName}({r.Abil})")));

            var r9 = female.Where(r => r.Id >= 9000 && r.Id <= 24999).ToList();
            Check("筛选：9000-24999 女性", r9.Count > 0, $"{r9.Count} 人");
            Check("筛选：已登场女性", female.Any(r => r.IsActive), female.Count(r => r.IsActive) + " 人");
            Check("索引：归属反查", rows.Any(r => !string.IsNullOrEmpty(r.Where) || !string.IsNullOrEmpty(r.Faction)),
                "有归属/势力信息的人物 " + rows.Count(r => !string.IsNullOrEmpty(r.Where) || !string.IsNullOrEmpty(r.Faction)) + " 人");

            // ---- 在副本上真实执行一次屏蔽，再回读校验 ----
            var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "zseditor_selftest_" + Guid.NewGuid().ToString("N")[..8] + System.IO.Path.GetExtension(file));
            File.Copy(file, tmp, true);

            var targets = (strongF.Count > 0 ? strongF : female.Take(3)).Take(5).Select(r => r.Id).ToList();
            var d2 = GameDocument.Load(tmp);
            var rep = BlockService.Apply(d2, targets);
            Check("应用屏蔽", rep.PersonsChanged > 0, $"目标 {targets.Count} 人，实际改动 {rep.PersonsChanged} 人");
            d2.Save();

            var d3 = GameDocument.Load(tmp);
            var blocked = new HashSet<int>(targets);
            var after = d3.Persons().Where(p => blocked.Contains(J.Int(p, "ID"))).ToList();
            Check("回读：被屏蔽者已置死", after.Count == targets.Count && after.All(p => !J.Bool(p, "Alive") && !J.Bool(p, "Available")),
                $"{after.Count}/{targets.Count} 人 Alive=false, Available=false");

            bool RefFree(JsonObject o, params string[] keys)
            {
                foreach (var k in keys)
                {
                    if (!o.ContainsKey(k)) continue;
                    if (J.Ids(J.Str(o, k)).Any(blocked.Contains)) return false;
                }
                return true;
            }

            var archBad = (d3.ArchitecturesArray ?? new JsonArray()).OfType<JsonObject>()
                .Count(a => !RefFree(a, "PersonsString", "MovingPersonsString", "NoFactionPersonsString", "NoFactionMovingPersonsString", "feiziliebiaoString", "CaptivesString"));
            var troopBad = (d3.TroopsArray ?? new JsonArray()).OfType<JsonObject>()
                .Count(t => !RefFree(t, "PersonsString", "CaptivesString") || blocked.Contains(J.Int(t, "LeaderIDString", -1)));
            var treasureBad = (d3.TreasuresArray ?? new JsonArray()).OfType<JsonObject>().Count(t => blocked.Contains(J.Int(t, "BelongedPersonIDString", -1)));
            var relBad = (d3.PersonRelationsArray ?? new JsonArray()).OfType<JsonObject>()
                .Count(r => blocked.Contains(J.Int(r, "PersonID1", -1)) || blocked.Contains(J.Int(r, "PersonID2", -1)));
            var capBad = (d3.CaptiveDataArray ?? new JsonArray()).OfType<JsonObject>().Count(c => blocked.Contains(J.Int(c, "CaptivePersonID", -1)));
            var mapBad = 0;
            foreach (var key in GameDocument.IdMapKeys)
            {
                if (d3.Root[key] is not JsonArray arr) continue;
                foreach (var eo in arr.OfType<JsonObject>())
                {
                    if (blocked.Contains(J.Int(eo, "Key", int.MinValue))) mapBad++;
                    var v = eo["Value"];
                    if (v is JsonArray va && va.Any(n => int.TryParse(n?.ToJsonString(), out var x) && blocked.Contains(x))) mapBad++;
                    if (v is JsonValue jv && jv.TryGetValue<int>(out var iv) && blocked.Contains(iv)) mapBad++;
                }
            }

            Check("回读：城池引用已清理", archBad == 0, "残留 " + archBad + " 座城");
            Check("回读：部队引用已清理", troopBad == 0, "残留 " + troopBad + " 支部队");
            Check("回读：宝物归属已清理", treasureBad == 0, "残留 " + treasureBad + " 件宝物");
            Check("回读：人际关系已清理", relBad == 0, "残留 " + relBad + " 条");
            Check("回读：俘虏记录已清理", capBad == 0, "残留 " + capBad + " 条");
            Check("回读：配偶/亲属/仇敌表已清理", mapBad == 0, "残留 " + mapBad + " 处");
            Check("回读：人数未变", d3.PersonsArray.Count == doc.PersonsArray.Count,
                $"{doc.PersonsArray.Count} -> {d3.PersonsArray.Count}");
            Check("回读：城池/部队数量未变",
                d3.ArchitecturesArray.Count == doc.ArchitecturesArray.Count && d3.TroopsArray.Count == doc.TroopsArray.Count,
                "城池 + 部队数量一致");

            // 幂等：再屏蔽一次不应再有改动
            var rep2 = BlockService.Apply(d3, targets);
            Check("幂等：重复屏蔽无副作用", rep2.PersonsChanged == 0 && rep2.CleanedRefs.Count == 0,
                "第二次改动 " + rep2.PersonsChanged + " 人，清理 " + rep2.CleanedRefs.Count + " 类");

            try { File.Delete(tmp); } catch { }
        }
        catch (Exception ex)
        {
            sb.AppendLine("FAIL  异常：" + ex);
            fails++;
        }

        sb.AppendLine();
        sb.AppendLine(fails == 0 ? "全部通过" : $"{fails} 项失败");
        var text = sb.ToString();
        if (!string.IsNullOrEmpty(logPath)) File.WriteAllText(logPath, text, new UTF8Encoding(false));
        return fails == 0 ? 0 : 1;
    }
}
