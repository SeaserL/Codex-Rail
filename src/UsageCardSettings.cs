using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace CodexTokenOverlay;

internal enum GaugeStyle { 电量条, 环形, 竖条, 数值 }
internal enum RingValuePosition { 环外, 环内, 不显示 }
internal enum RowAlignment { 居中, 靠左, 靠右 }
internal enum MetricKind { 聊天Token, 上下文占用, 周剩余, 五小时剩余, 缓存命中, 输入Token, 输出Token, 推理Token, 缓存Token, 上下文Token, 上下文容量, 平均输出速度, 首Token等待, 模型, 任务状态 }
internal sealed class GaugeModule
{
    public bool Enabled { get; set; } = true;
    public MetricKind Metric { get; set; }
    public RingValuePosition RingValue { get; set; }
    public GaugeStyle Style { get; set; } = GaugeStyle.竖条;
    public int Row { get; set; }
    public int Column { get; set; }
    public bool Unplaced { get; set; }
    public long Reference { get; set; } = 1_000_000;
    public override string ToString() => UiText.T(Metric + (Enabled ? "" : "（已关闭）"));
}
internal sealed class UsageCardSettings
{
    public const int MaximumRows = 16;
    [JsonIgnore] public int AvailableHeightDip { get; set; } = 980;
    public int Version { get; set; } = 14;
    public UiLanguage Language { get; set; } = UiLanguage.System;
    [JsonIgnore] public int RuntimeMinimumHeight { get; set; } = 310;
    [JsonIgnore] public int RuntimeFooterHeight { get; set; }
    public bool AllowDrag { get; set; } = true;
    public bool BoldText { get; set; } = true;
    public bool Material { get; set; }
    public int BarWidth { get; set; } = 8;
    public int RingDiameter { get; set; } = 36;
    public int HorizontalOffset { get; set; }
    public bool AutoText { get; set; } = true;
    public bool LivePreview { get; set; } = true;
    public int RingWidth { get; set; } = 4;
    public double BottomDip { get; set; } = 110;
    public RowAlignment[] Alignments { get; set; } = new RowAlignment[MaximumRows];
    public int Columns { get; set; } = 1;
    public int Rows { get; set; } = 4;
    public int CellWidth { get; set; } = 38;
    public int CellHeight { get; set; } = 88;
    public int Gap { get; set; } = 10;
    public int SidebarWidth { get; set; } = 56;
    public string BackgroundHex { get; set; } = "#191B25";
    public string AccentHex { get; set; } = "#94B9FF";
    public string TextHex { get; set; } = "#DEE2F2";
    [JsonIgnore] public Color Background { get => ColorTranslator.FromHtml(BackgroundHex); set => BackgroundHex = ColorTranslator.ToHtml(value); }
    [JsonIgnore] public Color Accent { get => ColorTranslator.FromHtml(AccentHex); set => AccentHex = ColorTranslator.ToHtml(value); }
    [JsonIgnore] public Color Text { get => ColorTranslator.FromHtml(TextHex); set => TextHex = ColorTranslator.ToHtml(value); }
    public int Opacity { get; set; } = 0;
    public int DetailOpacity { get; set; } = 235;
    public int ThemeId { get; set; } = 1;
    public string FontFile { get; set; } = "";
    public string BackgroundImage { get; set; } = "";
    public string BackgroundImageName { get; set; } = "";
    public int ImageOpacity { get; set; } = 160;
    public static readonly string[] ThemeNames = ["午夜蓝", "暖灰", "高对比", "浅色", "荧光绿", "紫罗兰", "海洋青", "珊瑚橙", "樱花粉", "石墨金", "森林绿", "冰川蓝"];
    public string FontFamily { get; set; } = "Microsoft YaHei UI";
    public bool ShowLabels { get; set; }
    public bool ShowValues { get; set; } = true;
    public bool ShowSessionDetails { get; set; } = true;
    public bool QuotaWarnings { get; set; } = true;
    public int FiveHourWarning { get; set; } = 20;
    public int WeeklyWarning { get; set; } = 20;
    public string WarningHex { get; set; } = "#FF5C93";
    [JsonIgnore] public Color Warning { get => ColorTranslator.FromHtml(WarningHex); set => WarningHex = ColorTranslator.ToHtml(value); }
    public bool HoverDetails { get; set; } = true;
    public int FadeMs { get; set; } = 360;
    public int ExpandMs { get; set; } = 150;
    public List<GaugeModule> Modules { get; set; } = [
        new() { Metric = MetricKind.聊天Token, Row = 0, Style = GaugeStyle.数值 },
        new() { Metric = MetricKind.上下文占用, Row = 1 },
        new() { Metric = MetricKind.周剩余, Row = 2, Style = GaugeStyle.环形, RingValue=RingValuePosition.环内 },
        new() { Metric=MetricKind.五小时剩余,Row=3,Style=GaugeStyle.环形,RingValue=RingValuePosition.环内 },
        new() { Metric = MetricKind.平均输出速度, Enabled=false,Style=GaugeStyle.数值,Reference=100 },
        new() { Metric = MetricKind.缓存命中, Enabled=false },
        new() { Metric = MetricKind.输入Token, Enabled=false, Style=GaugeStyle.数值 },
        new() { Metric = MetricKind.输出Token, Enabled=false, Style=GaugeStyle.数值 },
        new() { Metric = MetricKind.推理Token, Enabled=false, Style=GaugeStyle.数值 },
        new() { Metric = MetricKind.缓存Token, Enabled=false, Style=GaugeStyle.数值 },
        new() { Metric = MetricKind.上下文Token, Enabled=false, Style=GaugeStyle.数值 },
        new() { Metric = MetricKind.上下文容量, Enabled=false, Style=GaugeStyle.数值 },
        new() { Metric=MetricKind.首Token等待,Enabled=false,Style=GaugeStyle.数值,Reference=10 },
        new() { Metric=MetricKind.模型,Enabled=false,Style=GaugeStyle.数值 },
        new() { Metric=MetricKind.任务状态,Enabled=false,Style=GaugeStyle.数值 } ];
    [JsonIgnore] public int CompactWidth => 6 + Columns * CellWidth + (Columns - 1) * Gap;
    public int RowHeight(int row)
    {
        var modules = Modules.Where(m => m.Enabled && m.Row == row).ToArray(); if (modules.Length == 0) return 36;
        return modules.Max(m => m.Style == GaugeStyle.数值 ? (ShowLabels ? 52 : 36) : m.Style == GaugeStyle.环形 ? Math.Max(44, Math.Min(RingDiameter, (modules.Length == 1 ? CompactWidth - 6 : CellWidth) - 4) + 12 + (ShowValues && m.RingValue == RingValuePosition.环外 ? 18 : 0) + (ShowLabels ? 18 : 0)) : CellHeight);
    }
    public int RowTop(int row) => 6 + Enumerable.Range(0, row).Sum(r => RowHeight(r) + Gap);
    [JsonIgnore] public int CompactHeight => Math.Max(RuntimeMinimumHeight, 12 + Enumerable.Range(0, Rows).Sum(RowHeight) + (Rows - 1) * Gap + RuntimeFooterHeight);
    public void AutoFit()
    {
        Gap = Columns == 1 ? 8 : 4;
        CellWidth = Math.Clamp((SidebarWidth - 10 - (Columns - 1) * Gap) / Columns, 12, 100);
        var target = Math.Min(AvailableHeightDip, Rows <= 4 ? 394 : 12 + Rows * 72 + (Rows - 1) * Gap);
        CellHeight = Math.Clamp((target - 12 - (Rows - 1) * Gap) / Rows, 48, 120);
    }
    public void Reflow()
    {
        var index = 0;
        foreach (var module in Modules.Where(m => m.Enabled || m.Unplaced))
        {
            module.Unplaced = index >= Rows * Columns;
            module.Enabled = !module.Unplaced;
            if (module.Enabled) { module.Row = index / Columns; module.Column = index % Columns; }
            index++;
        }
        AutoFit();
    }
    public void Place(GaugeModule module, int row, int column)
    {
        var occupied = Modules.FirstOrDefault(m => m != module && m.Enabled && m.Row == row && m.Column == column);
        if (occupied != null) { if (module.Enabled) { occupied.Row = module.Row; occupied.Column = module.Column; } else { occupied.Enabled = false; occupied.Unplaced = true; } }
        module.Row = row; module.Column = column; module.Enabled = true; module.Unplaced = false;
    }
    public UsageCardSettings Clone() { var clone = JsonSerializer.Deserialize<UsageCardSettings>(JsonSerializer.Serialize(this))!; clone.AvailableHeightDip = AvailableHeightDip; return clone; }
    public void ExpandAlignments() { if (Alignments?.Length == MaximumRows) return; var expanded = new RowAlignment[MaximumRows]; if (Alignments is not null) Array.Copy(Alignments, expanded, Math.Min(Alignments.Length, expanded.Length)); Alignments = expanded; }
    public void ApplyDefaultLayout()
    {
        Columns = 1; Rows = 4; CellWidth = 38; CellHeight = 88; Gap = 10; AllowDrag = true; ShowValues = true; Alignments = new RowAlignment[MaximumRows]; foreach (var module in Modules) { module.Enabled = false; module.Unplaced = false; }
        var metrics = new[] { MetricKind.聊天Token, MetricKind.上下文占用, MetricKind.周剩余, MetricKind.五小时剩余 }; for (var i = 0; i < metrics.Length; i++) { var m = Modules.FirstOrDefault(m => m.Metric == metrics[i]); if (m is null) { m = new() { Metric = metrics[i] }; Modules.Add(m); } m.Enabled = true; m.Row = i; m.Column = 0; m.Style = i == 1 ? GaugeStyle.竖条 : i >= 2 ? GaugeStyle.环形 : GaugeStyle.数值; if (i >= 2) m.RingValue = RingValuePosition.环内; if (i == 3) m.Reference = 100; }
    }
    public void ApplyTheme(int index) { ThemeId = index; var colors = index switch { 6 => new[] { "#211C30", "#BC9EFF", "#EEE8FF", "#FFC66F" }, 7 => new[] { "#12272A", "#57D8D5", "#E0FAF7", "#FF919B" }, 8 => new[] { "#30201D", "#FF9979", "#FFF0E9", "#79D5FF" }, 9 => new[] { "#2D2029", "#F6ADD1", "#FEEEF8", "#8EE7B6" }, 10 => new[] { "#23252A", "#E5CB83", "#F5F2E8", "#A7AEFF" }, 11 => new[] { "#192A22", "#89CDA1", "#E6F4E9", "#F5A8C3" }, 12 => new[] { "#172631", "#91D8FF", "#E7F5FE", "#FFB87E" }, 2 => new[] { "#272321", "#EAB879", "#F1E8D8", "#B49CFF" }, 3 => new[] { "#101114", "#77DCBC", "#FFFFFF", "#FF6B78" }, 4 => new[] { "#F1F3F8", "#4268BD", "#222739", "#B93452" }, 5 => new[] { "#162019", "#A5FF39", "#F3FFE7", "#FF5CB9" }, _ => new[] { "#191B25", "#94B9FF", "#DEE2F2", "#FF5C93" } }; BackgroundHex = colors[0]; AccentHex = colors[1]; TextHex = colors[2]; WarningHex = colors[3]; Material = index == 5; }
    public void RestorePage(int page)
    {
        var d = new UsageCardSettings(); switch (page)
        {
            case 0: Columns = d.Columns; Rows = d.Rows; Modules = d.Modules; Alignments = d.Alignments; LivePreview = d.LivePreview; break;
            case 1: CellWidth = d.CellWidth; CellHeight = d.CellHeight; Gap = d.Gap; HorizontalOffset = d.HorizontalOffset; if (CompactWidth > SidebarWidth - 4) AutoFit(); break;
            case 2: ApplyTheme(1); FontFamily = d.FontFamily; FontFile = ""; BackgroundImage = ""; BackgroundImageName = ""; ImageOpacity = d.ImageOpacity; Opacity = d.Opacity; DetailOpacity = d.DetailOpacity; AllowDrag = d.AllowDrag; BoldText = d.BoldText; AutoText = d.AutoText; RingDiameter = d.RingDiameter; BarWidth = d.BarWidth; RingWidth = d.RingWidth; ShowLabels = d.ShowLabels; ShowValues = d.ShowValues; HoverDetails = d.HoverDetails; FadeMs = d.FadeMs; ExpandMs = d.ExpandMs; break;
            case 3: QuotaWarnings = d.QuotaWarnings; FiveHourWarning = d.FiveHourWarning; WeeklyWarning = d.WeeklyWarning; ShowSessionDetails = d.ShowSessionDetails; break;
        }
    }
    public string? Validate()
    {
        if (ImageOpacity is < 0 or > 255) return "图片不透明度超出范围。";
        if (FiveHourWarning is < 0 or > 100 || WeeklyWarning is < 0 or > 100) return "剩余阈值范围 0–100%。";
        if (RingDiameter is < 8 or > 48 || HorizontalOffset is < -100 or > 100) return "圆环直径 8–48，水平偏移 -100–100。";
        if (BarWidth is < 2 or > 24 || RingWidth is < 1 or > 12) return "竖条粗细 2–24，圆环粗细 1–12。";
        if (Alignments is null || Alignments.Length != MaximumRows) return "行对齐设置无效。";
        if (Columns is < 1 or > 3 || Rows is < 1 or > MaximumRows) return "每行 1–3 个，纵向 1–16 行。";
        if (CellWidth is < 12 or > 100 || CellHeight is < 48 or > 150 || Gap is < 0 or > 24) return "单元宽 12–100，高 48–150，间隔 0–24。";
        if (SidebarWidth is < 24 or > 600 || CompactWidth > SidebarWidth - 4) return "布局超出窄栏：请减少列数、单元宽度或间隔。";
        if (Modules is null || Modules.Count > 16 || !Modules.Any(m => m.Enabled)) return "请启用至少一个模块，最多 16 个。";
        var occupied = new HashSet<(int, int)>();
        foreach (var m in Modules.Where(m => m.Enabled))
        {
            if (m.Row < 0 || m.Row >= Rows || m.Column < 0 || m.Column >= Columns) return "模块行列位置超出网格。";
            if (!occupied.Add((m.Row, m.Column))) return "两个模块不能占用同一格。";
            if (!Enum.IsDefined(m.RingValue) || !Enum.IsDefined(m.Style) || !Enum.IsDefined(m.Metric) || m.Reference < 1) return "模块样式或参考量无效。";

        }
        if (Opacity is < 0 or > 255 || DetailOpacity is < 0 or > 255 || FadeMs is < 120 or > 1200 || ExpandMs is < 80 or > 1600) return "动画或透明度超出范围。";
        try { _ = Background; _ = Accent; _ = Text; _ = Warning; } catch { return "颜色格式无效。"; }
        return null;
    }
}
internal sealed class Toggle : CheckBox
{
    public Toggle() { Appearance = Appearance.Button; FlatStyle = FlatStyle.Flat; Width = 64; Height = 28; TextAlign = ContentAlignment.MiddleCenter; CheckedChanged += (_, _) => RefreshText(); RefreshText(); }
    private void RefreshText() { Text = Checked ? "开启" : "关闭"; BackColor = Checked ? Color.FromArgb(61, 105, 164) : Color.FromArgb(54, 57, 70); ForeColor = Color.White; }
}
