"""Append standard glTF node animations to the user's current edited GLBs.
Preserves all existing meshes, materials, node hierarchy and rest transforms.
Run without --install to build reviewable candidates; --install requires validation.
"""
from pathlib import Path
import copy, hashlib, json, math, struct
import numpy as np

ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT.parents[1]
ASSETS=PROJECT/'assets/characters/zombies'
CONFIG={
 'zombie_normal':dict(label='普通僵尸',walk=1.40,attack=1.00,impact=.43,stride=.155,lift=.055,bob=.018,sway=3.5,swing=10),
 'zombie_helmet':dict(label='工人僵尸',walk=1.25,attack=1.10,impact=.48,stride=.175,lift=.065,bob=.025,sway=2.8,swing=12),
 'zombie_fast':dict(label='狂奔僵尸',walk=.70,attack=.64,impact=.25,stride=.245,lift=.12,bob=.040,sway=4,swing=21),
 'zombie_strong':dict(label='壮硕僵尸',walk=1.65,attack=1.45,impact=.70,stride=.17,lift=.065,bob=.028,sway=4.5,swing=8),
}
ANIMATED=['body','head','armLeft','handLeft','armRight','handRight','ShoeLeft','ShoeRight']

def read(path):
    raw=Path(path).read_bytes();magic,version,size=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and size==len(raw)
    n,typ=struct.unpack_from('<I4s',raw,12);assert typ==b'JSON'
    doc=json.loads(raw[20:20+n]);length,typ=struct.unpack_from('<I4s',raw,20+n);assert typ==b'BIN\0'
    return doc,bytearray(raw[28+n:28+n+length])

def write(path,doc,blob):
    doc['buffers'][0]['byteLength']=len(blob)
    js=json.dumps(doc,ensure_ascii=False,separators=(',',':')).encode();js+=b' '*(-len(js)%4)
    blob=bytes(blob)+b'\0'*(-len(blob)%4)
    raw=struct.pack('<4sII',b'glTF',2,28+len(js)+len(blob))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(blob),b'BIN\0')+blob
    Path(path).write_bytes(raw)

def accessor(doc,blob,index):
    a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
    dt=np.dtype({5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]);w={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    x=np.ndarray((a['count'],w),dt,buffer=blob,offset=v.get('byteOffset',0)+a.get('byteOffset',0),strides=(v.get('byteStride',w*dt.itemsize),dt.itemsize)).copy()
    return x[:,0] if w==1 else x

def quat(euler):
    x,y,z=np.radians(euler)/2;cx,cy,cz=np.cos([x,y,z]);sx,sy,sz=np.sin([x,y,z])
    return np.array([sx*cy*cz-cx*sy*sz,cx*sy*cz+sx*cy*sz,cx*cy*sz-sx*sy*cz,cx*cy*cz+sx*sy*sz])

def multiply(a,b):
    av,bv=np.asarray(a[:3]),np.asarray(b[:3]);return np.r_[a[3]*bv+b[3]*av+np.cross(av,bv),a[3]*b[3]-av@bv]

def matrix(node):
    if 'matrix' in node:return np.asarray(node['matrix']).reshape(4,4).T
    x,y,z,w=node.get('rotation',[0,0,0,1]);m=np.eye(4)
    m[:3,:3]=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                      [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                      [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])@np.diag(node.get('scale',[1,1,1]))
    m[:3,3]=node.get('translation',[0,0,0]);return m

def lerp_quat(a,b,f):
    a,b=np.asarray(a),np.asarray(b);dot=a@b
    if dot<0:b=-b;dot=-dot
    if dot>.9995:q=a+(b-a)*f;return q/np.linalg.norm(q)
    theta=math.acos(np.clip(dot,-1,1));return (a*math.sin((1-f)*theta)+b*math.sin(f*theta))/math.sin(theta)

def world_matrices(doc,nodes=None):
    nodes=nodes or doc['nodes'];out={}
    def visit(i,parent):
        out[i]=parent@matrix(nodes[i])
        for child in nodes[i].get('children',[]):visit(child,out[i])
    for i in doc['scenes'][doc.get('scene',0)]['nodes']:visit(i,np.eye(4))
    return out

def pose(doc,blob,animation,t):
    nodes=copy.deepcopy(doc['nodes']);a=doc['animations'][animation]
    for ch in a['channels']:
        s=a['samplers'][ch['sampler']];times=accessor(doc,blob,s['input']);values=accessor(doc,blob,s['output'])
        k=min(max(int(np.searchsorted(times,t,side='right'))-1,0),len(times)-2)
        f=float(np.clip((t-times[k])/(times[k+1]-times[k]),0,1));path=ch['target']['path']
        v=lerp_quat(values[k],values[k+1],f) if path=='rotation' else values[k]*(1-f)+values[k+1]*f
        nodes[ch['target']['node']][path]=v.tolist()
    return nodes

def render_parts(doc,blob,nodes=None):
    worlds=world_matrices(doc,nodes);parts=[]
    for i,node in enumerate(doc['nodes']):
        if 'mesh' not in node:continue
        world=worlds[i]
        for p in doc['meshes'][node['mesh']]['primitives']:
            a=p['attributes'];v=accessor(doc,blob,a['POSITION']);n=accessor(doc,blob,a['NORMAL'])
            n=n@np.linalg.inv(world[:3,:3]);n/=np.linalg.norm(n,axis=1)[:,None]
            parts.append(dict(name=node['name'],positions=(v@world[:3,:3].T+world[:3,3]).tolist(),normals=n.tolist(),
                indices=accessor(doc,blob,p['indices']).tolist(),uv=[[0,0]]*len(v),textured=False,
                factor=doc['materials'][p['material']]['pbrMetallicRoughness'].get('baseColorFactor',[1,1,1,1])))
    return parts

def zero_pose():return {n:dict(t=np.zeros(3),r=np.zeros(3)) for n in ANIMATED}

def walk_pose(key,c,t):
    p=zero_pose();a=2*math.pi*(t%c['walk'])/c['walk'];s=math.sin(a)
    p['body']['t']=np.array([.018*s,c['bob']*(1-math.cos(2*a))*.5,0])
    p['body']['r']=np.array([-3-1.6*math.sin(2*a),3.0*s,c['sway']*s])
    p['head']['r']=np.array([-1.5*math.sin(a+.4),-2*s,-c['sway']*.65*s])
    for side,phase,sign in [('Left',0,-1),('Right',.5,1)]:
        cycle=(t/c['walk']+phase)%1
        if cycle<.60:
            u=cycle/.60;z=c['stride']*(-1+2*u);y=0;pitch=0
        else:
            u=(cycle-.60)/.40;z=c['stride']*math.cos(math.pi*u);y=c['lift']*math.sin(math.pi*u)**1.25;pitch=-12*math.sin(math.pi*u)
        p['Shoe'+side]['t']=np.array([0,y,z])
        p['Shoe'+side]['r']=np.array([pitch,sign*2,0])
        swing=c['swing']*math.sin(a+phase*2*math.pi+.35)
        p['arm'+side]['r']=np.array([4+swing,sign*2,sign*2+2*math.sin(2*a)])
        p['hand'+side]['r']=np.array([3-swing*.48,0,0])
    if key=='zombie_fast':p['body']['r'][0]-=5;p['head']['r'][0]-=3
    if key=='zombie_strong':p['body']['t'][0]*=1.5
    return p

def attack_poses(key):
    rest=zero_pose();wind=zero_pose();hit=zero_pose();follow=zero_pose()
    if key=='zombie_strong':
        wind['body']['r']=np.array([8,0,0]);wind['body']['t']=np.array([0,.04,.02])
        hit['body']['r']=np.array([-20,0,0]);hit['body']['t']=np.array([0,-.055,-.15])
        follow['body']['r']=np.array([-17,0,1]);follow['body']['t']=np.array([0,-.035,-.12])
        for side,sign in [('Left',-1),('Right',1)]:
            wind['arm'+side]['r']=np.array([92,sign*-7,sign*13]);wind['hand'+side]['r']=np.array([30,0,0])
            hit['arm'+side]['r']=np.array([30,sign*-8,sign*-7]);hit['hand'+side]['r']=np.array([-7,0,0])
            follow['arm'+side]['r']=np.array([8,0,sign*-6]);follow['hand'+side]['r']=np.array([-5,0,0])
        wind['head']['r']=np.array([8,0,0]);hit['head']['r']=np.array([-7,0,0]);follow['head']['r']=np.array([-3,0,0])
    elif key=='zombie_helmet':
        wind['body']['r']=np.array([5,0,0]);wind['body']['t']=np.array([0,.01,.025])
        hit['body']['r']=np.array([-13,0,0]);hit['body']['t']=np.array([0,-.015,-.115])
        follow['body']['r']=np.array([-11,0,0]);follow['body']['t']=np.array([0,-.01,-.10])
        for side,sign in [('Left',-1),('Right',1)]:
            wind['arm'+side]['r']=np.array([-18,0,sign*8]);wind['hand'+side]['r']=np.array([22,0,0])
            hit['arm'+side]['r']=np.array([52 if side=='Left' else 20,0,sign*-7]);hit['hand'+side]['r']=np.array([5,0,0])
            follow['arm'+side]['r']=np.array([40 if side=='Left' else 16,0,sign*-5])
        hit['head']['r']=np.array([-5,0,0])
    else:
        fast=key=='zombie_fast';active='Left' if fast else 'Right';other='Right' if fast else 'Left';sign=-1 if fast else 1
        wind['body']['r']=np.array([3,sign*-9,sign*3]);wind['body']['t']=np.array([0,.01,.035])
        hit['body']['r']=np.array([-18 if fast else -12,sign*13,sign*-3]);hit['body']['t']=np.array([sign*-.025,-.015,-.15 if fast else -.10])
        follow['body']['r']=np.array([-12,sign*9,0]);follow['body']['t']=np.array([sign*-.02,-.01,-.11 if fast else -.07])
        wind['arm'+active]['r']=np.array([-23,sign*-12,sign*22]);wind['hand'+active]['r']=np.array([24,0,0])
        hit['arm'+active]['r']=np.array([53 if fast else 45,sign*18,sign*-14]);hit['hand'+active]['r']=np.array([-8,0,0])
        follow['arm'+active]['r']=np.array([22,sign*24,sign*-25]);follow['hand'+active]['r']=np.array([-10,0,0])
        for pp in [wind,hit,follow]:pp['arm'+other]['r']=np.array([10,0,sign*-4]);pp['hand'+other]['r']=np.array([6,0,0])
        wind['head']['r']=np.array([1,sign*5,0]);hit['head']['r']=np.array([-7,sign*-7,0]);follow['head']['r']=np.array([-3,sign*-5,0])
    # A small bracing step keeps meaningful shoe tracks in both clips. It also
    # prevents engine import optimizers removing attack's foot reset channels.
    for side,sign in [('Left',-1),('Right',1)]:
        for pp,height in [(wind,.014),(hit,0),(follow,0)]:
            pp['Shoe'+side]['t']=np.array([0,height,sign*.025])
            pp['Shoe'+side]['r']=np.array([0,sign*4,0])
    return rest,wind,hit,follow

def attack_pose(key,c,t):
    poses=attack_poses(key);duration=c['attack'];impact=c['impact']
    ts=[0,impact*.64,impact,impact+.13*duration,duration]
    frames=[poses[0],poses[1],poses[2],poses[3],poses[0]]
    i=min(max(np.searchsorted(ts,t,side='right')-1,0),3);f=np.clip((t-ts[i])/(ts[i+1]-ts[i]),0,1);f=f*f*(3-2*f)
    return {n:{k:frames[i][n][k]*(1-f)+frames[i+1][n][k]*f for k in ['t','r']} for n in ANIMATED}

def append_animation(doc,blob,key,c,clip):
    names={n['name']:i for i,n in enumerate(doc['nodes'])};duration=c['walk' if clip=='walk_loop' else 'attack']
    times=np.linspace(0,duration,int(math.ceil(duration*30))+1)
    if clip=='attack':times=np.unique(np.r_[times,[c['impact']*.64,c['impact'],c['impact']+.13*duration]])
    channels=[];samplers=[]
    def add(values,kind):
        values=np.asarray(values,dtype='<f4');blob.extend(b'\0'*(-len(blob)%4))
        doc['bufferViews'].append(dict(buffer=0,byteOffset=len(blob),byteLength=values.nbytes));blob.extend(values.tobytes())
        a=dict(bufferView=len(doc['bufferViews'])-1,componentType=5126,count=len(values),type=kind)
        if kind=='SCALAR':a.update(min=[float(values.min())],max=[float(values.max())])
        doc['accessors'].append(a);return len(doc['accessors'])-1
    input_index=add(times,'SCALAR')
    poses=[walk_pose(key,c,float(t)) if clip=='walk_loop' else attack_pose(key,c,float(t)) for t in times]
    if clip=='walk_loop':poses[-1]=copy.deepcopy(poses[0])
    # Shoe rotation is compensated using the actual edited shoe vertices.
    for name in ANIMATED:
        node=doc['nodes'][names[name]];translations=[];rotations=[]
        rest_t=np.asarray(node.get('translation',[0,0,0]));rest_q=node.get('rotation',[0,0,0,1])
        shoe=name.startswith('Shoe');verts=None
        if shoe:verts=np.concatenate([accessor(doc,blob,p['attributes']['POSITION']) for p in doc['meshes'][node['mesh']]['primitives']])
        for p in poses:
            q=multiply(rest_q,quat(p[name]['r']));q/=np.linalg.norm(q);tr=rest_t+p[name]['t']
            if shoe and clip=='walk_loop':
                # Common stride centre avoids carrying the reference pose's stagger into every step.
                tr[2]=-float(np.mean(verts[:,2]))+p[name]['t'][2]
                rot=matrix(dict(rotation=q,scale=node.get('scale',[1,1,1])))[:3,:3]
                tr[1]=p[name]['t'][1]-float((verts@rot.T)[:,1].min())
            translations.append(tr);rotations.append(q)
        for path,values,kind in [('translation',translations,'VEC3'),('rotation',rotations,'VEC4')]:
            samplers.append(dict(input=input_index,output=add(values,kind),interpolation='LINEAR'))
            channels.append(dict(sampler=len(samplers)-1,target=dict(node=names[name],path=path)))
    extra=dict(loop=clip=='walk_loop',in_place=True,fps=30)
    if clip=='attack':extra['impact_time']=c['impact']
    doc.setdefault('animations',[]).append(dict(name=clip,samplers=samplers,channels=channels,extras=extra))

def viewer_model(doc,blob,key,c):
    nodes=copy.deepcopy(doc['nodes']);meshes=[]
    for node_index,node in enumerate(nodes):
        if 'mesh' not in node:continue
        for p in doc['meshes'][node['mesh']]['primitives']:
            a=p['attributes'];factor=doc['materials'][p['material']]['pbrMetallicRoughness']['baseColorFactor'][:3]
            color=np.asarray(factor);color=np.where(color<=.0031308,color*12.92,1.055*color**(1/2.4)-.055)
            meshes.append(dict(node=node_index,positions=accessor(doc,blob,a['POSITION']).tolist(),normals=accessor(doc,blob,a['NORMAL']).tolist(),indices=accessor(doc,blob,p['indices']).tolist(),color=color.tolist()))
    animations=[]
    for a in doc['animations']:
        tracks=[]
        for ch in a['channels']:
            s=a['samplers'][ch['sampler']];tracks.append(dict(node=ch['target']['node'],path=ch['target']['path'],times=accessor(doc,blob,s['input']).tolist(),values=accessor(doc,blob,s['output']).tolist()))
        animations.append(dict(name=a['name'],duration=tracks[0]['times'][-1],tracks=tracks,**a['extras']))
    allv=[]
    for ai,a in enumerate(animations):
        for t in np.linspace(0,a['duration'],14):
            for p in render_parts(doc,blob,pose(doc,blob,ai,t)):allv.extend(p['positions'])
    allv=np.asarray(allv)
    return dict(id=key,label=c['label'],nodes=nodes,roots=doc['scenes'][doc.get('scene',0)]['nodes'],meshes=meshes,animations=animations,
                min=allv.min(0).tolist(),max=allv.max(0).tolist(),file='models/'+key+'.glb')

def main():
    records=[];view=[]
    for key,c in CONFIG.items():
        original=ASSETS/(key+'.glb');backup=ROOT/'backups'/original.name
        if not backup.exists():backup.write_bytes(original.read_bytes())
        doc,blob=read(backup)
        assert not doc.get('animations'),'Refuse to replace existing animation clips'
        assert len(doc['buffers'])==1 and not doc.get('skins')
        for clip in ['walk_loop','attack']:append_animation(doc,blob,key,c,clip)
        write(ROOT/'models'/original.name,doc,blob)
        view.append(viewer_model(doc,blob,key,c))
        records.append(dict(id=key,label=c['label'],file='models/'+key+'.glb',walk_duration=c['walk'],attack_duration=c['attack'],impact_time=c['impact'],
                            source_sha256=hashlib.sha256(backup.read_bytes()).hexdigest(),triangles=sum(len(accessor(doc,blob,p['indices']))//3 for m in doc['meshes'] for p in m['primitives'])))
        print(f"BUILT {key}: walk_loop {c['walk']}s / attack {c['attack']}s / impact {c['impact']}s")
    (ROOT/'manifest.json').write_text(json.dumps(dict(models=records),ensure_ascii=False,indent=2),encoding='utf-8')
    (ROOT/'source/preview_data.json').write_text(json.dumps(dict(models=view),ensure_ascii=False,separators=(',',':')),encoding='utf-8')
    template=ROOT/'source/viewer_template.html'
    if template.exists():(ROOT/'preview.html').write_text(template.read_text(encoding='utf-8').replace('__DATA__',json.dumps(dict(models=view),ensure_ascii=False,separators=(',',':'))),encoding='utf-8')

if __name__=='__main__':main()
