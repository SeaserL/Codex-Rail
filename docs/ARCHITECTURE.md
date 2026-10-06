# Architecture / 架构

```mermaid
flowchart LR
    A[Codex app-server] -->|rate limits| Q[QuotaClient]
    B[Local session JSONL] --> T[TokenLogMonitor / SessionVitalsMonitor]
    C[Desktop named pipe] --> R[Active-thread route]
    R --> T
    W[Window events / DPI / rail-edge samples] --> X[UsageCardContext]
    Q --> X
    T --> X
    S[Settings panel] --> X
    X --> L[Adaptive display-only layout]
    L --> G[GDI renderer / cached native DIB]
    G --> O[Non-activating layered overlay]
```

No web renderer is included. Quota refresh is approximately once a minute while displayed; the helper exits after a read. Session polling is every two seconds, with bounded/coalesced file notifications. Foreground/window events drive visibility and geometry, with fallback checks. Animation timers stop at completion. Configuration edits are debounced and trigger live preview only when changed.

没有网页渲染器。显示时约每分钟读额度，辅助进程读取后退出；会话每两秒检查，文件通知合并且有上限。窗口事件驱动可见性与位置，另有补偿检查。动画结束即停止，设置预览由变更触发。

Namespaces, the configuration directory, startup registry key and single-instance mutex retain prototype names to keep existing preferences/startup behavior compatible. The public assembly and project are named Codex Rail. Session-log parsing and desktop IPC are version-sensitive; they are not presented as a stable extension API.

命名空间、配置目录、启动项和互斥锁保留历史标识以兼容已有设置；公开项目与程序集名为 Codex Rail。日志和 IPC 有版本依赖，不把它们宣传为稳定的插件 API。
