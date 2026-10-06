# Installation / 安装

## Choose a build / 选择版本

| File | Requirement | 文件要求 |
|---|---|---|
| `CodexRail-win-x64.exe` | .NET 10 **Desktop** Runtime, x64 | 需要桌面运行时，不能只装普通 .NET Runtime |
| `CodexRail-win-x64-standalone.exe` | Runtime included | 自带运行时，体积较大 |

Use Releases in the repository sidebar to download and extract the portable ZIP. It contains both EXEs, license notices and `SHA256SUMS.txt`; run the appropriate EXE. No installer, elevation, separate account sign-in or API key is added by this program. The initial build is unsigned; verify its source and release checksum before running.

在仓库侧栏的 Releases 下载并解压便携 ZIP，内含两个 EXE、许可证和 `SHA256SUMS.txt`，选择合适的 EXE 运行。本程序没有安装器、单独登录或 API Key 输入，也不要求管理员权限。首版未签名，可核对源码与校验值。

## Run / 运行

Run one EXE, then bring Codex to the foreground. Right-click the overlay or tray icon to open settings. `--settings` also opens settings, including when another instance is already running. Startup with Windows is optional in the tray menu; keep the executable at a stable path if enabled.

运行一个 EXE，再切到 Codex 前台。通过仪表/托盘右键进入设置；`--settings` 也可打开已有实例的设置。随 Windows 启动默认由用户选择；启用后不要随意移动 EXE。

Preferences and imported assets use `%LOCALAPPDATA%\CodexUsageCardLocal`. This legacy directory is retained for compatibility with local prototypes; the project name is Codex Rail. Exit before making manual configuration backups or restoring a previous file.

设置和导入资源使用 `%LOCALAPPDATA%\CodexUsageCardLocal`，保留历史目录以兼容早期本地版本。手动备份/恢复前请先退出。

## Missing quota / 额度缺失

Codex must be signed in. The reader discovers `codex.exe` in the desktop runtime directory or PATH. A custom executable can be selected through the `CODEX_USAGE_CODEX` environment variable. Session discovery honors `CODEX_HOME`; `--sessions <directory>` overrides the session directory.

Codex 需已登录。自动寻找桌面运行时或 PATH 中的 `codex.exe`；可用 `CODEX_USAGE_CODEX` 指定可执行文件路径。日志路径遵循 `CODEX_HOME`，也可传入 `--sessions <目录>`。

An unavailable 5-hour field is not zero remaining. `—` means data was not supplied or is unavailable. Stale quota is not used for color warnings. Only the supported Codex main allowance bucket is shown; model-specific buckets are not guessed.

`—` 表示缺失或暂不可用，五小时缺失不表示余额为零。过期数据不触发提醒；不猜测模型专属额度。

## No visible rail / 没有浮窗

Check that the program is running in the tray, Codex is foreground and the tray hide switch is off. The rail is hidden for minimized/other foreground apps and when fewer than 100 logical pixels remain between reserved navigation/account areas. Codex layout or IPC changes can also require a compatibility update.

检查托盘进程、Codex 前台和隐藏开关。最小化、其他应用前台或可用高度不足 100 DIP 时隐藏；Codex 界面及 IPC 更新也可能需要兼容修复。
