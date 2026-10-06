# Data flow and privacy / 数据与隐私

This document describes the current source, not a claim that all upstream components are offline.

| Component | Reads / 读取 | Writes / 写入 |
|---|---|---|
| Quota client | Local Codex app-server rate-limit responses | Initialization and quota requests over stdin/stdout |
| Session monitor | Local session JSONL metadata, usage counters and turn events | No session-file modifications |
| Page identity | Windows UI Automation primary document title; local thread catalog ID/name/title columns | No application or catalog modifications |
| Window tracking | Window geometry/DPI/foreground metadata; a few rail-edge pixels for theme/position | Moves its own overlay only |
| Settings/assets | User preferences, imported fonts/images | Local configuration and asset copies |

JSONL files may contain prompts, responses and project paths. The program reads log lines to extract its supported metadata and counters. It does not upload these logs, include them in releases, or write a conversation archive. No application telemetry, advertising SDK, analytics service or update service is added. Codex handles its own authentication and any network requests needed for account quota. The quota reader does not directly read Codex authentication files.

JSONL 可能包含提示词、回复及项目路径。本程序读取日志行并提取支持的元数据和计数，不上传日志、不在发布包内包含它们、不另存聊天档案。未加入广告、分析遥测或自动更新服务。额度请求的认证与网络连接由 Codex 处理，本程序不直接读取认证文件。

The window tracker samples a few host rail pixels to determine its boundary and light/dark text color. It does not record desktop screenshots. Documentation media uses synthetic counters and independent diagnostic renders, never a user's account/session dump.

定位和主题判断会采样少量宿主侧栏像素，不保存桌面截图。文档演示使用虚构数据与独立诊断渲染。

For public bug reports, remove account identifiers, thread IDs, local paths and chat content. Report the app/Windows version, DPI, configuration choices and a minimal reproduction instead. Do not attach raw session or authentication files.

公开反馈请去除账户标识、会话 ID、本地路径及聊天内容，只提供版本、DPI、配置与复现步骤；不要上传原始日志或认证文件。

Page identity reads the host window’s primary document title through native UI Automation and uniquely matches it to the local SQLite catalog. It does not read chat UI text, require a debugger, modify Codex, or upload titles. Duplicate titles remain unconfirmed. No WPF/database package is added; Windows supplies UIA and SQLite.

页面识别只读取宿主主文档标题，并与本地目录唯一匹配，不读取聊天界面的正文、不打开调试器、不改 Codex、不上传标题。同名不猜测。使用系统原生 UIA/SQLite，不添加 WPF 或数据库依赖。
