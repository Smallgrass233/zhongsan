using System.ComponentModel;
using System.Text.Json.Nodes;

namespace ZhongSanEditor.Core;

internal sealed class PersonRow : INotifyPropertyChanged
{
    public JsonObject Node { get; }
    public GameDocument Doc { get; }

    private bool _blocked;
    public bool Blocked
    {
        get => _blocked;
        set { if (_blocked == value) return; _blocked = value; Raise(nameof(Blocked)); }
    }

    public PersonRow(GameDocument doc, JsonObject node)
    {
        Doc = doc;
        Node = node;
    }

    public int Id => J.Int(Node, "ID");
    public string SurName => J.Str(Node, "SurName");
    public string GivenName => J.Str(Node, "GivenName");
    public string DisplayName => SurName + GivenName;
    public string CalledName => J.Str(Node, "CalledName");

    public bool IsFemale => J.Bool(Node, "Sex");
    public string SexText => IsFemale ? "女" : "男";

    public bool Alive => J.Bool(Node, "Alive");
    public string AliveText => Alive ? "存活" : "已亡";
    public bool Available => J.Bool(Node, "Available");
    public string NvGuanText => J.Bool(Node, "NvGuan") ? "女官" : "";
    public bool IsActive => Available && Alive;
    public string ActiveText => IsActive ? "已登场" : (Available ? "已登场(亡)" : "未登场");

    public int YearAvailable => J.Int(Node, "YearAvailable");
    public int YearBorn => J.Int(Node, "YearBorn");
    public int YearDead => J.Int(Node, "YearDead");
    public int Generation => J.Int(Node, "Generation");
    public string TagsText => J.Str(Node, "Tags");

    public int Command => J.Int(Node, "BaseCommand");
    public int Strength => J.Int(Node, "BaseStrength");
    public int Intelligence => J.Int(Node, "BaseIntelligence");
    public int Politics => J.Int(Node, "BasePolitics");
    public int Glamour => J.Int(Node, "BaseGlamour");
    public int Abil => Command + Strength + Intelligence + Politics + Glamour;

    public string Where { get; set; } = "";
    public string Faction { get; set; } = "";
    public string WhereText => string.IsNullOrEmpty(Where) ? "" : Where;
    public string FactionText => string.IsNullOrEmpty(Faction) ? "" : Faction;

    public string IdRangeText
    {
        get
        {
            int id = Id;
            if (id < 7000) return "0-6999";
            if (id < 8000) return "7000-7999";
            if (id < 9000) return "8000-8999";
            if (id < 25000) return "9000-24999";
            return "25000+";
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void RaiseAll()
    {
        foreach (var n in new[]
                 {
                     nameof(Id), nameof(SurName), nameof(GivenName), nameof(DisplayName), nameof(CalledName),
                     nameof(IsFemale), nameof(SexText), nameof(Alive), nameof(AliveText), nameof(Available), nameof(IsActive),
                     nameof(ActiveText), nameof(YearAvailable), nameof(YearBorn), nameof(YearDead),
                     nameof(Generation), nameof(TagsText), nameof(Command), nameof(Strength),
                     nameof(Intelligence), nameof(Politics), nameof(Glamour), nameof(Abil),
                 })
            Raise(n);
    }

    public void ClearRelations()
    {
        J.SetIfExists(Node, "preferredTroopPersonsString", "");
    }
}

internal sealed class PlaceInfo
{
    public string Where = "";
    public string Faction = "";
}

/// <summary>从城池/部队/俘虏列表反查每个人当前身处何地、属于哪个势力。</summary>
internal sealed class GameIndex
{
    private readonly Dictionary<int, PlaceInfo> _map = new();

    public PlaceInfo Get(int personId) => _map.TryGetValue(personId, out var v) ? v : null;

    public static GameIndex Build(GameDocument doc)
    {
        var idx = new GameIndex();

        // 势力名
        var factionName = new Dictionary<int, string>();
        var archFaction = new Dictionary<int, string>();
        var troopFaction = new Dictionary<int, string>();
        foreach (var f in doc.FactionsArray)
        {
            if (f is not JsonObject fo) continue;
            int fid = J.Int(fo, "ID");
            var name = J.Str(fo, "Name");
            factionName[fid] = name;
            foreach (var a in J.Ids(J.Str(fo, "ArchitecturesString"))) archFaction[a] = name;
            foreach (var t in J.Ids(J.Str(fo, "TroopListString"))) troopFaction[t] = name;
        }

        void Put(int pid, string where, string faction)
        {
            if (pid <= 0) return;
            idx._map[pid] = new PlaceInfo { Where = where, Faction = faction };
        }

        foreach (var a in doc.ArchitecturesArray)
        {
            if (a is not JsonObject ao) continue;
            int aid = J.Int(ao, "ID");
            var aname = J.Str(ao, "Name");
            var fname = archFaction.TryGetValue(aid, out var fn) ? fn : "";
            foreach (var p in J.Ids(J.Str(ao, "PersonsString"))) Put(p, aname, fname);
            foreach (var p in J.Ids(J.Str(ao, "MovingPersonsString"))) Put(p, aname + "(行军)", fname);
            foreach (var p in J.Ids(J.Str(ao, "NoFactionPersonsString"))) Put(p, aname + "(在野)", "");
            foreach (var p in J.Ids(J.Str(ao, "NoFactionMovingPersonsString"))) Put(p, aname + "(在野移动)", "");
            foreach (var p in J.Ids(J.Str(ao, "feiziliebiaoString"))) Put(p, aname + "(妃子)", fname);
        }

        foreach (var t in doc.TroopsArray)
        {
            if (t is not JsonObject to) continue;
            int tid = J.Int(to, "ID");
            var tname = J.Str(to, "Name");
            var fname = troopFaction.TryGetValue(tid, out var fn) ? fn : "";
            var where = string.IsNullOrEmpty(tname) ? "部队" : tname;
            foreach (var p in J.Ids(J.Str(to, "PersonsString"))) Put(p, where, fname);
            int leader = J.Int(to, "LeaderIDString", -1);
            if (leader > 0) Put(leader, where + "(主将)", fname);
        }

        foreach (var c in doc.CaptiveDataArray)
        {
            if (c is not JsonObject co) continue;
            int pid = J.Int(co, "CaptivePersonID", -1);
            if (pid <= 0) continue;
            int cf = J.Int(co, "CaptiveFactionID", -1);
            var fname = factionName.TryGetValue(cf, out var fn) ? fn : "";
            Put(pid, "被俘", fname);
        }

        return idx;
    }
}
