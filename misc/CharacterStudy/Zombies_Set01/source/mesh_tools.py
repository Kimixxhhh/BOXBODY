"""Small original flat-shaded mesh builder and glTF exporter, Y up / -Z forward."""
from pathlib import Path
import json, math, struct
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
PALETTE={'skin':'#84976B','pale':'#A1AD83','teal':'#566D72','rust':'#B0784D',
         'wine':'#81585D','olive':'#6F7350','ochre':'#CEAC55','dark':'#2D3635','bone':'#DDD7B5'}

def linear(key):
    c=np.array([int(PALETTE[key][i:i+2],16)/255 for i in (1,3,5)])
    return np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4).tolist()+[1]

class Part:
    def __init__(self,name,material,group):
        self.name,self.material,self.group=name,material,group
        self.vertices=[];self.normals=[];self.indices=[]
    def face(self,points,outward=None):
        p=np.array(points,float);n=np.cross(p[1]-p[0],p[2]-p[0])
        if np.linalg.norm(n)<1e-10:return
        if outward is not None and n@np.asarray(outward)<0:
            p=p[::-1];n=-n
        n/=np.linalg.norm(n);start=len(self.vertices)
        self.vertices.extend(p.tolist());self.normals.extend([n.tolist()]*len(p))
        for i in range(1,len(p)-1):self.indices.extend([start,start+i,start+i+1])

class Model:
    def __init__(self,name,label,subtitle):
        self.name,self.label,self.subtitle=name,label,subtitle
        self.parts=[];self.groups={};self.current=None;self.markers={}
    def group(self,name,pivot,parent=None):
        self.groups[name]={'pivot':np.array(pivot,float),'parent':parent};self.current=name
    def use(self,name):self.current=name
    def part(self,name,material):
        p=Part(name,material,self.current);self.parts.append(p);return p
    @property
    def triangles(self):return sum(len(p.indices)//3 for p in self.parts)
    def turn(self,group,pitch=0,roll=0):
        x,z=np.radians([pitch,roll]);cx,sx,cz,sz=np.cos(x),np.sin(x),np.cos(z),np.sin(z)
        rx=np.array([[1,0,0],[0,cx,-sx],[0,sx,cx]]);rz=np.array([[cz,-sz,0],[sz,cz,0],[0,0,1]])
        matrix=rz@rx;pivot=self.groups[group]['pivot'];selected={group}
        for name,g in self.groups.items():
            if g['parent'] in selected:selected.add(name)
        for part in self.parts:
            if part.group in selected:
                part.vertices=((np.asarray(part.vertices)-pivot)@matrix.T+pivot).tolist()
                part.normals=(np.asarray(part.normals)@matrix.T).tolist()
        for name in selected:self.groups[name]['pivot']=(self.groups[name]['pivot']-pivot)@matrix.T+pivot

def box(m,name,center,size,mat):
    x,y,z=center;a,b,c=np.asarray(size)/2
    v=[(x-a,y-b,z-c),(x+a,y-b,z-c),(x+a,y+b,z-c),(x-a,y+b,z-c),
       (x-a,y-b,z+c),(x+a,y-b,z+c),(x+a,y+b,z+c),(x-a,y+b,z+c)]
    p=m.part(name,mat)
    for f in [(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]:p.face([v[i] for i in f])
    return p

def triangulate(poly):
    p=np.asarray(poly,float);signed=sum(p[i,0]*p[(i+1)%len(p),1]-p[(i+1)%len(p),0]*p[i,1] for i in range(len(p)))
    if signed<0:p=p[::-1]
    order=list(range(len(p)));result=[]
    def cross(a,b,c):
        u,v=b-a,c-a;return u[0]*v[1]-u[1]*v[0]
    while len(order)>3:
        for j in range(len(order)):
            a,b,c=order[j-1],order[j],order[(j+1)%len(order)]
            if cross(p[a],p[b],p[c])<=1e-10:continue
            if any(min(cross(p[a],p[b],p[k]),cross(p[b],p[c],p[k]),cross(p[c],p[a],p[k]))>=-1e-9 for k in order if k not in (a,b,c)):continue
            result.append((a,b,c));order.pop(j);break
        else:raise ValueError('Invalid polygon')
    result.append(tuple(order));return p,result

def profile(m,name,xy,depth,mat,z=0):
    xy,triangles=triangulate(xy);p=m.part(name,mat)
    for tri in triangles:
        p.face([(xy[i,0],xy[i,1],z+depth/2) for i in tri])
        p.face([(xy[i,0],xy[i,1],z-depth/2) for i in tri[::-1]])
    for i,a in enumerate(xy):
        b=xy[(i+1)%len(xy)];p.face([(a[0],a[1],z-depth/2),(b[0],b[1],z-depth/2),(b[0],b[1],z+depth/2),(a[0],a[1],z+depth/2)])
    return p

def patch(m,name,xy,z,mat,front=True):
    xy,triangles=triangulate(xy);p=m.part(name,mat)
    for tri in triangles:p.face([(xy[i,0],xy[i,1],z) for i in tri],(0,0,-1 if front else 1))
    return p

def bar(m,name,a,b,width,depth,mat):
    a,b=np.asarray(a,float),np.asarray(b,float);axis=b-a;axis/=np.linalg.norm(axis)
    right=np.array([1.,0,0]);right-=axis*(right@axis);right/=np.linalg.norm(right);front=np.cross(right,axis)
    def ring(center):return [center+right*width*s/2+front*depth*t/2 for s,t in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    ra,rb=ring(a),ring(b);p=m.part(name,mat);mid=(a+b)/2
    for i in range(4):
        j=(i+1)%4;v=[ra[i],ra[j],rb[j],rb[i]];p.face(v,np.mean(v,axis=0)-mid)
    p.face(ra,-axis);p.face(rb,axis);return p

def export(m):
    blob=bytearray();views=[];accessors=[];meshes=[]
    used=list(dict.fromkeys(p.material for p in m.parts))
    materials=[{'name':key,'pbrMetallicRoughness':{'baseColorFactor':linear(key),'metallicFactor':0,'roughnessFactor':.95}} for key in used]
    def array(data,kind):
        data=np.asarray(data,dtype='<u2' if kind=='SCALAR' else '<f4');blob.extend(b'\0'*(-len(blob)%4))
        views.append({'buffer':0,'byteOffset':len(blob),'byteLength':data.nbytes,'target':34963 if kind=='SCALAR' else 34962});blob.extend(data.tobytes())
        a={'bufferView':len(views)-1,'componentType':5123 if kind=='SCALAR' else 5126,'count':len(data),'type':kind}
        if kind!='SCALAR':a.update(min=data.min(0).tolist(),max=data.max(0).tolist())
        accessors.append(a);return len(accessors)-1
    names=list(m.groups);nodes=[]
    for name,g in m.groups.items():
        grouped=[p for p in m.parts if p.group==name];primitives=[]
        for mat in dict.fromkeys(p.material for p in grouped):
            vertices=[];normals=[];indices=[]
            for p in grouped:
                if p.material!=mat:continue
                start=len(vertices);vertices.extend((np.asarray(p.vertices)-g['pivot']).tolist());normals.extend(p.normals);indices.extend([x+start for x in p.indices])
            # Planar UVs are provided for later retexturing even though these models use solid colors.
            vv=np.asarray(vertices);uv=vv[:,[0,1]]*.5+.5
            primitives.append({'attributes':{'POSITION':array(vertices,'VEC3'),'NORMAL':array(normals,'VEC3'),'TEXCOORD_0':array(uv,'VEC2')},'indices':array(indices,'SCALAR'),'material':used.index(mat)})
        parent=g['parent'];node={'name':name,'translation':(g['pivot']-(m.groups[parent]['pivot'] if parent else np.zeros(3))).tolist()}
        if primitives:meshes.append({'name':name,'primitives':primitives});node['mesh']=len(meshes)-1
        children=[i for i,n in enumerate(names) if m.groups[n]['parent']==name]
        if children:node['children']=children
        nodes.append(node)
    root_children=[i for i,n in enumerate(names) if m.groups[n]['parent'] is None]
    for name,point in m.markers.items():root_children.append(len(nodes));nodes.append({'name':name,'translation':list(point)})
    root=len(nodes);nodes.append({'name':m.name,'children':root_children})
    doc={'asset':{'version':'2.0','generator':'Original Block Undead Collection'},'scene':0,'scenes':[{'nodes':[root]}],
         'nodes':nodes,'meshes':meshes,'materials':materials,'accessors':accessors,'bufferViews':views,'buffers':[{'byteLength':len(blob)}],
         'extras':{'units':'metres','forward':'-Z','up':'+Y','origin':'ground center','triangles':m.triangles,'palette':{k:PALETTE[k] for k in used}}}
    js=json.dumps(doc,separators=(',',':')).encode();js+=b' '*(-len(js)%4);blob+=b'\0'*(-len(blob)%4)
    raw=struct.pack('<4sII',b'glTF',2,28+len(js)+len(blob))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(blob),b'BIN\0')+blob
    (ROOT/'models'/f'{m.name}.glb').write_bytes(raw)
    allv=np.concatenate([p.vertices for p in m.parts])
    return {'id':m.name,'name':m.label,'subtitle':m.subtitle,'file':f'models/{m.name}.glb','triangles':m.triangles,'materials':len(used),
            'meshes':len(meshes),'groups':names,'palette':used,'dimensions':np.ptp(allv,axis=0).round(3).tolist(),
            'partsData':[{'name':p.name,'group':p.group,'material':p.material,'positions':p.vertices,'normals':p.normals,'indices':p.indices} for p in m.parts]}

def render_parts(m):
    return [{'positions':p.vertices,'normals':p.normals,'indices':p.indices,'uv':[[0,0]]*len(p.vertices),
             'factor':linear(p.material),'textured':False} for p in m.parts]
