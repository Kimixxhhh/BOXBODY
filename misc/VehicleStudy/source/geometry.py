"""Original flat-shaded mesh, GLB export and rasterization helpers."""
from pathlib import Path


import json, math, struct


import numpy as np


from PIL import Image, ImageDraw, ImageFont, ImageFilter


ROOT = Path(__file__).resolve().parents[1]


PALETTE = {
    'charcoal': '#29363b',
    'glass': '#729a9d',
    'ivory': '#e8debd',
    'terracotta': '#bf6b54',
    'sage': '#81967b',
    'ochre': '#cca454',
    'slate': '#667f92',
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
        self.parts=[]; self.markers={}; self.groups={}; self.part_groups={}
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
    # Ear clipping supports the concave wheel-arch silhouettes.
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


def to_godot(v):
    a=np.asarray(v,dtype=float); return np.stack([a[...,2],a[...,1],-a[...,0]],axis=-1)


def export_glb(m):
    pivot=np.zeros(3)
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
        group=m.part_groups.get(p.name)
        local_pivot=np.array(m.groups[group]) if group else pivot
        pos=to_godot(np.array(p.vertices)-local_pivot); normal=to_godot(p.normals)
        pi=array(pos,5126,'VEC3',34962); ni=array(normal,5126,'VEC3',34962); ii=array(p.indices,5123,'SCALAR',34963)
        meshes.append({'name':p.name,'primitives':[{'attributes':{'POSITION':pi,'NORMAL':ni},'indices':ii,'material':used.index(p.material)}]})
        nodes.append({'name':p.name+'_'+str(len(nodes)),'mesh':len(meshes)-1})
    children = list(range(len(nodes)))
    for group, point in m.groups.items():
        ids = [i for i, p in enumerate(m.parts) if m.part_groups.get(p.name) == group]
        children = [i for i in children if i not in ids]
        children.append(len(nodes))
        nodes.append({'name':group,'translation':to_godot(point).tolist(),'children':ids})
    for name,point in m.markers.items():
        children.append(len(nodes))
        nodes.append({'name':name,'translation':to_godot(point).tolist()})
    root_index=len(nodes); nodes.append({'name':m.name,'children':children})
    document={'asset':{'version':'2.0','generator':'Original Lowpoly Vehicle Study'},'scene':0,'scenes':[{'nodes':[root_index]}],
        'nodes':nodes,'meshes':meshes,'materials':materials,'buffers':[{'byteLength':len(data)}], 'bufferViews':views,'accessors':accessors,
        'extras':{'units':'metres','forward':'-Z','up':'+Y','pivot':'ground center', 'wheelAxis':'+X', 'wheelGroups':list(m.groups),'palette':{k:PALETTE[k] for k in used},'triangles':m.triangle_count}}
    js=json.dumps(document,separators=(',',':')).encode(); js+=b' '*((-len(js))%4); data+=b'\0'*((-len(data))%4)
    payload=struct.pack('<4sII',b'glTF',2,12+8+len(js)+8+len(data))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(data),b'BIN\0')+data
    (ROOT/'models'/f'{m.name}.glb').write_bytes(payload)
    return {'id':m.name,'name':m.label,'subtitle':m.subtitle,'triangles':m.triangle_count,'materials':len(used),'parts':len(m.parts),
        'palette':used,'vertices':sum(len(p.vertices) for p in m.parts),'file':f'models/{m.name}.glb','wheelGroups':list(m.groups),
        'dimensions':[round(float(v),3) for v in np.ptp(to_godot(np.concatenate([p.vertices for p in m.parts])),axis=0)],
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
            # Lighting changes face brightness without adding palette materials.
            shade=.68+.39*max(0,float(normal@light))+.06*max(0,float(normal[1]))
            color=np.clip(RGB[part.material]*shade*255,0,255).astype(np.uint8)
            buffer[ymin:ymax+1,xmin:xmax+1][mask]=[*color,255]; old[mask]=zz[mask]
    return Image.fromarray(buffer)


def font(size,bold=False,chinese=False):
    base=Path('C:/Windows/Fonts')
    filename='msyhbd.ttc' if chinese and bold else 'msyh.ttc' if chinese else 'bahnschrift.ttf'
    return ImageFont.truetype(str(base/filename),size)

