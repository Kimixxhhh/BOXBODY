"""Assign requested materials and proportional UVs; preserve all original mesh geometry."""
import copy, datetime, hashlib, json, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from glb_tools import ROOT, ASSET, TEXTURE, read, write, accessor, parts, render

def linear(hexcolor):
    rgb=np.array([int(hexcolor[i:i+2],16)/255 for i in (1,3,5)])
    return np.where(rgb<=.04045,rgb/12.92,((rgb+.055)/1.055)**2.4).tolist()+[1]

def append_view(doc,blob,data,target=None):
    blob.extend(b'\0'*(-len(blob)%4));offset=len(blob);blob.extend(data)
    view={'buffer':0,'byteOffset':offset,'byteLength':len(data)}
    if target:view['target']=target
    doc['bufferViews'].append(view);return len(doc['bufferViews'])-1

def main():
    source_bytes=ASSET.read_bytes();doc,blob=read(ASSET);original=copy.deepcopy(doc)
    stamp=datetime.datetime.now().strftime('%Y%m%d_%H%M%S_%f')
    backup=ROOT/'backups'/f'militia_before_color_{stamp}.glb';backup.write_bytes(source_bytes)
    material_base={'doubleSided':True}
    doc['materials']=[
        {'name':'ForestCamouflage',**material_base,'pbrMetallicRoughness':{'baseColorFactor':[1,1,1,1],'baseColorTexture':{'index':0,'texCoord':0},'metallicFactor':0,'roughnessFactor':.95}},
        {'name':'Skin',**material_base,'pbrMetallicRoughness':{'baseColorFactor':linear('#DCA27D'),'metallicFactor':0,'roughnessFactor':.9}},
        {'name':'BlackBoots',**material_base,'pbrMetallicRoughness':{'baseColorFactor':linear('#141819'),'metallicFactor':0,'roughnessFactor':.92}},
    ]
    tex_bytes=TEXTURE.read_bytes();view=append_view(doc,blob,tex_bytes)
    doc['images']=[{'name':'forest_camo_lowpoly','mimeType':'image/png','bufferView':view}]
    doc['samplers']=[{'magFilter':9729,'minFilter':9987,'wrapS':33071,'wrapT':33071}]
    doc['textures']=[{'name':'ForestCamouflage_BaseColor','sampler':0,'source':0}]
    assignments=[]
    centers={'body':(.5,.5),'hat':(.50,.46),'armLeft':(.27,.49),'armRight':(.68,.50),'handLeft':(.36,.51),'handRight':(.63,.47)}
    for node in doc['nodes']:
        if 'mesh' not in node:continue
        name=node['name'];base=name.split('.')[0]
        material=0 if base in centers else 1 if base=='head' else 2 if base in ['ShoeLeft','ShoeRight'] else None
        assert material is not None, 'Unexpected part: '+name
        mesh=doc['meshes'][node['mesh']]
        for primitive in mesh['primitives']:
            primitive['material']=material
            if material==0:
                attrs=primitive['attributes'];p=accessor(doc,blob,attrs['POSITION']);n=accessor(doc,blob,attrs['NORMAL'])
                # Compensate for object scale and edited cube proportions. Each face uses metric planar projection.
                physical=p*np.abs(np.asarray(node.get('scale',[1,1,1])))
                centered=physical-(physical.min(0)+physical.max(0))/2
                uv=[];tile_meters=1.05;origin=np.asarray(centers[base])
                for v,normal in zip(centered,n):
                    axis=int(np.argmax(np.abs(normal)))
                    coords=v[[2,1]] if axis==0 else v[[0,2]] if axis==1 else v[[0,1]]
                    uv.append(origin+coords*np.array([1,-1])/tile_meters)
                uv=np.asarray(uv,dtype='<f4');assert uv.min()>=0 and uv.max()<=1
                view=append_view(doc,blob,uv.tobytes(),34962)
                doc['accessors'].append({'bufferView':view,'componentType':5126,'count':len(uv),'type':'VEC2','min':uv.min(0).tolist(),'max':uv.max(0).tolist()})
                attrs['TEXCOORD_0']=len(doc['accessors'])-1
        assignments.append({'node':name,'material':doc['materials'][material]['name']})
    # Topology, transforms, object names, index buffers, positions, and normals must stay identical.
    assert doc['nodes']==original['nodes'] and doc['scenes']==original['scenes']
    for before,after in zip(original['meshes'],doc['meshes']):
        for a,b in zip(before['primitives'],after['primitives']):
            assert a['indices']==b['indices']
            for key in ['POSITION','NORMAL']:assert a['attributes'][key]==b['attributes'][key]
    candidate=ROOT/'militia_materials_checked.glb';write(candidate,doc,blob)
    actual,actualblob=read(candidate)
    before_doc,before_blob=read(backup)
    assert actual['nodes']==before_doc['nodes']
    for a,b in zip(before_doc['meshes'],actual['meshes']):
        for ap,bp in zip(a['primitives'],b['primitives']):
            for key in ['POSITION','NORMAL']:
                assert np.array_equal(accessor(before_doc,before_blob,ap['attributes'][key]),accessor(actual,actualblob,bp['attributes'][key]))
            assert np.array_equal(accessor(before_doc,before_blob,ap['indices']),accessor(actual,actualblob,bp['indices']))
    embedded=actual['bufferViews'][actual['images'][0]['bufferView']]
    assert actualblob[embedded['byteOffset']:embedded['byteOffset']+embedded['byteLength']]==tex_bytes
    assert backup.read_bytes()==source_bytes
    # Check the source was not edited by another application while preparing the update.
    assert ASSET.read_bytes()==source_bytes,'Source changed during material update'
    shutil.copyfile(candidate,ASSET)
    (ROOT/'models').mkdir(exist_ok=True);shutil.copyfile(ASSET,ROOT/'models/militia.glb')
    candidate.unlink()
    report={'asset':str(ASSET),'backup':str(backup),'original_sha256':hashlib.sha256(source_bytes).hexdigest(),
            'updated_sha256':hashlib.sha256(ASSET.read_bytes()).hexdigest(),'texture':str(TEXTURE),'geometry_unchanged':True,
            'materials':assignments,'skin_srgb':'#DCA27D','boots_srgb':'#141819','embedded_texture':True}
    (ROOT/'material_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
    ps=parts(actual,actualblob);texture=Image.open(TEXTURE)
    sheet=Image.new('RGB',(1800,1300),'#e9e6dd');d=ImageDraw.Draw(sheet)
    title=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',60)
    cn=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',25)
    small=ImageFont.truetype('C:/Windows/Fonts/bahnschrift.ttf',22)
    d.text((65,42),'MILITIA / FOREST CAMO',font=title,fill='#29363b')
    d.text((68,130),'森林迷彩 · 肉色头部 · 黑色鞋子',font=cn,fill='#6d7870')
    for label,rect,eye in [('01 / THREE-QUARTER',(40,240,1000,950),(-2.5,1.1,-5)),('02 / FRONT',(1080,225,650,500),(0,.05,-5)),('03 / REAR',(1080,750,650,500),(1.6,.8,5))]:
        x,y,w,h=rect;d.text((x+25,y-36),label,font=small,fill='#6d7870')
        im=render(ps,texture,w*2,h*2,eye).resize((w,h),Image.Resampling.LANCZOS);sheet.paste(im,(x,y),im)
    sheet.save(ROOT/'previews/militia_colored.png')
    (ROOT/'source/preview_data.json').write_text(json.dumps({'parts':ps},separators=(',',':')),encoding='utf-8')
    print(json.dumps(report,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
