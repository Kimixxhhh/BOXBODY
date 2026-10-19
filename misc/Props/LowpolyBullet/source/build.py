"""Build a stylized game cartridge. Artistic proportions, not manufacturing data."""
from pathlib import Path
import json
import math
import shutil
import struct
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT.parents[2]
SIDES = 12
MATERIALS = {
    'Brass': ('#c49a46', .65, .43),
    'Copper': ('#b87547', .60, .46),
    'Primer': ('#c4b899', .55, .48),
}


def linear(hex_color):
    c = np.array([int(hex_color[i:i+2], 16)/255 for i in (1, 3, 5)])
    return np.where(c <= .04045, c/12.92, ((c+.055)/1.055)**2.4).tolist()


class Part:
    def __init__(self, name, material):
        self.name, self.material = name, material
        self.positions, self.normals, self.indices = [], [], []

    def face(self, points):
        p = np.asarray(points, dtype=float)
        n = np.cross(p[1]-p[0], p[2]-p[0])
        length = np.linalg.norm(n)
        assert length > 1e-12, self.name
        n /= length
        start = len(self.positions)
        self.positions.extend(p.tolist())
        self.normals.extend([n.tolist()]*len(p))
        for i in range(1, len(p)-1):
            self.indices.extend([start, start+i, start+i+1])


def lathe(name, material, profile, sides=SIDES):
    """Axial distance runs forward along -Z, matching the project's weapons."""
    part = Part(name, material)
    rings = []
    for distance, radius in profile:
        rings.append([(radius*math.cos(2*math.pi*i/sides),
                       radius*math.sin(2*math.pi*i/sides), -distance)
                      for i in range(sides)] if radius else [(0, 0, -distance)])
    for a, b in zip(rings, rings[1:]):
        assert len(a) > 1
        for i in range(sides):
            j = (i+1) % sides
            part.face([a[i], b[0], a[j]] if len(b) == 1 else [a[i], b[i], b[j], a[j]])
    part.face(rings[0])
    if len(rings[-1]) > 1:
        part.face(rings[-1][::-1])
    return part


def model():
    # Broad silhouette with a visible extraction groove and a recessed base.
    casing = lathe('Casing', 'Brass', [
        (.00032, .0022), (0, .0022), (0, .0056),
        (.0007, .0062), (.0022, .0062), (.0027, .00515),
        (.0042, .00515), (.0047, .00605), (.0275, .00555),
        (.0325, .00425), (.0375, .00425), (.0380, .00410),
        (.0380, .00352), (.0317, .00352),
    ])
    projectile = lathe('Projectile', 'Copper', [
        (.0340, .00395), (.0400, .00395), (.0460, .00365),
        (.0520, .0028), (.0570, .0015), (.0600, 0),
    ])
    primer = lathe('Primer', 'Primer', [
        (.00013, .00185), (.00020, .00208), (.00030, .00208),
    ])
    return [casing, projectile, primer]


def three_horizontal(parts):
    # All three tips point +X; equal spacing along Z, resting on the XZ plane.
    rotation = np.array([[0, 0, -1], [0, 1, 0], [1, 0, 0]], dtype=float)
    arranged = []
    for number, row in enumerate((-.016, 0, .016), 1):
        for source in parts:
            part = Part(f'Round{number}_{source.name}', source.material)
            part.positions = (np.asarray(source.positions) @ rotation.T
                              + [-.03, .0062, row]).tolist()
            part.normals = (np.asarray(source.normals) @ rotation.T).tolist()
            part.indices = source.indices.copy()
            arranged.append(part)
    return arranged


def export_glb(name, parts, offset=(0, 0, 0), forward='-Z', pivot='rear center'):
    binary, views, accessors, meshes, nodes = bytearray(), [], [], [], []
    used = list(dict.fromkeys(p.material for p in parts))

    def array(values, kind):
        binary.extend(b'\0' * (-len(binary) % 4))
        data = np.asarray(values, dtype='<u2' if kind == 'SCALAR' else '<f4')
        views.append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': data.nbytes,
                      'target': 34963 if kind == 'SCALAR' else 34962})
        binary.extend(data.tobytes())
        accessor = {'bufferView': len(views)-1, 'componentType': 5123 if kind == 'SCALAR' else 5126,
                    'count': len(data), 'type': kind}
        if kind == 'VEC3':
            accessor.update(min=data.min(0).tolist(), max=data.max(0).tolist())
        accessors.append(accessor)
        return len(accessors)-1

    for p in parts:
        attrs = {'POSITION': array(np.asarray(p.positions)+offset, 'VEC3'),
                 'NORMAL': array(p.normals, 'VEC3')}
        meshes.append({'name': p.name, 'primitives': [{
            'attributes': attrs, 'indices': array(p.indices, 'SCALAR'),
            'material': used.index(p.material)}]})
        nodes.append({'name': p.name, 'mesh': len(meshes)-1})
    root = len(nodes)
    nodes.append({'name': name, 'children': list(range(root))})
    materials = []
    for key in used:
        color, metal, rough = MATERIALS[key]
        materials.append({'name': key, 'pbrMetallicRoughness': {
            'baseColorFactor': [*linear(color), 1], 'metallicFactor': metal,
            'roughnessFactor': rough}, 'doubleSided': False})
    triangles = sum(len(p.indices)//3 for p in parts)
    doc = {'asset': {'version': '2.0', 'generator': 'Field Kit lowpoly prop builder'},
           'scene': 0, 'scenes': [{'nodes': [root]}], 'nodes': nodes,
           'meshes': meshes, 'materials': materials, 'buffers': [{'byteLength': len(binary)}],
           'bufferViews': views, 'accessors': accessors,
           'extras': {'units': 'metres', 'forward': forward, 'up': '+Y', 'pivot': pivot,
                      'triangles': triangles, 'style': 'stylized 12-sided flat shading',
                      'purpose': 'game art only'}}
    js = json.dumps(doc, separators=(',', ':')).encode()
    js += b' ' * (-len(js) % 4)
    binary.extend(b'\0' * (-len(binary) % 4))
    payload = (struct.pack('<4sII', b'glTF', 2, 28+len(js)+len(binary))
               + struct.pack('<I4s', len(js), b'JSON') + js
               + struct.pack('<I4s', len(binary), b'BIN\0') + binary)
    path = ROOT/'models'/f'{name}.glb'
    path.write_bytes(payload)
    validate(path, triangles)
    shutil.copy2(path, PROJECT/'assets/weapons/ammo'/path.name)
    bounds = np.concatenate([p.positions for p in parts]) + offset
    return {'file': path.name, 'triangles': triangles, 'meshes': len(parts),
            'materials': len(used), 'dimensions_metres': np.ptp(bounds, axis=0).round(6).tolist()}


def validate(path, expected_triangles):
    raw = path.read_bytes()
    assert struct.unpack_from('<4sII', raw) == (b'glTF', 2, len(raw))
    js_size = struct.unpack_from('<I', raw, 12)[0]
    doc = json.loads(raw[20:20+js_size])
    blob = raw[28+js_size:]

    def read(index):
        a = doc['accessors'][index]
        v = doc['bufferViews'][a['bufferView']]
        width = 3 if a['type'] == 'VEC3' else 1
        return np.frombuffer(blob, dtype='<f4' if a['componentType'] == 5126 else '<u2',
                             count=a['count']*width, offset=v['byteOffset']).reshape(-1, width)

    count = 0
    for mesh in doc['meshes']:
        p = mesh['primitives'][0]
        v = read(p['attributes']['POSITION'])
        n = read(p['attributes']['NORMAL'])
        ids = read(p['indices']).reshape(-1, 3)
        assert np.isfinite(v).all() and ids.max() < len(v)
        assert np.allclose(np.linalg.norm(n, axis=1), 1, atol=1e-5)
        cross = np.cross(v[ids[:, 1]]-v[ids[:, 0]], v[ids[:, 2]]-v[ids[:, 0]])
        assert (np.linalg.norm(cross, axis=1) > 1e-12).all()
        assert (np.sum(cross*n[ids[:, 0]], axis=1) > 0).all()
        count += len(ids)
    assert count == expected_triangles
    print(f'PASS {path.name}: {count} triangles, finite geometry, winding and normals')


def export_obj(parts, name='bullet_ak'):
    lines = ['# Stylized game asset. Metres. Y up.', f'mtllib {name}.mtl']
    offset = 1
    for p in parts:
        lines.extend(['o '+p.name, 'usemtl '+p.material])
        lines.extend('v '+' '.join(f'{x:.8f}' for x in v) for v in p.positions)
        lines.extend('vn '+' '.join(f'{x:.8f}' for x in n) for n in p.normals)
        for tri in np.asarray(p.indices).reshape(-1, 3):
            lines.append('f '+' '.join(f'{i+offset}//{i+offset}' for i in tri))
        offset += len(p.positions)
    (ROOT/'models'/f'{name}.obj').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    mtl = []
    for key, (color, metal, rough) in MATERIALS.items():
        rgb = [int(color[i:i+2], 16)/255 for i in (1, 3, 5)]
        mtl.extend(['newmtl '+key, 'Kd '+' '.join(map(str, rgb)),
                    'Ks 0.25 0.25 0.25', 'Ns 35', f'Pm {metal}', f'Pr {rough}'])
    (ROOT/'models'/f'{name}.mtl').write_text('\n'.join(mtl)+'\n', encoding='utf-8')


if __name__ == '__main__':
    for path in [ROOT/'models', ROOT/'previews', PROJECT/'assets/weapons/ammo']:
        path.mkdir(parents=True, exist_ok=True)
    parts = model()
    result = [export_glb('bullet_ak', parts),
              export_glb('bullet_projectile', [parts[1]], (0, 0, .034)),
              export_glb('bullet_casing', [parts[0], parts[2]])]
    export_obj(parts)
    triple = three_horizontal(parts)
    result.append(export_glb('bullet_ak_triple', triple, forward='+X', pivot='ground center'))
    export_obj(triple, 'bullet_ak_triple')
    (ROOT/'manifest.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, indent=2))
