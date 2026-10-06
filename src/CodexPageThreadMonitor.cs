using System.Runtime.InteropServices;

namespace CodexTokenOverlay;

internal sealed record PageThreadStatus(string? ThreadId, bool PageAvailable, int Matches, long Version);

// Read only the primary document title, never chat contents or the editor's text.
// UIA lives on a separate MTA thread so a slow provider cannot block the overlay.
internal sealed class CodexPageThreadMonitor : IDisposable
{
    private readonly object _sync = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly string _databasePath;
    private readonly Thread _worker;
    private IntPtr _target;
    private PageThreadStatus _status = new(null, false, 0, 0);
    private long _lastReadTick;
    private volatile bool _disposed;
    internal event Action? Changed;

    internal CodexPageThreadMonitor(string databasePath)
    {
        _databasePath = databasePath;
        _worker = new Thread(Run) { IsBackground = true, Name = "Codex Rail page identity" };
        _worker.SetApartmentState(ApartmentState.MTA);
        _worker.Start();
    }
    internal PageThreadStatus GetStatus()
    {
        lock (_sync)
        {
            if (_status.PageAvailable && Environment.TickCount64 - _lastReadTick > 2500)
                _status = new(null, false, 0, _status.Version + 1);
            return _status;
        }
    }
    internal void SetTarget(IntPtr target)
    {
        lock (_sync)
        {
            if (_disposed || target == _target) return;
            _target = target;
            _status = new(null, false, 0, _status.Version + 1);
        }
        Changed?.Invoke();
        _wake.Set();
    }
    private void Publish(IntPtr target, string? id, bool available, int matches)
    {
        lock (_sync)
        {
            if (_disposed || target != _target) return;
            _lastReadTick = Environment.TickCount64;
            if (_status.ThreadId == id && _status.PageAvailable == available && _status.Matches == matches) return;
            _status = new(id, available, matches, _status.Version + 1);
        }
        Changed?.Invoke();
    }
    private void Run()
    {
        IAutomation? automation = null;
        IElement? document = null;
        IntPtr boundWindow = IntPtr.Zero;
        string? lastTitle = null;
        string[] ids = [];
        var nextCatalog = DateTime.MinValue;
        try
        {
            while (!_disposed)
            {
                IntPtr target;
                lock (_sync) target = _target;
                if (target == IntPtr.Zero)
                {
                    Release(document); document = null; boundWindow = IntPtr.Zero; lastTitle = null;
                    _wake.WaitOne(); continue;
                }
                try
                {
                    automation ??= (IAutomation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("ff48dba4-60ef-4201-aa87-54103eef594e"))!)!;
                    if (document is null || boundWindow != target)
                    {
                        Release(document); document = null; lastTitle = null;
                        automation.ElementFromHandle(target, out var root);
                        IntPtr condition = IntPtr.Zero;
                        try
                        {
                            automation.CreatePropertyCondition(30003, 50030, out condition); // ControlType = Document
                            root.FindFirst(4, condition, out document); // Descendants of this host window only
                        }
                        finally { if (condition != IntPtr.Zero) Marshal.Release(condition); Release(root); }
                        boundWindow = target;
                    }
                    if (document is null) { Publish(target, null, false, 0); }
                    else
                    {
                        document.GetCurrentPropertyValue(30005, out var value); // Name / document title
                        var title = value as string ?? "";
                        if (title != lastTitle || DateTime.UtcNow >= nextCatalog)
                        {
                            ids = PageThreadCatalog.Resolve(_databasePath, title);
                            lastTitle = title; nextCatalog = DateTime.UtcNow.AddSeconds(5);
                        }
                        Publish(target, ids.Length == 1 ? ids[0] : null, true, ids.Length);
                    }
                }
                catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidOperationException or ArgumentException or DllNotFoundException)
                {
                    Release(document); document = null; boundWindow = IntPtr.Zero; lastTitle = null;
                    Publish(target, null, false, 0);
                }
                if (!_disposed) _wake.WaitOne(500);
            }
        }
        finally { Release(document); Release(automation); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
    public void Dispose()
    {
        _disposed = true; _wake.Set();
        // The worker is background-only; do not hang shutdown on an external UIA provider.
        if (_worker.Join(500)) _wake.Dispose();
    }

    // Unused slots preserve the native Windows SDK vtable ordering. No WPF dependency.
    [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAutomation
    {
        void Slot0(); void Slot1(); void Slot2();
        void ElementFromHandle(IntPtr handle, out IElement element);
        void Slot4(); void Slot5(); void Slot6(); void Slot7(); void Slot8(); void Slot9(); void Slot10(); void Slot11(); void Slot12(); void Slot13(); void Slot14(); void Slot15(); void Slot16(); void Slot17(); void Slot18(); void Slot19();
        void CreatePropertyCondition(int property, [MarshalAs(UnmanagedType.Struct)] object value, out IntPtr condition);
    }
    [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IElement
    {
        void Slot0(); void Slot1();
        void FindFirst(int scope, IntPtr condition, out IElement element);
        void Slot3(); void Slot4(); void Slot5(); void Slot6();
        void GetCurrentPropertyValue(int property, [MarshalAs(UnmanagedType.Struct)] out object value);
    }
}
