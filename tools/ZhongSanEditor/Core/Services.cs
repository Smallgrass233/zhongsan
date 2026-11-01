using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZhongSanEditor.Core;

internal sealed class AppConfig
{
    public string BackupDir { get; set; } = "";
    public string ScenarioDir { get; set; } = "";
    public string SaveDir { get; set; } = "";
    public string LastFile { get; set; } = "";
    public string LastBlockList { get; set; } = "";

    private static string ConfigPath => System.IO.Path.Combine(AppContext.BaseDirectory, "ZhongSanEditor.config.json");

    public static AppConfig Load()
    {
        var cfg = new AppConfig();
        try
        {
            if (File.Exists(ConfigPath))
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath));
                if (loaded != null) cfg = loaded;
            }
        }
        catch { /* 配置损坏就用默认值 */ }

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(cfg.SaveDir))
            cfg.SaveDir = System.IO.Path.Combine(docs, "WorldOfTheThreeKingdoms", "Save");

        if (string.IsNullOrWhiteSpace(cfg.ScenarioDir))
        {
            foreach (var guess in new[]
                     {
                         @"D:\app\中三\中华三国志1.23.1-1.24.1版本\Release\Content\Data\Scenario",
                     })
            {
                if (Directory.Exists(guess)) { cfg.ScenarioDir = guess; break; }
            }
        }

        if (string.IsNullOrWhiteSpace(cfg.BackupDir))
        {
            var candidate = @"D:\re\zhongsan\backup";
            cfg.BackupDir = Directory.Exists(candidate) ? candidate : System.IO.Path.Combine(AppContext.BaseDirectory, "backup");
        }

        Directory.CreateDirectory(cfg.BackupDir);
        return cfg;
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* 忽略 */ }
    }
}

internal static class BackupService
{
    public static string Backup(string file, string backupDir)
    {
        Directory.CreateDirectory(backupDir);
        var name = System.IO.Path.GetFileNameWithoutExtension(file);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var target = System.IO.Path.Combine(backupDir, $"{name}.bak.{stamp}.json");
        int n = 1;
        while (File.Exists(target))
            target = System.IO.Path.Combine(backupDir, $"{name}.bak.{stamp}-{n++}.json");
        File.Copy(file, target, overwrite: false);
        return target;
    }

    public static List<(string Path, DateTime Time, long Size)> List(string backupDir, string fileHint = null)
    {
        var list = new List<(string, DateTime, long)>();
        if (!Directory.Exists(backupDir)) return list;
        foreach (var f in Directory.GetFiles(backupDir, "*.bak.*.json"))
        {
            if (fileHint != null &&
                !System.IO.Path.GetFileName(f).StartsWith(System.IO.Path.GetFileNameWithoutExtension(fileHint) + ".bak.",
                    StringComparison.OrdinalIgnoreCase))
                continue;
            var fi = new FileInfo(f);
            list.Add((f, fi.LastWriteTime, fi.Length));
        }
        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }
}

internal static class ApplyRunner
{
    /// <summary>打开文件 → 应用屏蔽 → 备份 → 写回。返回报告与备份路径。</summary>
    public static ApplyReport Run(string file, ICollection<int> ids, bool force, string backupDir, out string backupPath)
    {
        var doc = GameDocument.Load(file);
        var rep = BlockService.Apply(doc, ids, force);
        backupPath = BackupService.Backup(file, backupDir);
        doc.Save(file);
        return rep;
    }
}
