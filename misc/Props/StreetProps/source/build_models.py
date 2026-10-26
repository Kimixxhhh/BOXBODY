"""Original low-poly street props. Run with Python 3, numpy and Pillow.
Metres, Y up, front +Z; ground-centred origin. No textures or external models.
"""
from pathlib import Path
import json
import math
import struct
import zipfile
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[2]
PALETTE = {'green':'#667B65', 'darkgreen':'#3D5148', 'charcoal':'#303A3C',
           'steel':'#9BAAA9', 'ochre':'#D4AD61', 'light':'#FFE8AF'}
RGB = {k:np.array([int(v[i:i+2],16)/255 for i in (1,3,5)]) for k,v in PALETTE.items()}

class Part:
    def __init__(self,name,material,group='Body'):
        self.name,self.material,self.group=name,material,group
        self.vertices=[]; self.normals=[]; self.indices=[]
    def face(self,points):
        p=np.array(points,float); n=np.cross(p[1]-p[0],p[2]-p[0]); length=np.linalg.norm(n)
        if length<1e-10: raise ValueError('Degenerate face: '+self.name)
        n/=length; start=len(self.vertices)
        self.vertices.extend(p.tolist()); self.normals.extend([n.tolist()]*len(p))
        for i in range(1,len(p)-1): self.indices.extend([start,start+i,start+i+1])

class Model:
    def __init__(self,name,label,subtitle):
        self.name,self.label,self.subtitle=name,label,subtitle
        self.parts=[]; self.group='Body'; self.pivots={'Body':[0,0,0]}; self.markers={}
    def part(self,name,material):
        p=Part(name,material,self.group); self.parts.append(p); return p
    @property
    def triangle_count(self): return sum(len(p.indices)//3 for p in self.parts)

def box(m,name,center,size,mat):
    x,y,z=center; a,b,c=np.array(size)/2
    pts=[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
         (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
    p=m.part(name,mat)
    for f in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]: p.face([pts[i] for i in f])
    return p

def tube(m,name,a,b,r1,mat,r2=None,sides=8,cap=True):
    a,b=np.array(a,float),np.array(b,float); w=b-a; w/=np.linalg.norm(w)
    helper=[0,1,0] if abs(w[1])<.9 else [1,0,0]
    u=np.cross(helper,w); u/=np.linalg.norm(u); v=np.cross(w,u)
    r2=r1 if r2 is None else r2
    rings=[[c+r*(math.cos(i*2*math.pi/sides)*u+math.sin(i*2*math.pi/sides)*v) for i in range(sides)] for c,r in [(a,r1),(b,r2)]]
    p=m.part(name,mat)
    for i in range(sides):
        j=(i+1)%sides; p.face([rings[0][i],rings[0][j],rings[1][j],rings[1][i]])
    if cap: p.face(rings[0][::-1]); p.face(rings[1])
    return p

def chamfer(m,name,rings,mat,cx=0,cz=0,bevel=.05,cap=True):
    # Rings are height, width, depth. Clockwise viewed from above: +Y caps reverse.
    p=m.part(name,mat); verts=[]
    for y,w,d in rings:
        a,b=w/2,d/2; c=min(bevel,a*.4,b*.4)
        verts.append([(cx+x,y,cz+z) for x,z in [(-a+c,-b),(a-c,-b),(a,-b+c),(a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c)]])
    for lo,hi in zip(verts,verts[1:]):
        for i in range(8): j=(i+1)%8; p.face([lo[i],hi[i],hi[j],lo[j]])
    if cap: p.face(verts[0]); p.face(verts[-1][::-1])
    return p

def trash_bin():
    m=Model('trash_bin','垃圾桶','带轮桶身 / 独立翻盖')
    chamfer(m,'Tapered_bin',[(.14,.47,.48),(.22,.49,.50),(.94,.60,.61)],'green',bevel=.055,cap=False)
    inside=chamfer(m,'Bin_inside',[(.21,.42,.43),(.97,.55,.56)],'darkgreen',cap=False)
    inside.normals=(-np.array(inside.normals)).tolist()
    inside.indices=np.array(inside.indices).reshape(-1,3)[:,::-1].flatten().tolist()
    chamfer(m,'Inside_floor',[(.19,.42,.43),(.21,.42,.43)],'darkgreen')
    chamfer(m,'Foot',[(.12,.46,.47),(.19,.48,.49)],'darkgreen')
    chamfer(m,'Upper_rim',[(.91,.63,.64),(.97,.63,.64),(.97,.55,.56)],'darkgreen',cap=False)
    for x in [-.20,.20]:
        box(m,'Front_rib',(x,.56,.278),(.026,.59,.030),'darkgreen')
    box(m,'Front_label', (0,.73,.309),(.22,.15,.013),'ochre')
    box(m,'Label_bin', (0,.725,.319),(.068,.068,.009),'darkgreen')
    box(m,'Label_lid', (0,.77,.319),(.089,.012,.009),'darkgreen')
    box(m,'Label_handle',(0,.781,.319),(.034,.009,.009),'darkgreen')
    for x in [-.018,.018]: box(m,'Label_slit',(x,.725,.325),(.007,.045,.004),'ochre')
    tube(m,'Axle',(-.32,.13,-.20),(.32,.13,-.20),.025,'steel')
    for side,x in [('Left',-.305),('Right',.305)]:
        m.group='Wheel_'+side; m.pivots[m.group]=[x,.13,-.20]
        tube(m,'Rubber_tire',(x-.045,.13,-.20),(x+.045,.13,-.20),.13,'charcoal',sides=12)
        for dx in [-.048,.048]: tube(m,'Wheel_hub',(x+dx-.004,.13,-.20),(x+dx+.004,.13,-.20),.062,'steel',sides=8)
    m.group='Body'
    for x in [-.235,.235]:
        tube(m,'Handle_mount',(x,.87,-.29),(x,.89,-.39),.022,'charcoal')
    tube(m,'Rear_handle',(-.255,.89,-.39),(.255,.89,-.39),.024,'charcoal')
    tube(m,'Hinge',(-.27,.985,-.30),(.27,.985,-.30),.025,'steel')
    m.group='Lid'; m.pivots['Lid']=[0,.985,-.30]
    chamfer(m,'Lid',[(.97,.66,.68),(1.015,.66,.68),(1.06,.58,.60)],'darkgreen')
    for x in [-.19,.19]: box(m,'Lid_ridge',(x,1.065,0),(.026,.018,.43),'green')
    box(m,'Front_grip',(0,1.005,.365),(.23,.045,.075),'charcoal')
    return m

def chain_link_fence():
    m=Model('chain_link_fence','铁丝网','3 m 模块 / 实体菱形网格')
    for x in [-1.5,1.5]:
        box(m,'Foot_plate',(x,.025,0),(.24,.05,.26),'charcoal')
        tube(m,'Post',(x,.05,0),(x,2.25,0),.055,'darkgreen')
        tube(m,'Post_cap',(x,2.24,0),(x,2.30,0),.070,'steel',r2=.025)
        for y in [.20,1.06,2.12]:
            tube(m,'Post_clamp',(x,y-.018,0),(x,y+.018,0),.064,'steel')
        for z in [-.085,.085]: tube(m,'Anchor_bolt',(x,.05,z),(x,.071,z),.022,'steel',sides=6)
    for y in [.18,2.13]: tube(m,'Horizontal_rail',(-1.5,y,0),(1.5,y,0),.032,'darkgreen')
    # Each diagonal is a genuine closed four-sided wire; no alpha cards.
    xmin,xmax=-1.46,1.46; ymin,ymax=.20,2.10
    for slope in [-1,1]:
        for k in range(-14,15):
            intercept=k*.28
            points=[]
            for x in [xmin,xmax]:
                y=slope*x+intercept
                if ymin-1e-8<=y<=ymax+1e-8: points.append((x,y,.012*slope))
            for y in [ymin,ymax]:
                x=(y-intercept)/slope
                if xmin-1e-8<=x<=xmax+1e-8: points.append((x,y,.012*slope))
            if len(points)==2 and np.linalg.norm(np.array(points[0])-points[1])>.001:
                tube(m,'Diamond_wire',points[0],points[1],.009,'steel',sides=4)
    for x in [-1.44,1.44]: tube(m,'Tension_bar',(x,.2,0),(x,2.1,0),.015,'steel',sides=4)
    box(m,'Safety_tab',(-1.17,1.68,.035),(.20,.12,.025),'ochre')
    m.markers={'Snap_Left':[-1.5,0,0],'Snap_Right':[1.5,0,0]}
    return m

def street_lamp():
    m=Model('street_lamp','路灯','4 m 弯臂 / 暖色灯罩')
    chamfer(m,'Concrete_plinth',[(0,.40,.40),(.09,.40,.40),(.16,.32,.32)],'steel')
    box(m,'Base_plate',(0,.18,0),(.30,.04,.30),'charcoal')
    for x in [-.10,.10]:
        for z in [-.10,.10]: tube(m,'Bolt',(x,.20,z),(x,.23,z),.023,'ochre',sides=6)
    tube(m,'Pedestal',(0,.20,0),(0,.70,0),.115,'charcoal',r2=.083)
    tube(m,'Tapered_pole',(0,.70,0),(0,3.52,0),.075,'darkgreen',r2=.049)
    for y in [.72,.84]: tube(m,'Reflector_band',(0,y,0),(0,y+.055,0),.076,'ochre')
    box(m,'Service_panel',(0,.47,.110),(.085,.19,.018),'darkgreen')
    tube(m,'Panel_screw',(0,.53,.122),(0,.53,.128),.009,'steel',sides=6)
    points=[(0,3.50,0),(0,3.77,0),(0,3.98,.20),(0,4.04,.48),(0,4.04,.94)]
    for i,(a,b) in enumerate(zip(points,points[1:])): tube(m,'Bent_arm_'+str(i),a,b,.049,'darkgreen')
    tube(m,'Head_collar',(0,4.04,.82),(0,4.04,1.00),.067,'charcoal')
    chamfer(m,'Lamp_housing',[(3.89,.30,.65),(4.005,.34,.72),(4.075,.24,.60)],'charcoal',cz=1.13,bevel=.065)
    chamfer(m,'Warm_diffuser',[(3.868,.245,.53),(3.892,.27,.57)],'light',cz=1.13,bevel=.045)
    for z in [.96,1.14,1.32]: box(m,'Lens_divider',(0,3.866,z),(.244,.008,.012),'steel')
    m.markers={'LightSocket':[0,3.84,1.13]}
    return m

def export(m):
    data=bytearray(); views=[]; accessors=[]; meshes=[]; nodes=[]
    used=list(dict.fromkeys(p.material for p in m.parts))
    mats=[]
    for key in used:
        c=RGB[key]; linear=np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
        material={'name':key,'pbrMetallicRoughness':{'baseColorFactor':[*linear.tolist(),1], 'metallicFactor':.28 if key=='steel' else 0, 'roughnessFactor':.8}}
        if key=='light': material['emissiveFactor']=(linear*.7).tolist()
        mats.append(material)
    def array(a,component,kind,target):
        while len(data)%4: data.append(0)
        start=len(data); a=np.asarray(a,dtype='<f4' if component==5126 else '<u2'); data.extend(a.tobytes())
        views.append({'buffer':0,'byteOffset':start,'byteLength':a.nbytes,'target':target})
        acc={'bufferView':len(views)-1,'componentType':component,'count':len(a),'type':kind}
        if kind=='VEC3': acc.update(min=a.min(0).tolist(),max=a.max(0).tolist())
        accessors.append(acc); return len(accessors)-1
    for group,pivot in m.pivots.items():
        primitives=[]
        for key in used:
            parts=[p for p in m.parts if p.group==group and p.material==key]
            if not parts: continue
            positions=[]; normals=[]; indices=[]
            for p in parts:
                start=len(positions); positions.extend((np.array(p.vertices)-pivot).tolist()); normals.extend(p.normals); indices.extend(i+start for i in p.indices)
            assert len(positions)<65536
            primitives.append({'attributes':{'POSITION':array(positions,5126,'VEC3',34962),'NORMAL':array(normals,5126,'VEC3',34962)},'indices':array(indices,5123,'SCALAR',34963),'material':used.index(key)})
        meshes.append({'name':group,'primitives':primitives}); nodes.append({'name':group,'mesh':len(meshes)-1,'translation':pivot})
    for name,pos in m.markers.items(): nodes.append({'name':name,'translation':pos})
    root=len(nodes); nodes.append({'name':m.name,'children':list(range(root))})
    allv=np.concatenate([p.vertices for p in m.parts]); dimensions=allv.max(0)-allv.min(0)
    doc={'asset':{'version':'2.0','generator':'Field / Street Props'},'scene':0,'scenes':[{'nodes':[root]}], 'nodes':nodes,'meshes':meshes,'materials':mats,'buffers':[{'byteLength':len(data)}], 'bufferViews':views,'accessors':accessors,'extras':{'units':'metres','up':'+Y','front':'+Z','pivot':'ground centre','triangles':m.triangle_count}}
    js=json.dumps(doc,separators=(',',':')).encode(); js+=b' '*((-len(js))%4); data+=b'\0'*((-len(data))%4)
    payload=struct.pack('<4sII',b'glTF',2,28+len(js)+len(data))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(data),b'BIN\0')+data
    (ROOT/'models'/f'{m.name}.glb').write_bytes(payload)
    destination=PROJECT/'assets/environmentObjects'
    if destination.is_dir(): (destination/f'{m.name}.glb').write_bytes(payload)
    return {'id':m.name,'name':m.label,'subtitle':m.subtitle,'triangles':m.triangle_count,'materials':len(used),'dimensions':np.round(dimensions,3).tolist(),'file':f'models/{m.name}.glb','partsData':[{'material':p.material,'positions':p.vertices,'normals':p.normals,'indices':p.indices} for p in m.parts]}

def main():
    for folder in ['models','previews']: (ROOT/folder).mkdir(parents=True,exist_ok=True)
    models=[trash_bin(),chain_link_fence(),street_lamp()]
    records=[export(m) for m in models]
    manifest={'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]}
    (ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    template=(ROOT/'source/viewer_template.html').read_text(encoding='utf-8')
    (ROOT/'preview.html').write_text(template.replace('__ASSET_DATA__',json.dumps({'palette':PALETTE,'models':records},ensure_ascii=False,separators=(',',':'))),encoding='utf-8')
    make_previews(models)
    with zipfile.ZipFile(ROOT/'street_props.zip','w',zipfile.ZIP_DEFLATED) as archive:
        for folder in ['models','previews','source']:
            for path in sorted((ROOT/folder).glob('*')):
                if path.is_file(): archive.write(path,path.relative_to(ROOT))
        for name in ['preview.html','manifest.json','README.txt']: archive.write(ROOT/name,name)
    print(json.dumps(manifest,ensure_ascii=False,indent=2))

# Renderer and sheet composition are below, keeping generation entirely offline.
def render(m,width,height,eye=(1.2,.9,3.7),side=False):
    """Orthographic flat-shaded rasterization of the exact exported triangles."""
    eye=np.array(eye,float); forward=eye/np.linalg.norm(eye)
    right=np.cross([0,1,0],forward); right/=np.linalg.norm(right); up=np.cross(forward,right)
    basis=np.array([right,up,forward])
    allv=np.concatenate([p.vertices for p in m.parts]); projected=allv@basis.T
    low,high=projected.min(0),projected.max(0); center=(low+high)/2
    scale=min(width*.89/(high[0]-low[0]),height*.77/(high[1]-low[1]))
    buffer=np.zeros((height,width,4),np.uint8); zbuffer=np.full((height,width),-np.inf)
    light=np.array([-.35,.8,1.2]); light/=np.linalg.norm(light)
    for part in m.parts:
        verts=np.array(part.vertices)@basis.T
        xy=(verts[:,:2]-center[:2])*[scale,-scale]+[width/2,height/2]
        for k in range(0,len(part.indices),3):
            ids=part.indices[k:k+3]; pts=xy[ids]; zs=verts[ids,2]
            normal=np.array(part.normals[ids[0]])
            if normal@forward<=0: continue
            xmin=max(0,int(math.floor(pts[:,0].min()))); xmax=min(width-1,int(math.ceil(pts[:,0].max())))
            ymin=max(0,int(math.floor(pts[:,1].min()))); ymax=min(height-1,int(math.ceil(pts[:,1].max())))
            if xmin>xmax or ymin>ymax: continue
            xx,yy=np.meshgrid(np.arange(xmin,xmax+1)+.5,np.arange(ymin,ymax+1)+.5)
            a,b,c=pts; den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(den)<1e-8: continue
            u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
            v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den; w=1-u-v
            zz=u*zs[0]+v*zs[1]+w*zs[2]; old=zbuffer[ymin:ymax+1,xmin:xmax+1]
            mask=(u>=-1e-7)&(v>=-1e-7)&(w>=-1e-7)&(zz>old)
            # Palette remains five base materials; only the lighting changes face brightness.
            shade=.68+.39*max(0,float(normal@light))+.06*max(0,float(normal[1]))
            color=np.clip(RGB[part.material]*shade*255,0,255).astype(np.uint8)
            buffer[ymin:ymax+1,xmin:xmax+1][mask]=[*color,255]; old[mask]=zz[mask]
    return Image.fromarray(buffer)

def font(size,bold=False,chinese=False):
    base=Path('C:/Windows/Fonts')
    filename='msyhbd.ttc' if chinese and bold else 'msyh.ttc' if chinese else 'bahnschrift.ttf'
    return ImageFont.truetype(str(base/filename),size)



def make_previews(models):
    board=Image.new('RGB',(1800,1080),'#EEEAE1'); d=ImageDraw.Draw(board)
    d.text((64,42),'FIELD / STREET',font=font(58,True),fill='#283238')
    d.text((67,116),'LOW-POLY ENVIRONMENT COLLECTION / 01',font=font(19),fill='#768078')
    d.text((67,158),'垃圾桶 / 铁丝网 / 路灯',font=font(24,chinese=True),fill='#58675F')
    for i,color in enumerate(PALETTE.values()): d.rounded_rectangle((1438+i*47,70,1471+i*47,103),radius=4,fill=color)
    d.line((64,218,1736,218),fill='#CDD0C5',width=2)
    cards=[(55,530,(1.7,1.0,3.4)),(600,685,(.9,.6,3.7)),(1300,445,(3.3,1.2,3.7))]
    for i,(m,(x,width,eye)) in enumerate(zip(models,cards)):
        d.rounded_rectangle((x,247,x+width,992),radius=12,fill='#E0E3D9')
        d.text((x+27,271),f'0{i+1} / {m.name.upper()}',font=font(16),fill='#7B847B')
        picture=render(m,(width-32)*2,1080,eye=eye).resize((width-32,540),Image.Resampling.LANCZOS)
        board.paste(picture,(x+16,330),picture)
        d.text((x+27,859),m.label,font=font(32,True,chinese=True),fill='#283238')
        d.text((x+27,910),m.subtitle,font=font(18,chinese=True),fill='#657269')
        d.text((x+27,949),f'{m.triangle_count:,} TRIANGLES / GLB',font=font(17),fill='#657269')
        sheet=Image.new('RGB',(1600,1080),'#EEEAE1'); sd=ImageDraw.Draw(sheet)
        sd.text((60,42),m.label,font=font(45,True,chinese=True),fill='#283238')
        sd.text((62,117),f'{m.name.upper()} / {m.triangle_count:,} TRIANGLES',font=font(20),fill='#657269')
        for j,view in enumerate([eye,(0,.05,4)]):
            img=render(m,1450,1500,eye=view).resize((725,750),Image.Resampling.LANCZOS)
            sheet.paste(img,(40+j*800,200),img)
            sd.text((64+j*800,990),'01 / PERSPECTIVE' if j==0 else '02 / FRONT',font=font(20),fill='#657269')
        sheet.save(ROOT/'previews'/f'{m.name}.png')
    d.text((64,1032),'ORIGINAL GAME ASSETS / METRES / Y-UP / FLAT SHADING',font=font(18),fill='#768078')
    board.save(ROOT/'previews/collection.png')

if __name__=='__main__': main()
