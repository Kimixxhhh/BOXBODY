"""Original, stylized game meshes. Standard glTF 2.0 binary export; no source models.
Run with Python 3 + numpy + Pillow. Coordinates while authoring: +X muzzle, +Y up.
Export rotates to Godot-friendly -Z muzzle, +Y up, metres; origin is the main grip.
"""
from pathlib import Path
import json, math, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
PALETTE = {
    'charcoal': '#283238',
    'steel': '#626e70',
    'wood': '#ad7046',
    'darkwood': '#694b37',
    'olive': '#737953',
}
RGB = {k: np.array([int(v[i:i+2],16)/255 for i in (1,3,5)]) for k,v in PALETTE.items()}

class Part:
    def __init__(self, name, material):
        self.name, self.material = name, material
        self.vertices, self.normals, self.indices = [], [], []
    def face(self, points):
        p = np.array(points, dtype=float)
        n = np.cross(p[1]-p[0],p[2]-p[0]); length = np.linalg.norm(n)
        if length < 1e-10: return
        n /= length
        start = len(self.vertices)
        self.vertices.extend(p.tolist()); self.normals.extend([n.tolist()] * len(p))
        for i in range(1,len(p)-1): self.indices.extend([start,start+i,start+i+1])

class Model:
    def __init__(self, name, label, subtitle):
        self.name, self.label, self.subtitle = name, label, subtitle
        self.parts=[]; self.markers={}
    def part(self, name, material):
        p=Part(name,material); self.parts.append(p); return p
    @property
    def triangle_count(self): return sum(len(p.indices)//3 for p in self.parts)

def box(m, name, center, size, mat):
    x,y,z=center; a,b,c=np.array(size)/2
    pts=[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
         (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
    p=m.part(name,mat)
    for f in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]:
        p.face([pts[i] for i in f])
    return p

def triangulate(poly):
    # Ear clipping supports the concave stock and curved-magazine profiles.
    poly=np.asarray(poly,float)
    signed=sum(poly[i,0]*poly[(i+1)%len(poly),1]-poly[(i+1)%len(poly),0]*poly[i,1] for i in range(len(poly)))
    order=list(range(len(poly))) if signed>0 else list(reversed(range(len(poly))))
    triangles=[]
    def cross(a,b,c):
        u,v=b-a,c-a
        return float(u[0]*v[1]-u[1]*v[0])
    while len(order)>3:
        found=False
        for j in range(len(order)):
            a,b,c=order[j-1],order[j],order[(j+1)%len(order)]
            if cross(poly[a],poly[b],poly[c]) <= 1e-10: continue
            inside=False
            for k in order:
                if k in (a,b,c): continue
                if min(cross(poly[a],poly[b],poly[k]),cross(poly[b],poly[c],poly[k]),cross(poly[c],poly[a],poly[k]))>=-1e-9:
                    inside=True; break
            if inside: continue
            triangles.append((a,b,c)); order.pop(j); found=True; break
        if not found: raise ValueError('Non-simple polygon')
    triangles.append(tuple(order)); return poly,triangles

def profile(m,name,poly,depth,mat,z=0):
    poly,tris=triangulate(poly)
    # Enforce CCW silhouette for side quads.
    signed=sum(poly[i,0]*poly[(i+1)%len(poly),1]-poly[(i+1)%len(poly),0]*poly[i,1] for i in range(len(poly)))
    if signed<0: poly=poly[::-1]; poly,tris=triangulate(poly)
    p=m.part(name,mat)
    for tri in tris:
        p.face([(poly[i,0],poly[i,1],z+depth/2) for i in tri])
        p.face([(poly[i,0],poly[i,1],z-depth/2) for i in tri[::-1]])
    for i in range(len(poly)):
        a,b=poly[i],poly[(i+1)%len(poly)]
        p.face([(a[0],a[1],z-depth/2),(b[0],b[1],z-depth/2),(b[0],b[1],z+depth/2),(a[0],a[1],z+depth/2)])
    return p

def lathe(m,name,rings,mat,cy=0,cz=0,sides=8,cap=True):
    # x-aligned, faceted tube / cone. Rings are (x, radius).
    p=m.part(name,mat); verts=[]
    for x,r in rings:
        verts.append([(x,cy+r*math.sin(2*math.pi*i/sides+math.pi/sides),cz+r*math.cos(2*math.pi*i/sides+math.pi/sides)) for i in range(sides)])
    for a,b in zip(verts,verts[1:]):
        for i in range(sides):
            j=(i+1)%sides; p.face([a[i],b[i],b[j],a[j]])
    if cap:
        p.face(verts[0]); p.face(list(reversed(verts[-1])))
    return p

def hollow(m,name,x1,x2,r1,r2,mat,cy=0,cz=0,sides=8,wall=0.008):
    # A real open muzzle, including the rim and dark inset interior.
    outer=lathe(m,name,[(x1,r1),(x2,r2)],mat,cy,cz,sides,False)
    inner=lathe(m,name+'_inside',[(x2,r2-wall),(x1,r1-wall)],'charcoal',cy,cz,sides,False)
    def ring(x,r): return [(x,cy+r*math.sin(2*math.pi*i/sides+math.pi/sides),cz+r*math.cos(2*math.pi*i/sides+math.pi/sides)) for i in range(sides)]
    for x,r,reverse in [(x1,r1,False),(x2,r2,True)]:
        a,b=ring(x,r),ring(x,r-wall)
        for i in range(sides):
            j=(i+1)%sides; v=[a[i],a[j],b[j],b[i]]
            outer.face(v[::-1] if reverse else v)
    return outer

def beam(m,name,a,b,width,depth,mat):
    a,b=np.array(a,float),np.array(b,float)
    delta=b-a; n=np.array([-delta[1],delta[0]]); n=n/np.linalg.norm(n)*width/2
    return profile(m,name,[a+n,a-n,b-n,b+n],depth,mat)

def ak47():
    m=Model('ak47','AK–47','弯弹匣 / 木质枪托')
    profile(m,'Stock',[(-.57,-.045),(-.55,.065),(-.34,.10),(-.265,.14),(-.23,.125),(-.23,.065),(-.32,.025),(-.51,-.05)],.064,'wood')
    profile(m,'Stock_butt',[(-.58,-.052),(-.56,.068),(-.544,.068),(-.564,-.052)],.070,'charcoal')
    profile(m,'Receiver',[(-.255,.065),(-.255,.16),(-.205,.19),(.04,.19),(.07,.16),(.07,.065)],.070,'charcoal')
    profile(m,'Dust_cover',[(-.247,.165),(-.20,.205),(.045,.205),(.065,.17)],.066,'steel')
    box(m,'Receiver_side',(-.09,.125,.037),(.22,.045,.009),'steel')
    box(m,'Selector',(-.06,.142,.045),(.095,.012,.009),'charcoal')
    profile(m,'Pistol_grip',[(-.145,.065),(-.093,.065),(-.115,-.074),(-.165,-.086),(-.177,-.062)],.048,'wood')
    beam(m,'Trigger_guard_rear',(-.092,.065),(-.105,.009),.011,.026,'charcoal')
    beam(m,'Trigger_guard_bottom',(-.105,.009),(-.027,.009),.010,.026,'charcoal')
    beam(m,'Trigger_guard_front',(-.027,.009),(-.016,.064),.010,.026,'charcoal')
    beam(m,'Trigger',(-.064,.058),(-.073,.029),.011,.017,'steel')
    # Eight-sided curved magazine: the broad concave silhouette remains visible at game scale.
    profile(m,'Magazine',[(.005,.07),(.074,.066),(.082,-.009),(.108,-.082),(.15,-.147),(.196,-.186),(.163,-.228),(.103,-.19),(.054,-.132),(.022,-.061)],.052,'charcoal')
    for z in [-.029,.029]:
        profile(m,'Magazine_rib',[(.034,.036),(.044,-.036),(.074,-.112),(.123,-.178),(.143,-.193),(.154,-.183),(.132,-.164),(.089,-.103),(.061,-.031),(.05,.037)],.005,'steel',z)
    profile(m,'Magazine_floor',[(.162,-.232),(.201,-.188),(.212,-.198),(.173,-.244)],.062,'steel')
    lathe(m,'Barrel',[(.11,.019),(.49,.019)],'charcoal',cy=.125)
    lathe(m,'Gas_tube',[(.07,.018),(.34,.018)],'steel',cy=.185)
    profile(m,'Lower_handguard',[(.065,.085),(.075,.155),(.235,.155),(.255,.13),(.25,.085)],.079,'wood')
    profile(m,'Upper_handguard',[(.075,.17),(.075,.205),(.23,.205),(.244,.18)],.063,'wood')
    box(m,'Handguard_rear_band',(.068,.142,0),(.014,.13,.085),'charcoal')
    box(m,'Handguard_front_band',(.247,.139,0),(.018,.105,.080),'charcoal')
    for x in [.116,.17]:
        box(m,'Vent',(x,.183,.033),(.028,.012,.003),'darkwood')
        box(m,'Vent',(x,.183,-.033),(.028,.012,.003),'darkwood')
    profile(m,'Gas_block',[(.30,.115),(.33,.115),(.33,.195),(.305,.195)],.035,'charcoal')
    profile(m,'Front_sight',[(.443,.11),(.466,.11),(.466,.223),(.454,.236),(.443,.223)],.022,'charcoal')
    box(m,'Sight_tip',(.454,.224,0),(.012,.012,.034),'steel')
    box(m,'Rear_sight',(.022,.221,0),(.029,.032,.038),'charcoal')
    hollow(m,'Muzzle',.486,.522,.024,.024,'steel',cy=.125,wall=.007)
    box(m,'Charging_handle',(-.055,.17,.063),(.04,.014,.047),'charcoal')
    m.markers={'Muzzle':(.523,.125,0),'Grip':(-.137,-.015,0),'Support':(.16,.115,0)}
    return m

def m870():
    m=Model('m870','M870','泵动霰弹枪 / 木质护木')
    profile(m,'Stock',[(-.61,-.115),(-.61,.055),(-.375,.065),(-.29,.13),(-.245,.13),(-.239,.055),(-.31,.018),(-.332,-.066),(-.377,-.044),(-.54,-.11)],.075,'wood')
    profile(m,'Stock_pad',[(-.63,-.12),(-.63,.06),(-.604,.06),(-.604,-.117)],.082,'charcoal')
    profile(m,'Stock_cheek',[(-.59,.055),(-.375,.065),(-.298,.12),(-.305,.094),(-.39,.041),(-.59,.031)],.079,'wood')
    profile(m,'Receiver',[(-.25,.045),(-.255,.16),(-.226,.18),(-.05,.18),(-.033,.163),(-.033,.05)],.078,'steel')
    box(m,'Receiver_top',(-.145,.183,0),(.195,.012,.061),'charcoal')
    profile(m,'Ejection_port',[(-.129,.122),(-.129,.159),(-.055,.159),(-.047,.15),(-.047,.122)],.004,'charcoal',.041)
    box(m,'Bolt_visible',(-.084,.124,.045),(.064,.009,.005),'steel')
    beam(m,'Trigger_guard_rear',(-.239,.049),(-.225,-.002),.011,.027,'charcoal')
    beam(m,'Trigger_guard_base',(-.225,-.002),(-.168,-.002),.011,.027,'charcoal')
    beam(m,'Trigger_guard_front',(-.168,-.002),(-.15,.047),.011,.027,'charcoal')
    beam(m,'Trigger',(-.196,.043),(-.207,.014),.010,.018,'charcoal')
    lathe(m,'Barrel', [(-.05,.022),(.53,.022)],'charcoal',cy=.148)
    lathe(m,'Magazine_tube',[(-.04,.022),(.508,.022)],'steel',cy=.087)
    hollow(m,'Barrel_muzzle',.523,.548,.027,.027,'charcoal',cy=.148,wall=.009)
    lathe(m,'Magazine_cap',[(.50,.026),(.52,.026)],'charcoal',cy=.087)
    lathe(m,'Pump',[(.10,.04),(.12,.049),(.31,.049),(.33,.04)],'wood',cy=.082,sides=8)
    for i in range(5):
        x=.15+i*.032
        lathe(m,'Pump_rib',[(x,.0505),(x+.009,.0505)],'darkwood',cy=.082,sides=8)
    box(m,'Action_bar',(.05,.092,.027),(.18,.012,.012),'steel')
    box(m,'Action_bar',(.05,.092,-.027),(.18,.012,.012),'steel')
    profile(m,'Barrel_clamp',[(.37,.055),(.39,.055),(.39,.173),(.37,.173)],.062,'charcoal')
    box(m,'Front_bead',(.505,.18,0),(.018,.021,.014),'steel')
    box(m,'Rear_bead',(-.2,.199,0),(.018,.018,.025),'charcoal')
    m.markers={'Muzzle':(.548,.148,0),'Grip':(-.303,.02,0),'Support':(.225,.082,0)}
    return m

def rpg7():
    m=Model('rpg7','RPG–7','棱面弹头 / 喇叭尾口')
    lathe(m,'Launch_tube',[(-.67,.041),(.30,.041)],'charcoal',cy=.10)
    lathe(m,'Heat_shield',[(-.58,.051),(-.43,.051),(-.39,.061),(-.19,.061)],'wood',cy=.10)
    for x in [-.575,-.422,-.19]: lathe(m,'Shield_band',[(x,.064),(x+.018,.064)],'charcoal',cy=.10)
    hollow(m,'Rear_flare',-.83,-.665,.088,.041,'charcoal',cy=.10,wall=.01)
    hollow(m,'Rear_rim',-.842,-.825,.092,.092,'steel',cy=.10,wall=.012)
    lathe(m,'Breech', [(-.19,.057),(-.06,.057),(-.04,.044)],'steel',cy=.10)
    lathe(m,'Forward_collar',[(.19,.046),(.225,.046)],'steel',cy=.10)
    lathe(m,'Rocket_neck',[(.29,.027),(.37,.027)],'steel',cy=.10)
    lathe(m,'Warhead',[(.34,.029),(.40,.042),(.50,.071),(.58,.081),(.60,.08),(.72,.042),(.77,.021)],'olive',cy=.10,sides=8)
    lathe(m,'Warhead_band',[(.578,.0825),(.598,.0825)],'olive',cy=.10)
    lathe(m,'Warhead_tip',[(.77,.021),(.795,.013)],'steel',cy=.10)
    profile(m,'Forward_grip', [(-.045,.064),(.014,.064),(.01,-.012),(-.005,-.025),(-.006,-.138),(-.053,-.138),(-.061,-.122),(-.057,-.006)],.047,'darkwood')
    profile(m,'Trigger_housing',[(.069,.058),(.18,.058),(.18,.017),(.145,.015),(.125,-.044),(.075,-.037)],.043,'steel')
    profile(m,'Pistol_grip',[(.092,-.023),(.14,-.026),(.13,-.17),(.085,-.17),(.08,-.155)],.046,'darkwood')
    beam(m,'Trigger_guard_top',(.16,.021),(.20,.021),.009,.023,'charcoal')
    beam(m,'Trigger_guard_front',(.20,.021),(.194,-.04),.009,.023,'charcoal')
    beam(m,'Trigger_guard_base',(.194,-.04),(.139,-.052),.009,.023,'charcoal')
    beam(m,'Trigger',(.164,.012),(.156,-.019),.009,.017,'charcoal')
    box(m,'Rear_sight_base',(-.115,.177,0),(.065,.022,.047),'charcoal')
    profile(m,'Rear_sight',[(-.143,.18),(-.14,.218),(-.093,.218),(-.085,.18)],.016,'steel')
    box(m,'Forward_sight',(.19,.17,0),(.055,.025,.02),'steel')
    box(m,'Grip_inset',(-.033,-.063,.025),(.018,.083,.004),'wood')
    box(m,'Grip_inset',(.107,-.10,.025),(.018,.08,.004),'wood')
    m.markers={'Muzzle':(.30,.10,0),'Grip':(.105,-.095,0),'Support':(-.03,-.07,0),'Projectile':(.34,.10,0)}
    return m

def to_godot(v):
    a=np.asarray(v,dtype=float); return np.stack([a[...,2],a[...,1],-a[...,0]],axis=-1)

def export_glb(m):
    pivot=np.array(m.markers['Grip'])
    data=bytearray(); views=[]; accessors=[]; meshes=[]; nodes=[]
    used=list(dict.fromkeys(p.material for p in m.parts))
    materials=[]
    for key in used:
        # glTF baseColorFactor is linear, while the art-direction swatches are sRGB.
        c=RGB[key]; linear=np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
        materials.append({'name':key,'pbrMetallicRoughness':{'baseColorFactor':[*linear.tolist(),1], 'metallicFactor':0,'roughnessFactor':.9},'doubleSided':False})
    def array(a,component,kind,target):
        while len(data)%4: data.append(0)
        start=len(data); a=np.asarray(a,dtype='<f4' if component==5126 else '<u2'); b=a.tobytes(); data.extend(b)
        view=len(views); views.append({'buffer':0,'byteOffset':start,'byteLength':len(b),'target':target})
        accessor={'bufferView':view,'componentType':component,'count':len(a),'type':kind}
        if kind=='VEC3': accessor.update(min=a.min(axis=0).tolist(),max=a.max(axis=0).tolist())
        accessors.append(accessor); return len(accessors)-1
    for p in m.parts:
        pos=to_godot(np.array(p.vertices)-pivot); normal=to_godot(p.normals)
        pi=array(pos,5126,'VEC3',34962); ni=array(normal,5126,'VEC3',34962); ii=array(p.indices,5123,'SCALAR',34963)
        meshes.append({'name':p.name,'primitives':[{'attributes':{'POSITION':pi,'NORMAL':ni},'indices':ii,'material':used.index(p.material)}]})
        nodes.append({'name':p.name+'_'+str(len(nodes)),'mesh':len(meshes)-1})
    for name,point in m.markers.items(): nodes.append({'name':name+'Socket','translation':to_godot(np.array(point)-pivot).tolist()})
    root_index=len(nodes); nodes.append({'name':m.name,'children':list(range(len(nodes)))})
    document={'asset':{'version':'2.0','generator':'Original Lowpoly Weapon Study'},'scene':0,'scenes':[{'nodes':[root_index]}],
        'nodes':nodes,'meshes':meshes,'materials':materials,'buffers':[{'byteLength':len(data)}], 'bufferViews':views,'accessors':accessors,
        'extras':{'units':'metres','forward':'-Z','up':'+Y','pivot':'main grip','palette':{k:PALETTE[k] for k in used},'triangles':m.triangle_count}}
    js=json.dumps(document,separators=(',',':')).encode(); js+=b' '*((-len(js))%4); data+=b'\0'*((-len(data))%4)
    payload=struct.pack('<4sII',b'glTF',2,12+8+len(js)+8+len(data))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(data),b'BIN\0')+data
    (ROOT/'models'/f'{m.name}.glb').write_bytes(payload)
    return {'id':m.name,'name':m.label,'subtitle':m.subtitle,'triangles':m.triangle_count,'materials':len(used),'parts':len(m.parts),
        'palette':used,'vertices':sum(len(p.vertices) for p in m.parts),'file':f'models/{m.name}.glb',
        'partsData':[{'name':p.name,'material':p.material,'positions':p.vertices,'normals':p.normals,'indices':p.indices} for p in m.parts]}

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
    # Render at 2x resolution for clean edges, then place onto a compact art-direction board.
    W,H=1800,1390
    board=Image.new('RGB',(W,H),'#eeeae1'); d=ImageDraw.Draw(board)
    d.text((76,55),'FIELD / ARMORY',font=font(62,True),fill='#263135')
    d.text((78,134),'LOW-POLY WEAPON STUDY     /     01',font=font(20),fill='#68716d')
    d.text((78,175),'少色板 · 棱面造型 · 原创游戏模型',font=font(23,chinese=True),fill='#68716d')
    for i,(key,hexcolor) in enumerate(PALETTE.items()):
        x=1390+i*65; d.rounded_rectangle((x,72,x+48,120),radius=6,fill=hexcolor)
    d.text((1390,139),'5 SHARED COLORS',font=font(18),fill='#68716d')
    d.line((76,229,1724,229),fill='#c9cdc5',width=2)
    for i,m in enumerate(models):
        y=258+i*352
        d.rounded_rectangle((60,y,1740,y+326),radius=14,fill='#e4e3d9')
        d.text((89,y+24),f'0{i+1}',font=font(21),fill='#a47752')
        d.text((89,y+73),m.label,font=font(49,True),fill='#263135')
        d.text((90,y+140),m.subtitle,font=font(20,chinese=True),fill='#68716d')
        d.text((90,y+198),f'{m.triangle_count:,} TRIANGLES',font=font(18),fill='#68716d')
        d.text((90,y+230),'GLB / FLAT SHADING',font=font(16),fill='#858c80')
        im=render(m,2560,630).resize((1280,315),Image.Resampling.LANCZOS)
        board.paste(im,(425,y+2),im)
        # A dedicated sheet lets the user compare the actual silhouette with a volumetric view.
        sheet=Image.new('RGB',(1800,1100),'#eeeae1'); sd=ImageDraw.Draw(sheet)
        sd.text((75,46),m.label,font=font(66,True),fill='#263135')
        sd.text((77,128),f'{m.triangle_count:,} TRIANGLES    /    {len(set(p.material for p in m.parts))} MATERIALS    /    GLB',font=font(22),fill='#68716d')
        sd.text((78,218),'01 / THREE-QUARTER',font=font(18),fill='#858c80')
        beauty=render(m,3200,780).resize((1600,390),Image.Resampling.LANCZOS); sheet.paste(beauty,(100,240),beauty)
        sd.line((78,660,1722,660),fill='#c9cdc5',width=2)
        sd.text((78,700),'02 / SIDE PROFILE',font=font(18),fill='#858c80')
        sideim=render(m,3200,570,eye=(0,0,4)).resize((1600,285),Image.Resampling.LANCZOS); sheet.paste(sideim,(100,745),sideim)
        sheet.save(ROOT/'previews'/f'{m.name}.png')
    d.text((78,1347),'GAME ASSET CONCEPTS  /  AK–47 · M870 · RPG–7',font=font(18),fill='#68716d')
    d.text((1405,1347),'OCTOBER 2026',font=font(18),fill='#68716d')
    board.save(ROOT/'previews'/'collection.png')

def main():
    models=[ak47(),m870(),rpg7()]
    for folder in ['models','previews']: (ROOT/folder).mkdir(exist_ok=True)
    records=[export_glb(m) for m in models]
    (ROOT/'source'/'mesh_data.json').write_text(json.dumps({'palette':PALETTE,'models':records},ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    (ROOT/'manifest.json').write_text(json.dumps({'palette':PALETTE,'models':[{k:v for k,v in r.items() if k!='partsData'} for r in records]},ensure_ascii=False,indent=2),encoding='utf-8')
    make_previews(models)
    for m in models: print(f'{m.name}: {m.triangle_count} triangles, {len(m.parts)} mesh parts')

if __name__=='__main__': main()
