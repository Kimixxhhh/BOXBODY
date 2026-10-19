from pathlib import Path
root=Path(__file__).resolve().parents[1]
template=(root/'source'/'viewer_template.html').read_text(encoding='utf-8')
data=(root/'source'/'mesh_data.json').read_text(encoding='utf-8')
(root/'preview.html').write_text(template.replace('__ASSET_DATA__',data),encoding='utf-8')
print('Standalone preview generated.')
