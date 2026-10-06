using System.Drawing.Drawing2D;
namespace CodexTokenOverlay;
internal static class MediaSuite
{
    internal static bool Run(string[] args)
    {
        if(args.Length!=2||args[0]!="--media")return false;
        var output=args[1];Directory.CreateDirectory(output);
        using var card=new UsageCardForm{Tokens=new("demo","",124050,114000,86000,10050,2020,48000,200000,DateTime.UtcNow),Quota=new(new(32,300,DateTimeOffset.Now.AddHours(3).ToUnixTimeSeconds()),new(22,10080,DateTimeOffset.Now.AddDays(4).ToUnixTimeSeconds()),DateTimeOffset.Now),Vitals=new("demo-model","medium","已完成",23.4,2900,18.5)};
        foreach(var language in new[]{UiLanguage.English,UiLanguage.Chinese})
        {
            UiText.Language=language;var suffix=language==UiLanguage.English?"en":"zh";
            card.Settings=new(){Language=language};card.Details=false;
            using(var image=card.Render(192))image.Save(Path.Combine(output,$"compact-{suffix}.png"));
            card.Details=true;using(var image=card.Render(192))image.Save(Path.Combine(output,$"expanded-{suffix}.png"));
            for(var frame=0;frame<32;frame++)
            {
                var t=frame<16?frame/15d:(31-frame)/15d;card.Expansion=t*t*(3-2*t);
                using var image=card.Render(192);image.Save(Path.Combine(output,$"hover-{suffix}-{frame:00}.png"));
            }
            for(var theme=1;theme<=12;theme++)
            {
                card.Settings=new(){Language=language};card.Settings.ApplyTheme(theme);card.Settings.Opacity=235;card.Details=false;
                using var image=card.Render(192);image.Save(Path.Combine(output,$"theme-{suffix}-{theme:00}.png"));
            }
            for(var style=0;style<4;style++)
            {
                card.Settings=new(){Language=language};foreach(var m in card.Settings.Modules)m.Enabled=false;
                var selected=card.Settings.Modules.First(m=>m.Metric==MetricKind.周剩余);selected.Enabled=true;selected.Row=0;selected.Style=(GaugeStyle)style;selected.RingValue=RingValuePosition.环内;
                card.Settings.Rows=1;card.Settings.RuntimeMinimumHeight=110;card.Details=false;
                using var image=card.Render(192);image.Save(Path.Combine(output,$"style-{suffix}-{style}.png"));
            }
            using var dialog=new UsageSettingsDialog(new(){Language=language},diagnostic:true){Opacity=0,StartPosition=FormStartPosition.Manual,Location=new(-10000,-10000)};
            dialog.Show();Application.DoEvents();
            foreach(var page in new[]{0,2})
            {
                dialog.DiagnosticPage(page);Application.DoEvents();using var image=new Bitmap(dialog.Width,dialog.Height);dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(output,$"settings-{suffix}-{page}.png"));
            }
            for(var frame=0;frame<12;frame++)
            {
                var p=frame/11d;card.Settings=new(){Language=language,HorizontalOffset=(int)Math.Round(-100+200*p)};card.Details=false;
                using var canvas=new Bitmap(720,480);using var g=Graphics.FromImage(canvas);g.Clear(Color.FromArgb(25,27,37));
                var host=new Rectangle(25+frame*10,20+frame*3,480,430-frame*5);
                using var panel=new SolidBrush(Color.FromArgb(36,39,52));g.FillRectangle(panel,host);using var border=new Pen(Color.FromArgb(80,87,110));g.DrawRectangle(border,host);
                using var image=card.Render(96);g.DrawImageUnscaled(image,host.Left+UsageCardContext.PlaceHorizontally(0,56,44,0),host.Bottom-image.Height-30);
                using var font=new Font("Segoe UI",11);g.DrawString("Illustrative host geometry / 示例窗口",font,Brushes.White,host.Left+70,host.Top+20);canvas.Save(Path.Combine(output,$"follow-{suffix}-{frame:00}.png"));
            }
            dialog.Hide();
        }
        UiText.Language=UiLanguage.System;Console.WriteLine("Media: production renderer + fixture data. No account/session access.");return true;
    }
}
