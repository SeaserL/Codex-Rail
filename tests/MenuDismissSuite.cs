namespace CodexTokenOverlay;
internal static class MenuDismissSuite
{
    internal static bool Run(string[] args)
    {
        if (args.Length != 2 || args[0] != "--menu-dismiss") return false;
        using var menu = new ContextMenuStrip();
        menu.Items.Add("test");
        using var dismissal = new MenuDismissController(menu);
        ToolStripDropDownCloseReason? reason = null;
        menu.Closed += (_, e) => reason = e.CloseReason;
        void Open()
        {
            reason = null; menu.Show(new Point(-10000, -10000));
            if (!menu.Visible || !dismissal.Monitoring || !dismissal.HooksAttached)
                throw new Exception("menu lifecycle/native hooks did not start");
        }
        void CheckClosed(ToolStripDropDownCloseReason expected)
        {
            Application.DoEvents();
            if (menu.Visible || dismissal.Monitoring || dismissal.HooksAttached || reason != expected)
                throw new Exception("menu did not dismiss/release hooks: " + expected);
        }
        Open();
        dismissal.ObserveMouseDown(new(menu.Left + 2, menu.Top + 2));
        Application.DoEvents();
        if (!menu.Visible) throw new Exception("menu-internal click was dismissed");
        dismissal.ObserveMouseDown(new(menu.Right + 20, menu.Bottom + 20));
        CheckClosed(ToolStripDropDownCloseReason.AppClicked);
        Open(); dismissal.ObserveEscape(); CheckClosed(ToolStripDropDownCloseReason.Keyboard);
        Open(); dismissal.ObserveForeground(IntPtr.Zero, 0);
        dismissal.ObserveForeground(new IntPtr(-1), (uint)Environment.ProcessId);
        Application.DoEvents();
        if (!menu.Visible) throw new Exception("temporary/own foreground closed menu");
        dismissal.ObserveForeground(new IntPtr(-1), uint.MaxValue);
        CheckClosed(ToolStripDropDownCloseReason.AppFocusChange);
        Open(); dismissal.ObserveEscape(); menu.Close(); Open(); Application.DoEvents();
        if (!menu.Visible) throw new Exception("old dismissal closed newly reopened menu");
        menu.Close();
        if (dismissal.Monitoring || dismissal.HooksAttached) throw new Exception("normal close leaked hooks");
        Open(); dismissal.Dispose();
        if (dismissal.Monitoring || dismissal.HooksAttached) throw new Exception("dispose leaked hooks");
        menu.Close();
        Console.WriteLine("PASS: menu outside click, Escape, focus switch, inside click, reopen race and hook cleanup.");
        return true;
    }
}
