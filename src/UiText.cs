using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
namespace CodexTokenOverlay;

internal enum UiLanguage { System, Chinese, English }
internal static class UiText
{
    internal static UiLanguage Language;
    [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();
    internal static bool IsEnglish => Language == UiLanguage.English || Language == UiLanguage.System && SystemEnglish;
    private static readonly bool SystemEnglish = !CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()).Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
    private static readonly (string Chinese,string English)[] Terms = new Dictionary<string,string>
    {
        ["当前会话未确认"]="Conversation unconfirmed", ["等待连接 Codex"]="Waiting for Codex connection",
        ["IPC 订阅会话"]="IPC subscribed session", ["最近活动会话（自动匹配）"]="Recently active session (auto-matched)",
        ["当前页面会话"]="Current page session", ["同名会话 · 无法唯一匹配"]="Duplicate names: ambiguous session", ["页面会话未确认"]="Page session unconfirmed",
        ["（导入）"]=" (imported)", ["文件需小于 32 MB。"]="File must be smaller than 32 MB.", ["字体过大。"]="Font is too large.", ["图片尺寸过大，请选择 6400 万像素以内的图片。"]="Image is too large; use at most 64 megapixels.",
        ["恢复当前页默认"]="Reset this page", ["额度不足变色"]="Low-quota color alert", ["详情显示状态摘要"]="Show session summary",
        ["达到剩余阈值时变色，恢复后还原；数据过期时不提示。\n平均速度=本轮输出 Token ÷ 整轮耗时，包含等待和工具执行。\n首 Token 等待仅在日志提供时显示。模型与状态可选为独立模块。"]="Alert color applies below the threshold; stale quota does not alert.\nOutput speed = output tokens / full turn time, including tools and waiting.\nFirst-token wait requires log data. Model and state can be separate metrics.",
        ["图片不透明度超出范围。"]="Image opacity is out of range.", ["剩余阈值范围 0–100%。"]="Remaining thresholds must be 0–100%.", ["圆环直径 8–48，水平偏移 -100–100。"]="Ring diameter: 8–48; offset: -100–100.", ["竖条粗细 2–24，圆环粗细 1–12。"]="Bar thickness: 2–24; ring thickness: 1–12.",
        ["单元宽 12–100，高 48–150，间隔 0–24。"]="Cell width: 12–100; bar height: 48–150; spacing: 0–24.", ["布局超出窄栏：请减少列数、单元宽度或间隔。"]="Layout exceeds the rail. Reduce columns, cell width or spacing.", ["请启用至少一个模块，最多 16 个。"]="Enable 1–16 metrics.", ["模块行列位置超出网格。"]="Metric position is outside the grid.", ["两个模块不能占用同一格。"]="Two metrics cannot occupy one cell.", ["模块样式或参考量无效。"]="Invalid metric style or scale.", ["动画或透明度超出范围。"]="Animation or opacity is out of range.", ["颜色格式无效。"]="Invalid color format.",
        ["单击参数行选中 → 滚轮调整；再次单击或 Esc 退出。未选中时滚动页面。"]="Click a parameter to enable wheel adjustment; click again or press Esc to exit.",
        [" · 滚轮调整 · 再次单击或 Esc 退出"]=" · wheel adjusts · click again / Esc to exit",
        ["选中左侧模块，再点空格放置；也可拖入格子。拖到已有模块上会交换。"]="Select a metric, then click a cell or drag it there. Occupied cells swap.",
        ["改变行列会自动匹配参考尺寸，可在这里继续微调。\n窄栏边界固定，偏移 -100 贴左、+100 贴右；底部保留默认位置。"]="Rows and columns set suggested dimensions; fine-tune here.\nOffset: -100 left, 0 centered, +100 right. Vertical position is saved.",
        ["点击参数行选中后，滚轮调整；再次点击退出选中。\n文字行和圆环行自动收紧，长条行高度在此单独调整。"]="Click a parameter to enable wheel adjustment; click again to exit.\nNumeric and ring rows use compact heights; bar height is adjustable.",
        ["单位 tok/s；达到此速度时填满。只控制图形比例，不是额度限制。"]="tok/s at full scale. Controls the gauge ratio, not an account limit.",
        ["单位秒；达到此等待时间时填满。只控制图形比例。"]="Seconds at full scale. Controls the gauge ratio only.",
        ["单位 Token；达到此数量时填满。只控制图形比例，不是账户额度。"]="Tokens at full scale. Controls the gauge ratio, not account quota.",
        ["TTF / OTF；仅本组件使用"]="TTF / OTF; used only by this app",
        ["受显示数值开关控制"]="Requires Show values",
        ["仪表控制面板 · 布局编辑"]="Gauge control panel",
        ["主窗口实时预览（试用）"]="Live overlay preview",
        ["恢复当前页面默认"]="Reset this page",
        ["随 Windows 启动（等待 Codex）"]="Start with Windows (wait for Codex)",
        ["Codex 剩余额度 · 本地修改版"]="Codex remaining usage",
        ["立即刷新额度"]="Refresh quota now", ["控制面板…"]="Control panel…", ["重置到窄栏"]="Reset position", ["暂时隐藏 / 恢复"]="Hide / restore", ["退出"]="Exit",
        ["布局与模块"]="Layout", ["尺寸与位置"]="Size & position", ["外观与动画"]="Appearance", ["额度与状态"]="Quota & status",
        ["自动匹配尺寸"]="Auto size", ["列数"]="Columns", ["行数"]="Rows", ["显示样式"]="Style", ["圆环数值位置"]="Ring values", ["满格对应的数值"]="Value at full scale",
        ["水平偏移（居中 0）"]="Horizontal offset (0=center)", ["单元宽度"]="Cell width", ["长条行高度"]="Bar row height", ["模块间隔"]="Spacing",
        ["主题与配色"]="Theme & colors", ["应用配色"]="Apply palette", ["额度提醒对比色"]="Low-quota alert", ["重点色"]="Accent", ["文字"]="Text", ["背景图片"]="Background image",
        ["未使用背景图片"]="No background image", ["导入背景图片"]="Import background", ["图片导入失败："]="Image import failed: ", ["字体导入失败："]="Font import failed: ", ["导入字体…"]="Import font…", ["导入字体"]="Import font", ["导入图片…"]="Import image…", ["移除图片"]="Remove image", ["字体文件"]="Font files", ["图片不透明度 %"]="Image opacity %",
        ["图形与文字"]="Gauges & text", ["允许竖向拖动"]="Allow vertical dragging", ["文字加粗"]="Bold text", ["文字跟随 Codex 明暗"]="Follow Codex light/dark", ["圆环外径"]="Ring diameter", ["竖条粗细"]="Bar thickness", ["圆环粗细"]="Ring thickness",
        ["详情背景不透明度 %"]="Details opacity %", ["背景不透明度 %"]="Background opacity %", ["显示名称"]="Show labels", ["显示数值"]="Show values", ["悬停展开"]="Hover to expand", ["打开淡入毫秒"]="Fade-in time (ms)", ["展开耗时毫秒"]="Expand time (ms)", ["动画"]="Animation",
        ["启用低额度提醒"]="Low-quota color alert", ["五小时剩余阈值 %"]="5-hour threshold %", ["每周剩余阈值 %"]="Weekly threshold %", ["展示聊天状态详情"]="Show session details",
        ["保存并应用"]="Save & apply", ["取消"]="Cancel", ["请调整"]="Please adjust", ["示例数据预览"]="Sample preview", ["主窗口实时预览"]="Live preview", ["示例预览"]="Sample preview", ["本次"]="Render", ["当前窗口偏小，建议不超过"]="Small window; suggested rows: ",
        ["每行 1–3 个，纵向 1–16 行。"]="Use 1–3 columns and 1–16 rows.", ["模块不能重叠或超出网格。"]="Metrics must not overlap or leave the grid.", ["行对齐设置无效。"]="Invalid row alignment.", ["至少显示一项。"]="Enable at least one metric.",
        ["聊天Token"]="Chat tokens", ["上下文占用"]="Context usage", ["周剩余"]="Weekly remaining", ["五小时剩余"]="5-hour remaining", ["平均输出速度"]="Output speed", ["首Token等待"]="First-token wait", ["缓存命中"]="Cache hit", ["输入Token"]="Input tokens", ["输出Token"]="Output tokens", ["推理Token"]="Reasoning tokens", ["缓存Token"]="Cached tokens", ["上下文Token"]="Context tokens", ["上下文容量"]="Context capacity", ["任务状态"]="Task status",
        ["（已关闭）"]=" (off)", ["电量条"]="Battery", ["环形"]="Ring", ["竖条"]="Bar", ["数值"]="Number", ["环外"]="Outside", ["环内"]="Inside", ["不显示"]="Hidden", ["居中"]="Center", ["靠左"]="Left", ["靠右"]="Right", ["空位"]="Empty",
        ["午夜蓝"]="Midnight blue", ["暖灰"]="Warm gray", ["高对比"]="High contrast", ["浅色"]="Light", ["荧光绿"]="Neon green", ["紫罗兰"]="Violet", ["海洋青"]="Ocean teal", ["珊瑚橙"]="Coral", ["樱花粉"]="Cherry pink", ["石墨金"]="Graphite gold", ["森林绿"]="Forest green", ["冰川蓝"]="Glacier blue",
        ["账户剩余额度"]="Remaining quota", ["正在读取额度…"]="Reading quota…", ["额度数据过期 · 正在重试"]="Quota stale · retrying", ["额度不可用 · 检查登录 / 网络"]="Quota unavailable · check sign-in/network", ["额度更新"]="Quota updated", ["暂不可用"]="Unavailable", ["等待数据"]="Waiting", ["数据暂不可用"]="Unavailable", ["模型未知"]="Unknown model", ["已完成"]="Complete", ["进行中"]="Running", ["未知"]="Unknown", ["五小时"]="5-hour", ["每周"]="Weekly", ["剩余"]="remaining", ["重置"]="Resets", ["当前聊天"]="Chat", ["上下文"]="Context", ["聊天"]="Chat", ["输入"]="Input", ["输出"]="Output", ["缓存"]="Cached", ["推理"]="Reasoning", ["平均输出"]="Output avg.", ["首 Token"]="First token", ["整轮"]="turn", ["状态"]="Status", ["模型"]="Model", ["已选中："]="Selected: ", ["开启"]="On", ["关闭"]="Off", ["颜色"]=" color", ["背景"]="Background", ["字体"]="Font",
        [" 行仅一项：在整行中放置"]=": align the single metric", ["第 "]="Row ", [" 行"]=" rows", [" 列"]=" columns"
    }.OrderByDescending(p=>p.Key.Length).Select(p=>(p.Key,p.Value)).ToArray();
    internal static string T(string value)
    {
        if(!IsEnglish)return value;
        foreach(var (from,to) in Terms)value=value.Replace(from,to,StringComparison.Ordinal);
        return value;
    }
    private sealed class Origin { internal string Source="",Rendered=""; internal bool Busy; }
    private static readonly ConditionalWeakTable<Control,Origin> Originals=new();
    internal static void Apply(Control root)
    {
        var origin=Originals.GetValue(root,c=>{var o=new Origin{Source=c.Text};c.TextChanged+=(_,_)=>Translate(c,o);return o;});
        Translate(root,origin);
        foreach(Control child in root.Controls)Apply(child);
    }
    private static void Translate(Control c,Origin o){if(o.Busy)return;o.Busy=true;try{if(c.Text!=o.Rendered)o.Source=c.Text;o.Rendered=T(o.Source);c.Text=o.Rendered;}finally{o.Busy=false;}}
    internal static void Format(object? sender,ListControlConvertEventArgs e)=>e.Value=T(e.ListItem?.ToString()??"");
}
