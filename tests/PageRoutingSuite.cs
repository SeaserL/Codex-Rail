using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace CodexTokenOverlay;

internal static class PageRoutingSuite
{
    internal static bool Run(string[] args)
    {
        if (args.Length == 1 && args[0] == "--page-diagnostic")
        {
            var target = Process.GetProcessesByName("ChatGPT").First(p => p.MainWindowHandle != IntPtr.Zero && p.MainModule!.FileName.Contains("OpenAI.Codex_"));
            var root = SessionPathResolver.Resolve();
            using var page = new CodexPageThreadMonitor(Path.Combine(Path.GetDirectoryName(root)!, "state_5.sqlite"));
            page.SetTarget(target.MainWindowHandle); Thread.Sleep(1500);
            var state = page.GetStatus();
            using var tokens = new TokenLogMonitor(root) { RequirePreferredThread = true, PreferredThreadId = state.ThreadId };
            Console.WriteLine(JsonSerializer.Serialize(new {state.PageAvailable, state.Matches, hasPageId=state.ThreadId is not null, hasSessionData=tokens.Poll(true) is not null}));
            return true;
        }
        if (args.Length != 2 || args[0] != "--page-routing") return false;
        Directory.CreateDirectory(args[1]);var path = Path.Combine(args[1], "catalog.sqlite");
        if (File.Exists(path)) File.Delete(path);
        Create(path, "CREATE TABLE threads(id TEXT,title TEXT,name TEXT);"+
            "INSERT INTO threads VALUES('11111111-1111-1111-1111-111111111111','old','renamed'),"+
            "('22222222-2222-2222-2222-222222222222','duplicate',NULL),"+
            "('33333333-3333-3333-3333-333333333333','duplicate',''),"+
            "('44444444-4444-4444-4444-444444444444','a''b',NULL);");
        var before = File.ReadAllBytes(path);
        if(PageThreadCatalog.Resolve(path,"renamed").Single()!="11111111-1111-1111-1111-111111111111")throw new Exception("renamed page not matched");
        if(PageThreadCatalog.Resolve(path,"old").Length!=0)throw new Exception("obsolete title matched renamed page");
        if(PageThreadCatalog.Resolve(path,"duplicate").Length!=2)throw new Exception("duplicate page titles guessed an ID");
        if(PageThreadCatalog.Resolve(path,"a'b").Length!=1||PageThreadCatalog.Resolve(path,"' OR 1=1 --").Length!=0)throw new Exception("title not bound safely");
        if(PageThreadCatalog.Resolve(path,"").Length!=0||PageThreadCatalog.Resolve(path+"-missing","renamed").Length!=0)throw new Exception("unknown page guessed an ID");
        if(!before.SequenceEqual(File.ReadAllBytes(path)))throw new Exception("catalog changed during read");
        var old=Path.Combine(args[1],"legacy.sqlite");if(File.Exists(old))File.Delete(old);
        Create(old,"CREATE TABLE threads(id TEXT,title TEXT);INSERT INTO threads VALUES('11111111-1111-1111-1111-111111111111','legacy');");
        if(PageThreadCatalog.Resolve(old,"legacy").Length!=1)throw new Exception("legacy schema not handled");
        Console.WriteLine("PASS: page catalog, rename, duplicate titles, missing page, read-only binding and legacy schema.");return true;
    }
    private static void Create(string path,string sql)
    {
        if(Open(Encoding.UTF8.GetBytes(path+'\0'),out var db,6,IntPtr.Zero)!=0)throw new Exception("fixture database open failed");
        try{if(Exec(db,Encoding.UTF8.GetBytes(sql+'\0'),IntPtr.Zero,IntPtr.Zero,out var error)!=0){if(error!=IntPtr.Zero)Free(error);throw new Exception("fixture SQL failed");}}finally{Close(db);}
    }
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_open_v2",CallingConvention=CallingConvention.Cdecl)] private static extern int Open(byte[] path,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_exec",CallingConvention=CallingConvention.Cdecl)] private static extern int Exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr context,out IntPtr error);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_free",CallingConvention=CallingConvention.Cdecl)] private static extern void Free(IntPtr pointer);
    [DllImport("winsqlite3.dll",EntryPoint="sqlite3_close",CallingConvention=CallingConvention.Cdecl)] private static extern int Close(IntPtr db);
}
