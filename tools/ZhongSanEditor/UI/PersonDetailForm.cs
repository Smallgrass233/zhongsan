using System.Globalization;
using System.Text.Json.Nodes;
using ZhongSanEditor.Core;

namespace ZhongSanEditor.UI;

internal sealed class PersonDetailForm : Form
{
    private static readonly HashSet<string> KeyFields = new()
    {
        "ID", "SurName", "GivenName", "CalledName", "Sex", "Alive", "Available", "YearAvailable",
        "YearBorn", "YearDead", "BornRegion", "AvailableLocation", "PictureIndex", "Generation", "Tags",
        "NvGuan", "BaseCommand", "BaseStrength", "BaseIntelligence", "BasePolitics", "BaseGlamour",
        "BaseBraveness", "BaseCalmness", "SkillsString", "StuntsString", "RealTitlesString",
        "PersonalTitleString", "UniqueTitlesString", "JoinFactionID", "ProhibitedFactionID",
        "Reputation", "Fund", "Karma", "Ideal", "Ambition",
    };

    private readonly PersonRow _row;
    private readonly JsonObject _node;
    private readonly DataGridView _grid = new();
    private readonly CheckBox _chkKey = new() { Text = "只看关键字段", AutoSize = true };
    private readonly Dictionary<string, string> _edited = new();
    private readonly List<(string Key, string Kind)> _rows = new();

    public PersonDetailForm(PersonRow row)
    {
        _row = row;
        _node = row.Node;

        Text = $"编辑：{row.DisplayName}（ID {row.Id}）";
        Width = 760;
        Height = 780;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        var top = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(10) };
        var lbl = new Label
        {
            Dock = DockStyle.Fill,
            Text = $"{row.DisplayName}　ID {row.Id}　{row.SexText}　五维 {row.Abil}" +
                   $"（{row.Command}/{row.Strength}/{row.Intelligence}/{row.Politics}/{row.Glamour}）\r\n" +
                   "双击右侧“值”单元格修改；文本/整数/true-false 可直接编辑，对象与数组只读。",
        };
        top.Controls.Add(lbl);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 92, Padding = new Padding(10) };
        var btnOk = new Button { Text = "保存", Width = 110, Height = 34, Dock = DockStyle.Right };
        var btnCancel = new Button { Text = "取消", Width = 110, Height = 34, Dock = DockStyle.Right };
        btnOk.Click += (_, _) => SaveAndClose();
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bottom.Controls.Add(btnOk);
        bottom.Controls.Add(btnCancel);

        _chkKey.Location = new Point(10, 12);
        _chkKey.CheckedChanged += (_, _) => Fill();
        bottom.Controls.Add(_chkKey);

        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colKey", HeaderText = "字段", ReadOnly = true, FillWeight = 34 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colVal", HeaderText = "值", FillWeight = 66 });
        _grid.CellEndEdit += (_, e) =>
        {
            if (e.RowIndex < 0) return;
            var key = _grid.Rows[e.RowIndex].Cells[0].Value?.ToString();
            var val = _grid.Rows[e.RowIndex].Cells[1].Value?.ToString() ?? "";
            if (key != null) _edited[key] = val;
        };

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(top);

        Fill();
    }

    private void Fill()
    {
        _grid.Rows.Clear();
        _rows.Clear();
        foreach (var kv in _node)
        {
            if (_chkKey.Checked && !KeyFields.Contains(kv.Key)) continue;
            var v = kv.Value;
            string kind;
            string text;
            if (v is JsonObject || v is JsonArray)
            {
                kind = "complex";
                text = v.ToJsonString();
            }
            else if (v is null)
            {
                kind = "null";
                text = "";
            }
            else
            {
                var raw = v.ToJsonString();
                kind = raw switch
                {
                    "true" or "false" => "bool",
                    _ when raw.StartsWith("\"") => "string",
                    _ when raw.Contains('.') || raw.Contains('e') || raw.Contains('E') => "double",
                    _ => "int",
                };
                text = kind == "string" ? v.GetValue<string>() : raw;
            }

            int idx = _grid.Rows.Add(kv.Key, text);
            _rows.Add((kv.Key, kind));
            var cell = _grid.Rows[idx].Cells[1];
            if (kind is "complex" or "null")
            {
                cell.ReadOnly = true;
                cell.Style.ForeColor = Color.Gray;
            }
            if (_edited.TryGetValue(kv.Key, out var edited)) cell.Value = edited;
        }
    }

    private void SaveAndClose()
    {
        if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);

        var problems = new List<string>();
        foreach (var (key, kind) in _rows)
        {
            if (!_edited.TryGetValue(key, out var text)) continue;
            var original = _node[key]?.ToJsonString();
            JsonNode newValue = null;
            switch (kind)
            {
                case "bool":
                    if (!bool.TryParse(text.Trim(), out var b)) { problems.Add($"{key}：需要 true / false"); continue; }
                    newValue = JsonValue.Create(b);
                    break;
                case "int":
                    if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    { problems.Add($"{key}：需要整数"); continue; }
                    newValue = JsonValue.Create(i);
                    break;
                case "double":
                    if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    { problems.Add($"{key}：需要小数"); continue; }
                    newValue = JsonValue.Create(d);
                    break;
                case "string":
                    newValue = JsonValue.Create(text);
                    break;
                default:
                    continue;
            }

            if (newValue != null && newValue.ToJsonString() != original)
            {
                if (_node.ContainsKey(key)) _node[key] = newValue;
            }
        }

        if (problems.Count > 0)
        {
            MessageBox.Show(this, "以下字段格式不对，未保存：\n\n" + string.Join("\n", problems), "校验失败");
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
