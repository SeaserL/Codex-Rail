using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace CodexTokenOverlay;

internal sealed class UsageCardForm : Form
{
    internal UsageCardSettings Settings = new();
    private UsageCardSettings? _display;
    private string? _layoutSignature;
    private int _viewportHeight=int.MaxValue, _viewportWidth=56, _page;
    internal int PageCount { get; private set; }=1;
    private UsageCardSettings DisplaySettings => _display ?? Settings;
    private void UpdateLayout()
    {
            if(_viewportHeight==int.MaxValue){_display=null;return;}
            var key=System.Text.Json.JsonSerializer.Serialize(Settings)+$"|{_viewportHeight}|{_viewportWidth}|{_page}";
            if(key!=_layoutSignature){var layout=AdaptiveGaugeLayout.Create(Settings,_viewportHeight,_viewportWidth,_page);_display=layout.Settings;PageCount=layout.Pages;_layoutSignature=key;}
    }
    internal bool ConfigureViewport(int height,int width){var oldHeight=HeightDip;var oldWidth=WidthDip;var oldSignature=_layoutSignature;_viewportHeight=height;_viewportWidth=width;UpdateLayout();return oldSignature!=_layoutSignature||oldHeight!=HeightDip||oldWidth!=WidthDip;}
    internal void ChangePage(int delta){_page+=delta;_layoutSignature=null;}
    internal event Action? PageChanged;
    internal SessionVitals? Vitals;
    internal string SessionSource = "";
    internal double Expansion;
    public int WidthDip => DisplaySettings.CompactWidth + (int)Math.Round(266 * Expansion);
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Details { get => Expansion > 0; set => Expansion = value ? 1 : 0; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool Light { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal QuotaData? Quota { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal TokenSnapshot? Tokens { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal string Status { get; set; } = "正在读取额度…";
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    internal bool QuotaFailed { get; set; }
    public int HeightDip => DisplaySettings.CompactHeight;
    private Bitmap? _bitmap;
    private uint _dpi = 96;
    private Point _dragStart;
    private Point _windowStart;
    internal Point PresentedPosition;
    internal bool DragLocked;
    private bool _dragged;
    public event Action<Point>? DragFinished;
    public event Action? DragStarted;
    public event Action<Point>? DragMoved;
    internal Func<Point, Point>? ConstrainDrag;

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x80000 | 0x80 | 0x08000000; return p; }
    }
    public UsageCardForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        MouseDown += (_, e) => { if (e.Button == MouseButtons.Left && PageCount > 1 && e.Y >= Height - (int)Math.Round(24 * _dpi / 96d)) { ChangePage(e.X < Width / 2 ? -1 : 1); PageChanged?.Invoke(); return; } if (e.Button == MouseButtons.Left && DisplaySettings.AllowDrag && !DragLocked) { _dragStart = Cursor.Position; _windowStart = PresentedPosition; _dragged = false; DragStarted?.Invoke(); Capture = true; } };
        MouseMove += (_, e) =>
        {
            if (!Capture || e.Button != MouseButtons.Left) return;
            var delta = new Size(Cursor.Position.X - _dragStart.X, Cursor.Position.Y - _dragStart.Y);
            if (Math.Abs(delta.Width) + Math.Abs(delta.Height) > 5) _dragged = true;
            if (_dragged) { var desired = new Point(_windowStart.X, _windowStart.Y + delta.Height); var point = ConstrainDrag?.Invoke(desired) ?? desired; Present(point, 255); DragMoved?.Invoke(point); }
        };
        MouseWheel += (_, e) => { if(PageCount>1){ChangePage(e.Delta>0?-1:1);PageChanged?.Invoke();} };
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (!Capture) return;
            Capture = false;
            if (_dragged) DragFinished?.Invoke(PresentedPosition);
            _dragged = false;

        };
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21) { m.Result = 3; return; }
        if (m.Msg == 0x14) { m.Result = 1; return; }
        base.WndProc(ref m);
    }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    public void Rebuild(uint dpi)
    {
        _dpi = dpi == 0 ? 96 : dpi;
        _bitmap?.Dispose();
        _bitmap = Render(_dpi, interactive: true); _surfaceDirty = true;
    }
    private PrivateFontCollection? _privateFonts;
    private FontFamily? _privateFontFamily;
    private readonly Dictionary<float, Font> _fontCache = new();
    private string? _fontAsset, _fontName, _imageAsset;
    private bool _fontBold;
    private Bitmap? _backgroundImage;
    private void ClearFonts() { foreach (var font in _fontCache.Values) font.Dispose(); _fontCache.Clear(); _privateFontFamily?.Dispose(); _privateFontFamily = null; _privateFonts?.Dispose(); _privateFonts = null; }
    private Font MakeFont(float size)
    {
        if (_fontAsset != DisplaySettings.FontFile || _fontName != DisplaySettings.FontFamily || _fontBold != DisplaySettings.BoldText)
        {
            ClearFonts(); _fontAsset = DisplaySettings.FontFile; _fontName = DisplaySettings.FontFamily; _fontBold = DisplaySettings.BoldText;
            var path = AssetStore.Resolve("fonts", DisplaySettings.FontFile);
            if (File.Exists(path)) { try { _privateFonts = new(); _privateFonts.AddFontFile(path); var families = _privateFonts.Families; _privateFontFamily = families.FirstOrDefault(); foreach (var extraFamily in families.Skip(1)) extraFamily.Dispose(); } catch { ClearFonts(); } }
        }
        if (_fontCache.TryGetValue(size, out var cached)) return cached;
        var style = DisplaySettings.BoldText ? FontStyle.Bold : FontStyle.Regular;
        if (_privateFontFamily is { } family) { if (!family.IsStyleAvailable(style)) style = family.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold; cached = new Font(family, size, style, GraphicsUnit.Pixel); }
        else cached = new Font(DisplaySettings.FontFamily, size, style, GraphicsUnit.Pixel);
        _fontCache[size] = cached; return cached;
    }
    private void ImageBackground(Graphics g, RectangleF rect) { if (_imageAsset != DisplaySettings.BackgroundImage) { _backgroundImage?.Dispose(); _imageAsset = DisplaySettings.BackgroundImage; _backgroundImage = AssetStore.LoadImage(_imageAsset); } if (_backgroundImage == null || DisplaySettings.ImageOpacity == 0) return; var state = g.Save(); using var path = Round(rect, 4); g.SetClip(path, CombineMode.Intersect); var scale = Math.Max(rect.Width / _backgroundImage.Width, rect.Height / _backgroundImage.Height); var sourceWidth = rect.Width / scale; var sourceHeight = rect.Height / scale; using var attributes = new ImageAttributes(); var matrix = new ColorMatrix { Matrix33 = DisplaySettings.ImageOpacity / 255f }; attributes.SetColorMatrix(matrix); g.DrawImage(_backgroundImage, Rectangle.Round(rect), (_backgroundImage.Width - sourceWidth) / 2, (_backgroundImage.Height - sourceHeight) / 2, sourceWidth, sourceHeight, GraphicsUnit.Pixel, attributes); g.Restore(state); }
    private sealed record RenderSignature(string Appearance, TokenSnapshot? Tokens, QuotaData? Quota, SessionVitals? Vitals, string Status, bool Light, bool Failed, uint Dpi, bool Interactive, long Minute);
    private RenderSignature? _renderSignature;
    private Bitmap? _renderCache;
    private bool _cacheExpanded;
    public Bitmap Render(uint dpi, bool interactive = false)
    {
        UpdateLayout();
        var signature = new RenderSignature(System.Text.Json.JsonSerializer.Serialize(DisplaySettings) + $"|{HeightDip}|{UiText.IsEnglish}|{_page}", Tokens, Quota, Vitals, Status, Light, QuotaFailed, dpi, interactive, DateTimeOffset.Now.ToUnixTimeSeconds() / 60);
        if (signature != _renderSignature || (!_cacheExpanded && Expansion > 0))
        {
            _renderCache?.Dispose(); _renderCache = null; _cacheExpanded = Expansion > 0;
            var expansion = Expansion;
            try { Expansion = _cacheExpanded ? 1 : 0; _renderCache = DrawContent(dpi, interactive); _renderSignature = signature; }
            finally { Expansion = expansion; }
        }
        var scale = Math.Max(1, dpi) / 96f;
        var result = new Bitmap((int)Math.Round(WidthDip * scale), (int)Math.Round(HeightDip * scale), PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(result)) { graphics.CompositingMode = CompositingMode.SourceCopy; graphics.DrawImageUnscaled(_renderCache!, 0, 0); }
        return result;
    }
    private Bitmap DrawContent(uint dpi, bool interactive)
    {
        var factor = Math.Max(1, dpi) / 96f;
        var width = (int)Math.Round(WidthDip * factor);
        var height = (int)Math.Round(HeightDip * factor);
        // Supersampling and per-pixel alpha preserve smooth corner/text edges on transparent windows.
        using var large = new Bitmap(width * 2, height * 2, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(large))
        {
            g.Clear(Color.Transparent);
            g.ScaleTransform(factor * 2, factor * 2);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var text = DisplaySettings.AutoText ? Light ? Color.Black : Color.White : Color.FromArgb(255, DisplaySettings.Text);
            var muted = Color.FromArgb(230, text);
            var track = Color.FromArgb(100, text);
            using (var fill = SurfaceBrush(new RectangleF(1, 1, DisplaySettings.CompactWidth - 3, HeightDip - 3), DisplaySettings.Opacity))
            using (var path = Round(new RectangleF(1, 1, DisplaySettings.CompactWidth - 3, HeightDip - 3), 4))
            {
                g.FillPath(fill, path);
                ImageBackground(g, new RectangleF(1, 1, DisplaySettings.CompactWidth - 3, HeightDip - 3));
            }
            if (Expansion > 0)
            {
                using var detailFill = SurfaceBrush(new RectangleF(DisplaySettings.CompactWidth, 1, Math.Max(1, WidthDip - DisplaySettings.CompactWidth - 1), HeightDip - 2), DisplaySettings.DetailOpacity);
                g.FillRectangle(detailFill, DisplaySettings.CompactWidth, 1, Math.Max(0, WidthDip - DisplaySettings.CompactWidth - 1), HeightDip - 2);
            }
            if (Expansion > 0) ImageBackground(g, new RectangleF(DisplaySettings.CompactWidth, 1, Math.Max(1, WidthDip - DisplaySettings.CompactWidth - 1), HeightDip - 2));
            if(PageCount>1) TextAt(g, $"‹ {((_page % PageCount + PageCount) % PageCount) + 1}/{PageCount} ›", new RectangleF(0, HeightDip-22, DisplaySettings.CompactWidth, 20), 10, text, true);
            var items = GetMetrics();
            for (var i = 0; i < items.Count; i++)
            {
                var m = items[i];
                var alert = IsQuotaLow(m.Module.Metric, m.Percent);
                var accent = alert ? DisplaySettings.Warning : DisplaySettings.Accent;
                var valueColor = alert ? DisplaySettings.Warning : text;
                var gaugeTrack = alert ? Color.FromArgb(160, DisplaySettings.Warning) : track;
                var outside = DisplaySettings.ShowValues && (m.Module.Style != GaugeStyle.环形 || m.Module.RingValue == RingValuePosition.环外);
                var x = 3 + m.Module.Column * (DisplaySettings.CellWidth + DisplaySettings.Gap);
                var y = DisplaySettings.RowTop(m.Module.Row);
                var single = DisplaySettings.Modules.Count(module => module.Enabled && module.Row == m.Module.Row) == 1;
                var cw = single ? DisplaySettings.CompactWidth - 6 : DisplaySettings.CellWidth;
                if (single) x = 3;
                var alignment = single ? DisplaySettings.Alignments[m.Module.Row] : RowAlignment.居中;
                var numberAlign = alignment == RowAlignment.靠左 ? StringAlignment.Near : alignment == RowAlignment.靠右 ? StringAlignment.Far : StringAlignment.Center;
                float Center(float gaugeWidth) => alignment == RowAlignment.靠左 ? x + gaugeWidth / 2 + 2 : alignment == RowAlignment.靠右 ? x + cw - gaugeWidth / 2 - 2 : x + cw / 2f;
                var ch = DisplaySettings.RowHeight(m.Module.Row);
                var top = y + (outside ? 18 : 2);
                var bottom = y + ch - (DisplaySettings.ShowLabels ? 20 : 3);
                var gaugeHeight = Math.Max(20, bottom - top);
                if (m.Module.Style == GaugeStyle.数值) { FitNumber(g, m.Value, new RectangleF(x, y + 3, cw, ch - (DisplaySettings.ShowLabels ? 23 : 6)), 14, valueColor, numberAlign); if (DisplaySettings.ShowLabels) TextAt(g, m.Name, new RectangleF(x, y + ch - 18, cw, 18), 9, muted, true); continue; }
                if (outside) FitNumber(g, m.Value, new RectangleF(x - 1, y, cw + 2, 18), 10, valueColor);
                if (m.Percent is not null || m.IsPercent)
                {
                    if (m.Module.Style == GaugeStyle.环形)
                    {
                        var outer = Math.Max(4, Math.Min(DisplaySettings.RingDiameter, Math.Min(cw - 4, gaugeHeight - 2)));
                        var diameter = Math.Max(2, outer - Math.Min(DisplaySettings.RingWidth, outer / 3));
                        var rect = new RectangleF(Center(diameter) - diameter / 2, top + (gaugeHeight - diameter) / 2, diameter, diameter);
                        var stroke = Math.Min(DisplaySettings.RingWidth, Math.Max(1, diameter / 3)); if (DisplaySettings.Material) { using var shadow = new Pen(Color.FromArgb(65, Color.Black), stroke + 2); g.DrawEllipse(shadow, rect.X + 1, rect.Y + 2, rect.Width, rect.Height); }
                        using var pen = new Pen(gaugeTrack, stroke); g.DrawEllipse(pen, rect);
                        if (m.Percent is > 0) { pen.Color = accent; pen.StartCap = pen.EndCap = LineCap.Round; g.DrawArc(pen, rect, -90, (float)(Math.Clamp(m.Percent.Value, 0, 100) * 3.6)); }
                        if (DisplaySettings.ShowValues && m.Module.RingValue == RingValuePosition.环内) { var inset = Math.Min(rect.Width / 3, stroke / 2 + 2); FitNumber(g, m.Value.TrimEnd('%'), new RectangleF(rect.X + inset, rect.Y + inset, Math.Max(1, rect.Width - inset * 2), Math.Max(1, rect.Height - inset * 2)), 10, valueColor); }
                    }
                    else if (m.Module.Style == GaugeStyle.电量条)
                    {
                        var rect = new RectangleF(Center(18) - 9, top + 3, 18, gaugeHeight - 5);
                        using var pen = new Pen(gaugeTrack, 1.2f);
                        using var outline = Round(rect, 4); g.DrawPath(pen, outline);
                        using var terminal = new SolidBrush(gaugeTrack); g.FillRectangle(terminal, rect.X + 5, rect.Y - 3, 8, 2);
                        var interior = new RectangleF(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
                        using var empty = new SolidBrush(Color.FromArgb(30, text)); g.FillRectangle(empty, interior);
                        if (m.Percent is > 0)
                        {
                            var level = interior.Height * (float)Math.Clamp(m.Percent.Value / 100, 0, 1);
                            using var fill = AccentBrush(new RectangleF(x, top, Math.Max(1, cw), Math.Max(1, gaugeHeight)), accent);
                            g.FillRectangle(fill, interior.X, interior.Bottom - level, interior.Width, level);
                        }
                    }
                    else
                    {
                        var thickness = Math.Min(DisplaySettings.BarWidth, Math.Max(2, cw - 4)); var rect = new RectangleF(Center(thickness) - thickness / 2, top + 2, thickness, gaugeHeight - 4);
                        using var empty = new SolidBrush(gaugeTrack);
                        using var path = Round(rect, 3); g.FillPath(empty, path);
                        if (m.Percent is > 0)
                        {
                            var level = (float)(rect.Height * Math.Clamp(m.Percent.Value / 100, 0, 1));
                            g.SetClip(path);
                            using var fill = AccentBrush(new RectangleF(x, top, Math.Max(1, cw), Math.Max(1, gaugeHeight)), accent); g.FillRectangle(fill, rect.X, rect.Bottom - level, rect.Width, level);
                            g.ResetClip();
                        }
                    }
                }

                if (DisplaySettings.ShowLabels) TextAt(g, m.Name, new RectangleF(x - 2, y + ch - 18, cw + 4, 18), 9, muted, true);
            }
            if (Details)
            {
                var detailState=g.Save();
                var detailScale=Math.Min(1f,HeightDip/310f);
                if(detailScale<1){g.TranslateTransform(DisplaySettings.CompactWidth,0);g.ScaleTransform(detailScale,detailScale);g.TranslateTransform(-DisplaySettings.CompactWidth,0);}
                if (DisplaySettings.AutoText && DisplaySettings.DetailOpacity >= 180) { text = DisplaySettings.Background.GetBrightness() < .5 ? Color.White : Color.Black; muted = Color.FromArgb(230, text); }
                var x = DisplaySettings.CompactWidth + 12;
                TextAt(g, "账户剩余额度", new RectangleF(x, 14, 238, 23), 12, text);
                DetailWindow(g, "五小时", Quota?.FiveHour, x, 45, text, muted);
                DetailWindow(g, "每周", Quota?.Weekly, x, 96, text, muted);
                var rows = new List<string>();
                if (Tokens is not null && SessionSource.Length > 0) rows.Add(SessionSource);
                if (DisplaySettings.ShowSessionDetails)
                {
                    rows.Add($"{Vitals?.Model ?? "模型未知"} · {Vitals?.Effort ?? ""}");
                    rows.Add($"状态 {Vitals?.State ?? "等待数据"} · ID {Tokens?.ThreadId[..Math.Min(8, Tokens.ThreadId.Length)] ?? "—"}");
                    rows.Add($"平均输出 {Vitals?.AverageOutputTps?.ToString("0.#") ?? "—"} tok/s（整轮）");
                    rows.Add($"首 Token {(Vitals?.FirstTokenMs is { } ms ? $"{ms / 1000:0.##} s" : "—")} · 整轮 {Vitals?.TurnSeconds?.ToString("0.#") ?? "—"} s");
                }
                rows.Add(Tokens is null ? "当前聊天 · 等待数据" : $"聊天 {Short(Tokens.TotalTokens)} · 上下文 {Tokens.ContextPercent:0.#}%");
                if (Tokens is not null) { rows.Add($"输入 {Short(Tokens.InputTokens)} · 输出 {Short(Tokens.OutputTokens)}"); rows.Add($"缓存 {Short(Tokens.CachedInputTokens)} · 推理 {Short(Tokens.ReasoningOutputTokens)}"); }
                var lineHeight = Math.Min(20, Math.Max(9, (Math.Max(310,HeightDip) - 175) / Math.Max(1, rows.Count)));
                for (var i = 0; i < rows.Count; i++) TextAt(g, rows[i], new RectangleF(x, 145 + i * lineHeight, 238, lineHeight), 10, text);
                var status = Quota is null ? Status : QuotaFailed || DateTimeOffset.Now - Quota.ReadAt > TimeSpan.FromMinutes(2) ? "额度数据过期 · 正在重试" : $"额度更新 {Quota.ReadAt:HH:mm}";
                TextAt(g, status, new RectangleF(x, Math.Max(310,HeightDip) - 25, 238, 20), 10, muted);
                g.Restore(detailState);

            }
        }
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(interactive ? Color.FromArgb(1, 0, 0, 0) : Color.Transparent);
            g.CompositingMode = interactive ? CompositingMode.SourceOver : CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(large, new Rectangle(0, 0, width, height));
        }
        return bitmap;
    }
    private sealed record Metric(string Name, string Value, double? Percent, bool IsPercent, GaugeModule Module);
    private List<Metric> GetMetrics()
    {
        var list = new List<Metric>();
        foreach (var module in DisplaySettings.Modules.Where(m => m.Enabled))
        {
            double? percent = null; long? amount = null;
            string? custom = null;
            switch (module.Metric)
            {
                case MetricKind.平均输出速度: custom = Vitals?.AverageOutputTps is { } speed ? $"{speed:0.#}t/s" : "—"; if (Vitals?.AverageOutputTps is { } ratio) percent = 100 * ratio / Math.Max(1, module.Reference); break;
                case MetricKind.首Token等待: custom = Vitals?.FirstTokenMs is { } ms ? $"{ms / 1000:0.##}s" : "—"; if (Vitals?.FirstTokenMs is { } first) percent = first / 10d / Math.Max(1, module.Reference); break;
                case MetricKind.模型: custom = Vitals?.Model.Replace("gpt-", "").Replace("-", " ") ?? "—"; break;
                case MetricKind.任务状态: custom = Vitals?.State ?? "等待"; break;
            }
            switch (module.Metric)
            {
                case MetricKind.五小时剩余: percent = Quota?.FiveHour?.Remaining; break;
                case MetricKind.周剩余: percent = Quota?.Weekly?.Remaining; break;
                case MetricKind.上下文占用: percent = Tokens?.ContextPercent; break;
                case MetricKind.缓存命中: percent = Tokens?.CacheHitPercent; break;
                case MetricKind.聊天Token: amount = Tokens?.TotalTokens; break;
                case MetricKind.输入Token: amount = Tokens?.InputTokens; break;
                case MetricKind.输出Token: amount = Tokens?.OutputTokens; break;
                case MetricKind.推理Token: amount = Tokens?.ReasoningOutputTokens; break;
                case MetricKind.缓存Token: amount = Tokens?.CachedInputTokens; break;
                case MetricKind.上下文Token: amount = Tokens?.ContextUsedTokens; break;
                case MetricKind.上下文容量: amount = Tokens?.ContextWindowTokens; break;
            }
            var isAmount = module.Metric is MetricKind.聊天Token or MetricKind.输入Token or MetricKind.输出Token or MetricKind.推理Token or MetricKind.缓存Token or MetricKind.上下文Token or MetricKind.上下文容量;
            var value = custom ?? (isAmount ? amount is long n ? Short(n) : "—" : percent is double p ? $"{p:0.#}%" : "—");
            if (isAmount && amount is long count) percent = count * 100d / Math.Max(1, module.Reference);
            list.Add(new(module.Metric.ToString(), value, percent, true, module));
        }
        return list;
    }
    private void FitNumber(Graphics g, string value, RectangleF bounds, float size, Color color, StringAlignment alignment = StringAlignment.Center)
    {
        value = UiText.T(value);
        using var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Alignment = alignment, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.None };
        using var brush = new SolidBrush(color);
        for (var current = size; current >= 2; current -= .5f)
        {
            var font = MakeFont(current);
            if (g.MeasureString(value, font, 1000, format).Width <= bounds.Width || current <= 2)
            { g.DrawString(value, font, brush, bounds, format); return; }
        }
    }
    private void TextAt(Graphics g, string text, RectangleF bounds, float size, Color color, bool center = false)
    {
        text = UiText.T(text);
        var font = MakeFont(size);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { FormatFlags = StringFormatFlags.NoWrap, Trimming = StringTrimming.None, Alignment = center ? StringAlignment.Center : StringAlignment.Near, LineAlignment = center ? StringAlignment.Center : StringAlignment.Near };
        g.DrawString(text, font, brush, bounds, format);
    }
    internal bool IsQuotaLow(MetricKind metric, double? remaining) => DisplaySettings.QuotaWarnings && !QuotaFailed && Quota is not null && DateTimeOffset.Now - Quota.ReadAt <= TimeSpan.FromMinutes(2) && remaining is { } value &&
        (metric == MetricKind.周剩余 ? value <= DisplaySettings.WeeklyWarning : metric == MetricKind.五小时剩余 && value <= DisplaySettings.FiveHourWarning);
    private void DetailWindow(Graphics g, string name, QuotaWindow? window, float x, float y, Color text, Color muted)
    {
        var color = IsQuotaLow(name == "每周" ? MetricKind.周剩余 : MetricKind.五小时剩余, window?.Remaining) ? DisplaySettings.Warning : text;
        TextAt(g, window is null ? name + " · 暂不可用" : $"{name} · 剩余 {window.Remaining:0.#}%", new(x, y, 238, 22), 12, color);
        var reset = window?.ResetsAt is long timestamp ? DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("MM-dd HH:mm") : "未知";
        TextAt(g, "重置 " + reset, new(x, y + 23, 238, 22), 10, muted);
    }
    internal static string Short(long value)
    {
        if (value >= 1_000_000_000_000) return value.ToString("0E0", System.Globalization.CultureInfo.InvariantCulture);
        var divisor = value >= 1_000_000_000 ? 1_000_000_000d : value >= 1_000_000 ? 1_000_000d : value >= 1000 ? 1000d : 1d;
        var suffix = divisor == 1_000_000_000 ? "B" : divisor == 1_000_000 ? "M" : divisor == 1000 ? "K" : "";
        var scaled = value / divisor;
        return scaled.ToString(scaled >= 10 ? "0" : "0.#", System.Globalization.CultureInfo.InvariantCulture) + suffix;
    }
    private Brush SurfaceBrush(RectangleF rect, int alpha)
    {
        var color = DisplaySettings.Background;
        if (!DisplaySettings.Material) return new SolidBrush(Color.FromArgb(alpha, color));
        var top = Color.FromArgb(alpha, Math.Min(255, color.R + 18), Math.Min(255, color.G + 18), Math.Min(255, color.B + 18));
        return new LinearGradientBrush(rect, top, Color.FromArgb(alpha, color), 90f);
    }
    private Brush AccentBrush(RectangleF rect, Color? overrideColor = null)
    {
        var color = overrideColor ?? DisplaySettings.Accent;
        if (!DisplaySettings.Material) return new SolidBrush(color);
        var light = Color.FromArgb(Math.Min(255, color.R + 32), Math.Min(255, color.G + 32), Math.Min(255, color.B + 32));
        return new LinearGradientBrush(rect, light, color, 0f);
    }
    private static GraphicsPath Round(RectangleF r, float radius)
    {
        radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
        var path = new GraphicsPath();
        var d = Math.Max(.01f, radius * 2);
        path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    private IntPtr _surfaceDc, _surfaceBitmap, _surfaceOld, _surfacePixels;
    private Size _surfaceSize;
    private bool _surfaceDirty=true;
    internal int SurfaceAllocations, SurfaceCopies;
    private void ReleaseSurface()
    {
        if(_surfaceDc!=IntPtr.Zero&&_surfaceOld!=IntPtr.Zero)SelectObject(_surfaceDc,_surfaceOld);
        if(_surfaceBitmap!=IntPtr.Zero)DeleteObject(_surfaceBitmap);
        if(_surfaceDc!=IntPtr.Zero)DeleteDC(_surfaceDc);
        _surfaceDc=_surfaceBitmap=_surfaceOld=_surfacePixels=IntPtr.Zero;_surfaceSize=Size.Empty;
    }
    public void Present(Point position, byte alpha)
    {
        if(_bitmap is null)Rebuild(_dpi);var bitmap=_bitmap!;var screen=GetDC(IntPtr.Zero);
        try
        {
            if(_surfaceSize!=bitmap.Size)
            {
                ReleaseSurface();_surfaceDc=CreateCompatibleDC(screen);
                var info=new BitmapHeader{Size=40,Width=bitmap.Width,Height=-bitmap.Height,Planes=1,Bits=32};
                _surfaceBitmap=CreateDIBSection(screen,ref info,0,out _surfacePixels,IntPtr.Zero,0);
                if(_surfaceBitmap==IntPtr.Zero||_surfaceDc==IntPtr.Zero){ReleaseSurface();throw new System.ComponentModel.Win32Exception();}
                _surfaceOld=SelectObject(_surfaceDc,_surfaceBitmap);_surfaceSize=bitmap.Size;_surfaceDirty=true;SurfaceAllocations++;
            }
            if(_surfaceDirty)
            {
                var data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                try{for(var row=0;row<bitmap.Height;row++)CopyMemory(_surfacePixels+row*bitmap.Width*4,data.Scan0+row*data.Stride,(nuint)(bitmap.Width*4));}
                finally{bitmap.UnlockBits(data);}_surfaceDirty=false;SurfaceCopies++;
            }
            var size=new NativeSize(bitmap.Width,bitmap.Height);var destination=new NativePoint(position.X,position.Y);var source=new NativePoint(0,0);var blend=new Blend{SourceConstantAlpha=alpha,AlphaFormat=1};
            if(!UpdateLayeredWindow(Handle,screen,ref destination,ref size,_surfaceDc,ref source,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            PresentedPosition=position;SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);
        }
        finally{ReleaseDC(IntPtr.Zero,screen);}
    }
    protected override void Dispose(bool disposing){if(disposing){ReleaseSurface();_bitmap?.Dispose();_renderCache?.Dispose();ClearFonts();_backgroundImage?.Dispose();}base.Dispose(disposing);}
    [StructLayout(LayoutKind.Sequential)] private struct BitmapHeader { public uint Size; public int Width, Height; public ushort Planes, Bits; public uint Compression, ImageSize; public int XPixels, YPixels; public uint Colors, Important; }
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapHeader header, uint usage, out IntPtr pixels, IntPtr section, uint offset);
    [DllImport("ntdll.dll", EntryPoint = "RtlMoveMemory")] private static extern void CopyMemory(IntPtr destination, IntPtr source, nuint length);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint(int x, int y) { public int X = x; public int Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize(int w, int h) { public int Width = w; public int Height = h; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dc, ref NativePoint position, ref NativeSize size, IntPtr sourceDc, ref NativePoint source, uint key, ref Blend blend, uint flags);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
