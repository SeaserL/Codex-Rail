# Changelog / 更新记录

## 0.14.6 — numeric deselection crash / 数字参数取消选中崩溃

- Restore each selected parameter control's original background instead of assigning transparent to NumericUpDown, which does not support transparent backgrounds.
- Add a regression reproducing row count 4 → 5 followed by deselection, numeric-to-numeric selection, Escape and page changes.
- 修复修改行数等数字参数后取消选中导致的闪退，保留原有点击选中与滚轮调整逻辑。
## 0.14.5 — menu dismissal / 右键菜单关闭

- Dismiss the nonactivating overlay menu on outside mouse-down, Escape and a foreground application change. Outside clicks continue to the original target.
- Attach low-level input hooks only while the menu is open and release them on close/dispose. Keep native AutoClose and reject stale queued dismissal after reopen.
- Close overlay-owned menus when hiding the overlay; tray menus can remain usable while Codex is in the background. Resume rendering and hover according to cursor position after dismissal.
- 点击菜单外、Esc 和切换应用可收起右键菜单；菜单打开期间暂停悬停展开，关闭后恢复。新增监听仅在菜单打开时存在。
## 0.14.4 — data transitions / 数据切换动效

- Blend changed metrics over 140 ms using premultiplied pixels; rapid changes continue from the displayed frame. Stop the animation timer when idle or hidden.
- Coalesce the first 120 ms of session switching to avoid a brief unavailable frame when fresh data is ready quickly. Reserve number widths to reduce font-size jumps.
- Keep geometry and native surface allocations stable during data changes; include session source in the render cache key.
- 数据变化采用短过渡，快速切换合并中间空白帧，数字预留排版宽度；隐藏和静止时停止动效计时器。
## 0.14.3 — page identity / 页面识别

- Replace production subscription/recent-log routing with native UI Automation document title + unique local catalog matching. No Codex patch, debugger, WPF or database package.
- Read identity on a separate MTA thread; stop polling while hidden and invalidate stale/ambiguous identity. Background subscriptions no longer determine the visible session.
- Add catalog regressions for manual rename, duplicate titles, missing metadata, read-only queries and legacy schemas. Live two-round page switching was observed in the diagnostic prototype; integrated page/log reading passed.
- 当前页面标题唯一匹配会话目录；同名不猜测。两次真实往返切换原型验证通过，集成版可读取页面 ID 和日志数据。跨窗口、目录重命名和 Codex 更新兼容性仍需实际体验验证。

## 0.14.2

- Restore session metrics when Codex keeps multiple subscriptions: select the most recently active subscribed local session, with a labeled local-log fallback when IPC is unavailable.
- Restore display without weakening quota/session separation or stale-result rejection. Candidate changes invalidate the selection; known routes take priority.
- Add live read diagnostics and regressions for ambiguous/offline subscriptions and removed candidates.
- 修复 0.14.1 在多个订阅时完全隐藏聊天数据的回退；详情标注自动匹配来源。仅查看旧聊天而没有日志活动时，仍不能保证匹配当前页面。

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
