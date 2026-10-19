"""Render the actual animation channels from the delivered GLBs."""
import math
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from animate import ROOT,CONFIG,read,pose,render_parts
from raster import render

def font(n):return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)

def sequence(doc,blob,ai,duration,steps):
    frames=[render_parts(doc,blob,pose(doc,blob,ai,t)) for t in np.linspace(0,duration,steps+1)]
    vv=np.concatenate([p['positions'] for f in frames for p in f]);bounds=[vv.min(0),vv.max(0)]
    dummy=dict(positions=[v.tolist() for v in bounds],normals=[[0,1,0]]*2,indices=[],uv=[[0,0]]*2,factor=[1,1,1,1],textured=False)
    return frames,dummy

def picture(parts,dummy,size):
    return render(parts+[dummy],None,*size,(-2.1,1.1,-5.4))

def main():
    board=Image.new('RGB',(1680,1540),'#ecece2');d=ImageDraw.Draw(board)
    d.text((45,30),'僵尸攻击 · 蓄力 / 击中 / 收势',font=font(33),fill='#293831')
    for col,(key,c) in enumerate(CONFIG.items()):
        doc,blob=read(ROOT/'models'/f'{key}.glb')
        frames,bounds=sequence(doc,blob,1,c['attack'],30)
        x=col*420;d.text((x+35,99),c['label'],font=font(25),fill='#293831')
        for row,t in enumerate([c['impact']*.64,c['impact'],c['impact']+.13*c['attack']]):
            y=150+row*450;fg=picture(render_parts(doc,blob,pose(doc,blob,1,t)),bounds,(400,400));board.paste(fg,(x+10,y),fg)
            d.text((x+35,y+400),f'{["蓄力","击中","收势"][row]}  {t:.2f}s',font=font(18),fill='#748075')
        for ai,name in enumerate(['walk','attack']):
            duration=c['walk' if ai==0 else 'attack'];steps=int(round(duration*24))
            frames,bounds=sequence(doc,blob,ai,duration,steps)
            images=[]
            for parts in frames[:-1] if ai==0 else frames:
                image=Image.new('RGB',(480,580),'#ecece2');di=ImageDraw.Draw(image)
                di.text((24,18),c['label']+' · '+('行走' if ai==0 else '攻击'),font=font(23),fill='#293831')
                fg=picture(parts,bounds,(456,490));image.paste(fg,(12,56),fg)
                di.text((25,550),f'{duration:.2f} 秒 / '+('原地循环' if ai==0 else '单次攻击 · 此处重复预览'),font=font(14),fill='#748075')
                images.append(image)
            delays=[max(10,round(duration/steps*1000/10)*10)]*len(images)
            if ai==1:delays[-1]=450
            images[0].save(ROOT/'previews'/f'{key}_{name}.gif',save_all=True,append_images=images[1:],duration=delays,loop=0,optimize=False,disposal=2)
        print('RENDERED',key,flush=True)
    board.save(ROOT/'previews/attack_poses.png')

if __name__=='__main__':main()
