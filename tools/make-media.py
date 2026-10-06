from PIL import Image, ImageDraw, ImageFont
from pathlib import Path
import shutil
root=Path(__file__).resolve().parents[1]
frames=root/'media-frames'
out=root/'docs/media'
out.mkdir(exist_ok=True)
def font(n): return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)
def canvas(w,h,title,subtitle):
    im=Image.new('RGB',(w,h),'#11131b');d=ImageDraw.Draw(im)
    d.text((38,24),title,font=font(28),fill='#e7ebfa')
    d.text((38,70),subtitle,font=font(16),fill='#a9b3ce')
    return im
def paste(im,name,xy):
    p=Image.open(frames/name).convert('RGBA');im.paste(p,xy,p)
for lang in ['en','zh']:
    zh=lang=='zh'
    demo='真实 UI 渲染 · 示例数据，非账户实录' if zh else 'Production UI renderer · synthetic metrics, not an account recording'
    im=canvas(1060,760,'Codex Rail · 把用量放在工作发生的地方' if zh else 'Codex Rail · Usage where you work',demo)
    d=ImageDraw.Draw(im);d.rounded_rectangle((40,120,168,725),14,fill='#202330')
    paste(im,f'compact-{lang}.png',(60,120))
    paste(im,f'expanded-{lang}.png',(210,120))
    d.text((860,260),'轻量侧栏\n剩余额度\n模块布局\n平滑展开' if zh else 'A narrow rail\nRemaining quota\nModular gauges\nSmooth expansion',font=font(18),fill='#94b9ff',spacing=22)
    im.save(out/f'hero-{lang}.png')
    hover=[]
    for i in range(32):
        im=canvas(760,755,'悬停展开 · 向右延伸' if zh else 'Hover details · expand to the right',demo)
        paste(im,f'hover-{lang}-{i:02}.png',(60,120));hover.append(im)
    durations=[600]+[35]*14+[1500]+[35]*15+[800]
    hover[0].save(out/f'hover-{lang}.gif',save_all=True,append_images=hover[1:],duration=durations,loop=0,optimize=True)
    names=['电池','圆环','竖条','数值'] if zh else ['Battery','Ring','Bar','Number']
    im=canvas(920,420,'四种样式，自由混用' if zh else 'Four styles. Mix them your way.',demo)
    d=ImageDraw.Draw(im)
    for i,name in enumerate(names):
        x=75+i*220;d.rounded_rectangle((x-15,115,x+130,355),12,fill='#202330')
        paste(im,f'style-{lang}-{i}.png',(x+14,122));d.text((x,365),name,font=font(18),fill='#e7ebfa')
    im.save(out/f'modules-{lang}.png')
    themes=['午夜蓝','暖灰','高对比','浅色','荧光绿','紫罗兰','海洋青','珊瑚橙','樱花粉','石墨金','森林绿','冰川蓝'] if zh else ['Midnight','Warm gray','High contrast','Light','Neon green','Violet','Ocean','Coral','Sakura','Graphite gold','Forest','Glacier']
    pages=[]
    for page in range(3):
        im=canvas(920,620,'12 套配色 · 含额度提醒对比色' if zh else '12 palettes · paired quota alert colors',demo)
        d=ImageDraw.Draw(im)
        for j in range(4):
            i=page*4+j;x=75+j*220
            p=Image.open(frames/f'theme-{lang}-{i+1:02}.png').convert('RGBA');p.thumbnail((72,455))
            im.paste(p,(x+25,120),p);d.text((x-8,575),themes[i],font=font(16),fill='#e7ebfa')
        pages.append(im)
    pages[0].save(out/f'themes-{lang}.gif',save_all=True,append_images=pages[1:],duration=2200,loop=0,optimize=True)
    shutil.copyfile(frames/f'settings-{lang}-0.png',out/f'settings-{lang}.png')
    seq=[]
    for i in list(range(12))+list(range(10,-1,-1)):
        seq.append(Image.open(frames/f'follow-{lang}-{i:02}.png').convert('RGB'))
    seq[0].save(out/f'follow-{lang}.gif',save_all=True,append_images=seq[1:],duration=100,loop=0)
print('Created',len(list(out.iterdir())),'public media files')
