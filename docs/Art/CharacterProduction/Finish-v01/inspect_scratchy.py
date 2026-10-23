import bpy
import gzip
import json
import sys
from pathlib import Path
from mathutils import Vector

sys.path.insert(0, 'C:/Backforge/Tripo3D/work')
from character_face import components

source = 'C:/Backforge/Tripo3D/work/cast_v05/sources/Scratchy_polish.blend'
bpy.ops.wm.open_mainfile(filepath=source)
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
parts = []
component_ids = {}
for part_id, part in enumerate(components(mesh)):
    for vertex in part:
        component_ids[vertex] = part_id
    center = sum((points[i] for i in part), Vector()) / len(part)
    parts.append(dict(id=part_id, count=len(part), center=list(center),
                      minimum=[min(points[i][a] for i in part) for a in range(3)],
                      maximum=[max(points[i][a] for i in part) for a in range(3)]))
output = Path(__file__).parent
(output / 'scratchy-components.json').write_text(json.dumps(sorted(parts, key=lambda p:-p['count']), indent=2))
data = dict(points=[list(p) for p in points],
            polygons=[dict(indices=list(p.vertices), uvs=[list(mesh.data.uv_layers.active.data[i].uv) for i in p.loop_indices], material=p.material_index, component=component_ids[p.vertices[0]]) for p in mesh.data.polygons])
with gzip.open(output / 'scratchy-uv-world.json.gz', 'wt', encoding='utf-8') as stream:
    json.dump(data, stream)
print('INSPECT', mesh.name, 'parts', len(parts), 'bounds', [list(mesh.matrix_world @ Vector(b)) for b in mesh.bound_box])
print('PARTS', json.dumps(sorted(parts, key=lambda p:-p['count'])[:15]))
