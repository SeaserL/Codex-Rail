# Codex Rail 0.14.5

A stability update for Windows 10/11 x64, covering changes since the previous public release, 0.14.0.

## English

- **Current conversation tracking:** identify the visible page through native Windows accessibility and uniquely match its title to the read-only local thread catalog. Background sessions no longer select the displayed counters; duplicate titles remain unconfirmed.
- **Faster session refresh:** session reads run independently of quota requests, reject stale results after a page change, and detect appended logs even when their timestamps are unchanged.
- **Smoother data changes:** 140 ms transitions, stable numeric width reserves and a short switch grace period reduce flicker. Animation timers stop when idle or hidden.
- **Right-click menu fix:** outside clicks, Escape and app switching dismiss the menu; hover details resume after dismissal. Temporary input hooks are removed when the menu closes.

Extract `CodexRail-v0.14.5-win-x64.zip`. Run `CodexRail-win-x64-standalone.exe` if you do not have the .NET 10 Desktop Runtime; otherwise use the smaller `CodexRail-win-x64.exe`. Exit the old instance before launching the new one. Settings are retained in `%LOCALAPPDATA%\CodexUsageCardLocal`; keep an older binary for rollback.

The ZIP includes both unsigned EXEs, license notices and `SHA256SUMS.txt`. Quota fields depend on your Codex version/account. Unavailable or ambiguous session data shows `—`; accessibility titles, local schemas and UI layout may change with Codex updates. This is an independent Windows overlay, not an official OpenAI extension.

## 中文

这是 Windows 10/11 x64 的稳定性更新，包含自上一个公开版本 0.14.0 以来的修复。

- **当前对话识别：**通过 Windows 原生无障碍接口读取当前页面标题，与本地会话目录只读、唯一匹配；后台会话不再决定显示数据，同名会话不猜测。
- **刷新响应：**聊天数据与额度请求分离，切换后拒绝过期结果，并识别时间戳未变化的日志追加。
- **切换动效：**加入 140 ms 平滑过渡、数字预留宽度和短暂切换合并，减少闪烁；静止或隐藏时停止动效计时器。
- **右键菜单：**点击菜单外、Esc、切换应用可收起；关闭后恢复悬停详情，临时输入监听随菜单关闭移除。

解压 ZIP。未安装 .NET 10 Desktop Runtime 时运行 standalone 版，已有运行时则使用小型版。升级前退出旧实例；设置保存在 `%LOCALAPPDATA%\CodexUsageCardLocal`，升级保留设置，可保留旧 EXE 以便回退。

两个 EXE 均未签名，压缩包内包含许可证与 `SHA256SUMS.txt`。额度字段依赖 Codex 版本和账户；缺失或无法唯一识别的数据显示 `—`。Codex 更新可能改变无障碍标题、本地数据结构和界面布局。本项目是独立浮窗，不是 OpenAI 官方扩展。

## Validation / 验证

Local regression passed: 240 layout cases, 12 language/DPI/window combinations, session routing, read-only page catalog matching, animated rendering and menu lifecycle/hook cleanup. Real page switching and smoother data updates were confirmed in the creator's local environment; this is not physical multi-monitor certification. See [testing boundaries](https://github.com/SeaserL/Codex-Rail/blob/main/docs/TESTING.md) and [full changelog](https://github.com/SeaserL/Codex-Rail/blob/main/CHANGELOG.md).
