"""Blender offline authoring. Metres: X across, Y toward bridge, Z up.
Run: blender --background --python ArtSource/BridgeTerminal/build.py
Exports separate cabinet / lever meshes; Unity owns CutePixel text and fill.
"""
import bpy, math, json, random, os, wave, struct
from mathutils import Vector
ROOT='X:/Work/duck-rails'
OUT=ROOT+'/Assets/DuckRails/BridgeTerminal'
SRC=ROOT+'/ArtSource/BridgeTerminal'
os.makedirs(OUT,exist_ok=True)
scene=bpy.data.scenes.new('Bridge Terminal'); bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
asset=bpy.data.collections.new('Terminal export'); scene.collection.children.link(asset)
colors=[(.23,.25,.12),(.34,.29,.16),(.065,.075,.065),(.67,.095,.035),(.87,.57,.07),(.12,.14,.13),(.45,.42,.30),(.98,.38,.015)]
rng=random.Random(47)
img=bpy.data.images.new('Terminal pixels',width=512,height=64)
pixels=[]
# Low-contrast 4px clusters; sparse oxidised chips. No image-generation dependency.
patches=[[rng.uniform(.91,1.09) for _ in range(16*16)] for _ in colors]
for y in range(64):
    for x in range(512):
        c=x//64; v=patches[c][(y//4)*16+(x%64)//4]
        col=colors[c]
        if c in (0,1) and ((x%64)//2*7+(y//2)*13)%103==0: col=(.39,.23,.08)
        pixels.extend([*(min(1,a*v) for a in col),1])
img.pixels=pixels; img.filepath_raw=OUT+'/TerminalPixels.png';img.file_format='PNG';img.save()
mat=bpy.data.materials.new('Terminal pixel atlas');mat.use_nodes=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.82
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img
tex.interpolation=next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items if i.identifier=='Closest')
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
groups={}
def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def finish(o,g,c,bevel=0):
    for co in list(o.users_collection):co.objects.unlink(o)
    asset.objects.link(o)
    if bevel:
        active(o);m=o.modifiers.new('Machined edge','BEVEL');m.width=bevel;m.segments=1;bpy.ops.object.modifier_apply(modifier=m.name)
    o.data.materials.append(mat)
    uv=o.data.uv_layers.active or o.data.uv_layers.new()
    for p in o.data.polygons:
        p.use_smooth=False
        n=p.normal;axes=sorted(range(3),key=lambda a:abs(n[a]))[:2]
        for li in p.loop_indices:
            v=o.data.vertices[o.data.loops[li].vertex_index].co
            u=(v[axes[0]]*48+32)%62+1;w=(v[axes[1]]*48+32)%62+1
            uv.data[li].uv=((64*c+u)/512,w/64)
    groups.setdefault(g,[]).append(o);return o
def box(g,name,pos,size,c,b=.005,tilt=False):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if tilt:o.rotation_euler.x=math.atan(1/3)
    return finish(o,g,c,b)
def cyl(g,name,pos,r,depth,c,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=depth,location=pos,rotation=rot)
    o=bpy.context.object;o.name=name;return finish(o,g,c,.002)
def mesh(g,name,v,f,c,b=0):
    me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);asset.objects.link(o);return finish(o,g,c,b)
def height(y):return .23+y/3
def surface(x,y,lift=0):return (x,y,height(y)+lift)
# Substantial shallow wedge; front fascia only 8 cm. No wire or pedestal.
verts=[(x,y,z) for z in [0,1] for y in [-.45,.45] for x in [-.48,.48]]
verts=[(x,y,(height(y) if z else .012)) for x,y,z in verts]
mesh('Cabinet','Enclosed olive wedge',verts,[(0,2,3,1),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5),(4,5,7,6)],0,.012)
box('Cabinet','Foot flange',(0,0,.017),(1,.94,.034),5,.01)
# Top perimeter seams and raised frame.
for x in [-.455,.455]:box('Cabinet','Side edge',surface(x,0,.013),(.022,.9,.018),1,.004,True)
box('Cabinet','Back edge',surface(0,.425,.013),(.89,.025,.018),1,.004,True)
box('Cabinet','Front lip',(0,-.449,.065),(.93,.022,.055),1,.004)
for i in range(9):
    x=-.405+i*.1
    mesh('Cabinet','Hazard stripe',[(x,-.462,.04),(x+.05,-.462,.04),(x+.085,-.462,.087),(x+.035,-.462,.087)],[(0,1,2,3)],4)
# Upper dashboard display, slight right bias; label and bar installed in Unity.
box('Cabinet','Screen bezel',surface(.075,.245,.022),(.60,.32,.035),1,.014,True)
box('Cabinet','Inset dark screen',surface(.075,.245,.043),(.53,.258,.009),2,.008,True)
for x in [-.205,.355]:
    for y in [.105,.385]:cyl('Cabinet','Screen bolt',surface(x,y,.05),.012,.008,6, (math.atan(1/3),0,0))
cyl('Cabinet','Lamp collar',surface(-.335,.24,.025),.055,.025,1,(math.atan(1/3),0,0))
cyl('Cabinet','Amber lamp',surface(-.335,.24,.049),.038,.029,7,(math.atan(1/3),0,0))
# Fore/aft lever gate, front enough to reach without leaning over display.
box('Cabinet','Lever gate rim',surface(0,-.22,.013),(.28,.37,.022),1,.008,True)
box('Cabinet','Dark lever slot',surface(0,-.22,.026),(.085,.32,.009),2,.003,True)
for x in [-.12,.12]:
    for y in [-.37,-.07]:cyl('Cabinet','Gate bolt',surface(x,y,.04),.012,.009,6,(math.atan(1/3),0,0))
for x in [-.43,.43]:
    for y in [-.4,.4]:cyl('Cabinet','Case bolt',surface(x,y,.026),.014,.009,6,(math.atan(1/3),0,0))
for side in [-1,1]:
    for y in [.12,.20,.28]:box('Cabinet','Side vent',(side*.481,y,.115),(.003,.045,.025),2,0)
# Lever exported in handle-local coordinates: pivot .26 below handle origin,
# grip .08 above it, matching the actual train grip and collider conventions.
box('Lever','Steel lever stem',(0,0,-.11),(.045,.045,.30),6,.006)
cyl('Lever','Red handgrip',(0,0,.08),.038,.32,3,(0,math.pi/2,0))
for x in [-.16,.16]:cyl('Lever','Grip end cap',(x,0,.08),.041,.014,3,(0,math.pi/2,0))
for x in [-.11,.11]:cyl('Lever','Grip seam',(x,0,.08),.039,.007,1,(0,math.pi/2,0))
# Export evaluated Blender meshes in Unity metres; no importer axis/scale ambiguity.
data={'parts':[]}
for group,objects in groups.items():
    vs=[];uvs=[];tris=[]
    for o in objects:
        me=o.data;me.calc_loop_triangles()
        for tri in me.loop_triangles:
            start=len(vs)
            for li in tri.loops:
                v=o.matrix_world@me.vertices[me.loops[li].vertex_index].co
                vs.append({'x':v.x,'y':v.z,'z':v.y});u=me.uv_layers.active.data[li].uv;uvs.append({'x':u.x,'y':u.y})
            tris.extend([start,start+2,start+1])
    data['parts'].append({'name':group,'vertices':vs,'uv':uvs,'triangles':tris})
with open(SRC+'/terminal-meshes.json','w') as f:json.dump(data,f,separators=(',',':'))
# Preview pose matches Unity neutral (-35 degrees). Preview stage excluded from export.
for o in groups['Lever']:
    from mathutils import Matrix
    o.matrix_world=Matrix.Translation((0,-.22,.17))@Matrix.Rotation(math.radians(35),4,'X')@Matrix.Translation((0,0,.26))@o.matrix_world
world=bpy.data.worlds.new('Review world');scene.world=world;world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.15,.18,.22,1)
floor=box('Preview','Review floor',(0,0,-.035),(3,3,.05),5,0)
for name,pos,power,size in [('Key',(-2,-3,4),350,3),('Fill',(2,0,2),180,2)]:
    ld=bpy.data.lights.new(name,'AREA');ld.energy=power;ld.shape='DISK';ld.size=size;o=bpy.data.objects.new(name,ld);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.2))-o.location).to_track_quat('-Z','Y').to_euler()
camd=bpy.data.cameras.new('Review camera');cam=bpy.data.objects.new('Review camera',camd);scene.collection.objects.link(cam);scene.camera=cam
cam.location=(1.25,-1.8,1.35);cam.rotation_euler=(Vector((0,0,.18))-cam.location).to_track_quat('-Z','Y').to_euler();camd.type='ORTHO';camd.ortho_scale=1.65
try:scene.render.engine='CYCLES'
except TypeError:pass
scene.cycles.samples=24;scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.render.image_settings.file_format=next(i.identifier for i in scene.render.image_settings.bl_rna.properties['file_format'].enum_items if i.identifier=='PNG')
scene.render.filepath=ROOT+'/output/bridge-terminal/blender-preview.png'
img.pack();bpy.ops.wm.save_as_mainfile(filepath=SRC+'/BridgeTerminal.blend')
bpy.ops.render.render(write_still=True)
# Mechanical audio is user supplied: Assets/Audio/Bridge/BridgeLower_Loop.wav
print('TERMINAL_EXPORT',[(p['name'],len(p['triangles'])//3) for p in data['parts']])
