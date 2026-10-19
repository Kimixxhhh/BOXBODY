"""Reproducible low-poly equipment case + open laptop. Metres, Y up, front +Z."""
from pathlib import Path
import json, math, struct, io, zipfile, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
from raster import render

ROOT=Path(__file__).resolve().parents[1]
PALETTE={'camo':'#ffffff','rubber':'#252d29','edge':'#46513b','metal':'#737b70',
 'latch':'#919784','brass':'#b49b68','computer':'#3d4947','deck':'#697570',
 'keys':'#26322f','keylight':'#9cae9f','display':'#ffffff','label':'#ffffff','led':'#70ddbd'}
TEXTURED={'camo','display','label'}
PARTS=[]; GROUP='Crate_Body'
PIVOTS={'Crate_Body':(0,0,0),'Crate_Lid':(0,.737,0),'Laptop_Base':(.08,.9,0),'Laptop_Screen':(.08,.934,-.205)}

def font(size):
    return ImageFont.truetype('C:/Windows/Fonts/consola.ttf',size)

def texture():
    im=Image.new('RGB',(1024,1024),'#60694b');d=ImageDraw.Draw(im)
    rng=np.random.default_rng(28)
    for k in range(90):
        x,y=rng.uniform(-70,1090),rng.uniform(-80,720)
        rx,ry=rng.uniform(45,150),rng.uniform(24,85)
        a=np.linspace(0,2*math.pi,10,endpoint=False);r=rng.uniform(.64,1.15,10)
        pts=[(x+math.cos(t)*rx*s,y+math.sin(t)*ry*s) for t,s in zip(a,r)]
        d.polygon(pts,fill=['#424c38','#79805a','#343f32','#686d4b'][k%4])
    d.rectangle((0,640,1024,1024),fill='#1c2929')
    # A legible, original field-computer display baked into the atlas.
    d.rectangle((10,650,630,990),fill='#172d30')
    d.rectangle((10,650,630,688),fill='#314b4a')
    d.text((27,658),'FIELD / TERMINAL 07',font=font(20),fill='#b6d4bd')
    d.text((480,660),'ONLINE',font=font(18),fill='#80d5a4')
    for x in range(28,410,32):d.line((x,711,x,968),fill='#2a4745',width=1)
    for y in range(713,970,32):d.line((28,y,412,y),fill='#2a4745',width=1)
    d.line([(40,932),(109,910),(143,856),(215,871),(259,804),(329,791),(395,730)],fill='#7ebca0',width=4)
    for x,y in [(143,856),(259,804),(395,730)]:
        d.ellipse((x-7,y-7,x+7,y+7),outline='#d0dcac',width=2)
    d.line((218,805,218,850),fill='#b4cf9e',width=2);d.line((197,827,240,827),fill='#b4cf9e',width=2)
    d.text((441,717),'LINK  98%',font=font(18),fill='#a8cfb6')
    d.text((441,755),'SYS / OK',font=font(18),fill='#a8cfb6')
    for i,w in enumerate([134,96,121,77,110]):
        d.rectangle((442,811+i*25,442+w,819+i*25),fill='#658d7c')
    d.text((441,951),'READY >_',font=font(18),fill='#c5d3b2')
    d.rectangle((665,665,1008,817),fill='#c1c4a6')
    d.rectangle((672,672,1001,810),outline='#424c39',width=3)
    d.text((690,685),'EQUIPMENT',font=font(34),fill='#343e30')
    d.text((690,733),'FIELD KIT / 07',font=font(25),fill='#343e30')
    d.text((690,774),'RUGGED SYSTEMS',font=font(17),fill='#586049')
    im.save(ROOT/'textures/field_atlas.png');return im

def linear(hex):
    a=np.array([int(hex[i:i+2],16)/255 for i in (1,3,5)])
    return np.where(a<=.04045,a/12.92,((a+.055)/1.055)**2.4).tolist()

class Part:
    def __init__(self,name,mat):
        self.name=name;self.mat=mat;self.group=GROUP;self.v=[];self.n=[];self.uv=[];self.idx=[];PARTS.append(self)
    def face(self,points,uv=None):
        p=np.asarray(points,float);n=np.cross(p[1]-p[0],p[2]-p[0]);n/=np.linalg.norm(n)
        if uv is None:
            axis=int(np.argmax(np.abs(n)))
            if axis==2: uv=np.stack([(p[:,0]+.76)/1.52,(.91-p[:,1])/.95],axis=1)
            elif axis==0:uv=np.stack([(p[:,2]+.47)/.94,(.91-p[:,1])/.95],axis=1)
            else:uv=np.stack([(p[:,0]+.76)/1.52,(p[:,2]+.47)/.94],axis=1)
            uv=np.clip(uv,0,1)*[1,.62]
        start=len(self.v);self.v.extend(p.tolist());self.n.extend([n.tolist()]*len(p));self.uv.extend(np.asarray(uv).tolist())
        for i in range(1,len(p)-1):self.idx.extend((start,start+i,start+i+1))
        return self

def solid(name,center,size,mat,bevel=.0):
    # An eight-sided footprint with single-segment bevels: deliberately flat shaded.
    x,y,z=center;a,h,b=np.asarray(size)/2;c=min(bevel,a*.45,b*.45,h*.7)
    if not c:
        poly=[(-a,-b),(a,-b),(a,b),(-a,b)]
        rings=[(y-h,poly),(y+h,poly)]
    else:
        def poly(a,b,c):return [(-a+c,-b),(a-c,-b),(a,-b+c),(a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c)]
        rings=[(y-h,poly(a-c*.5,b-c*.5,c*.5)),(y-h+c,poly(a,b,c)),(y+h-c,poly(a,b,c)),(y+h,poly(a-c*.5,b-c*.5,c*.5))]
    p=Part(name,mat);vv=[[(x+u,yy,z+v) for u,v in points] for yy,points in rings]
    p.face(vv[0]);p.face(vv[-1][::-1])
    for low,hi in zip(vv,vv[1:]):
        for i in range(len(low)):
            j=(i+1)%len(low);p.face([low[i],hi[i],hi[j],low[j]])
    return p

def bar(name,a,b,width,depth,mat):
    a,b=np.asarray(a,float),np.asarray(b,float);f=(b-a)/np.linalg.norm(b-a)
    v=np.cross(f,[0,1,0] if abs(f[1])<.9 else [0,0,1]);v/=np.linalg.norm(v);w=np.cross(f,v)
    p=solid(name,(0,0,0),(width,depth,np.linalg.norm(b-a)),mat)
    transform(p,np.stack([v,w,f],axis=1),(a+b)/2);return p

def transform(p,R,t):
    p.v=(np.asarray(p.v)@R.T+t).tolist();p.n=(np.asarray(p.n)@R.T).tolist();return p

def quad(name,points,mat,rect):
    u,v,U,V=np.asarray(rect)/1024
    return Part(name,mat).face(points,[(u,V),(U,V),(U,v),(u,v)])

def model():
    global GROUP
    solid('Molded_shell',(0,.382,0),(1.4,.704,.83),'camo',.065)
    solid('Lower_bumper',(0,.074,0),(1.416,.095,.842),'edge',.026)
    for x in [-.57,.57]:
        for z in [-.31,.31]:solid('Rubber_foot',(x,.025,z),(.20,.05,.16),'rubber',.014)
    for s in [-1,1]:
        for x in [-.58,-.30,0,.30,.58]:
            solid('Vertical_reinforcement',(x,.365,s*.42),(.105,.57,.065),'camo',.023)
        solid('Upper_body_rail',(0,.688,s*.419),(1.26,.045,.042),'edge',.012)
    # Short-side recessed U handles, both sides.
    for s in [-1,1]:
        solid('Handle_recess',(s*.702,.46,0),(.022,.25,.36),'rubber',.005)
        for z in [-.145,.145]:
            solid('Handle_mount',(s*.721,.50,z),(.055,.11,.065),'metal',.013)
            bar('Handle_arm',(s*.752,.51,z),(s*.79,.39,z),.038,.032,'metal')
        bar('Handle_grip',(s*.79,.39,-.145),(s*.79,.39,.145),.060,.052,'rubber')
    # Strong visual separation between box and lid.
    solid('Lid_gasket',(0,.738,0),(1.398,.018,.83),'rubber',.006)
    GROUP='Crate_Lid'
    solid('Lid_shell',(0,.812,0),(1.425,.135,.855),'camo',.044)
    for z in [-.344,.344]:solid('Lid_long_rim',(0,.875,z),(1.25,.033,.055),'edge',.012)
    for x in [-.625,.625]:solid('Lid_short_rim',(x,.875,0),(.058,.033,.7),'edge',.012)
    bar('Lid_X_brace_A',(-.59,.871,-.31),(.59,.871,.31),.048,.025,'edge')
    bar('Lid_X_brace_B',(-.59,.871,.31),(.59,.871,-.31),.048,.025,'edge')
    for x in [-.43,.43]:
        solid('Lock_recess',(x,.797,.427),(.19,.105,.020),'rubber',.008)
        solid('Lock_plate',(x,.798,.445),(.15,.077,.022),'metal',.007)
        for dx in [-.045,0,.045]:
            solid('Combination_roller',(x+dx,.802,.462),(.026,.050,.025),'latch',.005)
            for yy in [.787,.804,.821]:solid('Dial_tick',(x+dx,yy,.476),(.010,.004,.001),'brass')
    for x in [-.43,.43]:solid('Rear_hinge',(x,.745,-.44),(.19,.085,.053),'metal',.012)
    GROUP='Crate_Body'
    solid('Identity_plate_back',(0,.544,.458),(.27,.10,.012),'rubber',.003)
    quad('Equipment_ID',[(-.13,.501,.465),(.13,.501,.465),(.13,.588,.465),(-.13,.588,.465)],'label',(665,665,1008,817))
    GROUP='Laptop_Base'
    for x in [-.19,.35]:
        for z in [-.155,.18]:solid('Laptop_foot',(x,.899,z),(.057,.017,.04),'rubber',.004)
    solid('Laptop_lower',(.08,.920,.012),(.68,.034,.45),'rubber',.012)
    solid('Laptop_deck',(.08,.936,.012),(.663,.018,.435),'deck',.006)
    solid('Keyboard_well',(.08,.947,-.043),(.574,.008,.217),'computer',.002)
    # Individual low-poly key caps, including a distinct space bar and navigation bank.
    for row in range(4):
        for col in range(13):
            x=.08+(col-6)*.041;z=-.124+row*.043
            solid('Key',(x,.953,z),(.034,.007,.032),'keys')
            if row<3 and col%2==0:
                solid('Key_legend',(x-.004,.957,z-.004),(.010,.0008,.002),'keylight')
    for x,w in [(-.17,.046),(-.112,.046),(.06,.235),(.23,.046),(.287,.046)]:
        solid('Bottom_key',(x,.953,.046),(w,.007,.030),'keys')
    solid('Trackpad_border',(.08,.947,.151),(.203,.003,.096),'computer')
    solid('Trackpad',(.08,.949,.148),(.19,.002,.080),'deck')
    for x in [-.206,.366]:
        for z in np.linspace(-.13,.04,8):solid('Speaker_slot',(x,.947,z),(.013,.001,.007),'rubber')
    for x in [.23,.25,.27]:solid('Status_LED',(x,.947,.19),(.009,.001,.004),'led')
    for s in [-1,1]:
        for z in [-.10,-.035,.03]:solid('Side_port',(.08+s*.339,.919,z),(.002,.012,.035),'rubber')
    for x in [-.16,.32]:bar('Screen_hinge',(x-.04,.938,-.205),(x+.04,.938,-.205),.032,.032,'metal')
    GROUP='Laptop_Screen';start=len(PARTS)
    solid('Display_back',(0,.190,0),(.677,.382,.026),'computer',.010)
    solid('Display_bezel',(0,.190,.015),(.647,.351,.009),'rubber',.003)
    quad('Active_display',[(-.301,.037,.020),(.301,.037,.020),(.301,.343,.020),(-.301,.343,.020)],'display',(10,650,630,990))
    solid('Webcam',(0,.363,.020),(.012,.007,.003),'metal')
    solid('Camera_lens',(0,.363,.022),(.005,.004,.001),'rubber')
    solid('Back_logo',(0,.205,-.014),(.09,.044,.003),'edge',.001)
    angle=math.radians(-15);R=np.array([[1,0,0],[0,math.cos(angle),-math.sin(angle)],[0,math.sin(angle),math.cos(angle)]])
    for p in PARTS[start:]:transform(p,R,np.array(PIVOTS['Laptop_Screen']))

def export_glb():
    data=bytearray();views=[];accessors=[];meshes=[];nodes=[]
    def blob(raw,target=None):
        data.extend(b'\0'*((-len(data))%4));idx=len(views)
        view={'buffer':0,'byteOffset':len(data),'byteLength':len(raw)}
        if target:view['target']=target
        views.append(view);data.extend(raw);return idx
    def array(values,kind):
        a=np.asarray(values,dtype='<u2' if kind=='SCALAR' else '<f4');view=blob(a.tobytes(),34963 if kind=='SCALAR' else 34962)
        q={'bufferView':view,'componentType':5123 if kind=='SCALAR' else 5126,'count':len(a),'type':kind}
        if kind=='VEC3':q.update(min=a.min(0).tolist(),max=a.max(0).tolist())
        accessors.append(q);return len(accessors)-1
    materials=[];keys=list(PALETTE)
    for key in keys:
        pbr={'baseColorFactor':[*linear(PALETTE[key]),1],'metallicFactor':.45 if key in ['metal','latch','brass'] else 0,'roughnessFactor':.8}
        if key in TEXTURED:pbr['baseColorTexture']={'index':0}
        mat={'name':key,'pbrMetallicRoughness':pbr}
        if key=='display':mat.update(emissiveFactor=[.2,.2,.2],emissiveTexture={'index':0})
        materials.append(mat)
    for group,pivot in PIVOTS.items():
        primitives=[]
        for mat in keys:
            pp=[p for p in PARTS if p.group==group and p.mat==mat]
            if not pp:continue
            v=[];n=[];uv=[];idx=[]
            for p in pp:
                idx.extend(np.asarray(p.idx)+len(v));v.extend(p.v);n.extend(p.n);uv.extend(p.uv)
            position=np.asarray(v)-pivot
            attrs={'POSITION':array(position,'VEC3'),'NORMAL':array(n,'VEC3')}
            if mat in TEXTURED:attrs['TEXCOORD_0']=array(uv,'VEC2')
            primitives.append({'attributes':attrs,'indices':array(idx,'SCALAR'),'material':keys.index(mat)})
        meshes.append({'name':group,'primitives':primitives});nodes.append({'name':group,'mesh':len(meshes)-1,'translation':list(pivot)})
    image_view=blob((ROOT/'textures/field_atlas.png').read_bytes())
    nodes.append({'name':'WeaponBox_Laptop','children':[0,1,2,3]})
    doc={'asset':{'version':'2.0','generator':'WeaponBoxLaptop procedural lowpoly'},'scene':0,'scenes':[{'nodes':[4]}],
        'nodes':nodes,'meshes':meshes,'materials':materials,'buffers':[{'byteLength':len(data)}],'bufferViews':views,'accessors':accessors,
        'images':[{'bufferView':image_view,'mimeType':'image/png'}],'textures':[{'source':0,'sampler':0}],
        'samplers':[{'magFilter':9729,'minFilter':9987,'wrapS':33071,'wrapT':33071}],
        'extras':{'units':'metres','up':'+Y','front':'+Z','pivot':'ground center','screenOpeningDegrees':105}}
    j=json.dumps(doc,separators=(',',':')).encode();j+=b' '*((-len(j))%4);data.extend(b'\0'*((-len(data))%4))
    raw=struct.pack('<4sII',b'glTF',2,28+len(j)+len(data))+struct.pack('<I4s',len(j),b'JSON')+j+struct.pack('<I4s',len(data),b'BIN\0')+data
    (ROOT/'models/weapon_box_laptop.glb').write_bytes(raw)
    return doc

def export_obj():
    lines=['# Units: metres; Y up; front +Z','mtllib weapon_box_laptop.mtl'];offset=1
    for p in PARTS:
        lines+=['o '+p.group+'_'+p.name,'usemtl '+p.mat]
        lines+=['v '+' '.join(f'{n:.7f}' for n in v) for v in p.v]
        lines+=['vt %.7f %.7f'%(u,1-v) for u,v in p.uv]
        lines+=['vn '+' '.join(f'{x:.7f}' for x in n) for n in p.n]
        for tri in np.asarray(p.idx).reshape(-1,3):lines.append('f '+' '.join(f'{i+offset}/{i+offset}/{i+offset}' for i in tri))
        offset+=len(p.v)
    (ROOT/'models/weapon_box_laptop.obj').write_text('\n'.join(lines),encoding='utf-8')
    mtl=[]
    for key,value in PALETTE.items():
        rgb=[int(value[i:i+2],16)/255 for i in (1,3,5)]
        mtl+=['newmtl '+key,'Kd '+' '.join(map(str,rgb)),'Ks 0.05 0.05 0.05','Ns 20']
        if key in TEXTURED:mtl+=['map_Kd ../textures/field_atlas.png']
    (ROOT/'models/weapon_box_laptop.mtl').write_text('\n'.join(mtl),encoding='utf-8')

def previews(atlas):
    parts=[{'positions':p.v,'normals':p.n,'indices':p.idx,'uv':p.uv,'factor':[*linear(PALETTE[p.mat]),1],'textured':p.mat in TEXTURED} for p in PARTS]
    # Render the same positions, normals and UVs used in the export.
    for name,eye in [('hero',(-1.5,1.15,2.2)),('rear',(1.5,.95,-2.2)),('front',(0,.45,3))]:
        layer=render(parts,atlas,1800,1500,eye)
        bg=Image.new('RGBA',layer.size,'#e6e9df');shadow=Image.new('RGBA',layer.size)
        d=ImageDraw.Draw(shadow);d.ellipse((260,1240,1540,1390),fill=(44,56,41,40));shadow=shadow.filter(ImageFilter.GaussianBlur(35))
        bg.alpha_composite(shadow);bg.alpha_composite(layer)
        d=ImageDraw.Draw(bg);d.text((76,62),'FIELD KIT / 07',font=font(36),fill='#334336')
        d.text((78,112),'LOW POLY  /  EQUIPMENT CASE + LAPTOP',font=font(20),fill='#697660')
        d.text((78,1436),f'{sum(len(p.idx)//3 for p in PARTS):,} TRIANGLES   |   GLB + OBJ   |   Y UP / METRES',font=font(19),fill='#697660')
        bg.convert('RGB').resize((1440,1200),Image.Resampling.LANCZOS).save(ROOT/f'previews/{name}.png')
    return parts

def validate():
    raw=(ROOT/'models/weapon_box_laptop.glb').read_bytes();assert struct.unpack_from('<4sII',raw)==(b'glTF',2,len(raw))
    size=struct.unpack_from('<I',raw,12)[0];doc=json.loads(raw[20:20+size]);binary=raw[28+size:]
    def read(i):
        a=doc['accessors'][i];v=doc['bufferViews'][a['bufferView']];n={'VEC2':2,'VEC3':3,'SCALAR':1}[a['type']]
        return np.frombuffer(binary,dtype='<f4' if a['componentType']==5126 else '<u2',count=a['count']*n,offset=v['byteOffset']).reshape(-1,n)
    count=0
    for mesh in doc['meshes']:
        for p in mesh['primitives']:
            v=read(p['attributes']['POSITION']);n=read(p['attributes']['NORMAL']);idx=read(p['indices']).reshape(-1,3)
            assert np.isfinite(v).all() and idx.max()<len(v)
            assert np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-5)
            cross=np.cross(v[idx[:,1]]-v[idx[:,0]],v[idx[:,2]]-v[idx[:,0]])
            assert (np.linalg.norm(cross,axis=1)>1e-10).all()
            assert (np.sum(cross*n[idx[:,0]],axis=1)>0).all()
            if 'TEXCOORD_0' in p['attributes']:
                uv=read(p['attributes']['TEXCOORD_0']);assert uv.min()>=0 and uv.max()<=1
            count+=len(idx)
    assert count==sum(len(p.idx)//3 for p in PARTS)
    image_view=doc['bufferViews'][doc['images'][0]['bufferView']]
    Image.open(io.BytesIO(binary[image_view['byteOffset']:image_view['byteOffset']+image_view['byteLength']])).verify()
    allv=np.concatenate([p.v for p in PARTS]);assert abs(allv[:,1].min())<1e-6
    info={'triangles':count,'dimensions_metres':np.ptp(allv,axis=0).round(4).tolist(),'groups':list(PIVOTS),'materials':len(PALETTE),'texture':'embedded 1024x1024 PNG','screen_opening_degrees':105,'validation':'PASS: GLB structure, indices, normals, winding, UVs, embedded PNG and ground origin'}
    (ROOT/'manifest.json').write_text(json.dumps(info,indent=2),encoding='utf-8');print(json.dumps(info,indent=2))

if __name__=='__main__':
    atlas=texture();model();export_glb();export_obj();previews(atlas);validate()
