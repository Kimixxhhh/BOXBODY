"""Replace Ranger's embedded woodland image with the requested desert image."""
from pathlib import Path
import copy, datetime, hashlib, io, json, shutil, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT.parents[1]
sys.path.insert(0,str(ROOT.parent/'militia/source'))
from glb_tools import read, write, accessor, parts, render

ASSET=PROJECT/'assets/characters/Ranger.glb'
TEXTURE=PROJECT/'assets/textures/desert_camo_lowpoly.png'

def main():
    for folder in ['backups','previews']:(ROOT/folder).mkdir(parents=True,exist_ok=True)
    old_bytes=ASSET.read_bytes();doc,old_blob=read(ASSET);before=copy.deepcopy(doc)
    targets=[i for i,m in enumerate(doc['materials']) if m.get('name')=='ForestCamouflage']
    assert len(targets)==1,'Expected one existing forest camouflage material'
    material_index=targets[0]
    material=doc['materials'][material_index]
    texture_index=material['pbrMetallicRoughness']['baseColorTexture']['index']
    texture=doc['textures'][texture_index];image_index=texture['source']
    embedded=doc['images'][image_index];image_view=embedded['bufferView']
    payload=TEXTURE.read_bytes();im=Image.open(io.BytesIO(payload));im.verify()
    stamp=datetime.datetime.now().strftime('%Y%m%d_%H%M%S_%f')
    backup=ROOT/'backups'/f'Ranger_before_desert_{stamp}.glb';backup.write_bytes(old_bytes)
    material['name']='DesertCamouflage'
    texture['name']='DesertCamouflage_BaseColor'
    embedded.update(name='desert_camo_lowpoly',mimeType='image/png')
    # Repack only the buffer views, preserving all view indices and internal accessor offsets.
    # Replacing the existing image avoids carrying an unused woodland bitmap in the output.
    blob=bytearray()
    for i,view in enumerate(doc['bufferViews']):
        start=view.get('byteOffset',0);chunk=old_blob[start:start+view['byteLength']]
        if i==image_view:chunk=payload
        blob.extend(b'\0'*(-len(blob)%4));view['byteOffset']=len(blob);view['byteLength']=len(chunk);blob.extend(chunk)
    for key in ['nodes','scenes','meshes','accessors','samplers']:
        assert doc.get(key)==before.get(key),'Unexpected change to '+key
    for i,m in enumerate(before['materials']):
        if i!=material_index:assert doc['materials'][i]==m
    for i in range(len(before['accessors'])):
        assert np.array_equal(accessor(before,old_blob,i),accessor(doc,blob,i)),f'Accessor changed: {i}'
    candidate=ROOT/'Ranger_desert_checked.glb';write(candidate,doc,blob)
    check,check_blob=read(candidate)
    v=check['bufferViews'][image_view]
    assert check_blob[v['byteOffset']:v['byteOffset']+v['byteLength']]==payload
    for i in range(len(before['accessors'])):
        assert np.array_equal(accessor(before,old_blob,i),accessor(check,check_blob,i))
    assert ASSET.read_bytes()==old_bytes,'Ranger was edited by another application during the update'
    shutil.copyfile(candidate,ASSET);candidate.unlink()
    ps=parts(check,check_blob)
    report={'asset':str(ASSET),'backup':str(backup),'texture':str(TEXTURE),'embedded_texture':True,
            'mesh_count':len(check['meshes']),'triangles':sum(len(p['indices'])//3 for p in ps),
            'geometry_uv_and_transforms_unchanged':True,'skin_and_boot_materials_unchanged':True,
            'original_sha256':hashlib.sha256(old_bytes).hexdigest(),'updated_sha256':hashlib.sha256(ASSET.read_bytes()).hexdigest(),
            'materials':[{'part':p['name'],'material':p['material']} for p in ps]}
    (ROOT/'material_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    sheet=Image.new('RGB',(1800,1300),'#ede6d8');d=ImageDraw.Draw(sheet)
    title=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',60)
    cn=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
    small=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',22)
    d.text((65,42),'RANGER / DESERT CAMO',font=title,fill='#433c32')
    d.text((68,130),'沙色迷彩 · 袖口露手 · 黑色鞋子',font=cn,fill='#817461')
    image=Image.open(TEXTURE)
    for label,rect,eye in [('01 / THREE-QUARTER',(40,240,1000,950),(-2.5,1.1,-5)),('02 / FRONT',(1080,225,650,500),(0,.05,-5)),('03 / REAR',(1080,750,650,500),(1.6,.8,5))]:
        x,y,w,h=rect;d.text((x+25,y-36),label,font=small,fill='#817461')
        fg=render(ps,image,w*2,h*2,eye).resize((w,h),Image.Resampling.LANCZOS);sheet.paste(fg,(x,y),fg)
    sheet.save(ROOT/'previews/Ranger_desert_camo.png')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
