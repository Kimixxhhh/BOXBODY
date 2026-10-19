"""Check the delivered binary assets independently from the GLB writer."""
import json, struct
from pathlib import Path
import numpy as np

root=Path(__file__).resolve().parents[1]
source=json.loads((root/'source/mesh_data.json').read_text(encoding='utf-8'))
for model in source['models']:
    raw=(root/model['file']).read_bytes()
    magic,version,size=struct.unpack_from('<4sII',raw)
    assert magic==b'glTF' and version==2 and size==len(raw)
    jsize,jtype=struct.unpack_from('<I4s',raw,12)
    assert jtype==b'JSON'
    doc=json.loads(raw[20:20+jsize])
    bsize,btype=struct.unpack_from('<I4s',raw,20+jsize)
    assert btype==b'BIN\0'
    blob=raw[28+jsize:28+jsize+bsize]
    assert not doc.get('images') and not doc.get('textures')
    assert all('uri' not in b for b in doc['buffers'])
    def array(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        dtype={5126:'<f4',5123:'<u2'}[a['componentType']]
        count=a['count']*{'VEC3':3,'SCALAR':1}[a['type']]
        x=np.frombuffer(blob,dtype=dtype,count=count,offset=v.get('byteOffset',0)+a.get('byteOffset',0))
        return x.reshape((-1,3)) if a['type']=='VEC3' else x
    world={}
    visited=set()
    def visit(index,translation):
        assert index not in visited
        visited.add(index);node=doc['nodes'][index]
        translation=translation+np.array(node.get('translation',[0,0,0]))
        if 'mesh' in node: world[node['mesh']]=translation
        for child in node.get('children',[]):visit(child,translation)
    for index in doc['scenes'][0]['nodes']:visit(index,np.zeros(3))
    assert len(world)==model['parts']
    tris=0;world_vertices=[]
    for i,mesh in enumerate(doc['meshes']):
        prim=mesh['primitives'][0]
        p=array(prim['attributes']['POSITION']);n=array(prim['attributes']['NORMAL']);idx=array(prim['indices'])
        assert np.isfinite(p).all() and np.isfinite(n).all() and int(idx.max())<len(p)
        assert np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-5)
        p_world=p+world[i]; world_vertices.append(p_world)
        original=np.asarray(model['partsData'][i]['positions'])
        expected=np.stack([original[:,2],original[:,1],-original[:,0]],axis=-1)
        assert np.allclose(p_world,expected,atol=1e-6),mesh['name']
        tri=idx.reshape((-1,3));cross=np.cross(p[tri[:,1]]-p[tri[:,0]],p[tri[:,2]]-p[tri[:,0]])
        assert (np.linalg.norm(cross,axis=1)>1e-8).all(),mesh['name']
        assert (np.sum(cross*n[tri[:,0]],axis=1)>0).all(),mesh['name']
        tris+=len(tri)
    assert tris==model['triangles']
    assert abs(np.concatenate(world_vertices)[:,1].min())<1e-6
    print(f"PASS {model['id']}: geometry, winding, wheel transforms, ground origin and embedded materials")
