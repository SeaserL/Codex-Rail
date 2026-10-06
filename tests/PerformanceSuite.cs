using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace CodexTokenOverlay;
internal static class PerformanceSuite
{
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process,uint flag);
    private static void Pump(int milliseconds){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(10);}}
    private static object Memory(){using var p=Process.GetCurrentProcess();p.Refresh();return new{workingSetMiB=p.WorkingSet64/1048576d,privateMiB=p.PrivateMemorySize64/1048576d,managedMiB=GC.GetTotalMemory(false)/1048576d,heapCommittedMiB=GC.GetGCMemoryInfo().TotalCommittedBytes/1048576d,handles=p.HandleCount,gdi=GetGuiResources(p.Handle,0),user=GetGuiResources(p.Handle,1)};}
    private static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Pump(100);}
    internal static bool Run(string[] args)
    {
        if(args.Length==2&&args[0]=="--pending-paths"){
            using var monitor=new TokenLogMonitor(Path.Combine(args[1],"missing-fixture"));var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var callback=typeof(TokenLogMonitor).GetMethod("OnLogChanged",flags)!;var field=typeof(TokenLogMonitor).GetField("_changedPaths",flags)!;
            int Count(){var pending=field.GetValue(monitor)!;return (int)pending.GetType().GetProperty("Count")!.GetValue(pending)!;}
            for(var i=0;i<80000;i++)callback.Invoke(monitor,[monitor,new FileSystemEventArgs(WatcherChangeTypes.Changed,args[1],"same.jsonl")]);var repeated=Count();if(repeated!=1)throw new Exception("duplicate notification accumulation");
            for(var i=0;i<8000;i++)callback.Invoke(monitor,[monitor,new FileSystemEventArgs(WatcherChangeTypes.Changed,args[1],$"{i}.jsonl")]);var unique=Count();var rescan=(int)typeof(TokenLogMonitor).GetField("_rescanNeeded",flags)!.GetValue(monitor)!;if(unique>256||rescan!=1)throw new Exception("pending paths not bounded");
            Directory.CreateDirectory(args[1]);var logs=Path.Combine(args[1],"logs");Directory.CreateDirectory(logs);var log=Path.Combine(logs,"fixture-00000000-0000-0000-0000-000000000001.jsonl");
            string Usage(int total)=>JsonSerializer.Serialize(new{type="event_msg",payload=new{type="token_count",info=new{total_token_usage=new{total_tokens=total},last_token_usage=new{total_tokens=42},model_context_window=200000}}})+"\n";
            File.WriteAllText(log,"{\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"source\":\"vscode\"}}\n"+Usage(123));using(var reader=new TokenLogMonitor(logs)){if(reader.Poll(true)?.TotalTokens!=123)throw new Exception("initial log snapshot failed");Thread.Sleep(20);File.AppendAllText(log,Usage(456));callback.Invoke(reader,[reader,new FileSystemEventArgs(WatcherChangeTypes.Changed,logs,Path.GetFileName(log))]);if(reader.Poll()?.TotalTokens!=456)throw new Exception("changed log snapshot failed");}
            File.WriteAllText(Path.Combine(args[1],"pending-paths.json"),JsonSerializer.Serialize(new{repeatedEvents=80000,pendingAfterRepeated=repeated,uniqueEvents=8000,pendingAfterUnique=unique,rescanScheduled=rescan==1,logReadAfterChange=true}));return true;
        }
        if(args.Length!=2||args[0]!="--performance")return false;
        var directory=args[1];Directory.CreateDirectory(directory);AssetStore.Root=Path.Combine(directory,"fixtures");
        var results=new List<object>();using var card=new UsageCardForm{Tokens=new("fixture","",124050,114000,86000,10050,2020,48000,200000,DateTime.UtcNow),Quota=new(new(32,300,null),new(22,10080,null),DateTimeOffset.Now)};
        card.Rebuild(144);card.Present(new Point(-10000,-10000),0);
        object Frames(string name,int count,Action<int> configure)
        {
            for(var i=0;i<10;i++){configure(i);card.Rebuild(144);card.Present(new Point(-10000,-10000),0);}
            Collect();var baseline=Memory();using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var allocated=GC.GetTotalAllocatedBytes(true);var times=new List<double>();
            for(var i=0;i<count;i++){configure(i);var clock=Stopwatch.StartNew();card.Rebuild(144);card.Present(new Point(-10000,-10000),0);clock.Stop();times.Add(clock.Elapsed.TotalMilliseconds);}
            process.Refresh();var cpuMs=(process.TotalProcessorTime-cpu).TotalMilliseconds;var managed=GC.GetTotalAllocatedBytes(true)-allocated;Collect();times.Sort();return new{name,frames=count,dpi=144,averageMs=times.Average(),p95Ms=times[(int)(times.Count*.95)],cpuMsPerFrame=cpuMs/count,managedKiBPerFrame=managed/1024d/count,before=baseline,after=Memory()};
        }
        results.Add(Frames("default-compact",300,_=>card.Expansion=0));
        results.Add(Frames("default-expanded",150,_=>card.Expansion=1));
        results.Add(Frames("cold-compact",40,i=>{card.Expansion=0;card.Tokens=card.Tokens! with {TotalTokens=130000+i};}));
        results.Add(Frames("cold-expanded",40,i=>{card.Expansion=1;card.Tokens=card.Tokens! with {TotalTokens=140000+i};}));
        results.Add(Frames("width-animation",240,i=>card.Expansion=(i%20)/19d));
        var imagePath=Path.Combine(directory,"sample.png");using(var image=new Bitmap(1600,1200)){using var g=Graphics.FromImage(image);g.Clear(Color.FromArgb(30,90,120));image.Save(imagePath);}
        card.Settings.BackgroundImage=AssetStore.ImportImage(imagePath);card.Settings.FontFile=AssetStore.ImportFont(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"consola.ttf")).File;card.Settings.FontFamily="Consolas";
        results.Add(Frames("imported-image-font-animation",240,i=>card.Expansion=(i%20)/19d));
        results.Add(Frames("cold-image-font-expanded",30,i=>{card.Expansion=1;card.Tokens=card.Tokens! with {TotalTokens=150000+i};}));
        card.Settings.BackgroundImage="";card.Settings.FontFile="";card.Settings.FontFamily="Microsoft YaHei UI";card.Expansion=0;
        var liveFrames=0;using(var dialog=new UsageSettingsDialog(new(),diagnostic:true))
        {
            dialog.Opacity=0;dialog.Show();dialog.PreviewChanged+=settings=>{if(settings==null)return;liveFrames++;card.Settings=settings;card.Rebuild(144);card.Present(new Point(-10000,-10000),0);};
            foreach(var live in new[]{false,true})
            {
                dialog.DiagnosticPreview(live);Pump(1000);Collect();using var process=Process.GetCurrentProcess();var start=process.TotalProcessorTime;var baseline=Memory();var clock=Stopwatch.StartNew();var frames=liveFrames;Pump(10000);clock.Stop();process.Refresh();var cpuMs=(process.TotalProcessorTime-start).TotalMilliseconds;
                results.Add(new{name=live?"preview-on-idle":"preview-off-idle",seconds=clock.Elapsed.TotalSeconds,extraFrames=liveFrames-frames,cpuMs,cpuPercentOneCore=cpuMs/clock.Elapsed.TotalMilliseconds*100,before=baseline,after=Memory()});
            }
            dialog.Hide();
        }
        for(var batch=0;batch<3;batch++){Collect();var beforeDialogs=Memory();for(var i=0;i<30;i++){using var dialog=new UsageSettingsDialog(new(),diagnostic:true);dialog.Opacity=0;dialog.Show();dialog.RegressionExercise();dialog.Hide();}Collect();Pump(500);results.Add(new{name="settings-open-edit-close-stress",batch=batch+1,cycles=30,before=beforeDialogs,after=Memory()});}
        File.WriteAllText(Path.Combine(directory,"render-preview.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        using var quota=new QuotaClient();var readWatch=Stopwatch.StartNew();var task=Task.Run(()=>quota.ReadAsync());var samples=new List<object>();double peakWs=0,peakPrivate=0,observedCpu=0;int? childId=null;
        while(!task.IsCompleted)
        {
            try{if(quota.ProcessId is {} pid){using var child=Process.GetProcessById(pid);child.Refresh();childId=pid;peakWs=Math.Max(peakWs,child.WorkingSet64/1048576d);peakPrivate=Math.Max(peakPrivate,child.PrivateMemorySize64/1048576d);observedCpu=Math.Max(observedCpu,child.TotalProcessorTime.TotalMilliseconds);}}
            catch(Exception ex)when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
            Thread.Sleep(10);
        }
        string? error=null;bool five=false,week=false;try{var data=task.GetAwaiter().GetResult();five=data.FiveHour!=null;week=data.Weekly!=null;}catch(Exception ex){error=ex.GetType().Name;}readWatch.Stop();
        File.WriteAllText(Path.Combine(directory,"quota-cost.json"),JsonSerializer.Serialize(new{elapsedMs=readWatch.Elapsed.TotalMilliseconds,childPid=childId,peakWorkingSetMiB=peakWs,peakPrivateMiB=peakPrivate,observedCpuMs=observedCpu,childRetained=quota.ProcessId!=null,fiveHourAvailable=five,weeklyAvailable=week,error,samplingMs=10},new JsonSerializerOptions{WriteIndented=true}));
        return true;
    }
}
