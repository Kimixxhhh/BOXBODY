"""Bundle only the four original vehicles and their reproducible source."""
from pathlib import Path
import json, zipfile

root=Path(__file__).resolve().parents[1]
destination=root/'Lowpoly_Cars_01.zip'
with zipfile.ZipFile(destination,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as archive:
    for path in sorted(root.rglob('*')):
        relative=path.relative_to(root)
        if not path.is_file() or path.suffix in {'.zip','.pyc','.log'}:continue
        if any(p in {'.godot','__pycache__'} for p in relative.parts):continue
        archive.write(path,relative.as_posix())
with zipfile.ZipFile(destination) as archive:
    assert archive.testzip() is None
    glbs=sorted(p for p in archive.namelist() if p.endswith('.glb'))
    assert glbs==['models/hatchback.glb','models/pickup.glb','models/sedan.glb','models/van.glb']
    manifest=json.loads(archive.read('manifest.json'))
    assert len(manifest['models'])==4
    print(f'Package verified: {len(archive.namelist())} files / {destination.stat().st_size:,} bytes')
    print('\n'.join(glbs))
