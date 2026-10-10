namespace CodexTokenOverlay;
internal sealed partial class PageSlider {internal void DiagnosticWheel(MouseEventArgs e)=>OnMouseWheel(e);}
internal sealed partial class UsageSettingsDialog {private readonly bool _diagnostic;protected override bool ShowWithoutActivation=>_diagnostic;internal UsageSettingsDialog(UsageCardSettings settings,bool diagnostic):this(settings){_diagnostic=diagnostic;ShowInTaskbar=!diagnostic;}
    internal void SelectionRegression(){NumericSelectionRegression();var n=_numbers["展开耗时毫秒"];var row=n.Parent!;var start=n.Value;if(_selection.Wheel(n,120))throw new Exception("unselected wheel accepted");_selection.Toggle(row);if(!_selection.Wheel(n,120)||n.Value!=start+1)throw new Exception("selected wheel failed");_selection.Toggle(n);if(_selection.Wheel(n,120)||n.Value!=start+1)throw new Exception("second click failed");_selection.Toggle(row);_selection.Clear();if(_selection.Wheel(n,120))throw new Exception("clear failed");}
    private void NumericSelectionRegression()
    {
        var original = _rows.BackColor; _ = Handle; _ = _tabs.Handle; _tabs.SelectedIndex = 0;
        _rows.Value = 4;
        _selection.Toggle(_rows);
        _rows.Value = 5;
        _selection.Clear();
        if (_rows.BackColor != original || Result.Rows != 5) throw new Exception("row deselection did not restore background/layout");
        _selection.Toggle(_rows); _selection.Toggle(_reference);
        if (_rows.BackColor != original) throw new Exception("switch between numeric parameters failed");
        _selection.Toggle(_reference);
        _selection.Toggle(_rows);
        var message = Message.Create(_rows.Handle, 0x100, (IntPtr)27, IntPtr.Zero);
        if (!_selection.PreFilterMessage(ref message) || _rows.BackColor != original) throw new Exception("numeric Escape failed");
        _selection.Toggle(_rows); _tabs.SelectedIndex = 1;
        if (_rows.BackColor != original) throw new Exception("numeric page switch failed");
        _tabs.SelectedIndex = 0; _rows.Value = 4;
    }
    internal void RoutingRegression(){var n=_numbers["展开耗时毫秒"];_tabs.SelectedIndex=2;PerformLayout();Application.DoEvents();((ScrollableControl)n.Parent!.Parent!).ScrollControlIntoView(n.Parent);Application.DoEvents();var clicked=n.Controls[0];var msg=Message.Create(clicked.Handle,0x201,IntPtr.Zero,IntPtr.Zero);_selection.PreFilterMessage(ref msg);var value=n.Value;var point=n.PointToScreen(new Point(10,10));var packed=(point.X&65535)|((point.Y&65535)<<16);msg=Message.Create(clicked.Handle,0x20A,(IntPtr)(120<<16),(IntPtr)packed);if(!_selection.PreFilterMessage(ref msg)||n.Value!=value+1)throw new Exception($"child handle wheel routing failed: value={n.Value} start={value}, point={point}, feedback={_wheelHint.Text}, control={Control.FromChildHandle(clicked.Handle)?.GetType().Name}, visible={n.Visible}, bounds={n.Parent!.Bounds}");msg=Message.Create(clicked.Handle,0x201,IntPtr.Zero,IntPtr.Zero);_selection.PreFilterMessage(ref msg);msg=Message.Create(clicked.Handle,0x20A,(IntPtr)(120<<16),(IntPtr)packed);_selection.PreFilterMessage(ref msg);if(n.Value!=value+1)throw new Exception("unselected route changed value");_tabs.SelectedIndex=0;}
    internal void DiagnosticPage(int index){_tabs.SelectedIndex=index;}
    internal void DiagnosticPreview(bool enabled){_live.Checked=enabled;RenderPreview();}
    internal void RegressionExercise(){SelectionRegression();using(var slider=new PageSlider{Minimum=0,Maximum=100,Value=50}){var e=new HandledMouseEventArgs(MouseButtons.None,0,0,0,120);slider.DiagnosticWheel(e);if(slider.Value!=50||!e.Handled)throw new Exception("wheel changed unfocused slider");}var handle=_themes!.Handle;for(var i=0;i<20;i++){_columns.SelectedItem=i%3+1;_rows.Value=i%UsageCardSettings.MaximumRows+1;_style.SelectedItem=(GaugeStyle)(i%4);_themes.SelectedIndex=i%UsageCardSettings.ThemeNames.Length;}if(_themes.IsDisposed||_themes.Handle!=handle)throw new Exception("theme control recreated");_columns.SelectedItem=1;_rows.Value=4;_live.Checked=false;var updates=0;var restored=false;PreviewChanged+=s=>{if(s==null)restored=true;else{updates++;if(s.Validate()!=null)throw new Exception("invalid live preview");}};_live.Checked=true;RenderPreview();Result.RingDiameter=32;RenderPreview();_live.Checked=false;if(updates!=2||!restored)throw new Exception("live preview switching failed");RenderPreview();if(Result.Validate()!=null)throw new Exception("layout edit regression failed");}
}
