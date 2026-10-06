# Contributing / 贡献

Welcome fixes for compatibility, accessibility, translation, render/resource behavior and physical DPI coverage. Keep the runtime dependency surface small and separate diagnostic code from production. A feature should earn its always-on cost.

欢迎兼容性、无障碍、翻译、资源管理及真实 DPI 环境验证方面的贡献。保持依赖少，测试与生产分离；常驻功能需要解释其资源代价。

Build on Windows with .NET 10 SDK, run `./test.ps1`, and describe the trigger, before/after behavior and evidence in a pull request. Do not assert all resolutions are supported based on a single monitor. New screenshots must use fixture data or be explicitly scrubbed. Preserve the upstream MIT notice.

使用 Windows 和 .NET 10 SDK 编译，运行 `./test.ps1`。PR 说明触发条件、前后行为与验证证据；不要凭单屏测试声称全部适配。演示图使用虚构数据，保留上游 MIT 声明。

The project's goal is unobtrusive usage visibility until Codex supplies a native equivalent. It is not intended to become a general agent launcher or a replacement for Codex.
