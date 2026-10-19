"""Four original lowpoly zombies matching the current block character proportions."""
import json, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from mesh_tools import Model, PALETTE, ROOT, box, bar, patch, profile, export, render_parts
from raster import render

PROJECT=ROOT.parents[1]

def torso(m,w,d,top,cloth,skin='skin',bottom=.26,hem=.56):
    m.group('body',(0,top-.30,0))
    profile(m,'Torso_base',[(-w*.31,bottom),(w*.31,bottom),(w*.495,top-.006),(-w*.495,top-.006)],d,'dark')
    profile(m,'Torn_shirt',[
        (-w*.5-.006,top),(-w*.39,hem+.04),(-w*.26,hem),(-w*.18,hem+.075),
        (-w*.08,hem+.035),(w*.03,hem+.10),(w*.14,hem+.03),(w*.30,hem+.06),
        (w*.39,hem),(w*.5+.006,top)
    ],d+.016,cloth)
    front=-d/2-.010
    patch(m,'Open_collar',[(-w*.14,top+.001),(-w*.065,top-.13),(w*.015,top-.105),(w*.14,top+.001)],front,skin)
    patch(m,'Shirt_tear',[(-w*.31,top-.32),(-w*.13,top-.36),(-w*.20,top-.405),(-w*.14,top-.435),(-w*.31,top-.39)],front,skin)

def feet(m,w=.20,depth=.37,x=.19,offsets=(-.025,.025),sneakers=False):
    for side,xx,z in [('Left',-x,offsets[0]),('Right',x,offsets[1])]:
        m.group('Shoe'+side,(xx,.13,z+.065))
        box(m,'Boot',(xx,.07,z),(w,.14,depth),'dark')
        if sneakers:box(m,'Sneaker_sole',(xx,.016,z-.004),(w+.012,.028,depth+.008),'bone')

def arm(m,side,shoulder,elbow,tip,width=.13,skin='skin',sleeve='teal',sleeve_end=.71,fore_sleeve=0,fist=1.10):
    shoulder,elbow,tip=[np.array(x,float) for x in [shoulder,elbow,tip]]
    m.group('arm'+side,shoulder,'body')
    axis=elbow-shoulder;start=shoulder+axis*.055;end=elbow-axis*.045
    bar(m,'Upper_arm',start,end,width,width,skin)
    if sleeve and sleeve_end>0:
        bar(m,'Sleeve',start,shoulder+axis*sleeve_end,width+.029,width+.029,sleeve)
    m.group('hand'+side,elbow,'arm'+side)
    axis=tip-elbow;start=elbow+axis*.05;wrist=tip-axis*.23
    bar(m,'Forearm',start,wrist,width,width,skin)
    if sleeve and fore_sleeve>0:
        bar(m,'Forearm_sleeve',start,elbow+axis*fore_sleeve,width+.024,width+.024,sleeve)
    bar(m,'Hand',wrist,tip,width*fist,width*fist,skin)

def face_features(m,center,size,skin='skin',bandaged=False,brute=False):
    x,y,z=center;w,h,d=size;front=z-d/2-.003
    # The head stays a cube; simple inset-like patches read well from gameplay distance.
    for side in [-1,1]:
        ex=x+side*w*.215;ey=y+h*.065+(0.012 if side==1 else 0)
        patch(m,'Eye_socket',[(ex-w*.125,ey-.031),(ex+w*.115,ey-.023),(ex+w*.115,ey+.035),(ex-w*.125,ey+.042)],front,'dark')
        box(m,'Clouded_eye',(ex+side*.004,ey+.002,front-.002),(.035 if not brute else .045,.029,.004),'bone')
    if brute:
        box(m,'Heavy_brow',(x,y+h*.20,front-.025),(w*.96,.072,.065),skin)
        box(m,'Square_jaw',(x,y-h*.29,z-d/2-.024),(w*.82,h*.29,.105),skin)
        front=z-d/2-.079
    mouth_y=y-h*.22
    patch(m,'Mouth',[(x-w*.19,mouth_y-.015),(x+w*.17,mouth_y-.029),(x+w*.20,mouth_y+.012),(x-w*.16,mouth_y+.027)],front-.004,'dark')
    for dx,dy in [(-.029,.003),(.025,-.003)]:
        box(m,'Tooth',(x+dx,mouth_y+dy,front-.006),(.023,.020,.003),'bone')
    if bandaged:
        patch(m,'Eye_bandage',[(x-w*.47,y+h*.31),(x-w*.20,y+h*.30),(x+w*.015,y-h*.01),(x-w*.23,y-h*.055)],z-d/2-.012,'bone')

def head(m,center=(0,1.41,-.03),size=(.34,.34,.34),skin='skin',hair=True,bandage=False,brute=False):
    x,y,z=center;w,h,d=size
    m.group('head',(x,y-h/2,z),'body')
    box(m,'Head',center,size,skin)
    face_features(m,center,size,skin,bandage,brute)
    if hair:
        profile(m,'Uneven_hairline',[
            (x-w*.52,y+h*.37),(x-w*.25,y+h*.37),(x-w*.25,y+h*.28),
            (x-w*.05,y+h*.28),(x-w*.05,y+h*.40),(x+w*.50,y+h*.40),
            (x+w*.50,y+h*.55),(x-w*.52,y+h*.55)
        ],d*.88,'dark',z+d*.10)
        box(m,'Hair_tuft',(x-w*.31,y+h*.59,z+d*.22),(w*.32,h*.12,d*.48),'dark')
    if bandage:
        box(m,'Head_wrap_front',(x,y+h*.30,z-d/2-.008),(w+.018,.065,.022),'bone')
        box(m,'Head_wrap_back',(x,y+h*.30,z+d/2+.008),(w+.018,.065,.022),'bone')
        for xx in [x-w/2-.008,x+w/2+.008]:box(m,'Head_wrap_side',(xx,y+h*.30,z),(.022,.065,d+.018),'bone')
        box(m,'Head_wrap_top',(x-w*.14,y+h/2+.009,z),(.09,.018,d+.018),'bone')
        box(m,'Bandage_knot',(x+w*.22,y+h*.31,z+d/2+.032),(.07,.075,.055),'bone')
    if brute:
        box(m,'Flat_hair_ridge',(x,y+h*.54,z+.025),(w*.40,.040,d*.85),'dark')

def hardhat(m,center,size):
    x,y,z=center;w,h,d=size;top=y+h/2
    m.group('hat',(x,top,z),'head')
    box(m,'Hardhat_brim',(x,top+.026,z-.018),(w+.14,.043,d+.12),'ochre')
    p=m.part('Hardhat_crown','ochre')
    bottom=[(x-w*.60,top+.040,z-d*.57),(x+w*.60,top+.040,z-d*.57),(x+w*.60,top+.040,z+d*.57),(x-w*.60,top+.040,z+d*.57)]
    upper=[(x-w*.48,top+.188,z-d*.45),(x+w*.48,top+.188,z-d*.45),(x+w*.48,top+.188,z+d*.45),(x-w*.48,top+.188,z+d*.45)]
    for i in range(4):
        j=(i+1)%4;pts=[bottom[i],bottom[j],upper[j],upper[i]]
        p.face(pts,np.mean(pts,axis=0)-np.array([x,top+.1,z]))
    p.face(upper,(0,1,0));p.face(bottom,(0,-1,0))
    box(m,'Hardhat_ridge',(x,top+.205,z),(.054,.032,d*.91),'ochre')
    box(m,'Hardhat_badge',(x,top+.10,z-d*.535-.002),(.084,.045,.012),'dark')

def finish(m,head_pitch=0,head_roll=0):
    if head_pitch or head_roll:m.turn('head',head_pitch,head_roll)
    m.markers={'GroundOrigin':(0,0,0)}
    return m

def shambler():
    m=Model('zombie_shambler','普通僵尸','破旧蓝衫 / 歪头前伸');m.english='SHAMBLER'
    torso(m,.71,.31,1.15,'teal')
    head(m,(0,1.40,-.040),(.35,.35,.34))
    arm(m,'Left',(-.445,1.065,-.005),(-.46,.84,-.26),(-.49,.69,-.64),sleeve='teal',sleeve_end=.64)
    arm(m,'Right',(.445,1.04,.005),(.49,.76,-.19),(.53,.51,-.52),sleeve='teal',sleeve_end=.84,fore_sleeve=.22)
    feet(m,offsets=(-.06,.045))
    return finish(m,head_pitch=5,head_roll=-8)

def worker():
    m=Model('zombie_worker','工人僵尸','安全帽 / 反光背心');m.english='WORKER'
    torso(m,.76,.34,1.17,'rust',hem=.55)
    m.use('body');z=-.34/2-.012
    patch(m,'Vest_opening',[(-.032,.61),(.032,.61),(.040,1.17),(-.040,1.17)],z,'dark')
    for side in [-1,1]:
        x=side*.225
        patch(m,'Vest_reflective_vertical',[(x-.023,.79),(x+.023,.79),(x+.023,1.14),(x-.023,1.14)],z-.002,'bone')
    patch(m,'Vest_reflective_front',[(-.285,.72),(.285,.72),(.285,.768),(-.285,.768)],z-.002,'bone')
    patch(m,'Vest_reflective_back',[(-.29,.79),(.29,.79),(.29,.84),(-.29,.84)],.34/2+.012,'bone',False)
    center=(0,1.41,-.015);size=(.35,.35,.35)
    head(m,center,size,hair=False);hardhat(m,center,size)
    arm(m,'Left',(-.47,1.08,0),(-.49,.81,-.21),(-.53,.56,-.55),width=.14,sleeve='dark',sleeve_end=.56)
    arm(m,'Right',(.47,1.09,0),(.48,.95,-.31),(.49,.86,-.65),width=.14,sleeve='dark',sleeve_end=.65)
    feet(m,w=.22,depth=.40,x=.21,offsets=(.015,-.035))
    return finish(m,head_pitch=2,head_roll=4)

def runner():
    m=Model('zombie_runner','狂奔僵尸','瘦长轮廓 / 绷带头部');m.english='RUNNER'
    torso(m,.58,.27,1.13,'wine','pale',bottom=.29,hem=.60)
    # Lean the torso's upper half forward while retaining the familiar tapered shape.
    for p in m.parts:
        v=np.asarray(p.vertices);v[:,2]-=(v[:,1]-.29)*.13;p.vertices=v.tolist()
        shear=np.eye(3);shear[2,1]=-.13;n=np.asarray(p.normals)@np.linalg.inv(shear);n/=np.linalg.norm(n,axis=1)[:,None];p.normals=n.tolist()
    head(m,(.015,1.36,-.22),(.31,.33,.32),'pale',hair=False,bandage=True)
    arm(m,'Left',(-.37,1.03,-.10),(-.47,.87,-.40),(-.47,.89,-.80),width=.105,skin='pale',sleeve='wine',sleeve_end=.34,fist=1.15)
    arm(m,'Right',(.37,1.00,-.10),(.43,.67,-.26),(.44,.50,-.64),width=.105,skin='pale',sleeve='wine',sleeve_end=.30,fist=1.15)
    feet(m,w=.18,depth=.37,x=.16,offsets=(-.17,.16),sneakers=True)
    return finish(m,head_pitch=12,head_roll=5)

def brute():
    m=Model('zombie_brute','壮硕僵尸','宽肩厚臂 / 大型拳头');m.english='BRUTE'
    torso(m,1.04,.47,1.36,'olive',bottom=.29,hem=.60)
    m.use('body');front=-.47/2-.012
    # A wide ragged vest opening exposes the same green skin as the head and arms.
    patch(m,'Open_vest_chest',[(-.20,1.36),(-.24,1.15),(-.12,.95),(.02,1.01),(.13,.91),(.22,1.16),(.20,1.36)],front-.002,'skin')
    patch(m,'Vest_lapel_left',[(-.24,1.36),(-.21,1.10),(-.14,1.22),(-.12,1.36)],front-.004,'dark')
    patch(m,'Vest_lapel_right',[(.12,1.36),(.15,1.14),(.23,1.08),(.24,1.36)],front-.004,'dark')
    head(m,(0,1.66,-.12),(.42,.40,.40),hair=False,brute=True)
    arm(m,'Left',(-.66,1.24,0),(-.72,.94,-.25),(-.70,.61,-.58),width=.23,sleeve='olive',sleeve_end=.38,fist=1.30)
    arm(m,'Right',(.66,1.27,0),(.73,1.00,-.28),(.76,.72,-.66),width=.23,sleeve='olive',sleeve_end=.44,fist=1.30)
    feet(m,w=.27,depth=.47,x=.27,offsets=(-.06,.055))
    return finish(m,head_pitch=9,head_roll=-3)

def font(size,chinese=False):
    return ImageFont.truetype('C:/Windows/Fonts/'+('msyh.ttc' if chinese else 'bahnschrift.ttf'),size)

def paste_character(board,m,rect,eye,shadow=False):
    x,y,w,h=rect
    if shadow:
        sh=Image.new('RGBA',(w,h));d=ImageDraw.Draw(sh);d.ellipse((w*.29,h*.85,w*.74,h*.94),fill=(39,49,40,33))
        sh=sh.filter(ImageFilter.GaussianBlur(14));board.paste(sh,(x,y),sh)
    img=render(render_parts(m),None,w*2,h*2,eye).resize((w,h),Image.Resampling.LANCZOS)
    board.paste(img,(x,y),img)

def previews(models):
    board=Image.new('RGB',(2000,1850),'#ebe9df');d=ImageDraw.Draw(board)
    d.text((75,48),'BLOCK / UNDEAD',font=font(68),fill='#2d3635')
    d.text((80,140),'ORIGINAL ZOMBIE CAST     /     COLLECTION 01',font=font(24),fill='#74806c')
    d.text((80,187),'方块角色 · 纯色材质 · 分段手臂 · 独立鞋子',font=font(25,True),fill='#74806c')
    for i,m in enumerate(models):
        x=60+i%2*960;y=260+i//2*748
        d.rounded_rectangle((x,y,x+920,y+712),radius=18,fill='#dfe2d5')
        d.text((x+31,y+24),f'0{i+1} / {m.english}',font=font(37),fill='#2d3635')
        d.text((x+32,y+80),m.subtitle,font=font(23,True),fill='#74806c')
        paste_character(board,m,(x+105,y+132,710,505),(-2.4,1.0,-5.2),True)
        d.text((x+31,y+660),f'{m.triangles:,} TRIANGLES / {len(set(p.material for p in m.parts))} COLORS',font=font(22),fill='#687465')
        for j,key in enumerate(dict.fromkeys(p.material for p in m.parts)):
            d.rounded_rectangle((x+701+j*35,y+659,x+726+j*35,y+684),radius=3,fill=PALETTE[key])
        sheet=Image.new('RGB',(1800,1330),'#ebe9df');sd=ImageDraw.Draw(sheet)
        sd.text((65,42),m.english,font=font(61),fill='#2d3635')
        sd.text((68,123),m.label+' / '+m.subtitle,font=font(26,True),fill='#74806c')
        sd.text((70,198),'01 / THREE-QUARTER',font=font(21),fill='#74806c')
        paste_character(sheet,m,(40,240,1000,960),(-2.4,1.0,-5.2),True)
        sd.text((1100,198),'02 / FRONT',font=font(21),fill='#74806c')
        paste_character(sheet,m,(1060,240,670,450),(0,.05,-5))
        sd.text((1100,740),'03 / GAMEPLAY VIEW',font=font(21),fill='#74806c')
        paste_character(sheet,m,(1060,780,670,450),(0,5,-3.5))
        sd.text((70,1270),f'{m.triangles:,} TRIANGLES   /   GLB   /   -Z FORWARD   /   +Y UP',font=font(21),fill='#74806c')
        sheet.save(ROOT/'previews'/f'{m.name}.png')
    d.text((80,1790),'ORIGINAL GEOMETRY / 4 SEPARATE GLB ASSETS / SOLID COLOR MATERIALS',font=font(22),fill='#74806c')
    board.save(ROOT/'previews/collection.png')

def main():
    models=[shambler(),worker(),runner(),brute()]
    records=[export(m) for m in models]
    data={'palette':PALETTE,'models':records}
    manifest={'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'source/mesh_data.json').write_text(json.dumps(data,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    template=(ROOT/'source/viewer_template.html').read_text(encoding='utf-8')
    (ROOT/'preview.html').write_text(template.replace('__ASSET_DATA__',json.dumps(data,ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
    previews(models)
    for m in models:
        if (PROJECT/'project.godot').is_file():
            destination=PROJECT/'assets/characters/zombies'
            destination.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(ROOT/'models'/f'{m.name}.glb',destination/f'{m.name}.glb')
        print(f'{m.name}: {m.triangles} triangles / {len(m.groups)} mesh groups / {len(set(p.material for p in m.parts))} colors')

if __name__=='__main__':main()
