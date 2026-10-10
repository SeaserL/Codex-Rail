# Attribution / 致谢与许可

## codex-token-overlay

Source lineage: https://github.com/soleillevant0125/codex-token-overlay

Baseline: `b3a38d727fb2e0cf8e8c92ffff3f65da9a592dc5` (the original local checkout). License: MIT; copyright (c) 2026 soleillevant0125. The full notice is retained in LICENSE and embedded in the application.

此项目保留上游窗口识别、路由和日志解析等代码，并重写了侧栏仪表/设置与相关行为。上游 MIT 声明保留，不把此衍生项目描述为完全从零实现。

## TrafficMonitor

https://github.com/zhongyang219/TrafficMonitor — design and architecture inspiration only. No source, icons, screenshots, translations or other assets copied. Its license is not used to license this project's implementation.

仅借鉴界面与资源管理思路，未复制源码或素材。

## .NET and Windows

Framework-dependent distributions require .NET 10 Desktop Runtime. Self-contained distributions include Microsoft runtime components under their applicable licenses/notices. `tools/package-release.ps1` exports the runtime pack's license and third-party notices alongside the binary when available, and stops if it cannot locate them. Windows fonts are used for rendering on the build machine, not redistributed as font files.

The native watcher is built with Microsoft Visual C++ and a statically linked redistributable CRT. It requires no separate VC runtime installation. Windows API libraries are supplied by the operating system.
