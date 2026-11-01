using System.Text;
using ZhongSanEditor.Core;

namespace ZhongSanEditor;

internal static class Cli
{
    public static int Run(string[] args)
    {
        string file = null, idsArg = null, idsFile = null, outPath = null, reportPath = null, backupDir = null;
        bool force = false, selfTest = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file": file = Next(args, ref i); break;
                case "--ids": idsArg = Next(args, ref i); break;
                case "--ids-file": idsFile = Next(args, ref i); break;
                case "--out": outPath = Next(args, ref i); break;
                case "--report": reportPath = Next(args, ref i); break;
                case "--backup-dir": backupDir = Next(args, ref i); break;
                case "--force": force = true; break;
                case "--selftest": selfTest = true; break;
                case "--help":
                case "-h":
                    WriteReport(reportPath, HelpText(), file);
                    return 0;
                default:
                    Console.Error.WriteLine("未知参数：" + args[i]);
                    return 3;
            }
        }

        if (file == null)
        {
            WriteReport(reportPath, "缺少 --file\n\n" + HelpText(), null);
            return 3;
        }

        if (selfTest)
            return SelfTest.Run(file, reportPath ?? file + ".selftest.txt");

        var ids = new List<int>();
        if (!string.IsNullOrEmpty(idsArg))
            foreach (var t in idsArg.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(t, out var id)) ids.Add(id);

        if (!string.IsNullOrEmpty(idsFile) && File.Exists(idsFile))
        {
            foreach (var line in File.ReadAllLines(idsFile))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                if (int.TryParse(t, out var id)) ids.Add(id);
            }
        }

        if (ids.Count == 0)
        {
            WriteReport(reportPath, "没有提供要屏蔽的 ID（--ids / --ids-file）", file);
            return 3;
        }

        try
        {
            var cfg = AppConfig.Load();
            var dir = backupDir ?? cfg.BackupDir;

            var doc = GameDocument.Load(file);
            var rep = BlockService.Apply(doc, ids, force);

            string backup;
            if (!string.IsNullOrEmpty(outPath))
            {
                doc.Save(outPath);
                backup = "（另存，未改动原文件）";
            }
            else
            {
                backup = BackupService.Backup(file, dir);
                doc.Save(file);
            }

            var text = rep.ToText() + $"\n备份：{backup}\n文件：{file}\n";
            WriteReport(reportPath, text, file);
            return 0;
        }
        catch (LeaderConflictException ex)
        {
            var text = "已中止（存在君主/主将/太守冲突）：\n" + string.Join("\n", ex.Conflicts.Select(c => "  ! " + c)) +
                       "\n\n确认要继续请加 --force。\n";
            WriteReport(reportPath, text, file);
            return 2;
        }
        catch (Exception ex)
        {
            WriteReport(reportPath, "失败：" + ex.Message + "\n" + ex, file);
            return 1;
        }
    }

    private static string Next(string[] args, ref int i)
        => i + 1 < args.Length ? args[++i] : null;

    private static void WriteReport(string reportPath, string text, string file)
    {
        Console.WriteLine(text);
        try
        {
            reportPath ??= file == null ? null : file + ".report.txt";
            if (reportPath != null)
                File.WriteAllText(reportPath, text, new UTF8Encoding(false));
        }
        catch { /* 报告写不出去也不影响主流程 */ }
    }

    private static string HelpText() =>
        "中三武将管理工具（命令行模式）\n" +
        "  --file <剧本或存档.json>   必填\n" +
        "  --ids 12,345,678           要屏蔽的人物 ID，逗号/空格分隔\n" +
        "  --ids-file <文本>          从文件读 ID（每行一个，# 开头为注释）\n" +
        "  --out <新文件>             另存为（默认原地写回）\n" +
        "  --report <报告路径>        默认 <文件>.report.txt\n" +
        "  --backup-dir <目录>        备份目录\n" +
        "  --force                    即使对方是君主/主将/太守也继续\n";
}
