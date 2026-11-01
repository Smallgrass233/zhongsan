using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZhongSanEditor.Core;

internal enum DocKind { Unknown, Scenario, Save }

/// <summary>一个剧本或存档 JSON 文档。始终保持原有成员顺序，只改值。</summary>
internal sealed class GameDocument
{
    public string Path { get; private set; }
    public JsonObject Root { get; private set; }
    public DocKind Kind { get; private set; }
    public long OriginalSize { get; private set; }

    public JsonArray PersonsArray => J.GameObjects(Root, "Persons");
    public JsonArray ArchitecturesArray => J.GameObjects(Root, "Architectures");
    public JsonArray TroopsArray => J.GameObjects(Root, "Troops");
    public JsonArray FactionsArray => J.GameObjects(Root, "Factions");
    public JsonArray TreasuresArray => J.GameObjects(Root, "Treasures");
    public JsonArray TroopEventsArray => J.GameObjects(Root, "TroopEvents");
    public JsonArray CaptiveDataArray => J.GameObjects(Root, "captiveData");
    public JsonArray PersonRelationsArray => J.ArrayOf(Root, "PersonRelationIds");

    public string Title => J.Str(Root, "ScenarioTitle");

    public static readonly string[] IdMapKeys =
    {
        "SpouseIds", "FatherIds", "MotherIds", "BrotherIds",
        "HatedIds", "CloseIds", "SuoshuIds", "MarriageGranterId",
    };

    public static GameDocument Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("文件不存在", path);

        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
            throw new InvalidOperationException(
                "这是 gzip 压缩的加密存档（Setting 里 EncryptSave=True）。请先在游戏设置中关闭存档加密，或使用未加密的剧本文件。");

        string text;
        using (var ms = new MemoryStream(bytes))
        using (var sr = new StreamReader(ms, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
            text = sr.ReadToEnd();

        // 游戏的存档写出流程会把 "},{" 替换成 "},\r\n{" 做美化；如果某个字符串值里正好含有 "},{",
        // 换行就被插进了字符串内部，产生非法 JSON。游戏自己的载入器（SimpleSerializer.DeserializeJsonFile）
        // 是先把整段文本的 \r 和 \n 全部删掉再解析，我们照抄这一步即可兼容。
        text = text.Replace("\r", "").Replace("\n", "");

        JsonNode node;
        try
        {
            node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
                MaxDepth = 256,
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("JSON 解析失败：" + ex.Message, ex);
        }

        if (node is not JsonObject root) throw new InvalidOperationException("根节点不是 JSON 对象，无法识别。");
        if (J.GameObjects(root, "Persons") is not JsonArray persons)
            throw new InvalidOperationException("找不到 Persons.GameObjects，这不是中三的剧本/存档文件。");

        var doc = new GameDocument
        {
            Path = path,
            Root = root,
            OriginalSize = bytes.Length,
        };
        // 存档和剧本的顶层键几乎一样（都有 CurrentPlayerID/Troops），只能靠"放在 Save 目录里"来区分。
        var dir = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? "");
        doc.Kind = string.Equals(dir, "Save", StringComparison.OrdinalIgnoreCase) ? DocKind.Save : DocKind.Scenario;
        if (persons.Count == 0) throw new InvalidOperationException("Persons.GameObjects 为空。");
        return doc;
    }

    public IEnumerable<JsonObject> Persons()
    {
        foreach (var n in PersonsArray)
            if (n is JsonObject o) yield return o;
    }

    public string ToJson() => Root.ToJsonString(J.WriteOpts);

    /// <summary>原子写入：先写临时文件，再替换目标。</summary>
    public void Save(string targetPath = null)
    {
        targetPath ??= Path;
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(targetPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var tmp = targetPath + ".tmp";
        File.WriteAllText(tmp, ToJson(), new UTF8Encoding(false));
        if (File.Exists(targetPath)) File.Replace(tmp, targetPath, null, ignoreMetadataErrors: true);
        else File.Move(tmp, targetPath);
        Path = targetPath;
    }
}
