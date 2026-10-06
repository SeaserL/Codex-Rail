# Changelog / 更新记录

## 0.14.1 — session refresh / 会话刷新修复

- Separate quota requests from session reads; a slow quota request no longer blocks chat metrics.
- Coalesce IPC route changes into immediate refreshes, clear old values on switch/disconnect, and reject reads completed for an obsolete route.
- Check file length as well as modification time so same-timestamp appends are not missed. Visible session polling is 500 ms; hidden overlays do not poll logs.
- Stop treating the last IPC subscription replay or most recently written log as proof of the visible conversation. Multiple subscriptions remain ambiguous and show unavailable metrics rather than another chat's numbers.

额度请求与聊天刷新分离；切换时清除旧值并拒绝过期异步结果；追加日志同时校验长度与时间。可见时每 500 ms 检查聊天数据，隐藏时不轮询日志。

重要限制：Codex IPC 提供订阅状态，并不保证提供当前查看的对话。多段订阅时本版显示“当前会话未确认”和 `—`，不会猜测最后一条订阅或最近写入的日志。不能据此宣称所有切换都已自动识别。

## 0.14.0 — initial public candidate / 首次公开候选

- Mixed numeric, bar, battery and ring modules; up to 3 columns / 16 rows.
- Weekly and 5-hour remaining quota/reset times when available, plus session counters and turn-average output speed.
- Hover expansion, threshold colors, 12 palettes, font/background import and live preview.
- System/Chinese/English language selection, DPI-aware settings and work-area fitting.
- Display-only pagination, saved constrained vertical dragging and native fade-buffer reuse.
- Separate production/test projects, MIT attribution, bilingual documentation and fixture media.

This release candidate is based on the locally iterated usage-card 0.14 build. Codex Rail is the public name; internal prototype identifiers remain for preference compatibility. Only Windows x64 is packaged. No hosted-CI, physical 1080p/4K monitor or clean-machine-without-.NET result is claimed before those environments are actually exercised.

该公开候选基于本地多次迭代的 0.14，名称改为 Codex Rail，历史内部标识保留。仅打包 Windows x64；不宣称已完成尚未实际执行的 GitHub CI、物理多屏或无运行时纯净机验证。
