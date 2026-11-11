"""Run in the reviewed YardMule-Refined.blend via Blender MCP.

Exports only Train Export, retaining separate interactive artwork in Unity.
Run Duck Rails > Art > Import Reviewed Blender Train, then rebake train navigation.
"""
import bpy
import json
from pathlib import Path

repo = Path(bpy.data.filepath).parents[2]
assert (repo / 'Assets/DuckRails/Train/Blender').is_dir(), 'Open the project review .blend first'
output = repo / 'output/train-model'


def mesh_data(obj, polygons=None):
    mesh = obj.data
    vertices, uv, triangles, slots = [], [], [], []
    for polygon in mesh.polygons if polygons is None else polygons:
        start = len(vertices)
        for index in polygon.loop_indices:
            point = obj.matrix_world @ mesh.vertices[mesh.loops[index].vertex_index].co
            # Reflection from Blender Z-up/+Y-front to Unity Y-up/+Z-front.
            vertices.append(dict(x=point.x, y=point.z, z=point.y))
            coord = mesh.uv_layers.active.data[index].uv
            uv.append(dict(x=coord.x, y=coord.y))
        for i in range(1, len(polygon.loop_indices) - 1):
            triangles.extend((start, start + i + 1, start + i))
            slots.append(polygon.material_index)
    materials = []
    for material in mesh.materials:
        path = material.get('unity_material')
        if not path:
            assert material.name == 'Furnace lip black', 'Map new material explicitly: ' + material.name
            path = 'BLACK_LIP'
        materials.append(path)
    return dict(name=obj.name, vertices=vertices, uv=uv, triangles=triangles,
                triangleMaterials=slots, materials=materials)


export = bpy.data.collections['Train Export']
assert len(export.objects) == 3
reference = next(obj for obj in bpy.data.collections['Original Unity Reference (hidden)'].objects
                 if obj.get('unity_path', '').endswith('/Dash trim'))
gauge = [polygon for polygon in reference.data.polygons
         if -.83 < polygon.center.x < -.13 and polygon.center.y < 4.65]
data = dict(visuals=[mesh_data(obj) for obj in export.objects],
            parts=[mesh_data(obj) for obj in bpy.data.collections['Train Working Model'].objects],
            railLowering=bpy.data.collections['Train Working Model'].get('rail_lowering', 0.0),
            controlTrim=mesh_data(reference, gauge))
target = repo / 'Assets/DuckRails/Train/Blender/unity-integration.json'
target.write_text(json.dumps(data), encoding='utf-8')
previous = list(bpy.context.selected_objects)
active = bpy.context.view_layer.objects.active
try:
    for obj in previous:
        obj.select_set(False)
    for obj in export.objects:
        obj.select_set(True)
    properties = bpy.ops.export_scene.fbx.get_rna_type().properties
    options = {'axis_forward': '-Z', 'axis_up': 'Y', 'path_mode': 'COPY'}
    for key, value in options.items():
        assert value in [item.identifier for item in properties[key].enum_items]
    bpy.ops.export_scene.fbx(filepath=str(output / 'YardMule-Refined.fbx'),
                             use_selection=True, object_types={'MESH'},
                             bake_anim=False, embed_textures=True, **options)
finally:
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    for obj in previous:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active
print('Exported reviewed train geometry and gauge-only control trim:', target)
