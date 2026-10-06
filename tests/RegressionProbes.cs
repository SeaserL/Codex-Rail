using System.Diagnostics;
using System.Text.Json;
namespace CodexTokenOverlay;
internal sealed partial class UsageCardContext {
    internal static bool TryRunProbe(string[] args)
    {
        if(args.Length==2 && args[0]=="--assets-probe")
        {
            Directory.CreateDirectory(args[1]);AssetStore.Root=Path.Combine(args[1],"test-assets");
            var source=Path.Combine(args[1],"sample.png");using(var sample=new Bitmap(1600,1200)){using var g=Graphics.FromImage(sample);g.Clear(Color.FromArgb(25,100,130));sample.Save(source);}
            var image=AssetStore.ImportImage(source);using(var loaded=AssetStore.LoadImage(image)){if(loaded==null||loaded.Width>1024||loaded.Height>1024)throw new Exception("image cache failed");}
            var font=AssetStore.ImportFont(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"consola.ttf"));if(!AssetStore.Fonts().Any(f=>f.File==font.File))throw new Exception("font catalog persistence failed");
            if(AssetStore.Resolve("fonts","../bad.ttf")!="")throw new Exception("unsafe asset path");
            using var card=new UsageCardForm{Tokens=new("demo","",124050,114000,86000,10050,2020,48000,200000,DateTime.UtcNow)};
            card.Settings.FontFile=font.File;card.Settings.FontFamily=font.Name;card.Settings.BackgroundImage=image;card.Settings.ImageOpacity=160;
            using(var bitmap=card.Render(96)){if(bitmap.GetPixel(20,280).A<100)throw new Exception("imported background missing");bitmap.Save(Path.Combine(args[1],"image-font-demo.png"));}
            var cachedStart=Stopwatch.StartNew();for(var i=0;i<40;i++){using var bitmap=card.Render(96);}cachedStart.Stop();
            card.Settings.BackgroundImage="missing.png";using(var bitmap=card.Render(96)){if(bitmap.GetPixel(20,280).A>0)throw new Exception("missing image fallback failed");}
            card.Settings.FontFile="missing.ttf";using(var bitmap=card.Render(96)){}
            using var dialog=new UsageSettingsDialog(new(),diagnostic:true);dialog.SelectionRegression();dialog.DiagnosticPage(2);dialog.Opacity=0;dialog.Show();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[1],"appearance.png"));}dialog.Hide();
            File.WriteAllText(Path.Combine(args[1],"assets.json"),JsonSerializer.Serialize(new{fontImport=true,imageImport=true,missingAssetFallback=true,wheelToggle=true,imageCacheMaximum=1024,cachedRenderAverageMs=cachedStart.Elapsed.TotalMilliseconds/40,themeCount=UsageCardSettings.ThemeNames.Length}));return true;
        }
        if(args.Length==2 && args[0]=="--visual-regression")
        {
            Directory.CreateDirectory(args[1]);
            using var card=new UsageCardForm { Tokens=new("test","",16000000,15000000,10000000,1000000,500000,10000,200000,DateTime.UtcNow),Quota=new(new(30,300,null),new(24,10080,null),DateTimeOffset.Now) };
            card.Settings.Opacity=0;
            using(var transparent=card.Render(96))
            {
                var opaqueText=0;
                for(var y=20;y<80;y++)for(var x=4;x<40;x++)if(transparent.GetPixel(x,y).A>240)opaqueText++;
                if(opaqueText<5)throw new Exception("text alpha tied to background");
                transparent.Save(Path.Combine(args[1],"zero-background.png"));
            }
            card.Settings.Material=true;card.Settings.BackgroundHex="#162019";card.Settings.AccentHex="#A5FF39";card.Settings.TextHex="#F3FFE7";card.Settings.Opacity=225;card.Details=true;
            using(var material=card.Render(144))material.Save(Path.Combine(args[1],"material-green.png"));
            card.Settings.BarWidth=16;card.Settings.RingWidth=8;
            using(var thick=card.Render(96))thick.Save(Path.Combine(args[1],"custom-thickness.png"));
            return true;
        }
        if(args.Length==2 && args[0]=="--usability-probe")
        {
            Directory.CreateDirectory(args[1]);var settings=new UsageCardSettings();if(!settings.LivePreview||settings.ExpandMs!=150||settings.RingDiameter!=36)throw new Exception("new defaults failed");
            if(!(settings.RowHeight(0)<settings.RowHeight(2)&&settings.RowHeight(2)<settings.RowHeight(1)))throw new Exception("style row density failed");
            settings.WeeklyWarning=7;settings.FontFamily="Consolas";settings.ApplyTheme(5);settings.Rows=12;settings.RestorePage(0);if(settings.WeeklyWarning!=7||settings.FontFamily!="Consolas"||!settings.Material||settings.Rows!=4)throw new Exception("layout reset leaked across tabs");
            settings.ExpandMs=777;settings.RingDiameter=40;settings.RestorePage(3);if(settings.ExpandMs!=777||settings.RingDiameter!=40||settings.WeeklyWarning!=20||!settings.Material)throw new Exception("quota reset leaked across tabs");
            settings.Rows=12;settings.RestorePage(2);if(settings.Rows!=12||settings.ExpandMs!=150||settings.RingDiameter!=36||settings.Material)throw new Exception("appearance reset failed");
            var warnings=new HashSet<string>();for(var i=1;i<=UsageCardSettings.ThemeNames.Length;i++){settings.ApplyTheme(i);if(settings.WarningHex==settings.AccentHex)throw new Exception("theme contrast color missing");warnings.Add(settings.WarningHex);}if(warnings.Count!=UsageCardSettings.ThemeNames.Length)throw new Exception("theme alert palettes not distinct");
            settings=new();using var card=new UsageCardForm{Settings=settings,Vitals=new("gpt-6.1-sol","medium","进行中",24.3,2500,18),Tokens=new("demo","",124050,114000,86000,10050,2020,48000,200000,DateTime.UtcNow),Quota=new(new(32,300,null),new(22,10080,null),DateTimeOffset.Now)};
            using(var bitmap=card.Render(144))bitmap.Save(Path.Combine(args[1],"compact-default.png"));card.Details=true;using(var bitmap=card.Render(144))bitmap.Save(Path.Combine(args[1],"compact-expanded.png"));
            File.WriteAllText(Path.Combine(args[1],"usability.json"),JsonSerializer.Serialize(new{numericRow=settings.RowHeight(0),ringRow=settings.RowHeight(2),barRow=settings.RowHeight(1),pageResetPassed=true,themePalettes=UsageCardSettings.ThemeNames.Length}));return true;
        }
        if(args.Length==2 && args[0]=="--rows-probe")
        {
            Directory.CreateDirectory(args[1]);var settings=new UsageCardSettings();
            var enabled=settings.Modules.Where(m=>m.Enabled).OrderBy(m=>m.Row).ToArray();
            if(settings.Columns!=1||settings.Rows!=4||!settings.AllowDrag||enabled.Length!=4||enabled[0].Metric!=MetricKind.聊天Token||enabled[1].Metric!=MetricKind.上下文占用||enabled[2].Metric!=MetricKind.周剩余||enabled[2].RingValue!=RingValuePosition.环内||enabled[3].Metric!=MetricKind.五小时剩余)throw new Exception("new defaults failed");
            using var card=new UsageCardForm{Settings=settings,Vitals=new("gpt-6.1-sol","medium","进行中",24.3,2500,18),Tokens=new("demo","",124050,114000,86000,10050,2020,48000,200000,DateTime.UtcNow),Quota=new(new(32,300,null),new(22,10080,null),DateTimeOffset.Now)};
            using(var image=card.Render(144))image.Save(Path.Combine(args[1],"default.png"));
            settings.Rows=16;settings.Alignments=new[]{RowAlignment.靠右,RowAlignment.居中,RowAlignment.居中,RowAlignment.居中};settings.ExpandAlignments();if(settings.Alignments.Length!=16||settings.Alignments[0]!=RowAlignment.靠右)throw new Exception("legacy alignment migration failed");
            foreach(var module in settings.Modules)module.Enabled=true;settings.Reflow();settings.Alignments[15]=RowAlignment.靠左;
            if(settings.Validate()!=null||settings.CompactHeight>980||settings.Modules.Count(m=>m.Enabled)!=15)throw new Exception("full-height layout failed");
            using(var image=card.Render(96))image.Save(Path.Combine(args[1],"all-metrics-16-rows.png"));
            var clone=settings.Clone();if(clone.Alignments.Length!=16||clone.Rows!=16||clone.Validate()!=null)throw new Exception("expanded layout persistence failed");
            File.WriteAllText(Path.Combine(args[1],"rows.json"),JsonSerializer.Serialize(new{maximumRows=16,modules=15,heightDip=settings.CompactHeight,cellHeight=settings.CellHeight,gap=settings.Gap,defaultsPassed=true,migrationPassed=true}));return true;
        }
        if(args.Length==2 && args[0]=="--features-probe")
        {
            Directory.CreateDirectory(args[1]);var monitor=new SessionVitalsMonitor();var empty=Path.Combine(args[1],"empty-fixture.jsonl");File.WriteAllText(empty,"");monitor.Poll(empty);
            monitor.Accept("{\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-test\",\"effort\":\"high\"}}");
            monitor.Accept("{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"output_tokens\":100}}}}");
            monitor.Accept("{\"timestamp\":\"2026-10-06T00:00:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"turn_id\":\"a\"}}");
            monitor.Accept("{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"output_tokens\":300,\"input_tokens\":999999},\"last_token_usage\":{\"output_tokens\":200}}}}");
            var active=monitor.Snapshot(DateTimeOffset.Parse("2026-10-06T00:00:10Z"));if(active.AverageOutputTps!=20||active.Model!="gpt-test"||active.Effort!="high")throw new Exception("turn throughput/model failed");
            monitor.Accept("{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"turn_id\":\"other\",\"duration_ms\":1000}}");if(monitor.Snapshot(DateTimeOffset.Parse("2026-10-06T00:00:10Z")).State!="进行中")throw new Exception("wrong turn bound");
            monitor.Accept("{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\",\"turn_id\":\"a\",\"duration_ms\":20000,\"time_to_first_token_ms\":2500}}");
            var completed=monitor.Snapshot(DateTimeOffset.Parse("2026-10-06T01:00:00Z"));if(completed.AverageOutputTps!=10||completed.FirstTokenMs!=2500||completed.State!="已完成")throw new Exception("completion freeze failed");
            if(monitor.Poll(null).AverageOutputTps is not null)throw new Exception("thread state leaked");
            var incremental=new SessionVitalsMonitor();File.WriteAllText(empty,"{\"timestamp\":\"2026-10-06T00:00:00Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"turn_id\":\"i\"}}\n");incremental.Poll(empty);
            var usage="{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"output_tokens\":350},\"last_token_usage\":{\"output_tokens\":50}}}}";
            File.AppendAllText(empty,usage[..40]);incremental.Poll(empty);if(incremental.Snapshot(DateTimeOffset.Parse("2026-10-06T00:00:10Z")).AverageOutputTps is not null)throw new Exception("partial line consumed");
            File.AppendAllText(empty,usage[40..]+"\n");incremental.Poll(empty);if(incremental.Snapshot(DateTimeOffset.Parse("2026-10-06T00:00:10Z")).AverageOutputTps!=5)throw new Exception("incremental/baseline inference failed");

            using var card=new UsageCardForm{Quota=new(new(80,300,null),new(90,10080,null),DateTimeOffset.Now),Vitals=completed};card.Settings.Columns=1;card.Settings.Modules[2].RingValue=RingValuePosition.环内;card.Settings.RingDiameter=40;
            if(!card.IsQuotaLow(MetricKind.周剩余,10)||!card.IsQuotaLow(MetricKind.五小时剩余,20)||card.IsQuotaLow(MetricKind.周剩余,21)||card.IsQuotaLow(MetricKind.上下文占用,10)||card.IsQuotaLow(MetricKind.周剩余,null))throw new Exception("quota thresholds failed");
            foreach(var position in Enum.GetValues<RingValuePosition>()){card.Settings.Modules[2].RingValue=position;using var bitmap=card.Render(144);bitmap.Save(Path.Combine(args[1],$"ring-{position}.png"));}
            card.Details=true;using(var detail=card.Render(144))detail.Save(Path.Combine(args[1],"status-details.png"));
            card.QuotaFailed=true;if(card.IsQuotaLow(MetricKind.周剩余,10))throw new Exception("failed data warns");card.QuotaFailed=false;card.Settings.QuotaWarnings=false;if(card.IsQuotaLow(MetricKind.周剩余,10))throw new Exception("warning toggle failed");
            card.Settings.QuotaWarnings=true;card.Quota=new(null,new(90,10080,null),DateTimeOffset.Now.AddMinutes(-3));if(card.IsQuotaLow(MetricKind.周剩余,10))throw new Exception("stale data warns");
            File.Delete(empty);
            File.WriteAllText(Path.Combine(args[1],"features.json"),JsonSerializer.Serialize(new{turnThroughput=true,completionFreeze=true,threadReset=true,thresholds=true,ringModes=3}));return true;
        }
        if(args.Length==2 && args[0]=="--rail-probe")
        {
            if(!CodexWindowLocator.TryGetForegroundCodexTarget(out var target)){File.WriteAllText(args[1],"{\"available\":false}");return true;}
            var hwnd=target.HostWindow.Handle;GetClientRect(hwnd,out var rect);var dc=GetDC(hwnd);var colors=new List<object>();
            try{for(var x=20;x<=80;x++){var samples=new List<uint>();foreach(var f in new[]{.32,.44,.56,.68,.8})samples.Add(GetPixel(dc,x,(int)(rect.Bottom*f)));colors.Add(new{x,colors=samples});}}finally{ReleaseDC(hwnd,dc);}
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{available=true,dpi=target.HostWindow.Dpi,rail=MeasureRail(hwnd,target.HostWindow.Dpi),colors}));return true;
        }
        if(args.Length==2 && args[0]=="--idle-preview-benchmark")
        {
            using var main=new UsageCardForm();main.Rebuild(96);main.Present(new Point(-10000,-10000),0);
            using var dialog=new UsageSettingsDialog(new(),diagnostic:true);dialog.Opacity=0;dialog.Show();
            var frames=0;dialog.PreviewChanged+=settings=>{if(settings is null)return;frames++;main.Settings=settings;main.Rebuild(96);main.Present(new Point(-10000,-10000),0);};
            static void Pump(int ms){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(20);}}
            var results=new List<object>();
            foreach(var live in new[]{false,true}){
                dialog.DiagnosticPreview(live);Pump(500);using var process=Process.GetCurrentProcess();GC.Collect();process.Refresh();var memory=process.PrivateMemorySize64;var cpu=process.TotalProcessorTime;var count=frames;
                Pump(4000);process.Refresh();results.Add(new{live,seconds=4,additionalPreviewFrames=frames-count,cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds,privateBytes=process.PrivateMemorySize64,privateGrowthBytes=process.PrivateMemorySize64-memory});
            }
            dialog.Hide();File.WriteAllText(args[1],JsonSerializer.Serialize(results));return true;
        }
        if(args.Length==2 && args[0]=="--interaction-probe")
        {
            using var card=new UsageCardForm();card.Rebuild(96);var desired=new Point(-10000,-9900);card.Present(desired,0);
            if(card.PresentedPosition!=desired)throw new Exception("native drag position not tracked");
            using var hit=card.Render(96,interactive:true);for(var y=0;y<hit.Height;y++)for(var x=0;x<hit.Width;x++)if(hit.GetPixel(x,y).A==0)throw new Exception("click-through hole");
            for(var rail=40;rail<95;rail++)for(var compact=24;compact<=rail;compact++){
                var x=PlaceHorizontally(13,rail,compact,0);if(Math.Abs((x-13)-(13+rail-x-compact))>1)throw new Exception("pixel centering failed");
                if(PlaceHorizontally(13,rail,compact,-100)!=13||PlaceHorizontally(13,rail,compact,100)!=13+rail-compact)throw new Exception("edge placement failed");
            }
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{nativePositionTracked=true,fullRectangleHitAlpha=true,pixelCenterDifferenceMaximum=1}));return true;
        }
        if(args.Length==2 && args[0]=="--live-benchmark")
        {
            using var card=new UsageCardForm();card.Rebuild(96);card.Present(new Point(-10000,-10000),0);
            using var process=Process.GetCurrentProcess();GC.Collect();process.Refresh();var baseline=process.PrivateMemorySize64;
            var cpu=process.TotalProcessorTime;var clock=Stopwatch.StartNew();var allocated=GC.GetTotalAllocatedBytes(true);
            for(var i=0;i<120;i++){card.Settings.HorizontalOffset=i%201-100;card.Settings.RingDiameter=8+i%41;card.Rebuild(96);card.Present(new Point(-10000,-10000),0);}
            clock.Stop();process.Refresh();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;
            GC.Collect();GC.WaitForPendingFinalizers();process.Refresh();
            File.WriteAllText(args[1],JsonSerializer.Serialize(new{frames=120,averageRenderAndPresentMs=clock.Elapsed.TotalMilliseconds/120,cpuMsPerFrame=cpuMs/120,managedAllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,privateMemoryGrowthBytes=process.PrivateMemorySize64-baseline,debounceMs=120,maximumFramesPerSecond=1000d/120}));
            return true;
        }
        if(args.Length==2 && args[0]=="--appearance-probe")
        {
            Directory.CreateDirectory(args[1]);using var card=new UsageCardForm{Tokens=new("test","",16000000,15000000,10000000,1000000,500000,10000,200000,DateTime.UtcNow),Quota=new(new(30,300,null),new(24,10080,null),DateTimeOffset.Now)};
            foreach(var light in new[]{false,true}){card.Light=light;using var image=card.Render(96);var strong=0;for(var y=20;y<80;y++)for(var x=4;x<40;x++){var c=image.GetPixel(x,y);if(c.A>240 && (light?c.R<20:c.R>235))strong++;}if(strong<5)throw new Exception("automatic text contrast failed");if(image.GetPixel(1,100).A!=0)throw new Exception("background default is not transparent");image.Save(Path.Combine(args[1],light?"day.png":"night.png"));}
            foreach(var size in new[]{12,24,40}){card.Settings.RingDiameter=size;using var image=card.Render(96);image.Save(Path.Combine(args[1],$"ring-{size}.png"));}
            return true;
        }
        if(args.Length==2 && args[0]=="--render-benchmark")
        {
            using var card=new UsageCardForm();
            using(var warmup=card.Render(96)) { }
            var allocated=GC.GetTotalAllocatedBytes(true);var clock=Stopwatch.StartNew();
            for(var i=0;i<40;i++) { using var bitmap=card.Render(96); }
            clock.Stop();
            File.WriteAllText(args[1],JsonSerializer.Serialize(new { renders=40,averageMs=clock.Elapsed.TotalMilliseconds/40,managedAllocatedBytes=GC.GetTotalAllocatedBytes(true)-allocated,cacheBytes=card.WidthDip*card.HeightDip*4,previewDebounceMs=120 }));
            return true;
        }
        if (args.Length == 2 && args[0] == "--card-demo")
        {
            using var card = new UsageCardForm
            {
                Quota = new(new(32, 300, DateTimeOffset.Now.AddHours(2).ToUnixTimeSeconds()), new(56, 10080, DateTimeOffset.Now.AddDays(3).ToUnixTimeSeconds()), DateTimeOffset.Now),
                Tokens = new("demo", "", 124050, 114000, 86000, 10050, 2020, 48000, 200000, DateTime.UtcNow)
            };
            using var bitmap = card.Render(144); bitmap.Save(args[1]); return true;
        }
        if (args.Length == 2 && args[0] == "--grid-probe")
        {
            Directory.CreateDirectory(args[1]);
            using var card = new UsageCardForm { Quota = new(new(32,300,1791580738),new(22,10080,1791580738),DateTimeOffset.Now), Tokens = new("demo","",124050,114000,86000,10050,2020,48000,200000,DateTime.UtcNow) };
            if (card.Settings.Validate() is not null || card.Settings.CompactWidth != 44) throw new Exception("narrow default failed");
            using var compact = card.Render(96); compact.Save(Path.Combine(args[1],"mixed-compact.png"));
            card.Details = true;
            using var expanded = card.Render(96); expanded.Save(Path.Combine(args[1],"mixed-expanded.png"));
            if (expanded.Height != compact.Height || card.WidthDip != 310) throw new Exception("width-only expansion failed");
            for (var y=4;y<compact.Height-4;y++) for(var x=4;x<compact.Width-4;x++)
                if(compact.GetPixel(x,y)!=expanded.GetPixel(x,y)) throw new Exception("compact content moved during expansion");
            card.Expansion = .5; using var half = card.Render(96); half.Save(Path.Combine(args[1],"mixed-half.png"));
            if (half.Width != 177 || half.Height != compact.Height) throw new Exception("intermediate geometry failed");
            card.Details = false; foreach (var m in card.Settings.Modules) m.Style=GaugeStyle.电量条;
            using var battery=card.Render(96); battery.Save(Path.Combine(args[1],"battery.png"));
            var settings = new UsageCardSettings(); settings.Modules[1].Row=0;
            if(settings.Validate() is null) throw new Exception("overlap accepted");
            settings=new();settings.CellWidth=60;
            if(settings.Validate() is null) throw new Exception("overflow accepted");
            settings=new();settings.Background=Color.MidnightBlue;
            if(settings.Clone().Background.ToArgb()!=settings.Background.ToArgb()) throw new Exception("color persistence failed");
            settings = new() { Columns = 2, Rows = 3, CellWidth = 14, Gap = 6, ShowValues = false };
            settings.Modules[0].Row = 0; settings.Modules[0].Column = 0;
            settings.Modules[1].Row = 0; settings.Modules[1].Column = 1;
            settings.Modules[2].Row = 1; settings.Modules[2].Column = 0;
            settings.Modules[3].Row = 2; settings.Modules[3].Column = 0;
            if (settings.Validate() is not null) throw new Exception("mixed aligned layout rejected");
            card.Settings = settings;
            using var spanning = card.Render(96); spanning.Save(Path.Combine(args[1], "two-bars-one-ring.png"));
            return true;
        }
        if (args.Length == 2 && args[0] == "--settings-probe")
        {
            using var dialog = new UsageSettingsDialog(new(), diagnostic: true);
            dialog.RegressionExercise();
            dialog.Opacity = 0;
            dialog.Show();dialog.RoutingRegression();
            using var bitmap = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bitmap, new Rectangle(0, 0, dialog.Width, dialog.Height));
            bitmap.Save(args[1]); dialog.Hide(); return true;
        }
        if (args.Length == 2 && args[0] == "--quota-probe")
        {
            using var quota = new QuotaClient();
            var data = quota.ReadAsync().GetAwaiter().GetResult();
            File.WriteAllText(args[1], JsonSerializer.Serialize(data)); return true;
        }
        if (args.Length == 2 && args[0] == "--card-layer-probe")
        {
            using var card = new UsageCardForm();
            using var bitmap = card.Render(96);
            var partialAlpha = 0;
            for (var y = 0; y < bitmap.Height; y++) for (var x = 0; x < bitmap.Width; x++)
            { var alpha = bitmap.GetPixel(x, y).A; if (alpha is > 0 and < 245) partialAlpha++; }
            if (bitmap.GetPixel(0, 0).A != 0 || partialAlpha == 0) throw new Exception("alpha edges failed");
            card.Rebuild(96); card.Present(new Point(-10000, -10000), 0);
            card.Present(new Point(-10000, -10000), 128);
            File.WriteAllText(args[1], JsonSerializer.Serialize(new { partialAlpha, layerUpdateSucceeded = true }));
            return true;
        }
        if (args.Length == 1 && args[0] == "--quota-selftest")
        {
            static QuotaData Parse(string json) { using var doc = JsonDocument.Parse(json); return QuotaData.Parse(doc.RootElement); }
            var data = Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":25,\"windowDurationMins\":300,\"resetsAt\":123},\"secondary\":{\"usedPercent\":90,\"windowDurationMins\":10080}}}");
            if (data.FiveHour?.Remaining != 75 || data.Weekly?.Remaining != 10 || data.Weekly.ResetsAt is not null) throw new Exception("legacy / remaining / missing reset failed");
            data = Parse("{\"rateLimitsByLimitId\":{\"other\":{\"primary\":{\"usedPercent\":1,\"windowDurationMins\":300}}},\"rateLimits\":{\"primary\":{\"usedPercent\":0,\"windowDurationMins\":300}}}");
            if (data.FiveHour is not null) throw new Exception("wrong bucket accepted");
            data = Parse("{\"rateLimitsByLimitId\":{\"codex\":{\"secondary\":{\"usedPercent\":150,\"windowDurationMins\":300},\"primary\":{\"usedPercent\":-1,\"windowDurationMins\":10080}}}}");
            if (data.FiveHour?.Remaining != 0 || data.Weekly?.Remaining != 100) throw new Exception("swapped windows / clamping failed");
            data = Parse("{\"rateLimits\":{\"primary\":null,\"secondary\":{\"usedPercent\":2,\"windowDurationMins\":15}}}");
            if (data.FiveHour is not null || data.Weekly is not null) throw new Exception("unknown duration accepted");
            data = Parse("{\"rateLimits\":null}");
            if (data.FiveHour is not null || data.Weekly is not null) throw new Exception("null quota accepted");
            return true;
        }
        return false;
    }
}
