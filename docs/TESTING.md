# Validation / 验证

The 0.14 baseline was tested locally on Windows 10 x64. Automated coverage includes 240 layout cases across 1366×768, 1600×900, 1920×1080, 2560×1440 and 3840×2160; 100–250% scale; maximized and reduced windows; default and full-metric layouts. 164 cases had sufficient reserved space and passed bounds/no-loss/paging assertions; 76 had insufficient space and exercised the intentional hide boundary.

0.14 本地基线验证覆盖 240 组布局：五档分辨率、100–250% 缩放、最大化/缩小窗口、默认/全部指标。164 组空间足够并通过边界、分页和指标完整性验证；76 组触发空间不足时的保护性隐藏。

12 English/Chinese settings-window combinations exercised synthetic DPI changes, work-area fitting, four pages and language round trips. Other probes cover asset import, right-click/drag behavior, quota parsing, warning thresholds, display consistency and bounded filesystem notifications.

另有 12 组中英文设置窗口组合，验证合成 DPI 消息、工作区约束、四个设置页和语言往返；其他探针覆盖导入、交互、额度解析、阈值、展开一致性和文件通知限流。

These are production-renderer and synthetic geometry/message tests, **not physical multi-monitor certification**. Real compositing, cross-monitor message ordering, different taskbar layouts and future Codex layouts remain areas for community testing. Short stress tests are not proof of leak-free operation over days.

这些是生产渲染器和模拟几何/消息测试，**不是物理多屏认证**。真实合成、跨屏消息顺序、任务栏布局和未来 Codex 界面仍需人工环境验证；短时压力测试不证明长期无泄漏。

Resource notes: a 120-frame constant-size fade reused one native surface allocation and one pixel copy. Live preview on/off each produced zero additional preview frames while idle in a 10-second observation. 90 settings open/edit/close cycles showed stable GDI counts after warm-up. Resident memory depends on displayed/hidden state, DPI, assets and .NET loading; no single memory number is advertised as universal. The standalone binary remains roughly 49 MiB.

资源验证：同尺寸 120 帧淡入复用一次表面分配与像素复制；实时预览开/关分别静置 10 秒，没有新增帧；设置开关 90 次后 GDI 计数预热后稳定。常驻内存随状态、DPI、资源和运行时加载变化，不宣传一个通用固定值。

Run `./test.ps1` for synthetic regression checks. `--performance` additionally reads the signed-in account quota; hosted CI deliberately excludes that probe. README media uses fixture data, and the hover GIF is renderer-generated, not a recording of real hover detection or Windows compositor behavior.

`./test.ps1` 只运行合成回归。`--performance` 会额外读取当前登录账户额度，CI 不运行该探针。动图是示例渲染，不是鼠标悬停判定或 Windows 合成器录屏。

0.14.5 adds regressions for unique/ambiguous read-only page catalog lookup, stale session reads, premultiplied data transitions and temporary menu-hook lifecycle. Native menu tests install hooks and inject controller observations; they do not claim end-to-end clicks on Codex or the tray. / 0.14.5 新增页面目录匹配、过期读取、像素过渡和菜单监听生命周期回归；菜单测试不代替人工在 Codex/托盘上的完整操作验证。
