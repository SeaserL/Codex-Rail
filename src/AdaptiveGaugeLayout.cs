namespace CodexTokenOverlay;

internal sealed record GaugePage(UsageCardSettings Settings,int Page,int Pages);
internal static class AdaptiveGaugeLayout
{
    // A display-only projection: never changes saved positions, enabled metrics or styles.
    internal static GaugePage Create(UsageCardSettings source,int availableHeight,int railWidth,int requestedPage)
    {
        var height=Math.Max(80,availableHeight);var s=source.Clone();
        if(height<120)s.ShowLabels=false;
        s.RuntimeMinimumHeight=Math.Min(310,height);
        if(s.CompactWidth>railWidth-8){s.Gap=Math.Min(s.Gap,4);s.CellWidth=Math.Max(8,(railWidth-14-(s.Columns-1)*s.Gap)/s.Columns);}
        var rows=Enumerable.Range(0,s.Rows).Where(r=>s.Modules.Any(m=>m.Enabled&&m.Row==r)).ToArray();
        // Keep the chosen bar height unless it alone is taller than the usable viewport.
        s.CellHeight=Math.Min(s.CellHeight,Math.Max(36,height-40));
        var pages=new List<List<int>>();var current=new List<int>();var used=12;
        var fullHeight=12+rows.Sum(s.RowHeight)+Math.Max(0,rows.Length-1)*s.Gap;
        var budget=height-(fullHeight>height?24:0);
        foreach(var row in rows)
        {
            var needed=s.RowHeight(row)+(current.Count>0?s.Gap:0);
            if(current.Count>0&&used+needed>budget){pages.Add(current);current=new();used=12;needed=s.RowHeight(row);}
            current.Add(row);used+=needed;
        }
        if(current.Count>0)pages.Add(current);if(pages.Count==0)pages.Add([0]);
        var index=((requestedPage%pages.Count)+pages.Count)%pages.Count;var visible=pages[index];
        foreach(var m in s.Modules){if(!m.Enabled)continue;var row=visible.IndexOf(m.Row);m.Enabled=row>=0;if(m.Enabled)m.Row=row;}
        var oldAlign=s.Alignments;s.Alignments=new RowAlignment[UsageCardSettings.MaximumRows];for(var i=0;i<visible.Count;i++)s.Alignments[i]=oldAlign[visible[i]];
        s.Rows=visible.Count;s.RuntimeFooterHeight=pages.Count>1?24:0;
        return new(s,index,pages.Count);
    }
    internal static (int Top,int Bottom) RailViewport(int clientHeight,double scale)
    {
        // Reserve the upper navigation and bottom account/update buttons.
        var bottom=Math.Max(0,clientHeight-(int)Math.Round(100*scale));
        var top=Math.Min((int)Math.Round(250*scale),bottom);
        return(top,bottom);
    }
}
