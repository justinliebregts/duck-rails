import bpy, json, os
base = 'X:/Work/duck-rails'
data = json.load(open(base+'/output/train-model/unity-reference.json'))
col = bpy.data.collections.get('Train Working Model')
if col is None:
    col = bpy.data.collections.new('Train Working Model')
    bpy.context.scene.collection.children.link(col)
assert len(col.objects) == 0, 'Reference already imported'
mats = {}
for src in data['materials']:
    mat = bpy.data.materials.new(os.path.basename(src['path']).replace('.mat',''))
    mat.use_nodes = True
    mat['unity_material'] = src['path']
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = 0.9
    bsdf.inputs['Base Color'].default_value = tuple(src['color'][k] for k in ('r','g','b','a'))
    for key, socket in [('texture','Base Color'), ('emission','Emission Color')]:
        if src[key] and os.path.isfile(base+'/'+src[key]):
            tex = nodes.new('ShaderNodeTexImage')
            tex.image = bpy.data.images.load(base+'/'+src[key], check_existing=True)
            tex.interpolation = next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items if i.identifier.lower() == 'closest')
            mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs[socket])
            if key == 'emission':
                bsdf.inputs['Emission Strength'].default_value = max(src['emissionColor'][k] for k in ('r','g','b'))
    mats[src['path']] = mat
for src in data['parts']:
    verts = [(v['x'],v['z'],v['y']) for v in src['vertices']]
    faces, slots = [], []
    for idx, sub in enumerate(src['submeshes']):
        t = sub['triangles']
        faces.extend([(t[i],t[i+2],t[i+1]) for i in range(0,len(t),3)])
        slots.extend([idx]*(len(t)//3))
    mesh = bpy.data.meshes.new(src['path'].split('/')[-1])
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(src['path'].split('/')[-1], mesh)
    col.objects.link(obj)
    obj['unity_path'], obj['unity_mesh'] = src['path'], src['mesh']
    for sub in src['submeshes']:
        mesh.materials.append(mats[sub['material']])
    uv = mesh.uv_layers.new(name='UVMap')
    for poly, idx in zip(mesh.polygons,slots):
        poly.material_index = idx
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            if vi < len(src['uv']):
                uv.data[li].uv = (src['uv'][vi]['x'],src['uv'][vi]['y'])
for obj in bpy.context.scene.objects:
    if obj not in list(col.objects):
        obj.hide_set(True)
        obj.hide_render = True
for obj in bpy.context.selected_objects:
    obj.select_set(False)
for obj in col.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = col.objects[0]
print('Imported', len(col.objects), 'parts')
