# Rebuilding documentation media

On Windows, install Python 3 and Pillow (`python -m pip install Pillow`), then run from the repository root:

```powershell
dotnet run --project tests/CodexUsageCard.Tests.csproj -c Release -- --media media-frames
python tools/make-media.py
```

The test fixture uses the production renderer with synthetic metrics. It does not access account quotas or real session logs. GIF hover frames demonstrate rendering, not actual mouse detection or Windows compositor timing. Font files are not redistributed.

在 Windows 下安装 Python 3 与 Pillow，执行上述两条命令即可重建中英展示素材。所有指标均为示例数据。动图展示渲染过程，不作为系统动画同步的证明。
