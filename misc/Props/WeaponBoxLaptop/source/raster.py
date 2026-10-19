import numpy as np
from PIL import Image

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

