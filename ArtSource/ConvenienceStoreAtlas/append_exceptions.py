"""Full-block visual context only; these meshes never replace the retained Unity originals."""
import bpy, json
import numpy as np
from pathlib import Path
BASE=Path('X:/Work/duck-rails/ArtSource/ConvenienceStoreAtlas')
data=json.loads((BASE/'exceptions.json').read_text())
scene=bpy.data.scenes['ConvenienceStore full map - atlas review']
bpy.context.window.scene=scene
name='RETAINED EXCEPTIONS - reference pose, Unity owns animation and text'
if name in bpy.data.collections: raise RuntimeError('Do not overwrite existing exception review edits')
collection=bpy.data.collections.new(name);scene.collection.children.link(collection)
def linear(v):
    v=np.asarray(v);return np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
def texture(payload,name):
    if not payload:return None
    im=bpy.data.images.new(name,width=payload['width'],height=payload['height'],alpha=True,float_buffer=True)
    im.colorspace_settings.name='Non-Color'
    pixels=np.array(payload['pixels'],dtype=np.float32)
    if not payload['linear']:pixels[:,:3]=linear(pixels[:,:3])
    im.pixels.foreach_set(pixels.ravel());im.pack();return im
materials=[]
for i,m in enumerate(data['materials']):
    mat=bpy.data.materials.new('Retained '+m['name']);mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*linear(m['color'][:3]),1)
    bs.inputs['Metallic'].default_value=m['metallic'];bs.inputs['Roughness'].default_value=1-m['smoothness']
    bs.inputs['Alpha'].default_value=m['color'][3]
    for key,socket,multiplier in [('basePixels','Base Color',linear(m['color'][:3])),('emissionPixels','Emission Color',m['emission'])]:
        image=texture(m.get(key),'Retained '+m['name']+' '+key)
        if image:
            tex=nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Closest';tex.extension='EXTEND'
            tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(*multiplier,1)
            links.new(tex.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],bs.inputs[socket])
        elif key=='emissionPixels':bs.inputs[socket].default_value=(*m['emission'],1)
    bs.inputs['Emission Strength'].default_value=1
    materials.append(mat)
for p in data['parts']:
    verts=[(v[0],v[2]-80,v[1]) for v in p['vertices']]
    faces=[(p['triangles'][i],p['triangles'][i+2],p['triangles'][i+1]) for i in range(0,len(p['triangles']),3)]
    mesh=bpy.data.meshes.new('Retained '+p['name']);mesh.from_pydata(verts,[],faces);mesh.update()
    if len(p['uv'])==len(verts):
        uv=mesh.uv_layers.new(name='Original UV')
        for loop in mesh.loops:uv.data[loop.index].uv=p['uv'][loop.vertex_index]
    if len(p['normals'])==len(verts):mesh.normals_split_custom_set_from_vertices([(n[0],n[2],n[1]) for n in p['normals']])
    mesh.materials.append(materials[p['material']])
    ob=bpy.data.objects.new('Retained '+p['name'],mesh);collection.objects.link(ob)
    ob['Source key']=p['sourceKey'];ob['Unity hierarchy']=p['path'];ob['Reference only']='Original Unity renderer retained; brushes shown at saved pose. TMP remains Unity-only.'
for ob in bpy.context.selected_objects:ob.select_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'ConvenienceStore-FullMap-Atlas.blend'))
print('Retained exception reference parts:',len(data['parts']))
