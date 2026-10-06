using System.Diagnostics;
using System.Text.Json;
namespace CodexTokenOverlay;
internal static class ScreenLayoutSuite
{
    internal static bool Run(string[] args)
    {
        if(args.Length!=2||args[0]!="--screens")return false;
        var folder=args[1];Directory.CreateDirectory(folder);
        var rows=new List<object>();var renderResults=new List<object>();
        using var card=new UsageCardForm{Tokens=new("fixture","",16000000,15000000,10000000,1000000,500000,48000,200000,DateTime.UtcNow),Quota=new(new(32,300,null),new(22,10080,null),DateTimeOffset.Now)};
        foreach(var percent in new[]{100,125,150,175,200,250})
        {
            var dpi=(uint)(96*percent/100);var scale=dpi/96d;card.Settings=new();card.Details=false;
            using var compact=card.Render(dpi);
            if(compact.Width!=(int)Math.Round(44*scale)||compact.Height!=(int)Math.Round(310*scale))throw new Exception("DPI dimensions mismatch");
            compact.Save(Path.Combine(folder,$"compact-{percent}.png"));
            card.Details=true;using var expanded=card.Render(dpi);
            var changed=0;for(var y=0;y<compact.Height;y++)for(var x=0;x<compact.Width-2;x++)if(compact.GetPixel(x,y)!=expanded.GetPixel(x,y))changed++;
            if(changed!=0)throw new Exception("compact content moved during expansion");
            expanded.Save(Path.Combine(folder,$"expanded-{percent}.png"));
            var watch=Stopwatch.StartNew();for(var i=0;i<60;i++){card.Expansion=i/59d;using var frame=card.Render(dpi);}watch.Stop();
            renderResults.Add(new{percent,dpi,compactWidth=compact.Width,compactHeight=compact.Height,expandedWidth=expanded.Width,compactPixelsChanged=changed,cachedAnimationMs=watch.Elapsed.TotalMilliseconds/60,expandedCacheMiB=expanded.Width*expanded.Height*4/1048576d});
        }
        foreach(var size in new[]{new Size(1920,1080),new Size(2560,1440),new Size(3840,2160)})
        foreach(var percent in new[]{100,125,150,175,200,250})
        foreach(var window in new[]{"maximized","75-percent"})
        foreach(var all in new[]{false,true})
        {
            var scale=percent/100d;var logicalHeight=(size.Height-40*scale)/scale*(window=="maximized"?1:.75);
            var s=new UsageCardSettings();s.AvailableHeightDip=Math.Max(48,(int)logicalHeight-420);
            if(all){s.Rows=16;foreach(var m in s.Modules)m.Enabled=true;s.Reflow();}
            var h=(int)Math.Round(s.CompactHeight*scale);var hostHeight=(int)Math.Round(logicalHeight*scale);var width=(int)Math.Round(size.Width*(window=="maximized"?1:.75));
            var shown=width>=(int)Math.Round((s.CompactWidth+80)*scale)&&hostHeight>=h+120*scale;
            var top=Math.Clamp(hostHeight-h-(int)Math.Round(s.BottomDip*scale),0,Math.Max(0,hostHeight-h));
            var rail=(int)Math.Round(56*scale);var compact=(int)Math.Round(s.CompactWidth*scale);var x=UsageCardContext.PlaceHorizontally(0,rail,compact,0);
            var centerError=Math.Abs(x+compact/2d-rail/2d);if(centerError>.5)throw new Exception("rail center mismatch");
            foreach(var origin in new[]{-3840,0,3840})if(UsageCardContext.PlaceHorizontally(origin,rail,compact,0)-origin!=x)throw new Exception("negative monitor origin mismatch");
            rows.Add(new{resolution=$"{size.Width}x{size.Height}",percent,window,layout=all?"all-15-metrics":"default-4",heightDip=s.CompactHeight,hostLogicalHeight=logicalHeight,shown,topDip=top/scale,estimatedNavigationOverlap=shown&&top/scale<250,centerErrorPx=centerError,settingsDefaultFits=900*scale<=size.Width&&740*scale<=size.Height-40*scale,settingsMinimumFits=850*scale<=size.Width&&660*scale<=size.Height-40*scale});
            if(!all&&window=="maximized"&&((size.Width==1920&&percent==125)||(size.Width==3840&&percent==200)))
            {
                card.Settings=s;card.Details=false;using var render=card.Render((uint)(96*scale));using var screen=new Bitmap(size.Width,size.Height);using var g=Graphics.FromImage(screen);
                g.Clear(Color.FromArgb(25,27,37));using var railBrush=new SolidBrush(Color.FromArgb(33,36,48));g.FillRectangle(railBrush,0,0,rail,hostHeight);
                using var pen=new Pen(Color.FromArgb(90,110,140));g.DrawLine(pen,rail,0,rail,hostHeight);
                using var f=new Font("Segoe UI",12*(float)scale);g.DrawString($"SIMULATION: {size.Width}x{size.Height}, {percent}% DPI. Fixture data; not a physical monitor capture.",f,Brushes.White,rail+20,20);
                g.DrawImageUnscaled(render,x,top);screen.Save(Path.Combine(folder,$"viewport-{size.Width}-{percent}.png"));
            }
        }
        var dialogResults=new List<object>();
        foreach(var percent in new[]{100,125,150,175,200,250})
        using(var dialog=new UsageSettingsDialog(new(),diagnostic:true))
        {
            dialog.Opacity=0;dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new(-10000,-10000);dialog.Show();Application.DoEvents();
            var baseline=dialog.Size;dialog.SimulateDpi(96*percent/100);Application.DoEvents();
            dialogResults.Add(new{percent,baselineWidth=baseline.Width,baselineHeight=baseline.Height,dialog.Width,dialog.Height,minimumWidth=dialog.MinimumSize.Width,minimumHeight=dialog.MinimumSize.Height,dialog.DeviceDpi,autoScaleMode=dialog.AutoScaleMode.ToString(),dialog.AutoScaleDimensions});
            using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(folder,$"settings-synthetic-dpi-{percent}.png"));dialog.Hide();
        }
        File.WriteAllText(Path.Combine(folder,"settings-synthetic-dpi.json"),JsonSerializer.Serialize(dialogResults,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(folder,"screen-matrix.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(folder,"render-dpi.json"),JsonSerializer.Serialize(renderResults,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"PASS: {rows.Count} geometry cases, {renderResults.Count} production renderer DPI levels; navigation boundary is an estimate (250 DIP), not UI detection.");return true;
    }
}
internal sealed partial class UsageSettingsDialog
{
    internal void SimulateDpi(int dpi)
    {
        if(dpi==DeviceDpi)return;
        var factor=dpi/(double)DeviceDpi;
        var rect=new[]{Left,Top,Left+(int)Math.Round(Width*factor),Top+(int)Math.Round(Height*factor)};
        var pointer=System.Runtime.InteropServices.Marshal.AllocHGlobal(16);
        try{System.Runtime.InteropServices.Marshal.Copy(rect,0,pointer,4);var message=Message.Create(Handle,0x2E0,(IntPtr)((dpi<<16)|dpi),pointer);WndProc(ref message);}
        finally{System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);}
    }
}
