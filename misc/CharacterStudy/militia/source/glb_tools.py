"""Read the character's existing GLB without rewriting its geometry or transforms."""
from pathlib import Path
import io, json, math, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
ASSET=ROOT.parents[1]/'assets/characters/militia.glb'
TEXTURE=ROOT.parents[1]/'assets/textures/forest_camo_lowpoly.png'

def read(path):
    raw=Path(path).read_bytes(); magic,version,length=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and length==len(raw)
    n,kind=struct.unpack_from('<I4s',raw,12);assert kind==b'JSON'
    doc=json.loads(raw[20:20+n]);size,kind=struct.unpack_from('<I4s',raw,20+n);assert kind==b'BIN\0'
    return doc,bytearray(raw[28+n:28+n+size])

def write(path,doc,blob):
    doc['buffers']=[{'byteLength':len(blob)}]
    js=json.dumps(doc,ensure_ascii=False,separators=(',',':')).encode('utf-8');js+=b' '*(-len(js)%4)
    blob=bytes(blob)+b'\0'*(-len(blob)%4)
    output=struct.pack('<4sII',b'glTF',2,28+len(js)+len(blob))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(blob),b'BIN\0')+blob
    Path(path).write_bytes(output)

def accessor(doc,blob,index):
    a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']]
    dtype={5126:'<f4',5125:'<u4',5123:'<u2',5121:'u1'}[a['componentType']]
    width={'VEC2':2,'VEC3':3,'VEC4':4,'SCALAR':1}[a['type']]
    start=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',np.dtype(dtype).itemsize*width)
    data=np.ndarray((a['count'],width),dtype=dtype,buffer=blob,offset=start,strides=(stride,np.dtype(dtype).itemsize)).copy()
    return data[:,0] if width==1 else data

def transform(node):
    if 'matrix' in node:return np.asarray(node['matrix']).reshape(4,4).T
    x,y,z,w=node.get('rotation',[0,0,0,1])
    r=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
    m=np.eye(4);m[:3,:3]=r@np.diag(node.get('scale',[1,1,1]));m[:3,3]=node.get('translation',[0,0,0]);return m

def parts(doc,blob):
    result=[]
    def visit(i,parent):
        node=doc['nodes'][i];matrix=parent@transform(node)
        if 'mesh' in node:
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                attrs=primitive['attributes'];p=accessor(doc,blob,attrs['POSITION']);normal=accessor(doc,blob,attrs['NORMAL'])
                normal=normal@np.linalg.inv(matrix[:3,:3]);normal/=np.linalg.norm(normal,axis=1)[:,None]
                mat=doc['materials'][primitive['material']]
                result.append({'name':node['name'],'positions':(p@matrix[:3,:3].T+matrix[:3,3]).tolist(),'normals':normal.tolist(),
                               'indices':accessor(doc,blob,primitive['indices']).tolist(),
                               'uv':accessor(doc,blob,attrs['TEXCOORD_0']).tolist(),'material':mat['name'],
                               'factor':mat.get('pbrMetallicRoughness',{}).get('baseColorFactor',[1,1,1,1]),
                               'textured':'baseColorTexture' in mat.get('pbrMetallicRoughness',{})})
        for child in node.get('children',[]):visit(child,matrix)
    for node in doc['scenes'][doc.get('scene',0)]['nodes']:visit(node,np.eye(4))
    return result

def render(parts,texture,width,height,eye):
    forward=np.array(eye,float);forward/=np.linalg.norm(forward)
    right=np.cross([0,1,0],forward);right/=np.linalg.norm(right);up=np.cross(forward,right)
    basis=np.array([right,up,forward]);allv=np.concatenate([p['positions'] for p in parts]);allp=allv@basis.T
    low,high=allp.min(0),allp.max(0);center=(low+high)/2
    scale=min(width*.84/(high[0]-low[0]),height*.88/(high[1]-low[1]))
    pixels=np.zeros((height,width,4),np.uint8);zbuffer=np.full((height,width),-np.inf)
    # Upper front key light, with a fill sufficient to preserve the camouflage colors.
    light=np.array([-.45,.85,-1.5]);light/=np.linalg.norm(light)
    tex=np.asarray(texture.convert('RGB')) if texture else None
    for part in parts:
        verts=np.asarray(part['positions'])@basis.T;xy=(verts[:,:2]-center[:2])*[scale,-scale]+[width/2,height/2]
        normals=np.asarray(part['normals']);uv=np.asarray(part['uv']);factor=np.array(part['factor'][:3])
        color=np.where(factor<=.0031308,factor*12.92,1.055*factor**(1/2.4)-.055)*255
        for ids in np.asarray(part['indices']).reshape(-1,3):
            n=normals[ids[0]]
            if n@forward<=0:continue
            pts=xy[ids];zs=verts[ids,2]
            xmin=max(0,int(np.floor(pts[:,0].min())));xmax=min(width-1,int(np.ceil(pts[:,0].max())))
            ymin=max(0,int(np.floor(pts[:,1].min())));ymax=min(height-1,int(np.ceil(pts[:,1].max())))
            if xmin>xmax or ymin>ymax:continue
            xx,yy=np.meshgrid(np.arange(xmin,xmax+1)+.5,np.arange(ymin,ymax+1)+.5)
            a,b,c=pts;den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(den)<1e-9:continue
            u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
            v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;w=1-u-v
            zz=u*zs[0]+v*zs[1]+w*zs[2];old=zbuffer[ymin:ymax+1,xmin:xmax+1]
            mask=(u>=-1e-7)&(v>=-1e-7)&(w>=-1e-7)&(zz>old)
            shade=.69+.33*max(0,float(n@light))+.05*max(0,float(n[1]))
            if part['textured'] and tex is not None:
                tuv=u[...,None]*uv[ids[0]]+v[...,None]*uv[ids[1]]+w[...,None]*uv[ids[2]]
                tx=np.clip((tuv[...,0]*(tex.shape[1]-1)).round().astype(int),0,tex.shape[1]-1)
                ty=np.clip((tuv[...,1]*(tex.shape[0]-1)).round().astype(int),0,tex.shape[0]-1)
                rgb=np.clip(tex[ty,tx]*shade,0,255).astype(np.uint8)
                pixels[ymin:ymax+1,xmin:xmax+1,:3][mask]=rgb[mask]
            else:pixels[ymin:ymax+1,xmin:xmax+1,:3][mask]=np.clip(color*shade,0,255).astype(np.uint8)
            pixels[ymin:ymax+1,xmin:xmax+1,3][mask]=255;old[mask]=zz[mask]
    return Image.fromarray(pixels)

if __name__=='__main__':
    doc,blob=read(ASSET);ps=parts(doc,blob)
    for p in ps:
        verts=np.asarray(p['positions']);print(p['name'],'min',verts.min(0).round(3),'max',verts.max(0).round(3))
    img=Image.new('RGB',(1200,900),'#e9e6dd')
    fg=render(ps,None,1000,800,(-2.5,1.1,-5));img.paste(fg,(100,50),fg)
    img.save(ROOT/'previews'/'before.png')
