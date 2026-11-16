"""Blender static checks and front/rear evidence, no Unity gameplay."""
import bpy, json, math
from mathutils import Vector, Matrix
OUT='X:/Work/duck-rails/ArtSource/RadioConsole'
s=bpy.context.scene
parts=list(bpy.data.collections['PA Console - export'].objects)
assert len(parts)==9
assert all(o.type=='MESH' for o in parts)
assert all(len(o.data.uv_layers)==1 and o.data.uv_layers[0].name=='PaletteUV' for o in parts)
assert all(all(math.isfinite(c) for c in v.co) for o in parts for v in o.data.vertices)
assert all(len(o.data.materials)==1 for o in parts)
assert len({o.data.materials[0] for o in parts})==1
assert abs(bpy.data.objects['Shelf'].dimensions.x-1.72)<1e-5
label=bpy.data.objects['Label']
assert all(abs(uv.uv.x-3.5/8)<1e-5 for uv in label.data.uv_layers.active.data)
assert .2<label.dimensions.x<.35
assert bpy.data.objects['TalkButton'].get('suggested_travel_metres')==.012
angle=math.atan(.075/.37)
normal=Matrix.Rotation(angle,4,'X')@Vector((0,0,1))
anchor=Vector((.46,-.035,.475+.165/.37*.075))
cap=bpy.data.objects['TalkButton']
orange=[];icon=[]
for poly in cap.data.polygons:
    swatch=cap.data.uv_layers.active.data[poly.loop_start].uv.x
    dest=orange if abs(swatch-4.5/8)<1e-5 else icon
    dest.extend((cap.matrix_world@cap.data.vertices[i].co-anchor).dot(normal) for i in poly.vertices)
assert abs(max(orange)-.0725)<1e-5
assert min(icon)-max(orange)>.0008, 'Icon intersects the orange cap'
for name in ['TalkButton','ButtonMount']:
    obj=bpy.data.objects[name]
    # Broad octagonal top faces must share the base's slope normal.
    tops=[p for p in obj.data.polygons if len(p.vertices)==8 and (obj.matrix_world.to_3x3()@p.normal).dot(normal)>.9]
    assert tops and all((obj.matrix_world.to_3x3()@p.normal).normalized().dot(normal)>.999999 for p in tops), name
cable=bpy.data.objects['Cable']
assert max((cable.matrix_world@v.co).y for v in cable.data.vertices)<.212
assert min((cable.matrix_world@v.co).z for v in cable.data.vertices)>.42
stats=json.load(open(OUT+'/model-checks.json'))
stats['static_checks']='PASS: 9 mesh parts, shared palette, consistent UVs, finite vertices, shelf width, cream label, button metadata, aligned collar/cap normals and icon clearance >0.8 mm.'
s.render.filepath=OUT+'/preview.png';bpy.ops.render.render(write_still=True)
cam=s.camera;loc=cam.location.copy();rot=cam.rotation_euler.copy();scale=cam.data.ortho_scale
cam.location=(.15,.7,3.4);cam.rotation_euler=(Vector((0,.07,.53))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=1.95
refs=[o for o in bpy.data.collections['Train wall reference - preview only'].objects if o.type=='MESH']
for o in refs:o.hide_render=True
s.render.filepath=OUT+'/rear-cable.png';bpy.ops.render.render(write_still=True)
for o in refs:o.hide_render=False
cam.location=loc;cam.rotation_euler=rot;cam.data.ortho_scale=scale;s.render.filepath=OUT+'/preview.png'
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['TalkButton']
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/RadioConsole.blend')
# Re-import only into a temporary scene to verify exported geometry survived.
test=bpy.data.scenes.new('FBX validation');bpy.context.window.scene=test
bpy.ops.import_scene.fbx(filepath=OUT+'/RadioConsole.fbx')
meshes=[o for o in test.objects if o.type=='MESH'];assert len(meshes)==9
tri=0
for o in meshes:o.data.calc_loop_triangles();tri+=len(o.data.loop_triangles)
assert tri==stats['triangles'], (tri,stats['triangles'])
stats['fbx_roundtrip']='PASS: 9 mesh components, matching triangle count.'
with open(OUT+'/model-checks.json','w') as f:json.dump(stats,f,indent=2)
print(json.dumps(stats,indent=2))
