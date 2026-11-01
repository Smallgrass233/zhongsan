using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

class LoadTest
{
    static object Get(object o, string n)
    {
        var t = o.GetType();
        var p = t.GetProperty(n, BindingFlags.Public | BindingFlags.Instance);
        if (p != null) return p.GetValue(o);
        var f = t.GetField(n, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(o);
        throw new Exception("no member " + n + " on " + t.FullName);
    }
    static void Main(string[] args)
    {
        try
        {
            string file = args[0], dir = args[1];
            var asm = Assembly.LoadFrom(Path.Combine(dir, "WorldOfTheThreeKingdoms.exe"));
            var ser = asm.GetTypes().First(t => t.Name == "SimpleSerializer");
            var gs = asm.GetTypes().First(t => t.Name == "GameScenario");
            var m = ser.GetMethods().First(x => x.Name == "DeserializeJson" && x.IsGenericMethodDefinition);
            var text = File.ReadAllText(file).Replace("\r", "").Replace("\n", "");
            object r;
            try { r = m.MakeGenericMethod(gs).Invoke(null, new object[] { text, false, false }); }
            catch (TargetInvocationException ex) { Console.WriteLine("THROW: " + ex.InnerException); return; }
            if (r == null) { Console.WriteLine("NULL"); return; }
            Console.WriteLine("loaded type: " + r.GetType().FullName);
            var pl = Get(r, "Persons");
            Console.WriteLine("personlist type: " + pl.GetType().FullName);
            var list = ((IEnumerable)Get(pl, "GameObjects")).Cast<object>().ToList();
            Console.WriteLine("persons: " + list.Count);
            var neu = list.Where(p => { var id = (int)Get(p, "ID"); return id >= 9700 && id <= 9719; }).ToList();
            Console.WriteLine("new persons: " + neu.Count);
            foreach (var p in neu.Take(4))
                Console.WriteLine("  {0} {1}{2} sex={3} alive={4} avail={5} abil={6} yavail={7} skills='{8}' titles='{9}'",
                    Get(p, "ID"), Get(p, "SurName"), Get(p, "GivenName"), Get(p, "Sex"), Get(p, "Alive"), Get(p, "Available"),
                    (int)Get(p, "BaseCommand") + (int)Get(p, "BaseStrength") + (int)Get(p, "BaseIntelligence") + (int)Get(p, "BasePolitics") + (int)Get(p, "BaseGlamour"),
                    Get(p, "YearAvailable"), Get(p, "SkillsString"), Get(p, "RealTitlesString"));
            var ab = Get(r, "AllBiographies");
            var bl = ((IEnumerable)Get(ab, "Biographys")).Cast<object>().ToList();
            Console.WriteLine("bios: " + bl.Count);
            var b0 = bl.FirstOrDefault(b => (int)Get(b, "Key") == 9700);
            if (b0 != null) Console.WriteLine("bio9700: " + Get(Get(b0, "Value"), "Name"));
            Console.WriteLine("date: " + Get(r, "Date"));
        }
        catch (Exception ex) { Console.WriteLine("ERR: " + ex); }
    }
}
