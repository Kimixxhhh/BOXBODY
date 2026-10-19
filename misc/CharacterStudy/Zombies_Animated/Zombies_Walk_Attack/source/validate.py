"""Check asset preservation, animation closure and actual animated geometry."""
import json,hashlib
import numpy as np
from animate import ROOT,CONFIG,ANIMATED,read,accessor,pose,world_matrices,render_parts

def main():
    manifest=json.loads((ROOT/'manifest.json').read_text(encoding='utf-8'))
    for model in manifest['models']:
        key=model['id'];base,old=read(ROOT/'backups'/f'{key}.glb');doc,blob=read(ROOT/'models'/f'{key}.glb')
        for field in ['nodes','meshes','materials','scenes','scene','skins','images','textures','samplers']:
            assert base.get(field)==doc.get(field),(key,field,'original asset changed')
        assert bytes(blob[:len(old)])==bytes(old),key
        assert doc['accessors'][:len(base['accessors'])]==base['accessors']
        assert doc['bufferViews'][:len(base['bufferViews'])]==base['bufferViews']
        assert [a['name'] for a in doc['animations']]==['walk_loop','attack']
        assert hashlib.sha256((ROOT/'backups'/f'{key}.glb').read_bytes()).hexdigest()==model['source_sha256']
        names={n['name']:i for i,n in enumerate(doc['nodes'])};root=doc['scenes'][doc.get('scene',0)]['nodes'][0]
        sets=[]
        for ai,anim in enumerate(doc['animations']):
            targeted=set()
            for ch in anim['channels']:
                s=anim['samplers'][ch['sampler']];ts=accessor(doc,blob,s['input']);vs=accessor(doc,blob,s['output'])
                assert np.isfinite(ts).all() and np.isfinite(vs).all() and np.all(np.diff(ts)>0)
                assert len(ts)==len(vs) and ts[0]==0
                assert ch['target']['node']!=root,'Root motion detected'
                targeted.add((ch['target']['node'],ch['target']['path']))
                if ch['target']['path']=='rotation':assert np.allclose(np.linalg.norm(vs,axis=1),1,atol=1e-6)
                if ai==0:assert np.allclose(vs[0],vs[-1],atol=1e-6),'Walk seam'
                else:
                    node=doc['nodes'][ch['target']['node']];default=[0,0,0,1] if ch['target']['path']=='rotation' else [0,0,0]
                    assert np.allclose(vs[0],node.get(ch['target']['path'],default),atol=1e-6)
                    assert np.allclose(vs[0],vs[-1],atol=1e-6),'Attack does not recover'
            sets.append(targeted)
            duration=float(ts[-1]);first=None;changed=False
            for t in np.linspace(0,duration,61):
                ns=pose(doc,blob,ai,t);worlds=world_matrices(doc,ns)
                for mat in worlds.values():assert np.isfinite(mat).all()
                assert np.allclose(worlds[root],world_matrices(base)[root])
                current=worlds[names['handRight']]
                if first is None:first=current.copy()
                else:changed=changed or not np.allclose(first,current)
                for part in render_parts(doc,blob,ns):
                    if part['name'].startswith('Shoe'):assert np.asarray(part['positions'])[:,1].min()>-.001,(key,ai,t,'foot penetration')
            assert changed
        assert sets[0]==sets[1],'Clip channel coverage differs'
        print('PASS',key,'original geometry/materials/rest pose preserved; walk seam; attack recovery; feet; no root motion')
    (ROOT/'validation_passed.json').write_text(json.dumps({'files':{m['id']:hashlib.sha256((ROOT/m['file']).read_bytes()).hexdigest() for m in manifest['models']}},indent=2),encoding='utf-8')

if __name__=='__main__':main()
