# Codex Rail 0.14.7

Startup and update improvements for Windows 10/11 x64. This candidate also includes the numeric selection crash fix from 0.14.6.

- Launching the EXE again restores a hidden instance; startup/foreground state refresh no longer relies only on a periodic retry.
- A small native watcher starts at login, launches the overlay when Codex becomes foreground and avoids loading .NET while waiting. Follow-launched overlays exit when Codex closes.
- GitHub stable-release checks notify before installing. Installation requires confirmation, checks ZIP/binary hashes and versions, preserves settings/assets, retains a previous executable and rolls back if the new process cannot be created.
- Development checkouts and --dev runs isolate settings and instance signals and disable production startup/update actions.

Windows 10/11 x64 启动和更新改进；包含 0.14.6 数字输入框取消选中闪退修复。再次双击可恢复显示，原生发现器负责随 Codex 启动；检查新版后提示确认，保留设置及资源并备份旧 EXE。C 盘开发配置独立。

Extract the complete ZIP including CodexRail.Watcher.exe. Exit the old instance, run the standalone EXE unless .NET 10 Desktop Runtime is installed, then enable Windows startup from the tray menu. 0.14.5 requires this one manual upgrade before in-app updates are available. Both variants and the watcher are unsigned.

完整解压 ZIP 并保留相邻发现器。退出旧实例后运行新版（无 .NET 10 Desktop Runtime 时选 standalone），从托盘启用自启动。0.14.5 首次需手动升级，此后可确认式更新。三个 EXE 均未签名。

Regression, package-verification and independent native updater tests passed. Watcher standby sample: ~116 KiB EXE, ~5.6 MiB working set, CPU below timer resolution over 20 seconds. This is a bounded local sample, not boot-time or physical multi-monitor certification. A post-launch application crash requires manual rollback. See docs/INSTALL.md, docs/TESTING.md and CHANGELOG.md for limitations.
