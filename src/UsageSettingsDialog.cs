using System.Drawing.Drawing2D;
namespace CodexTokenOverlay;

internal static class WheelNavigation
{
    internal static void Scroll(Control control, MouseEventArgs e) { if (e is HandledMouseEventArgs handled) handled.Handled = true; for (var parent = control.Parent; parent != null; parent = parent.Parent) if (parent is ScrollableControl scroll && scroll.AutoScroll) { var y = Math.Max(0, -scroll.AutoScrollPosition.Y - e.Delta / 120 * 48); scroll.AutoScrollPosition = new Point(-scroll.AutoScrollPosition.X, y); return; } }
}
internal sealed class PageNumeric : NumericUpDown { protected override void OnMouseWheel(MouseEventArgs e) { WheelNavigation.Scroll(this, e); } }
internal sealed partial class PageSlider : TrackBar { protected override void OnMouseWheel(MouseEventArgs e) { WheelNavigation.Scroll(this, e); } }
internal sealed class PageChoice : ComboBox { internal void RefreshLanguage()=>RefreshItems(); protected override void OnMouseWheel(MouseEventArgs e) { if (DroppedDown) base.OnMouseWheel(e); else WheelNavigation.Scroll(this, e); } }
internal sealed class ParameterSelection : IMessageFilter, IDisposable
{
    private readonly Form _form; private readonly Label _feedback;
    private readonly Dictionary<Control, (Control row, NumericUpDown number, string title)> _items = new();
    private Control? _selected; private Color _originalBackColor; private int _wheelRemainder;
    internal ParameterSelection(Form form, Label feedback) { _form = form; _feedback = feedback; Application.AddMessageFilter(this); Clear(); }
    internal void Register(Control row, NumericUpDown number, string title) { _items[row] = (row, number, title); number.AccessibleName = title; }
    private (Control row, NumericUpDown number, string title)? Item(Control? control) { for (var c = control; c != null; c = c.Parent) if (_items.TryGetValue(c, out var item)) return item; return null; }
    private void RestoreSelection()
    {
        if (_selected is { IsDisposed: false }) _selected.BackColor = _originalBackColor;
        _selected = null;
    }
    internal void Toggle(Control control)
    {
        _wheelRemainder = 0;
        var item = Item(control);
        if (item == null) { Clear(); return; }
        var selecting = _selected != item.Value.row;
        RestoreSelection();
        if (selecting)
        {
            _selected = item.Value.row;
            _originalBackColor = _selected.BackColor;
            _selected.BackColor = Color.FromArgb(208, 229, 255);
        }
        _feedback.Text = _selected == null ? "单击参数行选中 → 滚轮调整；再次单击或 Esc 退出。未选中时滚动页面。" : $"已选中：{item.Value.title} · 滚轮调整 · 再次单击或 Esc 退出";
    }
    internal void Clear()
    {
        _wheelRemainder = 0; RestoreSelection();
        _feedback.Text = "单击参数行选中 → 滚轮调整；再次单击或 Esc 退出。未选中时滚动页面。";
    }
    internal bool Wheel(Control control, int delta) { var item = Item(control); if (item == null || item.Value.row != _selected) return false; var n = item.Value.number; _wheelRemainder += delta; var steps = _wheelRemainder / 120; _wheelRemainder %= 120; n.Value = Math.Clamp(n.Value + steps * n.Increment, n.Minimum, n.Maximum); return true; }
    public bool PreFilterMessage(ref Message m)
    {
        var control = Control.FromChildHandle(m.HWnd); if (control?.FindForm() != _form) return false;
        if (m.Msg == 0x201) { Toggle(control); return false; }
        if (m.Msg == 0x100 && m.WParam.ToInt32() == 27 && _selected != null) { Clear(); return true; }
        if (m.Msg == 0x100 && m.WParam.ToInt32() == 32 && Item(control) != null) { Toggle(control); return true; }
        if (m.Msg != 0x20A) return false;
        var packed = m.LParam.ToInt64(); var pt = new Point((short)(packed & 65535), (short)((packed >> 16) & 65535)); Control hit = _form; while (hit.Controls.Cast<Control>().FirstOrDefault(c => c.Visible && c.Bounds.Contains(hit.PointToClient(pt))) is { } child) hit = child;
        if (hit is ComboBox combo && combo.DroppedDown) return false;
        var delta = (short)((m.WParam.ToInt64() >> 16) & 65535); if (!Wheel(hit, delta)) WheelNavigation.Scroll(hit, new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, delta)); return true;
    }
    public void Dispose() { Application.RemoveMessageFilter(this); }
}
internal sealed class LayoutGrid : Control
{
    internal Func<UsageCardSettings> Settings = null!;
    internal Action<GaugeModule> SelectModule = null!;
    internal Action<GaugeModule, int, int> MoveModule = null!;
    internal Func<GaugeModule?> Selected = null!;
    private GaugeModule? _drag;
    private Point _down;
    public LayoutGrid()
    {
        DoubleBuffered = true; AllowDrop = true; Size = new Size(330, 210); BackColor = Color.FromArgb(239, 242, 247);
        MouseDown += (_, e) => { _down = e.Location; var s = Settings(); var row = Math.Clamp(e.Y * s.Rows / Height, 0, s.Rows - 1); var col = Math.Clamp(e.X * s.Columns / Width, 0, s.Columns - 1); var hit = s.Modules.FirstOrDefault(m => m.Enabled && m.Row == row && m.Column == col); if (hit != null) { SelectModule(hit); _drag = hit; } else if (Selected() is { } selected) MoveModule(selected, row, col); };
        MouseMove += (_, e) => { if (e.Button == MouseButtons.Left && _drag != null && Math.Abs(e.X - _down.X) + Math.Abs(e.Y - _down.Y) > 6) { var module = _drag; _drag = null; DoDragDrop(module, DragDropEffects.Move); } };
        MouseUp += (_, _) => _drag = null;
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(typeof(GaugeModule)) == true ? DragDropEffects.Move : DragDropEffects.None;
        DragDrop += (_, e) => { if (e.Data?.GetData(typeof(GaugeModule)) is not GaugeModule module) return; var p = PointToClient(new Point(e.X, e.Y)); var s = Settings(); MoveModule(module, Math.Clamp(p.Y * s.Rows / Height, 0, s.Rows - 1), Math.Clamp(p.X * s.Columns / Width, 0, s.Columns - 1)); };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var s = Settings(); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        for (var row = 0; row < s.Rows; row++) for (var col = 0; col < s.Columns; col++)
        {
            var rect = new Rectangle(col * Width / s.Columns + 3, row * Height / s.Rows + 3, Width / s.Columns - 6, Height / s.Rows - 6);
            var module = s.Modules.FirstOrDefault(m => m.Enabled && m.Row == row && m.Column == col);
            using var fill = new SolidBrush(module == Selected() && module != null ? Color.FromArgb(210, 227, 252) : Color.White); e.Graphics.FillRectangle(fill, rect);
            using var pen = new Pen(Color.FromArgb(171, 185, 204)); e.Graphics.DrawRectangle(pen, rect);
            var text = UiText.T(module == null ? $"{row + 1} 行 · {col + 1} 列\n空位" : $"{module.Metric}\n{module.Style}");
            TextRenderer.DrawText(e.Graphics, text, Font, rect, Color.FromArgb(41, 54, 73), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        }
    }
}
internal sealed partial class UsageSettingsDialog : Form
{
    private readonly ParameterSelection _selection;
    private readonly Label _wheelHint = new() { Dock = DockStyle.Top, Height = 40, Padding = new Padding(12, 8, 0, 0), BackColor = Color.FromArgb(230, 239, 252) };
    private readonly Label _imageName = new() { Width = 540, Height = 28 };
    private bool _sync, _closing;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly Panel _preview = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 26, 35) };
    private readonly Label _hint = new() { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(8) };
    private readonly ListBox _modules = new() { Width = 185, Height = 210 };
    private readonly ComboBox _columns = Choice(new object[] { 1, 2, 3 }, 80);
    private readonly NumericUpDown _rows = new PageNumeric { Minimum = 1, Maximum = UsageCardSettings.MaximumRows, Width = 80 };
    private readonly ComboBox _style = Choice(Enum.GetValues<GaugeStyle>().Cast<object>().ToArray(), 110);
    private readonly ComboBox _align = Choice(Enum.GetValues<RowAlignment>().Cast<object>().ToArray(), 100);
    private readonly Label _alignmentText = new() { Width = 280, AutoSize = false };
    private FlowLayoutPanel? _alignmentPanel, _referencePanel, _ringPanel;
    private readonly Label _referenceHelp = new() { Width = 550, Height = 36 };
    private readonly ComboBox _ring = Choice(Enum.GetValues<RingValuePosition>().Cast<object>().ToArray(), 110);
    private readonly ComboBox _font = Choice(new object[] { "Microsoft YaHei UI", "Segoe UI", "Consolas" }, 230);
    private readonly Toggle _enabled = new(), _labels = new(), _values = new(), _hover = new(), _drag = new(), _bold = new(), _contrast = new(), _live = new(), _warnings = new(), _summary = new();
    private readonly NumericUpDown _reference = new PageNumeric() { Minimum = 1, Maximum = 1_000_000_000, Width = 130, ThousandsSeparator = true };
    private readonly Dictionary<string, NumericUpDown> _numbers = new();
    private readonly List<(Button, Func<Color>)> _colors = new();
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 120 };
    private readonly UsageCardForm _previewCard = new();
    private Bitmap? _previewBitmap;
    private ComboBox? _themes;
    private readonly LayoutGrid _grid = new();
    private GaugeModule? Current => _modules.SelectedItem as GaugeModule;
    public event Action<UsageCardSettings?>? PreviewChanged;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool HostLight { get => _previewCard.Light; set { _previewCard.Light = value; _preview.BackColor = value ? Color.FromArgb(239, 242, 247) : Color.FromArgb(24, 26, 35); } }
    public UsageCardSettings Result { get; private set; }
    public UsageSettingsDialog(UsageCardSettings current)
    {
        _selection = new(this, _wheelHint); _tabs.SelectedIndexChanged += (_, _) => _selection.Clear(); Deactivate += (_, _) => _selection.Clear();
        Result = current.Clone(); UiText.Language=Result.Language; AutoScaleDimensions=new SizeF(96,96); AutoScaleMode=AutoScaleMode.Dpi;
        _previewCard.Quota = new(new(32, 300, null), new(84, 10080, null), DateTimeOffset.Now);
        _previewCard.Vitals = new("gpt-6.1-sol", "medium", "已完成", 23.4, 2900, 18.5);
        _previewCard.Tokens = new("preview", "", 124050, 114000, 86000, 10050, 2020, 48000, 200000, DateTime.UtcNow);
        Text = "Codex Rail · 仪表控制面板 · 布局编辑"; Width = 900; Height = 740; MinimumSize = new Size(620, 300); Font = SettingsTypography.Body; StartPosition = FormStartPosition.CenterScreen;
        var split = new SplitContainer { Size = new Size(860, 620), Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel2, SplitterDistance = 610, Panel2MinSize = 170 }; split.Panel1.Controls.Add(_tabs); split.Panel2.Controls.Add(_preview);
        _preview.Paint += (_, e) => { TextRenderer.DrawText(e.Graphics, UiText.T("示例数据预览"), Font, new Point(Dip(12), Dip(7)), HostLight ? Color.Black : Color.White); if (_previewBitmap == null) return; var scale = Math.Min(1f, Math.Min((_preview.Height-Dip(45))/(float)_previewBitmap.Height,(_preview.Width-Dip(20))/(float)_previewBitmap.Width)); e.Graphics.DrawImage(_previewBitmap, (_preview.Width - _previewBitmap.Width * scale) / 2, Dip(25), _previewBitmap.Width * scale, _previewBitmap.Height * scale); };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RenderPreview(); };
        var layout = Page("布局与模块");
        var dimensions = new FlowLayoutPanel { Width = 570, Height = 38 }; dimensions.Controls.AddRange([new Label { Text = "列数", AutoSize = true }, _columns, new Label { Text = "行数", AutoSize = true }, _rows]);
        var auto = new Button { Text = "自动匹配尺寸", AutoSize = true }; auto.Click += (_, _) => { Result.AutoFit(); SyncNumbers(); Changed(); }; dimensions.Controls.Add(auto); layout.Controls.Add(dimensions); _selection.Register(_rows, _rows, "行数");
        layout.Controls.Add(new Label { Text = "选中左侧模块，再点空格放置；也可拖入格子。拖到已有模块上会交换。", Width = 570, Height = 28 });
        var board = new FlowLayoutPanel { Width = 570, Height = 220 }; board.Controls.Add(_modules); board.Controls.Add(_grid); layout.Controls.Add(board);
        _grid.Settings = () => Result; _grid.Selected = () => Current; _grid.SelectModule = m => _modules.SelectedItem = m; _grid.MoveModule = (m, r, c) => { Result.Place(m, r, c); RefreshModules(m); Changed(); };
        _columns.SelectedIndexChanged += (_, _) => { if (_sync) return; Result.Columns = (int)_columns.SelectedItem!; Result.Reflow(); SyncNumbers(); RefreshModules(Current); Changed(); };
        _rows.ValueChanged += (_, _) => { if (_sync) return; Result.Rows = (int)_rows.Value; RefreshGridSize(); Result.Reflow(); SyncNumbers(); RefreshModules(Current); Changed(); };
        _modules.SelectedIndexChanged += (_, _) => { if (!_sync) SyncEditor(); _grid.Invalidate(); };
        _modules.MouseMove += (_, e) => { if (e.Button == MouseButtons.Left && Current is { } m) _modules.DoDragDrop(m, DragDropEffects.Move); };
        var edit = new FlowLayoutPanel { Width = 570, Height = 40 }; edit.Controls.AddRange([_enabled, new Label { Text = "显示样式", AutoSize = true }, _style]); layout.Controls.Add(edit);
        _enabled.CheckedChanged += (_, _) => { if (_sync || Current == null) return; Current.Enabled = _enabled.Checked; Current.Unplaced = false; if (Current.Enabled) { var free = FreeCell(); if (free == null) { Current.Enabled = false; Current.Unplaced = true; } else Result.Place(Current, free.Value.r, free.Value.c); } RefreshModules(Current); Changed(); };
        _style.SelectedIndexChanged += (_, _) => { if (_sync || Current == null) return; Current.Style = (GaugeStyle)_style.SelectedItem!; SyncEditor(); Changed(); };
        var ringRow = new FlowLayoutPanel { Width = 570, Height = 36 }; _ringPanel = ringRow; ringRow.Controls.AddRange([new Label { Text = "圆环数值位置", AutoSize = true }, _ring, new Label { Text = "受显示数值开关控制", AutoSize = true }]); layout.Controls.Add(ringRow);
        _ring.SelectedIndexChanged += (_, _) => { if (_sync || Current == null) return; Current.RingValue = (RingValuePosition)_ring.SelectedItem!; Changed(); };
        var extra = new FlowLayoutPanel { Width = 570, Height = 76 }; _referencePanel = extra; extra.Controls.AddRange([new Label { Text = "满格对应的数值", AutoSize = true }, _reference, _referenceHelp]); layout.Controls.Add(extra);
        _selection.Register(_reference, _reference, "满格对应的数值");
        _reference.ValueChanged += (_, _) => { if (_sync || Current == null) return; Current.Reference = (long)_reference.Value; Changed(); };
        var alignment = new FlowLayoutPanel { Width = 570, Height = 38 }; _alignmentPanel = alignment; alignment.Controls.AddRange([_alignmentText, _align]); layout.Controls.Add(alignment);
        _align.SelectedIndexChanged += (_, _) => { if (_sync || Current is not { } m) return; Result.Alignments[m.Row] = (RowAlignment)_align.SelectedItem!; Changed(); };
        Switch(layout, "主窗口实时预览（试用）", _live, () => Result.LivePreview, v => { Result.LivePreview = v; if (!v) PreviewChanged?.Invoke(null); });
        var size = Page("尺寸与位置"); Number(size, "单元宽度", 12, 100, () => Result.CellWidth, v => Result.CellWidth = v); Number(size, "长条行高度", 48, 150, () => Result.CellHeight, v => Result.CellHeight = v); Number(size, "模块间隔", 0, 24, () => Result.Gap, v => Result.Gap = v); Number(size, "水平偏移（居中 0）", -100, 100, () => Result.HorizontalOffset, v => Result.HorizontalOffset = v);
        size.Controls.Add(new Label { Text = "改变行列会自动匹配参考尺寸，可在这里继续微调。\n窄栏边界固定，偏移 -100 贴左、+100 贴右；底部保留默认位置。", Width = 570, Height = 46 });
        size.Controls.Add(new Label { Text = "点击参数行选中后，滚轮调整；再次点击退出选中。\n文字行和圆环行自动收紧，长条行高度在此单独调整。", Width = 570, Height = 46 });
        var look = Page("外观与动画"); Heading(look, "主题与配色"); var theme = Choice(UsageCardSettings.ThemeNames.Cast<object>().ToArray(), 230); _themes = theme; theme.SelectedIndex = 0; var themeRow = new FlowLayoutPanel { Width = 550, Height = 38 }; var applyTheme = new Button { Text = "应用配色", Width = 110 }; applyTheme.Click += (_, _) => { Result.ApplyTheme(theme.SelectedIndex + 1); SyncColors(); Changed(); }; themeRow.Controls.AddRange([theme, applyTheme]); look.Controls.Add(themeRow);
        theme.SelectedIndexChanged += (_, _) => { if (_sync || theme.SelectedIndex < 0) return; Result.ApplyTheme(theme.SelectedIndex + 1); SyncColors(); Changed(); };
        var palette = new FlowLayoutPanel { Width = 550, Height = 80 }; look.Controls.Add(palette); ColorChoice(palette, "背景", () => Result.Background, c => Result.Background = c); ColorChoice(palette, "重点色", () => Result.Accent, c => Result.Accent = c); ColorChoice(palette, "额度提醒对比色", () => Result.Warning, c => Result.Warning = c); ColorChoice(palette, "文字", () => Result.Text, c => { Result.Text = c; Result.AutoText = false; _sync = true; _contrast.Checked = false; _sync = false; });

        Heading(look, "字体"); RefreshFonts(); look.Controls.Add(_font); _font.SelectedIndexChanged += (_, _) => { if (_sync || _font.SelectedItem is not FontAsset asset) return; Result.FontFamily = asset.Name; Result.FontFile = asset.File; Changed(); };
        _font.AutoCompleteSource = AutoCompleteSource.ListItems; _font.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        var fonts = new FlowLayoutPanel { Width = 570, Height = 38 }; var importFont = new Button { Text = "导入字体…", Width = 120 }; importFont.Click += (_, _) => ImportFont(); fonts.Controls.Add(importFont); fonts.Controls.Add(new Label { Text = "TTF / OTF；仅本组件使用", AutoSize = true, Padding = new Padding(0, 7, 0, 0) }); look.Controls.Add(fonts);

        Heading(look, "背景图片"); var images = new FlowLayoutPanel { Width = 570, Height = 38 }; var importImage = new Button { Text = "导入图片…", Width = 120 }; var clearImage = new Button { Text = "移除图片", Width = 100 }; importImage.Click += (_, _) => ImportImage(); clearImage.Click += (_, _) => { Result.BackgroundImage = ""; Result.BackgroundImageName = ""; _imageName.Text = "未使用背景图片"; Changed(); }; images.Controls.AddRange([importImage, clearImage]); look.Controls.Add(images); look.Controls.Add(_imageName); Number(look, "图片不透明度 %", 0, 100, () => (int)Math.Round(Result.ImageOpacity * 100d / 255), v => Result.ImageOpacity = (int)Math.Round(v * 255d / 100));
        Heading(look, "图形与文字"); Switch(look, "允许竖向拖动", _drag, () => Result.AllowDrag, v => Result.AllowDrag = v); Switch(look, "文字加粗", _bold, () => Result.BoldText, v => Result.BoldText = v); Switch(look, "文字跟随 Codex 明暗", _contrast, () => Result.AutoText, v => Result.AutoText = v); Number(look, "圆环外径", 8, 48, () => Result.RingDiameter, v => Result.RingDiameter = v); Number(look, "竖条粗细", 2, 24, () => Result.BarWidth, v => Result.BarWidth = v); Number(look, "圆环粗细", 1, 12, () => Result.RingWidth, v => Result.RingWidth = v); Number(look, "背景不透明度 %", 0, 100, () => (int)Math.Round(Result.Opacity * 100d / 255), v => Result.Opacity = (int)Math.Round(v * 255d / 100)); Number(look, "详情背景不透明度 %", 0, 100, () => (int)Math.Round(Result.DetailOpacity * 100d / 255), v => Result.DetailOpacity = (int)Math.Round(v * 255d / 100));
        Switch(look, "显示名称", _labels, () => Result.ShowLabels, v => Result.ShowLabels = v); Switch(look, "显示数值", _values, () => Result.ShowValues, v => Result.ShowValues = v); Switch(look, "悬停展开", _hover, () => Result.HoverDetails, v => Result.HoverDetails = v);
        Heading(look, "动画"); Number(look, "打开淡入毫秒", 120, 1200, () => Result.FadeMs, v => Result.FadeMs = v); Number(look, "展开耗时毫秒", 80, 1600, () => Result.ExpandMs, v => Result.ExpandMs = v);
        var account = Page("额度与状态");
        Switch(account, "额度不足变色", _warnings, () => Result.QuotaWarnings, v => Result.QuotaWarnings = v);
        Number(account, "五小时剩余阈值 %", 0, 100, () => Result.FiveHourWarning, v => Result.FiveHourWarning = v);
        Number(account, "每周剩余阈值 %", 0, 100, () => Result.WeeklyWarning, v => Result.WeeklyWarning = v);

        Switch(account, "详情显示状态摘要", _summary, () => Result.ShowSessionDetails, v => Result.ShowSessionDetails = v);
        account.Controls.Add(new Label { Text = "达到剩余阈值时变色，恢复后还原；数据过期时不提示。\n平均速度=本轮输出 Token ÷ 整轮耗时，包含等待和工具执行。\n首 Token 等待仅在日志提供时显示。模型与状态可选为独立模块。", Width = 570, Height = 76 });
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) }; var save = new Button { Text = "保存并应用", AutoSize = true }; var cancel = new Button { Text = "取消", AutoSize = true }; var reset = new Button { Text = "恢复当前页默认", AutoSize = true };
        save.Click += (_, _) => { if (Result.Validate() is { } error) { MessageBox.Show(this, UiText.T(error), UiText.T("请调整")); return; } DialogResult = DialogResult.OK; Close(); }; cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); }; reset.Click += (_, _) => { Result.RestorePage(_tabs.SelectedIndex); SyncAll(); Changed(); }; actions.Controls.AddRange([save, cancel, reset]); Controls.Add(split); Controls.Add(_hint); Controls.Add(actions); Controls.Add(_wheelHint); AcceptButton = save; CancelButton = cancel;
        InstallResponsive(split,actions); SyncAll(); RenderPreview();
    }
    private static ComboBox Choice(object[] items, int width) { var combo = new PageChoice { Width = width, DropDownStyle = ComboBoxStyle.DropDownList }; combo.Items.AddRange(items); return combo; }
    private FlowLayoutPanel Page(string title) { var tab = new TabPage(title); var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(10) }; tab.Controls.Add(flow); _tabs.TabPages.Add(tab); return flow; }
    private (int r, int c)? FreeCell() { for (var r = 0; r < Result.Rows; r++) for (var c = 0; c < Result.Columns; c++) if (!Result.Modules.Any(m => m.Enabled && m.Row == r && m.Column == c)) return (r, c); return null; }
    private void RefreshModules(GaugeModule? selected = null) { _sync = true; _modules.BeginUpdate(); _modules.Items.Clear(); _modules.Items.AddRange(Result.Modules.Cast<object>().ToArray()); if (selected != null && Result.Modules.Contains(selected)) _modules.SelectedItem = selected; else if (_modules.Items.Count > 0) _modules.SelectedIndex = 0; _modules.EndUpdate(); _sync = false; SyncEditor(); }
    private void SyncEditor() { _sync = true; var m = Current; _enabled.Enabled = _style.Enabled = m != null; if (m != null) { _enabled.Checked = m.Enabled; _style.SelectedItem = m.Style; _ring.SelectedItem = m.RingValue; _reference.Value = Math.Clamp(m.Reference, 1, 1_000_000_000); } _style.Enabled = m != null && m.Metric is not (MetricKind.模型 or MetricKind.任务状态); _ring.Enabled = m?.Style == GaugeStyle.环形; if (_ringPanel != null) _ringPanel.Visible = _ring.Enabled; _reference.Enabled = m != null && m.Style != GaugeStyle.数值 && m.Metric is MetricKind.聊天Token or MetricKind.输入Token or MetricKind.输出Token or MetricKind.推理Token or MetricKind.缓存Token or MetricKind.上下文Token or MetricKind.上下文容量 or MetricKind.平均输出速度 or MetricKind.首Token等待; if (_referencePanel != null) _referencePanel.Visible = _reference.Enabled; _referenceHelp.Text = m?.Metric == MetricKind.平均输出速度 ? "单位 tok/s；达到此速度时填满。只控制图形比例，不是额度限制。" : m?.Metric == MetricKind.首Token等待 ? "单位秒；达到此等待时间时填满。只控制图形比例。" : "单位 Token；达到此数量时填满。只控制图形比例，不是账户额度。"; if (_alignmentPanel != null) { _alignmentPanel.Visible = Result.Columns > 1 && m is { Enabled: true } && Result.Modules.Count(other => other.Enabled && other.Row == m.Row) == 1; if (m != null) { _alignmentText.Text = $"第 {m.Row + 1} 行仅一项：在整行中放置"; _align.SelectedItem = Result.Alignments[m.Row]; } } _sync = false; }
    private void SyncAll() { _sync = true; if (_themes != null) _themes.SelectedIndex = Math.Clamp(Result.ThemeId - 1, 0, UsageCardSettings.ThemeNames.Length - 1); _columns.SelectedItem = Result.Columns; _rows.Value = Result.Rows; _font.SelectedItem = _font.Items.Cast<FontAsset>().FirstOrDefault(f => f.Name == Result.FontFamily && f.File == Result.FontFile); _imageName.Text = string.IsNullOrEmpty(Result.BackgroundImage) ? "未使用背景图片" : Result.BackgroundImageName; _labels.Checked = Result.ShowLabels; _values.Checked = Result.ShowValues; _hover.Checked = Result.HoverDetails; _drag.Checked = Result.AllowDrag; _bold.Checked = Result.BoldText; _contrast.Checked = Result.AutoText; _live.Checked = Result.LivePreview; _warnings.Checked = Result.QuotaWarnings; _summary.Checked = Result.ShowSessionDetails; _sync = false; RefreshGridSize(); SyncNumbers(); SyncColors(); RefreshModules(); _grid.Invalidate(); }
    private readonly Dictionary<string, Func<int>> _readNumbers = new();
    private void SyncNumbers() { var before = _sync; _sync = true; foreach (var pair in _numbers) pair.Value.Value = Math.Clamp(_readNumbers[pair.Key](), (int)pair.Value.Minimum, (int)pair.Value.Maximum); _sync = before; }
    private void SyncColors() { foreach (var (button, get) in _colors) { button.BackColor = get(); button.ForeColor = get().GetBrightness() < .5 ? Color.White : Color.Black; } }
    private void Changed() { if (_closing) return; RefreshGridSize(); if (Result.CompactWidth > 52) { Result.Gap = Math.Min(Result.Gap, Math.Max(0, (46 - Result.Columns * 12) / Math.Max(1, Result.Columns - 1))); Result.CellWidth = Math.Max(12, (46 - (Result.Columns - 1) * Result.Gap) / Result.Columns); SyncNumbers(); } _hint.Text = Result.Validate() ?? $"{Result.Columns} 列 × {Result.Rows} 行 · {Result.CompactWidth} × {Result.CompactHeight} · 示例预览"; _grid.Invalidate(); _debounce.Stop(); _debounce.Start(); }
    private string HeightNote() => Result.CompactHeight > Result.AvailableHeightDip ? $" · 当前窗口偏小，建议不超过 {Math.Max(1, (Result.AvailableHeightDip - 12 + Result.Gap) / (48 + Result.Gap))} 行" : "";
    private void RenderPreview() { if (Result.Validate() != null) return; var clock = System.Diagnostics.Stopwatch.StartNew(); _previewCard.Settings = Result; var bitmap = _previewCard.Render((uint)DeviceDpi); var old = _previewBitmap; _previewBitmap = bitmap; old?.Dispose(); _preview.Invalidate(); if (Result.LivePreview) PreviewChanged?.Invoke(Result.Clone()); clock.Stop(); _hint.Text = $"{Result.CompactWidth} × {Result.CompactHeight} · {(Result.LivePreview ? "主窗口实时预览" : "示例预览")} · 本次 {clock.Elapsed.TotalMilliseconds:F1} ms{HeightNote()}"; }
    private void Number(FlowLayoutPanel flow, string title, int min, int max, Func<int> read, Action<int> write) { var row = new FlowLayoutPanel { Width = 570, Height = 44, WrapContents = false }; var n = new PageNumeric { Minimum = min, Maximum = max, Width = 82 }; var slider = new PageSlider { Minimum = min, Maximum = max, Width = 235, Height = 35, TickStyle = TickStyle.None }; row.Controls.AddRange([new Label { Text = title, Width = 180, Padding = new Padding(0, 6, 0, 0) }, n, slider]); n.ValueChanged += (_, _) => { slider.Value = (int)n.Value; if (_sync) return; write((int)n.Value); Changed(); }; slider.ValueChanged += (_, _) => n.Value = slider.Value; _selection.Register(row, n, title); slider.AccessibleName = title; _numbers[title] = n; _readNumbers[title] = read; flow.Controls.Add(row); }
    private void Switch(FlowLayoutPanel flow, string title, Toggle toggle, Func<bool> read, Action<bool> write) { var row = new FlowLayoutPanel { Width = 570, Height = 36 }; row.Controls.AddRange([new Label { Text = title, Width = 150 }, toggle]); toggle.CheckedChanged += (_, _) => { if (_sync) return; write(toggle.Checked); Changed(); }; flow.Controls.Add(row); }
    private void ColorChoice(FlowLayoutPanel flow, string title, Func<Color> read, Action<Color> write) { var button = new Button { Text = title + "颜色", Width = 230, Height = 30 }; button.Click += (_, _) => { using var picker = new ColorDialog { FullOpen = true, Color = read() }; if (picker.ShowDialog(this) == DialogResult.OK) { write(picker.Color); SyncColors(); Changed(); } }; _colors.Add((button, read)); flow.Controls.Add(button); }
    private static void Heading(FlowLayoutPanel flow, string title) { flow.Controls.Add(new Label { Text = title, Font = SettingsTypography.Heading, Width = 550, Height = 38, Padding = new Padding(0, 10, 0, 0) }); }
    private void RefreshFonts() { var before = _sync; _sync = true; _font.Items.Clear(); _font.Items.AddRange(AssetStore.Fonts().Cast<object>().ToArray()); _font.SelectedItem = _font.Items.Cast<FontAsset>().FirstOrDefault(f => f.Name == Result.FontFamily && f.File == Result.FontFile); _sync = before; }
    private void ImportFont() { using var picker = new OpenFileDialog { Title = UiText.T("导入字体"), Filter = "字体文件 (*.ttf;*.otf)|*.ttf;*.otf", CheckFileExists = true }; if (picker.ShowDialog(this) != DialogResult.OK) return; try { var font = AssetStore.ImportFont(picker.FileName); Result.FontFile = font.File; Result.FontFamily = font.Name; RefreshFonts(); Changed(); } catch (Exception ex) { MessageBox.Show(this, UiText.T("字体导入失败：") + ex.Message, UiText.T("导入字体")); } }
    private void ImportImage() { using var picker = new OpenFileDialog { Title = UiText.T("导入背景图片"), Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", CheckFileExists = true }; if (picker.ShowDialog(this) != DialogResult.OK) return; try { Result.BackgroundImage = AssetStore.ImportImage(picker.FileName); Result.BackgroundImageName = Path.GetFileName(picker.FileName); _imageName.Text = Result.BackgroundImageName; Changed(); } catch (Exception ex) { MessageBox.Show(this, UiText.T("图片导入失败：") + ex.Message, UiText.T("导入背景图片")); } }
    protected override void Dispose(bool disposing) { if (disposing) { _selection.Dispose(); _debounce.Dispose(); _previewBitmap?.Dispose(); _previewCard.Dispose(); } base.Dispose(disposing); }
    protected override void OnFormClosing(FormClosingEventArgs e) { _closing = true; _sync = true; _debounce.Stop(); base.OnFormClosing(e); }
}
