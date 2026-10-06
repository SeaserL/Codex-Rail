using System.Text.Json;
using System.Diagnostics;
namespace CodexTokenOverlay;
internal static class AdaptationSuite
{
    internal static bool Run(string[] args)
    {
        if(args.Length!=2||args[0]!="--adaptation")return false;
        var output=args[1];Directory.CreateDirectory(output);var cases=new List<object>();var settingsCases=new List<object>();
        foreach(var size in new[]{new Size(1366,768),new Size(1600,900),new Size(1920,1080),new Size(2560,1440),new Size(3840,2160)})
        foreach(var percent in new[]{100,125,150,175,200,250})
        foreach(var window in new[]{1d,.75})
        foreach(var columns in new[]{1,2,3})
        foreach(var all in new[]{false,true})
        {
            var scale=percent/100d;var clientHeight=(int)Math.Round((size.Height-40*scale)*window);var viewport=AdaptiveGaugeLayout.RailViewport(clientHeight,scale);var height=(int)Math.Floor((viewport.Bottom-viewport.Top)/scale);
            var s=new UsageCardSettings();if(all){s.Rows=16;s.Columns=columns;foreach(var m in s.Modules)m.Enabled=true;s.Reflow();}else if(columns!=1)continue;
            var before=JsonSerializer.Serialize(s);
            if(height<100){cases.Add(new{size.Width,size.Height,percent,window,columns,all,usable=false,availableDip=height,reason="less than 100 DIP between navigation and account buttons"});continue;}
            var first=AdaptiveGaugeLayout.Create(s,height,56,0);var found=new HashSet<MetricKind>();
            using var form=new UsageCardForm{Settings=s,Tokens=new("fixture","",16000000,15000000,10000000,1000000,500000,48000,200000,DateTime.UtcNow),Quota=new(new(32,300,null),new(22,10080,null),DateTimeOffset.Now)};
            form.ConfigureViewport(height,56);
            for(var page=0;page<first.Pages;page++)
            {
                var layout=AdaptiveGaugeLayout.Create(s,height,56,page);
                if(layout.Settings.CompactHeight>height)throw new Exception($"layout overflow {size} {percent} {height} / {layout.Settings.CompactHeight}");
                foreach(var m in layout.Settings.Modules.Where(m=>m.Enabled))if(!found.Add(m.Metric))throw new Exception("duplicate page metric");
                foreach(var m in layout.Settings.Modules.Where(m=>m.Enabled))if(layout.Settings.RowTop(m.Row)+layout.Settings.RowHeight(m.Row)>layout.Settings.CompactHeight-layout.Settings.RuntimeFooterHeight)throw new Exception("gauge overlaps pagination");
                using var image=form.Render((uint)(96*scale));if(image.Height>(viewport.Bottom-viewport.Top)+1)throw new Exception("bitmap exceeds viewport");
                form.ChangePage(1);
            }
            if(found.Count!=s.Modules.Count(m=>m.Enabled)||JsonSerializer.Serialize(s)!=before)throw new Exception("adaptive paging loses or persists metrics");
            cases.Add(new{size.Width,size.Height,percent,window,columns,all,usable=true,availableDip=height,pages=first.Pages,heightDip=first.Settings.CompactHeight});
        }
        foreach(var language in new[]{UiLanguage.Chinese,UiLanguage.English})
        foreach(var scenario in new[]{(1920,1080,100),(1920,1080,150),(1920,1080,200),(3840,2160,200),(3840,2160,250),(1366,768,150)})
        {
            var (width,height,percent)=scenario;var scale=percent/100d;UiText.Language=language;
            using var dialog=new UsageSettingsDialog(new(){Language=language},diagnostic:true){Opacity=0,StartPosition=FormStartPosition.Manual,Location=new(-10000,-10000)};
            dialog.Show();Application.DoEvents();dialog.SimulateDpi((int)(96*scale));Application.DoEvents();
            var area=new Rectangle(-width,0,width,height-(int)Math.Round(40*scale));dialog.FitToWorkArea(area);Application.DoEvents();
            if(!area.Contains(dialog.Bounds))throw new Exception($"settings out of workarea: {dialog.Bounds} / {area}");
            for(var page=0;page<4;page++)
            {
                dialog.DiagnosticPage(page);Application.DoEvents();dialog.VerifyTranslation(language);
                if((width==1920&&percent==150)||(width==3840&&percent==200))
                {using var bitmap=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(output,$"settings-{language}-{width}-{percent}-page{page}.png"));}
            }
            dialog.RegressionExercise();
            var originalLanguage=language;
            dialog.DiagnosticLanguage(UiLanguage.English);dialog.VerifyTranslation(UiLanguage.English);
            dialog.DiagnosticLanguage(UiLanguage.Chinese);
            if(dialog.Text!="Codex Rail · 仪表控制面板 · 布局编辑")throw new Exception("language round trip failed");
            dialog.DiagnosticLanguage(originalLanguage);
            settingsCases.Add(new{screenWidth=width,screenHeight=height,percent,language=language.ToString(),dialog.Width,dialog.Height,dialog.DeviceDpi,contained=true});dialog.Hide();
        }
        UiText.Language=UiLanguage.English;
        using(var form=new UsageCardForm())
        {
            form.Rebuild(192);form.Present(new(-10000,-10000),0);var allocations=form.SurfaceAllocations;var copies=form.SurfaceCopies;
            var timer=Stopwatch.StartNew();for(var i=0;i<120;i++)form.Present(new(-10000,-10000),(byte)(i*2));timer.Stop();
            if(form.SurfaceAllocations!=allocations||form.SurfaceCopies!=copies)throw new Exception("fade reallocates/copies native surface");
            File.WriteAllText(Path.Combine(output,"surface-cache.json"),JsonSerializer.Serialize(new{frames=120,allocations=form.SurfaceAllocations,copies=form.SurfaceCopies,averagePresentMs=timer.Elapsed.TotalMilliseconds/120}));
        }
        File.WriteAllText(Path.Combine(output,"geometry.json"),JsonSerializer.Serialize(cases,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"settings.json"),JsonSerializer.Serialize(settingsCases,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {cases.Count} layout cases; {settingsCases.Count} locale/DPI/window cases; native fade buffer reused.");UiText.Language=UiLanguage.System;return true;
    }
}
internal sealed partial class UsageSettingsDialog
{
    internal void DiagnosticLanguage(UiLanguage language){Result.Language=language;ApplyLanguage();}
    internal void VerifyTranslation(UiLanguage language)
    {
        if(language!=UiLanguage.English)return;
        static bool Chinese(string s)=>s.Any(c=>c is >= '\u4e00' and <= '\u9fff');
        void Walk(Control c){if(c.Visible&&c.Text is {} text&&Chinese(text)&&!text.Contains("中文")&&!text.Contains("语言")&&!text.Contains("System /"))throw new Exception($"untranslated {c.GetType().Name}: {text}");foreach(Control child in c.Controls)Walk(child);}
        Walk(this);
        foreach(var metric in Result.Modules)if(Chinese(metric.ToString()))throw new Exception("untranslated metric");
        foreach(var combo in new[]{_style,_align,_ring,_themes})if(combo!=null)foreach(var item in combo.Items)if(Chinese(combo.GetItemText(item)??""))throw new Exception("untranslated dropdown: "+combo.GetItemText(item));
    }
}
