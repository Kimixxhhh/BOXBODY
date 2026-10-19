"""Independently validate exported arrays, materials, transforms and source agreement."""
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
    doc=json.loads(raw[20:20+jsize]);bsize,btype=struct.unpack_from('<I4s',raw,20+jsize)
    assert btype==b'BIN\0';blob=raw[28+jsize:28+jsize+bsize]
    assert not doc.get('images') and not doc.get('textures')
    assert all('uri' not in b for b in doc['buffers'])
    assert len(doc['materials'])==model['materials']
    def array(index):
        a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
        dtype={5126:'<f4',5123:'<u2'}[a['componentType']];size={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']]
        x=np.frombuffer(blob,dtype=dtype,count=a['count']*size,offset=v.get('byteOffset',0)+a.get('byteOffset',0))
        return x.reshape((-1,size)) if size>1 else x
    world={};visited=set();parents={};nodes={}
    def visit(index,translation,parent=None):
        assert index not in visited
        visited.add(index);node=doc['nodes'][index];nodes[node['name']]=node;parents[node['name']]=parent
        assert 'matrix' not in node and 'rotation' not in node and 'scale' not in node
        translation=translation+np.array(node.get('translation',[0,0,0]))
        if 'mesh' in node:world[node['mesh']]=translation
        for child in node.get('children',[]):visit(child,translation,node['name'])
    for index in doc['scenes'][0]['nodes']:visit(index,np.zeros(3))
    assert len(visited)==len(doc['nodes'])
    is_character='groups' in model
    assert len(world)==(model['meshes'] if is_character else model['parts'])
    if is_character:
        for side in ['Left','Right']:
            assert parents['hand'+side]=='arm'+side
            assert parents['arm'+side]=='body'
        assert parents['head']=='body' and 'GroundOrigin' in nodes
    else:
        assert 'GripSocket' in nodes
        assert np.allclose(nodes['GripSocket']['translation'],0)
    tris=0;all_vertices=[]
    for i,mesh in enumerate(doc['meshes']):
        for prim in mesh['primitives']:
            p=array(prim['attributes']['POSITION']);n=array(prim['attributes']['NORMAL']);idx=array(prim['indices'])
            assert np.isfinite(p).all() and np.isfinite(n).all() and int(idx.max())<len(p)
            assert len(p)==len(n) and len(idx)%3==0
            assert np.allclose(np.linalg.norm(n,axis=1),1,atol=1e-5)
            p_world=p+world[i];all_vertices.append(p_world)
            mat=doc['materials'][prim['material']]['name']
            if is_character:
                selected=[q for q in model['partsData'] if q['group']==mesh['name'] and q['material']==mat]
                expected=np.concatenate([q['positions'] for q in selected])
            else:
                q=model['partsData'][i];assert q['name']==mesh['name'] and q['material']==mat
                original=np.asarray(q['positions'])-np.asarray(model['markers']['Grip'])
                expected=np.stack([original[:,2],original[:,1],-original[:,0]],axis=-1)
            assert np.allclose(p_world,expected,atol=1e-6),(model['id'],mesh['name'])
            tri=idx.reshape((-1,3));cross=np.cross(p[tri[:,1]]-p[tri[:,0]],p[tri[:,2]]-p[tri[:,0]])
            assert (np.linalg.norm(cross,axis=1)>1e-10).all(),(model['id'],mesh['name'],'zero area')
            assert (np.sum(cross*n[tri[:,0]],axis=1)>0).all(),(model['id'],mesh['name'],'winding')
            tris+=len(tri)
    assert tris==model['triangles']
    if is_character:assert abs(np.concatenate(all_vertices)[:,1].min())<1e-6
    asset_dir=root.parents[1]/('assets/characters/zombies' if is_character else 'assets/weapons/'+model['category'])
    if (root.parents[1]/'project.godot').is_file():
        assert (asset_dir/(model['id']+'.glb')).read_bytes()==raw
    print(f"PASS {model['id']}: {tris} triangles, arrays, normals, winding, source transforms and project copy")
