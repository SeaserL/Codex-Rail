using System.Diagnostics;
namespace CodexTokenOverlay;
internal static class DataTransitionSuite
{
    internal static bool Run(string[] args)
    {
        if(args.Length!=2||args[0]!="--data-transition")return false;
        byte[] from=[0,0,0,0],to=[80,40,20,100],mixed=new byte[4];
        UsageCardForm.MixPixels(from,to,mixed,.5);
        if(!mixed.SequenceEqual(new byte[]{40,20,10,50}))throw new Exception("premultiplied alpha changed incorrectly");
        UsageCardForm.MixPixels(to,to,mixed,.4);
        if(!mixed.SequenceEqual(to))throw new Exception("unchanged pixels dimmed");
        using var form=new UsageCardForm {Tokens=Snapshot(10),Location=new(-10000,-10000)};
        form.Rebuild(96);form.Present(new(-10000,-10000),255);form.Show();
        var size=form.Size;var allocations=form.SurfaceAllocations;
        for(var i=0;i<12;i++)
        {
            form.Tokens=Snapshot(i%2==0?999000:12);form.Rebuild(96,true);form.Present(new(-10000,-10000),255);Pump(20);
            if(form.Size!=size||form.SurfaceAllocations!=allocations)throw new Exception("data update resized/reallocated window");
        }
        Pump(220);
        if(form.DataTransitionActive)throw new Exception("animation did not stop");
        var copies=form.SurfaceCopies;Pump(100);
        if(form.SurfaceCopies!=copies)throw new Exception("idle animation kept repainting");
        form.Tokens=Snapshot(900000);form.Rebuild(96,true);form.Hide();
        if(form.DataTransitionActive)throw new Exception("hidden animation kept running");
        form.Details=true;form.SessionSource="source A";using var a=form.Render(96);
        form.SessionSource="source B";using var b=form.Render(96);
        var changed=false;
        for(var y=0;y<a.Height&&!changed;y++)for(var x=0;x<a.Width;x++)if(a.GetPixel(x,y)!=b.GetPixel(x,y)){changed=true;break;}
        if(!changed)throw new Exception("session source missing from render cache signature");
        Console.WriteLine("PASS: alpha-correct blend, rapid retarget, stable geometry/DIB, idle/hide shutdown and source cache.");return true;
    }
    private static TokenSnapshot Snapshot(long n)=>new("fixture","",n,n,0,0,0,n,1000000,DateTime.UtcNow);
    private static void Pump(int milliseconds){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<milliseconds){Application.DoEvents();Thread.Sleep(5);}}
}
