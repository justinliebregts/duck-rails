"""Rebuild the approved modular cockpit. Run with Blender --background --python."""
import bpy, math, json, os
from mathutils import Vector
ROOT = 'X:/Work/duck-rails'
OUT = ROOT+'/Assets/DuckRails/Train/CockpitV2'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
colors=[(.10,.115,.13,1),(.035,.04,.045,1),(.38,.12,.045,1),(.95,.48,.025,1),(.82,.74,.52,1),(.95,.84,.57,1)]
img=bpy.data.images.new('Cockpit palette',width=96,height=16)
img.pixels=[v for y in range(16) for x in range(96) for v in colors[x//16]]
img.filepath_raw=OUT+'/Palette.png'; img.file_format='PNG'; img.save()
mat=bpy.data.materials.new('Cockpit palette');mat.use_nodes=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bs.inputs['Roughness'].default_value=.9
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
groups={}
def cube(group, name, pos, size, color, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=(pos[0],-pos[2],pos[1]))
    o=bpy.context.object;o.name=name;o.dimensions=(size[0],size[2],size[1]);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new('Single facet bevel','BEVEL');m.width=bevel;m.segments=1
        bpy.ops.object.modifier_apply(modifier=m.name)
    finish(o,group,color)
    return o
def finish(o,g,c):
    o.data.materials.append(mat)
    uv=o.data.uv_layers.active or o.data.uv_layers.new()
    for p in o.data.polygons:
        p.use_smooth=False
        for i in p.loop_indices: uv.data[i].uv=((c+.5)/6,.5)
    groups.setdefault(g,[]).append(o)
def cyl(g,name,pos,r,depth,c):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=depth,location=(pos[0],-pos[2],pos[1]))
    o=bpy.context.object;o.name=name;finish(o,g,c)
# Base built from rails: three genuine open fore/aft slots above black recesses.
cube('ThrottleBase','Recess floor',(0,-.063,0),(.36,.015,.34),1,.006)
for x in [-.17,-.057,.057,.17]: cube('ThrottleBase','Slot divider',(x,-.035,0),(.045,.055,.34),0,.006)
for z in [-.162,.162]: cube('ThrottleBase','End bridge',(0,-.035,z),(.36,.055,.022),0,.005)
for x in [-.113,0,.113]:
    cube('ThrottleMoving','Linked arm',(x,-.09,0),(.021,.34,.026),0,.003)
    for z in [-.095,0,.095]: cube('ThrottleBase','Travel tick',(x+.037,-.005,z),(.012,.002,.006),5)
cube('ThrottleMoving','Single solid grip',(0,.08,0),(.36,.065,.075),2,.014)
cube('HornBase','Mounting plate',(0,.006,0),(.245,.025,.23),0,.014)
cyl('HornBase','Button collar',(0,.027,0),.105,.025,1)
cyl('HornCap','Amber eight sided cap',(0,.05,0),.09,.055,3)
# Cream trumpet pictogram on the moving cap.
points=[(-.052,-.012),(-.018,-.012),(.019,-.036),(.019,.036),(-.018,.012),(-.052,.012)]
me=bpy.data.meshes.new('Horn pictogram');me.from_pydata([(x,-z,.078) for x,z in points],[],[list(reversed(range(len(points))))])
o=bpy.data.objects.new('Horn pictogram',me);bpy.context.collection.objects.link(o);finish(o,'HornCap',5)
for z in [-.029,0,.029]:cube('HornCap','Sound ray',(.043,.079,z),(.022,.002,.005),5)
for x in [-.097,.097]:
    for z in [-.088,.088]: cube('HornBase','Square bolt',(x,.024,z),(.018,.012,.018),4,.002)
cube('DistanceBase','Distance bezel',(0,0,0),(.68,.025,.28),0,.012)
cube('DistanceBase','Distance inset',(0,.016,0),(.63,.01,.23),1,.006)
cube('Card','Paper',(0,0,0),(.55,.003,.26),4)
for x,z in [(-.24,.11),(.24,-.11)]: cube('Tape','Tape',(x,.004,z),(.11,.004,.035),4)
# Export each component with origin at its integration anchor; split faces preserve flat normals.
parts=[]
for name,obs in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=obs[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    o.data.calc_loop_triangles();vs=[];uvs=[];tris=[]
    for tri in o.data.loop_triangles:
        for li in tri.loops:
            v=o.matrix_world @ o.data.vertices[o.data.loops[li].vertex_index].co
            vs.append(dict(x=v.x,y=v.z,z=-v.y));u=o.data.uv_layers.active.data[li].uv
            uvs.append(dict(x=u.x,y=u.y));tris.append(len(vs)-1)
    parts.append(dict(name=name,vertices=vs,uv=uvs,triangles=tris))
with open(OUT+'/geometry.json','w') as f:json.dump(dict(parts=parts),f)
# FBX is a component library with local anchors. Arrange the editable source for viewing afterwards.
bpy.ops.export_scene.fbx(filepath=OUT+'/Cockpit.fbx',use_selection=False,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False)
for name,pos in {'ThrottleBase':(.05,.295,-.035),'ThrottleMoving':(.05,.375,-.035),'HornBase':(.55,.25,.015),'HornCap':(.55,.25,.015),'DistanceBase':(-1.2,.25,.025),'Card':(1.14,.25,.025),'Tape':(1.14,.25,.025)}.items():
    bpy.data.objects[name].location=(pos[0],-pos[2],pos[1])
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Cockpit/Cockpit.blend')
print('EXPORTED',len(parts),'parts',sum(len(p['triangles'])//3 for p in parts),'triangles')
