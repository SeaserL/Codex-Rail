using System.Runtime.InteropServices;
using System.Text;

namespace CodexTokenOverlay;

// Windows' SQLite library avoids adding a database package or runtime to the overlay.
internal static class PageThreadCatalog
{
    internal static string[] Resolve(string databasePath, string title)
    {
        if (string.IsNullOrWhiteSpace(title) || !File.Exists(databasePath)) return [];
        IntPtr db = IntPtr.Zero, statement = IntPtr.Zero;
        try
        {
            if (Open(Utf8(databasePath), out db, 1, IntPtr.Zero) != 0) return [];
            BusyTimeout(db, 25);
            // UI names take precedence over generated titles after a manual rename.
            var sql = "SELECT id FROM threads WHERE COALESCE(NULLIF(name,''),title)=? LIMIT 2";
            if (Prepare(db, Utf8(sql), -1, out statement, IntPtr.Zero) != 0)
            {
                if (statement != IntPtr.Zero) { Finalize(statement); statement = IntPtr.Zero; }
                if (Prepare(db, Utf8("SELECT id FROM threads WHERE title=? LIMIT 2"), -1, out statement, IntPtr.Zero) != 0) return [];
            }
            var text = Encoding.UTF8.GetBytes(title);
            if (BindText(statement, 1, text, text.Length, new IntPtr(-1)) != 0) return [];
            var result = new List<string>();
            int step;
            while ((step = Step(statement)) == 100)
            {
                var id = Marshal.PtrToStringUTF8(ColumnText(statement, 0));
                if (!Guid.TryParse(id, out _)) return [];
                result.Add(id!);
            }
            return step == 101 ? result.ToArray() : [];
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException) { return []; }
        finally { if (statement != IntPtr.Zero) Finalize(statement); if (db != IntPtr.Zero) Close(db); }
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + '\0');
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_open_v2", CallingConvention=CallingConvention.Cdecl)] private static extern int Open(byte[] path, out IntPtr db, int flags, IntPtr vfs);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_busy_timeout", CallingConvention=CallingConvention.Cdecl)] private static extern int BusyTimeout(IntPtr db, int milliseconds);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_prepare_v2", CallingConvention=CallingConvention.Cdecl)] private static extern int Prepare(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_bind_text", CallingConvention=CallingConvention.Cdecl)] private static extern int BindText(IntPtr statement, int index, byte[] text, int length, IntPtr destructor);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_step", CallingConvention=CallingConvention.Cdecl)] private static extern int Step(IntPtr statement);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_column_text", CallingConvention=CallingConvention.Cdecl)] private static extern IntPtr ColumnText(IntPtr statement, int index);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_finalize", CallingConvention=CallingConvention.Cdecl)] private static extern int Finalize(IntPtr statement);
    [DllImport("winsqlite3.dll", EntryPoint="sqlite3_close", CallingConvention=CallingConvention.Cdecl)] private static extern int Close(IntPtr db);
}
