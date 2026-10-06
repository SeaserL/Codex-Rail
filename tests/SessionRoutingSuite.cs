using System.Text;
using System.Text.Json;
namespace CodexTokenOverlay;
internal static class SessionRoutingSuite
{
    internal static bool Run(string[] args)
    {
        if(args.Length==1&&args[0]=="--ipc-diagnostic")
        {
            using var live=new CodexIpcActiveThreadMonitor();Thread.Sleep(2000);
            var status=live.GetStatus();Console.WriteLine(JsonSerializer.Serialize(new{status.IsConnected,hasThread=status.ThreadId!=null,status.ActiveWindowCount}));return true;
        }
        if(args.Length!=2||args[0]!="--session-routing")return false;
        Directory.CreateDirectory(args[1]);var logs=Path.Combine(args[1],"fixtures");Directory.CreateDirectory(logs);
        const string a="11111111-1111-1111-1111-111111111111", b="22222222-2222-2222-2222-222222222222", c="33333333-3333-3333-3333-333333333333";
        var meta="{\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"source\":\"vscode\"}}\n";
        static string Usage(int n)=>"{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"total_tokens\":"+n+"},\"last_token_usage\":{\"total_tokens\":24},\"model_context_window\":100}}}\n";
        var pa=Path.Combine(logs,"rollout-"+a+".jsonl");var pb=Path.Combine(logs,"rollout-"+b+".jsonl");var pc=Path.Combine(logs,"rollout-"+c+".jsonl");
        File.WriteAllText(pa,meta+Usage(100));File.WriteAllText(pb,meta+Usage(900));File.WriteAllText(pc,meta);
        using var monitor=new TokenLogMonitor(logs){RequirePreferredThread=true};
        if(monitor.Poll(true)!=null)throw new Exception("unidentified route guessed newest log");
        monitor.PreferredThreadId=a;if(monitor.Poll()?.TotalTokens!=100)throw new Exception("A not selected");
        monitor.PreferredThreadId=b;if(monitor.Poll()?.TotalTokens!=900)throw new Exception("cached B not selected");
        monitor.PreferredThreadId=c;if(monitor.Poll()!=null)throw new Exception("empty C retained B");
        monitor.PreferredThreadId=a;if(monitor.Poll()?.TotalTokens!=100)throw new Exception("return to A failed");
        var stamp=File.GetLastWriteTimeUtc(pa);File.AppendAllText(pa,Usage(200));File.SetLastWriteTimeUtc(pa,stamp);
        if(monitor.Poll()?.TotalTokens!=200)throw new Exception("same timestamp append hidden by cache");
        monitor.PreferredThreadId=null;if(monitor.Poll()!=null)throw new Exception("disconnection retained A");
        using var route=new CodexIpcActiveThreadMonitor(connect:false);var changes=0;route.Changed+=()=>changes++;
        void Follow(string id,bool following)=>route.ProcessFrame(JsonSerializer.SerializeToUtf8Bytes(new{type="broadcast",method="thread-stream-following-changed",sourceClientId="window",@params=new{conversationId=id,hostId="local",following}}));
        Follow(a,true);var version=route.GetStatus().Version;Follow(b,true);
        if(route.GetStatus().ThreadId!=null||route.GetStatus().Version==version||changes!=2)throw new Exception("ambiguous subscriptions guessed last replay");
        Follow(a,false);if(route.GetStatus().ThreadId!=b)throw new Exception("late old unsubscribe cleared B");
        Follow(b,false);if(route.GetStatus().ThreadId!=null)throw new Exception("closed route retained B");
        Console.WriteLine("PASS: cached A/B switches, empty thread, disconnect, same-timestamp append, IPC invalidation and late unsubscribe.");return true;
    }
}
