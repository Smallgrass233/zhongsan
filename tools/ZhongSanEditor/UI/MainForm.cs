using System.ComponentModel;
using System.Text.Json.Nodes;
using ZhongSanEditor.Core;

namespace ZhongSanEditor.UI;

internal sealed class MainForm : Form
{
    private readonly AppConfig _cfg;
    private GameDocument _doc;
    private GameIndex _index;
    private readonly List<PersonRow> _all = new();
    private List<PersonRow> _filtered = new();
    private readonly HashSet<int> _blocked = new();
    private bool _loading;

    private readonly TextBox _txtSearch = new() { Width = 240, PlaceholderText = "姓名或 ID" };
    private readonly ComboBox _cbSex = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cbRange = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cbState = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _numAbil = new() { Width = 90, Minimum = 0, Maximum = 600, Value = 0, Increment = 10 };
    private readonly CheckBox _chkFemaleOnly = new() { Text = "只看女性", AutoSize = true };
    private readonly DataGridView _grid = new();
    private readonly Label _lblStats = new() { AutoSize = false, Height = 96, Dock = DockStyle.Top };
    private readonly Label _lblFile = new() { AutoSize = false, Height = 40, Dock = DockStyle.Top, AutoEllipsis = true };
    private readonly ToolStripStatusLabel _sslFile = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private string _sortProp;
    private bool _sortAsc = true;

    private readonly string _startupFile;

    public MainForm(string startupFile = null)
    {
        _startupFile = startupFile;
        _cfg = AppConfig.Load();
        Text = "中三武将管理工具";
        Width = 1400;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 640);

        BuildUi();

        Shown += (_, _) =>
        {
            var file = _startupFile ?? _cfg.LastFile;
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
                LoadDocument(file);
        };
    }

    private void BuildUi()
    {
        var menu = new MenuStrip();

        var mFile = new ToolStripMenuItem("文件(&F)");
        mFile.DropDownItems.Add(Item("打开剧本…", Keys.Control | Keys.O, (_, _) => OpenWith(true)));
        mFile.DropDownItems.Add(Item("打开存档…", Keys.Control | Keys.D, (_, _) => OpenWith(false)));
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add(Item("重新载入", Keys.F5, (_, _) => { if (_doc != null) LoadDocument(_doc.Path); }));
        mFile.DropDownItems.Add(Item("另存为…", Keys.None, (_, _) => SaveAs()));
        mFile.DropDownItems.Add(new ToolStripSeparator());
        mFile.DropDownItems.Add(Item("退出", Keys.None, (_, _) => Close()));

        var mList = new ToolStripMenuItem("名单(&B)");
        mList.DropDownItems.Add(Item("导出名单…", Keys.None, (_, _) => ExportBlockList()));
        mList.DropDownItems.Add(Item("导入名单…", Keys.None, (_, _) => ImportBlockList()));
        mList.DropDownItems.Add(new ToolStripSeparator());
        mList.DropDownItems.Add(Item("清空勾选", Keys.None, (_, _) => ClearBlocked()));

        var mTools = new ToolStripMenuItem("工具(&T)");
        mTools.DropDownItems.Add(Item("应用屏蔽到另一个文件…", Keys.None, (_, _) => ApplyToAnotherFile()));
        mTools.DropDownItems.Add(Item("备份与恢复…", Keys.None, (_, _) => ShowBackups()));
        mTools.DropDownItems.Add(new ToolStripSeparator());
        mTools.DropDownItems.Add(Item("关于", Keys.None, (_, _) => MessageBox.Show(this,
            "中三武将管理工具\n\n读取剧本/存档 JSON，批量把武将设为永久下架，并清理城池、部队、俘虏、配偶等引用。\n写入前自动备份。\n\n命令行：--file <json> --ids 1,2,3 [--force]",
            "关于", MessageBoxButtons.OK, MessageBoxIcon.Information)));

        menu.Items.AddRange(new ToolStripItem[] { mFile, mList, mTools });
        MainMenuStrip = menu;

        // 左侧筛选面板
        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
            Width = 320,
        };

        left.Controls.Add(Caption("当前文件"));
        _lblFile.Font = new Font(Font, FontStyle.Regular);
        left.Controls.Add(_lblFile);

        var gbFilter = new GroupBox { Text = "筛选", Width = 296, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var fl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(8) };
        fl.Controls.Add(Caption("搜索"));
        fl.Controls.Add(_txtSearch);
        _txtSearch.TextChanged += (_, _) => RebuildView();

        fl.Controls.Add(Caption("性别"));
        _cbSex.Items.AddRange(new object[] { "全部", "女", "男" });
        _cbSex.SelectedIndex = 0;
        _cbSex.SelectedIndexChanged += (_, _) => RebuildView();
        fl.Controls.Add(_cbSex);

        fl.Controls.Add(Caption("ID 段"));
        _cbRange.Items.AddRange(new object[] { "全部", "0-6999 三国", "7000-7999", "8000-8999", "9000-24999 新增", "25000+ 随机" });
        _cbRange.SelectedIndex = 0;
        _cbRange.SelectedIndexChanged += (_, _) => RebuildView();
        fl.Controls.Add(_cbRange);

        fl.Controls.Add(Caption("状态"));
        _cbState.Items.AddRange(new object[] { "全部", "已登场", "未登场", "已勾选", "未勾选" });
        _cbState.SelectedIndex = 0;
        _cbState.SelectedIndexChanged += (_, _) => RebuildView();
        fl.Controls.Add(_cbState);

        fl.Controls.Add(Caption("五维总分 ≥"));
        _numAbil.ValueChanged += (_, _) => RebuildView();
        fl.Controls.Add(_numAbil);

        _chkFemaleOnly.CheckedChanged += (_, _) => RebuildView();
        fl.Controls.Add(_chkFemaleOnly);

        gbFilter.Controls.Add(fl);
        left.Controls.Add(gbFilter);

        var gbRule = new GroupBox { Text = "规则勾选", Width = 296, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        var fr = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(8) };
        fr.Controls.Add(Btn("9000 段全部女性", (_, _) => ApplyRule(r => r.IsFemale && r.Id >= 9000 && r.Id < 25000)));
        fr.Controls.Add(Btn("五维 ≥ 阈值 的女性", (_, _) => ApplyRule(r => r.IsFemale && r.Abil >= (int)_numAbil.Value && _numAbil.Value > 0)));
        fr.Controls.Add(Btn("五维 ≥ 300 的女性", (_, _) => ApplyRule(r => r.IsFemale && r.Abil >= 300)));
        fr.Controls.Add(Btn("当前筛选结果 → 勾选", (_, _) => ApplyRule(_ => true, useFiltered: true)));
        fr.Controls.Add(Btn("当前筛选结果 → 取消勾选", (_, _) => ApplyRule(_ => true, useFiltered: true, value: false)));
        fr.Controls.Add(Btn("全部清除勾选", (_, _) => ClearBlocked()));
        gbRule.Controls.Add(fr);
        left.Controls.Add(gbRule);

        _lblStats.Padding = new Padding(4);
        left.Controls.Add(_lblStats);

        var btnApply = new Button
        {
            Text = "应用屏蔽到当前文件…",
            Width = 296,
            Height = 44,
            BackColor = Color.FromArgb(0xC6, 0x28, 0x28),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        btnApply.Click += (_, _) => ApplyToCurrent(force: false);
        left.Controls.Add(btnApply);

        // 表格
        _grid.Dock = DockStyle.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoGenerateColumns = false;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.EditMode = DataGridViewEditMode.EditOnEnter;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "colBlocked", HeaderText = "屏蔽", DataPropertyName = "Blocked",
            Width = 52, Frozen = true, SortMode = DataGridViewColumnSortMode.NotSortable,
        });
        _grid.Columns.Add(Col("colId", "ID", "Id", 62));
        _grid.Columns.Add(Col("colName", "姓名", "DisplayName", 120));
        _grid.Columns.Add(Col("colSex", "性别", "SexText", 52));
        _grid.Columns.Add(Col("colAbil", "五维", "Abil", 62));
        _grid.Columns.Add(Col("colCmd", "将略", "Command", 52));
        _grid.Columns.Add(Col("colStr", "武学", "Strength", 52));
        _grid.Columns.Add(Col("colInt", "谋略", "Intelligence", 52));
        _grid.Columns.Add(Col("colPol", "政理", "Politics", 52));
        _grid.Columns.Add(Col("colGla", "风度", "Glamour", 52));
        _grid.Columns.Add(Col("colActive", "状态", "ActiveText", 82));
        _grid.Columns.Add(Col("colAlive", "生死", "AliveText", 56));
        _grid.Columns.Add(Col("colYAvail", "出场年", "YearAvailable", 62));
        _grid.Columns.Add(Col("colBorn", "生年", "YearBorn", 52));
        _grid.Columns.Add(Col("colDead", "卒年", "YearDead", 52));
        _grid.Columns.Add(Col("colGen", "世代", "Generation", 52));
        _grid.Columns.Add(Col("colWhere", "所在", "Where", 150));
        _grid.Columns.Add(Col("colFaction", "势力", "Faction", 96));
        _grid.Columns.Add(Col("colRange", "ID 段", "IdRangeText", 92));

        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
                _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) =>
        {
            if (_loading || e.RowIndex < 0) return;
            if (e.ColumnIndex != _grid.Columns["colBlocked"].Index) return;
            SyncBlockedFromRows();
            UpdateStats();
        };
        _grid.ColumnHeaderMouseClick += (_, e) => SortByColumn(e.ColumnIndex);
        _grid.CellFormatting += GridCellFormatting;
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditPerson(e.RowIndex); };

        var ctx = new ContextMenuStrip();
        ctx.Items.Add("查看 / 编辑此人…", null, (_, _) => { if (_grid.CurrentRow != null) EditPerson(_grid.CurrentRow.Index); });
        ctx.Items.Add("勾选", null, (_, _) => SetCurrentRowBlocked(true));
        ctx.Items.Add("取消勾选", null, (_, _) => SetCurrentRowBlocked(false));
        _grid.ContextMenuStrip = ctx;

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 330, FixedPanel = FixedPanel.Panel1 };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(_grid);

        var status = new StatusStrip();
        status.Items.Add(_sslFile);

        Controls.Add(split);
        Controls.Add(status);
        Controls.Add(menu);
    }

    private static ToolStripMenuItem Item(string text, Keys keys, EventHandler handler)
    {
        var it = new ToolStripMenuItem(text, null, handler);
        if (keys != Keys.None) it.ShortcutKeys = keys;
        return it;
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, ForeColor = Color.DimGray };

    private static Button Btn(string text, EventHandler handler)
    {
        var b = new Button { Text = text, Width = 262, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
        b.Click += handler;
        return b;
    }

    private static DataGridViewTextBoxColumn Col(string name, string header, string prop, int width) => new()
    {
        Name = name, HeaderText = header, DataPropertyName = prop, Width = width,
        ReadOnly = true, SortMode = DataGridViewColumnSortMode.Programmatic,
    };

    // ---------------- 载入 ----------------

    private void OpenWith(bool scenario)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "中三数据 (*.json)|*.json|所有文件 (*.*)|*.*",
            InitialDirectory = scenario ? _cfg.ScenarioDir : _cfg.SaveDir,
            Title = scenario ? "选择剧本 JSON" : "选择存档 JSON",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        LoadDocument(dlg.FileName);
        if (scenario) { _cfg.ScenarioDir = Path.GetDirectoryName(dlg.FileName); _cfg.Save(); }
        else { _cfg.SaveDir = Path.GetDirectoryName(dlg.FileName); _cfg.Save(); }
    }

    private void LoadDocument(string path)
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            var doc = GameDocument.Load(path);
            _doc = doc;
            _index = GameIndex.Build(doc);
            _all.Clear();
            foreach (var p in doc.Persons())
            {
                var row = new PersonRow(doc, p);
                var info = _index.Get(row.Id);
                if (info != null) { row.Where = info.Where; row.Faction = info.Faction; }
                row.Blocked = _blocked.Contains(row.Id);
                _all.Add(row);
            }

            _cfg.LastFile = path;
            _cfg.Save();

            _lblFile.Text = path;
            _sslFile.Text = $"{doc.Kind} | {doc.Title} | {_all.Count} 人 | {doc.OriginalSize / 1024 / 1024} MB";
            Text = $"中三武将管理工具 - {Path.GetFileName(path)}";
            RebuildView();
            UpdateStats();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "打开失败：\n" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void SaveAs()
    {
        if (_doc == null) return;
        using var dlg = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = Path.GetFileNameWithoutExtension(_doc.Path) + "-mod.json",
            InitialDirectory = Path.GetDirectoryName(_doc.Path),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _doc.Save(dlg.FileName);
            MessageBox.Show(this, "已另存：\n" + dlg.FileName, "完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "另存失败：" + ex.Message, "错误");
        }
    }

    // ---------------- 视图 ----------------

    private void RebuildView()
    {
        if (_all.Count == 0) { _filtered = new List<PersonRow>(); BindGrid(); return; }

        IEnumerable<PersonRow> q = _all;
        var s = _txtSearch.Text.Trim();
        if (s.Length > 0)
        {
            q = q.Where(r => r.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || r.CalledName.Contains(s, StringComparison.OrdinalIgnoreCase)
                             || r.Id.ToString() == s);
        }

        switch (_cbSex.SelectedIndex)
        {
            case 1: q = q.Where(r => r.IsFemale); break;
            case 2: q = q.Where(r => !r.IsFemale); break;
        }

        q = _cbRange.SelectedIndex switch
        {
            1 => q.Where(r => r.Id < 7000),
            2 => q.Where(r => r.Id >= 7000 && r.Id < 8000),
            3 => q.Where(r => r.Id >= 8000 && r.Id < 9000),
            4 => q.Where(r => r.Id >= 9000 && r.Id < 25000),
            5 => q.Where(r => r.Id >= 25000),
            _ => q,
        };

        q = _cbState.SelectedIndex switch
        {
            1 => q.Where(r => r.IsActive),
            2 => q.Where(r => !r.IsActive),
            3 => q.Where(r => r.Blocked),
            4 => q.Where(r => !r.Blocked),
            _ => q,
        };

        if (_numAbil.Value > 0) q = q.Where(r => r.Abil >= (int)_numAbil.Value);
        if (_chkFemaleOnly.Checked) q = q.Where(r => r.IsFemale);

        _filtered = q.ToList();
        BindGrid();
        UpdateStats();
    }

    private void BindGrid()
    {
        _loading = true;
        if (!string.IsNullOrEmpty(_sortProp)) SortList(_filtered, _sortProp, _sortAsc);
        _grid.DataSource = null;
        _grid.DataSource = new BindingList<PersonRow>(_filtered);
        _loading = false;
    }

    private void SortByColumn(int columnIndex)
    {
        if (columnIndex < 0) return;
        var col = _grid.Columns[columnIndex];
        var prop = col.DataPropertyName;
        if (string.IsNullOrEmpty(prop) || prop == "Blocked") return;
        if (_sortProp == prop) _sortAsc = !_sortAsc;
        else { _sortProp = prop; _sortAsc = true; }
        foreach (DataGridViewColumn c in _grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
        col.HeaderCell.SortGlyphDirection = _sortAsc ? SortOrder.Ascending : SortOrder.Descending;
        BindGrid();
    }

    private static void SortList(List<PersonRow> list, string prop, bool asc)
    {
        var pi = typeof(PersonRow).GetProperty(prop);
        if (pi == null) return;
        Comparison<PersonRow> cmp = (a, b) =>
        {
            var x = pi.GetValue(a);
            var y = pi.GetValue(b);
            if (x == null && y == null) return 0;
            if (x == null) return 1;
            if (y == null) return -1;
            if (x is IComparable cx) return cx.CompareTo(y);
            return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
        };
        list.Sort((a, b) => asc ? cmp(a, b) : cmp(b, a));
    }

    private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _grid.Rows.Count) return;
        if (_grid.Rows[e.RowIndex].DataBoundItem is not PersonRow row) return;

        if (row.Blocked)
        {
            e.CellStyle.BackColor = Color.FromArgb(0xEC, 0xEC, 0xEC);
            e.CellStyle.ForeColor = Color.Gray;
        }
        else if (row.IsFemale)
        {
            e.CellStyle.BackColor = Color.FromArgb(0xFD, 0xF0, 0xF4);
        }

        if (row.IsFemale && row.Abil >= 300 && _grid.Columns[e.ColumnIndex].Name == "colAbil")
        {
            e.CellStyle.Font = new Font(_grid.Font, FontStyle.Bold);
            e.CellStyle.ForeColor = Color.FromArgb(0xB0, 0x10, 0x30);
        }
    }

    private void UpdateStats()
    {
        if (_all.Count == 0) { _lblStats.Text = "未打开文件。"; return; }
        int fem = _all.Count(r => r.IsFemale);
        int active = _all.Count(r => r.IsActive);
        int blocked = _all.Count(r => r.Blocked);
        int blockedActive = _all.Count(r => r.Blocked && r.IsActive);
        int shown = _filtered.Count;
        _lblStats.Text =
            $"总计：{_all.Count} 人，女性 {fem}，已登场 {active}\r\n" +
            $"已勾选屏蔽：{blocked}（其中已登场 {blockedActive}）\r\n" +
            $"当前显示：{shown} 行";
    }

    private void SyncBlockedFromRows()
    {
        _blocked.Clear();
        foreach (var r in _all) if (r.Blocked) _blocked.Add(r.Id);
    }

    private void SetCurrentRowBlocked(bool value)
    {
        if (_grid.CurrentRow?.DataBoundItem is not PersonRow row) return;
        row.Blocked = value;
        _grid.InvalidateRow(_grid.CurrentRow.Index);
        SyncBlockedFromRows();
        UpdateStats();
    }

    private void ApplyRule(Func<PersonRow, bool> pred, bool useFiltered = false, bool value = true)
    {
        var target = useFiltered ? _filtered : _all;
        int n = 0;
        foreach (var r in target)
        {
            if (!pred(r)) continue;
            if (r.Blocked == value) continue;
            r.Blocked = value;
            n++;
        }
        SyncBlockedFromRows();
        _grid.Refresh();
        UpdateStats();
        MessageBox.Show(this, $"已{(value ? "勾选" : "取消")} {n} 人。", "规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ClearBlocked()
    {
        foreach (var r in _all) r.Blocked = false;
        _blocked.Clear();
        _grid.Refresh();
        UpdateStats();
    }

    // ---------------- 编辑 ----------------

    private void EditPerson(int rowIndex)
    {
        if (_grid.Rows[rowIndex].DataBoundItem is not PersonRow row) return;
        using var dlg = new PersonDetailForm(row);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        row.RaiseAll();
        _grid.Refresh();
        UpdateStats();
    }

    // ---------------- 应用 ----------------

    private List<int> SelectedIds() => _all.Where(r => r.Blocked).Select(r => r.Id).ToList();

    private void ApplyToCurrent(bool force)
    {
        if (_doc == null) return;
        var ids = SelectedIds();
        if (ids.Count == 0)
        {
            MessageBox.Show(this, "还没有勾选任何武将。", "提示");
            return;
        }

        int active = _all.Count(r => r.Blocked && r.IsActive);
        var answer = MessageBox.Show(this,
            $"即将把 {ids.Count} 名武将设为永久下架（其中 {active} 人当前已登场）。\n\n" +
            $"将清理城池、部队、俘虏、宝物、配偶等处的引用。\n写入前会自动备份。\n\n继续？",
            "确认应用", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (answer != DialogResult.Yes) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            var rep = BlockService.Apply(_doc, ids, force);
            var backup = BackupService.Backup(_doc.Path, _cfg.BackupDir);
            _doc.Save();
            Cursor = Cursors.Default;
            ReportForm.ShowReport(this, "应用完成", rep.ToText() + $"\n备份文件：{backup}");
            LoadDocument(_doc.Path);
        }
        catch (LeaderConflictException ex)
        {
            Cursor = Cursors.Default;
            var msg = "下列人物是势力君主 / 储君 / 城池太守 / 部队主将：\n\n" +
                      string.Join("\n", ex.Conflicts) +
                      "\n\n继续会：势力君主/储君自动改立他人，城池太守与部队主将清空。\n建议先在游戏里存个档，出现异常可回退。仍要继续吗？";
            if (MessageBox.Show(this, msg, "冲突", MessageBoxButtons.YesNo, MessageBoxIcon.Stop,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                ApplyToCurrent(force: true);
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, "应用失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyToAnotherFile()
    {
        var ids = SelectedIds();
        if (ids.Count == 0)
        {
            MessageBox.Show(this, "还没有勾选任何武将。\n\n先在左侧勾选，或使用规则批量勾选。", "提示");
            return;
        }

        using var dlg = new OpenFileDialog
        {
            Filter = "中三数据 (*.json)|*.json|所有文件 (*.*)|*.*",
            Title = $"把当前勾选的 {ids.Count} 人应用到另一个文件（如剧本）",
            InitialDirectory = _doc != null ? Path.GetDirectoryName(_doc.Path) : _cfg.ScenarioDir,
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            var doc = GameDocument.Load(dlg.FileName);
            var rep = BlockService.Apply(doc, ids, force: false);
            var backup = BackupService.Backup(dlg.FileName, _cfg.BackupDir);
            doc.Save();
            Cursor = Cursors.Default;
            ReportForm.ShowReport(this, "应用到 " + Path.GetFileName(dlg.FileName),
                rep.ToText() + $"\n备份文件：{backup}");
        }
        catch (LeaderConflictException ex)
        {
            Cursor = Cursors.Default;
            var msg = "目标文件里存在冲突（君主/主将/太守）：\n\n" + string.Join("\n", ex.Conflicts) +
                      "\n\n仍要继续吗？";
            if (MessageBox.Show(this, msg, "冲突", MessageBoxButtons.YesNo, MessageBoxIcon.Stop,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                try
                {
                    var doc = GameDocument.Load(dlg.FileName);
                    var rep = BlockService.Apply(doc, ids, force: true);
                    var backup = BackupService.Backup(dlg.FileName, _cfg.BackupDir);
                    doc.Save();
                    ReportForm.ShowReport(this, "应用完成", rep.ToText() + $"\n备份文件：{backup}");
                }
                catch (Exception ex2)
                {
                    MessageBox.Show(this, "应用失败：" + ex2.Message, "错误");
                }
            }
        }
        catch (Exception ex)
        {
            Cursor = Cursors.Default;
            MessageBox.Show(this, "应用失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------------- 名单 ----------------

    private void ExportBlockList()
    {
        var ids = SelectedIds();
        if (ids.Count == 0) { MessageBox.Show(this, "名单为空。", "提示"); return; }
        using var dlg = new SaveFileDialog { Filter = "名单 (*.json)|*.json", FileName = "blocklist.json" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var names = new JsonObject();
        foreach (var id in ids)
        {
            var r = _all.FirstOrDefault(x => x.Id == id);
            names[id.ToString()] = r?.DisplayName ?? "";
        }
        var root = new JsonObject
        {
            ["ids"] = new JsonArray(ids.Select(i => (JsonNode)JsonValue.Create(i)).ToArray()),
            ["names"] = names,
            ["exportedAt"] = DateTime.Now.ToString("s"),
        };
        File.WriteAllText(dlg.FileName, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        _cfg.LastBlockList = dlg.FileName;
        _cfg.Save();
        MessageBox.Show(this, $"已导出 {ids.Count} 个 ID。", "完成");
    }

    private void ImportBlockList()
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "名单 (*.json)|*.json|文本 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            InitialDirectory = string.IsNullOrEmpty(_cfg.LastBlockList) ? null : Path.GetDirectoryName(_cfg.LastBlockList),
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var ids = new List<int>();
        try
        {
            if (dlg.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in File.ReadAllLines(dlg.FileName))
                    if (int.TryParse(line.Trim(), out var id)) ids.Add(id);
            }
            else
            {
                var node = JsonNode.Parse(File.ReadAllText(dlg.FileName));
                if (node is JsonObject o && o["ids"] is JsonArray arr)
                    foreach (var n in arr)
                        if (int.TryParse(n?.ToJsonString(), out var id)) ids.Add(id);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "读取名单失败：" + ex.Message, "错误");
            return;
        }

        var set = new HashSet<int>(ids);
        int hit = 0;
        foreach (var r in _all)
        {
            bool b = set.Contains(r.Id);
            if (b) hit++;
            r.Blocked = b;
        }
        SyncBlockedFromRows();
        _grid.Refresh();
        UpdateStats();
        MessageBox.Show(this, $"名单里 {ids.Count} 个 ID，当前文件命中 {hit} 个。", "导入完成");
    }

    // ---------------- 备份 ----------------

    private void ShowBackups()
    {
        using var dlg = new BackupForm(_cfg.BackupDir, _doc?.Path);
        dlg.ShowDialog(this);
    }
}
