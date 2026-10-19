"""Expose a short skin-colored section at both forearm tips, retaining their shape."""
import copy, datetime, hashlib, json, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from glb_tools import ROOT, ASSET, TEXTURE, read, write, accessor, parts, render
from apply_materials import append_view

FRACTION=.23

def clip(vertices,cut,skin):
    """Clip position/normal/UV tuples against a transverse plane."""
    output=[]
    def inside(v):return v[1]>=cut-1e-9 if skin else v[1]<=cut+1e-9
    for a,b in zip(vertices,vertices[1:]+vertices[:1]):
        ia,ib=inside(a),inside(b)
        if ia:output.append(a)
        if ia!=ib:
            t=(cut-a[1])/(b[1]-a[1]);v=a+(b-a)*t;v[1]=cut;output.append(v)
    return output

def area(positions,indices):
    triangles=np.asarray(positions)[np.asarray(indices).reshape(-1,3)]
    return np.linalg.norm(np.cross(triangles[:,1]-triangles[:,0],triangles[:,2]-triangles[:,0]),axis=1).sum()/2

def add_accessor(doc,blob,values,kind):
    dtype='<u2' if kind=='SCALAR' else '<f4'
    values=np.asarray(values,dtype=dtype)
    view=append_view(doc,blob,values.tobytes(),34963 if kind=='SCALAR' else 34962)
    a={'bufferView':view,'componentType':5123 if kind=='SCALAR' else 5126,'count':len(values),'type':kind}
    if kind!='SCALAR':a.update(min=values.min(0).tolist(),max=values.max(0).tolist())
    doc['accessors'].append(a);return len(doc['accessors'])-1

def main():
    old_bytes=ASSET.read_bytes();doc,blob=read(ASSET);old_doc=copy.deepcopy(doc)
    skin=next(i for i,m in enumerate(doc['materials']) if m['name']=='Skin')
    camo=next(i for i,m in enumerate(doc['materials']) if m['name']=='ForestCamouflage')
    stamp=datetime.datetime.now().strftime('%Y%m%d_%H%M%S_%f')
    backup=ROOT/'backups'/f'militia_before_exposed_hands_{stamp}.glb';backup.write_bytes(old_bytes)
    edited=[]
    for node in doc['nodes']:
        if not node.get('name','').startswith(('handLeft.','handRight.')):continue
        mesh=doc['meshes'][node['mesh']]
        assert len(mesh['primitives'])==1,'This adjustment expects an unsplit forearm'
        old=mesh['primitives'][0];attrs=old['attributes']
        p=accessor(doc,blob,attrs['POSITION']);n=accessor(doc,blob,attrs['NORMAL']);uv=accessor(doc,blob,attrs['TEXCOORD_0'])
        indices=accessor(doc,blob,old['indices']);low,high=float(p[:,1].min()),float(p[:,1].max())
        # These original Blender nodes have negative Y scale: local +Y is the distal tip.
        assert node['scale'][1]<0
        cut=high-(high-low)*FRACTION;data=np.concatenate([p,n,uv],axis=1).astype(float)
        replacements=[];combined_area=0;new_positions=[]
        for is_skin,material in [(False,camo),(True,skin)]:
            out=[]
            for tri in indices.reshape(-1,3):
                poly=clip([data[i].copy() for i in tri],cut,is_skin)
                for i in range(1,len(poly)-1):
                    t=np.asarray([poly[0],poly[i],poly[i+1]])
                    if np.linalg.norm(np.cross(t[1,:3]-t[0,:3],t[2,:3]-t[0,:3]))>1e-10:out.extend(t)
            out=np.asarray(out);out[:,3:6]/=np.linalg.norm(out[:,3:6],axis=1)[:,None]
            assert len(out)>0
            ii=np.arange(len(out),dtype=np.uint16)
            combined_area+=area(out[:,:3],ii);new_positions.append(out[:,:3])
            replacements.append({'attributes':{
                'POSITION':add_accessor(doc,blob,out[:,:3],'VEC3'),
                'NORMAL':add_accessor(doc,blob,out[:,3:6],'VEC3'),
                'TEXCOORD_0':add_accessor(doc,blob,out[:,6:8],'VEC2')},
                'indices':add_accessor(doc,blob,ii,'SCALAR'),'material':material})
            assert np.all(out[:,1]>=cut-1e-7) if is_skin else np.all(out[:,1]<=cut+1e-7)
        assert np.isclose(combined_area,area(p,indices),rtol=1e-6)
        merged=np.concatenate(new_positions)
        assert np.allclose(merged.min(0),p.min(0)) and np.allclose(merged.max(0),p.max(0))
        mesh['primitives']=replacements
        edited.append({'node':node['name'],'mesh':node['mesh'],'skin_tip_fraction':FRACTION,
                       'skin_tip_length_m':round((high-cut)*abs(node['scale'][1]),4),'local_y_boundary':cut})
    assert len(edited)==2 and doc['nodes']==old_doc['nodes'] and doc['materials']==old_doc['materials']
    edited_ids={e['mesh'] for e in edited}
    for i,mesh in enumerate(doc['meshes']):
        if i not in edited_ids:assert mesh==old_doc['meshes'][i]
    candidate=ROOT/'militia_hands_checked.glb';write(candidate,doc,blob)
    actual,actual_blob=read(candidate)
    for e in edited:
        for prim in actual['meshes'][e['mesh']]['primitives']:
            positions=accessor(actual,actual_blob,prim['attributes']['POSITION'])
            assert np.isfinite(positions).all()
            if prim['material']==skin:assert positions[:,1].min()>=e['local_y_boundary']-1e-6
    assert ASSET.read_bytes()==old_bytes,'Source changed during editing'
    shutil.copyfile(candidate,ASSET);shutil.copyfile(candidate,ROOT/'models/militia.glb');candidate.unlink()
    ps=parts(actual,actual_blob);texture=Image.open(TEXTURE)
    sheet=Image.new('RGB',(1800,1300),'#e9e6dd');d=ImageDraw.Draw(sheet)
    title=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',60)
    cn=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
    small=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',22)
    d.text((65,42),'MILITIA / FOREST CAMO',font=title,fill='#29363b')
    d.text((68,130),'袖口露手 · 肉色头部 · 黑色鞋子',font=cn,fill='#6d7870')
    for label,rect,eye in [('01 / THREE-QUARTER',(40,240,1000,950),(-2.5,1.1,-5)),('02 / FRONT',(1080,225,650,500),(0,.05,-5)),('03 / REAR',(1080,750,650,500),(1.6,.8,5))]:
        x,y,w,h=rect;d.text((x+25,y-36),label,font=small,fill='#6d7870')
        im=render(ps,texture,w*2,h*2,eye).resize((w,h),Image.Resampling.LANCZOS);sheet.paste(im,(x,y),im)
    sheet.save(ROOT/'previews/militia_exposed_hands.png');sheet.save(ROOT/'previews/militia_colored.png')
    (ROOT/'source/preview_data.json').write_text(json.dumps({'parts':ps},separators=(',',':')),encoding='utf-8')
    report={'asset':str(ASSET),'backup':str(backup),'skin_material':'Skin','changes':edited,'node_transforms_unchanged':True,
            'surface_area_and_bounds_unchanged':True,'triangles':sum(len(p['indices'])//3 for p in ps),
            'updated_sha256':hashlib.sha256(ASSET.read_bytes()).hexdigest()}
    (ROOT/'hands_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    prior=json.loads((ROOT/'material_report.json').read_text(encoding='utf-8'))
    prior.update(updated_sha256=report['updated_sha256'],latest_backup=str(backup),geometry_unchanged=False,silhouette_unchanged=True,hand_tip_fraction=FRACTION)
    for record in prior['materials']:
        if record['node'].startswith(('handLeft.','handRight.')):record['material']='ForestCamouflage + Skin'
    (ROOT/'material_report.json').write_text(json.dumps(prior,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
