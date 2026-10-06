namespace CodexTokenOverlay;
internal sealed partial class UsageSettingsDialog
{
    private SplitContainer? _split;
    private FlowLayoutPanel? _actions;
    private Button? _languageButton;
    private bool _responsive;
    internal event Action? LanguageChanged;
    private int Dip(int value)=>(int)Math.Round(value*DeviceDpi/96d);
    private void InstallResponsive(SplitContainer split,FlowLayoutPanel actions)
    {
        _split=split;_actions=actions;
        var languageBar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=34,Padding=new Padding(10,2,0,0)};
        var language=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=155};language.Items.AddRange(["System / 系统","中文","English"]);language.SelectedIndex=(int)Result.Language;
        _languageButton=new Button{Text="中文 / English",AutoSize=true};
        language.SelectedIndexChanged+=(_,_)=>{if(language.SelectedIndex<0)return;Result.Language=(UiLanguage)language.SelectedIndex;ApplyLanguage();Changed();};
        _languageButton.Click+=(_,_)=>language.SelectedIndex=UiText.IsEnglish?1:2;
        languageBar.Controls.AddRange([new Label{Text="Language / 语言",AutoSize=true,Padding=new Padding(0,5,0,0)},language,_languageButton]);Controls.Add(languageBar);
        _modules.FormattingEnabled=true;_modules.Format+=UiText.Format;
        foreach(var combo in new[]{_style,_align,_ring,_themes,_font})if(combo!=null){combo.FormattingEnabled=true;combo.Format+=UiText.Format;}
        Resize+=(_,_)=>ResponsiveLayout();
        ApplyLanguage();ResponsiveLayout();
    }
    private void ApplyLanguage(){UiText.Language=Result.Language;UiText.Apply(this);var before=_sync;_sync=true;try{foreach(var c in new[]{_style,_align,_ring,_themes,_font})if(c is PageChoice choice)choice.RefreshLanguage();}finally{_sync=before;}RefreshModules(Current);_grid.Invalidate();LanguageChanged?.Invoke();}
    private void RefreshGridSize(){_grid.Height=Dip(Math.Max(210,Result.Rows*52));if(_grid.Parent is {} board)board.Height=_grid.Height+Dip(10);}
    private void ResponsiveLayout()
    {
        if(_responsive||_split==null)return;_responsive=true;SuspendLayout();
        try
        {
            _split.Panel2Collapsed=ClientSize.Width<Dip(840);
            if(!_split.Panel2Collapsed){_split.Panel2MinSize=Dip(170);_split.SplitterDistance=Math.Max(0,_split.Width-Dip(230));}
            foreach(TabPage tab in _tabs.TabPages)
            {
                if(tab.Controls[0] is not FlowLayoutPanel flow)continue;
                var width=Math.Max(Dip(420),flow.ClientSize.Width-flow.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-Dip(8));
                foreach(Control c in flow.Controls)
                {
                    if(c is FlowLayoutPanel||c is Label)c.Width=width;
                    if(c is FlowLayoutPanel row&&row.Controls.Count>=3&&row.Controls[0] is Label label&&row.Controls[1] is NumericUpDown&&row.Controls[2] is TrackBar slider)
                    {label.Width=Math.Min(Dip(210),Math.Max(Dip(150),width-Dip(250)));slider.Width=Math.Max(Dip(110),width-label.Width-row.Controls[1].Width-Dip(30));}
                    if(c is FlowLayoutPanel switches&&switches.Controls.Count==2&&switches.Controls[0] is Label title&&switches.Controls[1] is Toggle)title.Width=Dip(250);
                }
            }
            if(_grid.Parent is {} board){_modules.Width=Dip(185);_grid.Width=Math.Max(Dip(140),board.Width-_modules.Width-Dip(24));}
            RefreshGridSize();
            if(_actions!=null){foreach(Control c in _actions.Controls)if(c is Button b)b.AutoSize=true;}
        }
        finally{ResumeLayout();_responsive=false;}
    }
    internal void FitToWorkArea(Rectangle area)
    {
        var margin=Math.Min(Dip(10),Math.Min(area.Width,area.Height)/20);
        var maximum=new Size(Math.Max(1,area.Width-2*margin),Math.Max(1,area.Height-2*margin));
        MinimumSize=new(Math.Min(Dip(620),maximum.Width),Math.Min(Dip(300),maximum.Height));
        Size=new(Math.Min(Width,maximum.Width),Math.Min(Height,maximum.Height));
        Location=new(Math.Clamp(Left,area.Left+margin,area.Right-margin-Width),Math.Clamp(Top,area.Top+margin,area.Bottom-margin-Height));
        ResponsiveLayout();
    }
    protected override void OnShown(EventArgs e){base.OnShown(e);FitToWorkArea(Screen.FromControl(this).WorkingArea);RenderPreview();}
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);ResponsiveLayout();RenderPreview();
        if(IsHandleCreated)BeginInvoke(()=>{if(!IsDisposed)FitToWorkArea(Screen.FromRectangle(e.SuggestedRectangle).WorkingArea);});
    }
}
