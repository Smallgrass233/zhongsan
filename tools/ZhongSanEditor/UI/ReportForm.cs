namespace ZhongSanEditor.UI;

internal sealed class ReportForm : Form
{
    private readonly TextBox _text;

    private ReportForm(string title, string content)
    {
        Text = title;
        Width = 900;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        _text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5f),
            WordWrap = false,
            Text = content,
        };

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8) };
        var btnCopy = new Button { Text = "复制", Width = 90, Height = 32, Dock = DockStyle.Left };
        btnCopy.Click += (_, _) =>
        {
            try { Clipboard.SetText(_text.Text); } catch { }
        };
        var btnClose = new Button { Text = "关闭", Width = 90, Height = 32, Dock = DockStyle.Right };
        btnClose.Click += (_, _) => Close();
        bottom.Controls.Add(btnCopy);
        bottom.Controls.Add(btnClose);

        Controls.Add(_text);
        Controls.Add(bottom);
    }

    public static void ShowReport(IWin32Window owner, string title, string content)
    {
        using var f = new ReportForm(title, content);
        f.ShowDialog(owner);
    }
}

internal sealed class BackupForm : Form
{
    private readonly ListView _list = new();
    private readonly string _dir;
    private readonly string _target;

    public BackupForm(string backupDir, string targetFile)
    {
        _dir = backupDir;
        _target = targetFile;

        Text = "备份与恢复";
        Width = 860;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.Columns.Add("时间", 170);
        _list.Columns.Add("大小", 110);
        _list.Columns.Add("备份文件", 540);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 102, Padding = new Padding(8) };
        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = "恢复 = 把选中的备份复制回目标文件（当前文件会先自动再备份一次）。\r\n备份目录：" + backupDir,
            AutoEllipsis = true,
        };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.LeftToRight };
        var btnRestore = new Button { Text = "恢复到当前文件", Width = 150, Height = 32, Enabled = targetFile != null };
        var btnOpenDir = new Button { Text = "打开备份目录", Width = 130, Height = 32 };
        var btnRefresh = new Button { Text = "刷新", Width = 80, Height = 32 };
        var btnClose = new Button { Text = "关闭", Width = 80, Height = 32 };
        bar.Controls.AddRange(new Control[] { btnRestore, btnOpenDir, btnRefresh, btnClose });
        bottom.Controls.Add(bar);
        bottom.Controls.Add(info);

        btnRefresh.Click += (_, _) => Reload();
        btnClose.Click += (_, _) => Close();
        btnOpenDir.Click += (_, _) =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_dir) { UseShellExecute = true }); }
            catch { }
        };
        btnRestore.Click += (_, _) => Restore();

        Controls.Add(_list);
        Controls.Add(bottom);
        Reload();
    }

    private void Reload()
    {
        _list.Items.Clear();
        var items = Core.BackupService.List(_dir, _target);
        foreach (var (path, time, size) in items)
        {
            var li = new ListViewItem(time.ToString("yyyy-MM-dd HH:mm:ss"));
            li.SubItems.Add((size / 1024.0 / 1024.0).ToString("0.00") + " MB");
            li.SubItems.Add(System.IO.Path.GetFileName(path));
            li.Tag = path;
            _list.Items.Add(li);
        }
        if (_list.Items.Count > 0) _list.Items[0].Selected = true;
    }

    private void Restore()
    {
        if (_target == null || _list.SelectedItems.Count == 0) return;
        var src = _list.SelectedItems[0].Tag as string;
        if (src == null) return;

        if (MessageBox.Show(this, $"将用备份覆盖：\n{_target}\n\n来源：{System.IO.Path.GetFileName(src)}\n\n继续？",
                "确认恢复", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        try
        {
            var safety = Core.BackupService.Backup(_target, _dir);
            File.Copy(src, _target, overwrite: true);
            MessageBox.Show(this, "已恢复。\n恢复前状态另存为：" + System.IO.Path.GetFileName(safety), "完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "恢复失败：" + ex.Message, "错误");
        }
    }
}
