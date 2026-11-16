"""Approved PA console. Run in Blender; creates an isolated scene, never clears others.
Source axes: X across shelf, +Y toward wall, Z up; metres.
Unity placement mapping: (1.64 + y, z, 2 - x), measured from current train.
"""
import bpy, math, json, os
from mathutils import Vector, Matrix
ROOT = 'X:/Work/duck-rails'
OUT = ROOT + '/ArtSource/RadioConsole'
os.makedirs(OUT, exist_ok=True)
scene = bpy.data.scenes.new('Radio Console Review')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
asset = bpy.data.collections.new('PA Console - export')
stage = bpy.data.collections.new('Train wall reference - preview only')
scene.collection.children.link(asset)
scene.collection.children.link(stage)
groups = {}
colors = [(0.12,.29,.32,1),(.16,.19,.21,1),(.065,.075,.08,1),(.91,.84,.61,1),(1,.42,.025,1),(1,.67,.035,1),(.39,.24,.13,1),(.25,.16,.10,1)]
palette = bpy.data.images.new('PA Palette',width=128,height=16)
palette.pixels = [v for y in range(16) for x in range(128) for v in colors[x//16]]
palette.filepath_raw=OUT+'/Palette.png'; palette.file_format='PNG';palette.save()
mat=bpy.data.materials.new('PA shared palette');mat.use_nodes=True
bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bs.inputs['Roughness'].default_value=.87
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=palette
tex.interpolation=next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items if i.identifier=='Closest')
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])

def enum_set(obj, prop, wanted):
    items=obj.bl_rna.properties[prop].enum_items
    setattr(obj,prop,next(i.identifier for i in items if i.identifier==wanted))

def activate(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def finish(o,g,c):
    for col in list(o.users_collection):col.objects.unlink(o)
    (stage if g=='STAGE' else asset).objects.link(o)
    o.data.materials.clear();o.data.materials.append(mat)
    uv=o.data.uv_layers.active or o.data.uv_layers.new(name='PaletteUV')
    uv.name='PaletteUV'
    for p in o.data.polygons:
        p.use_smooth=False
        for li in p.loop_indices:uv.data[li].uv=((c+.5)/8,.5)
    if g!='STAGE':groups.setdefault(g,[]).append(o)
    return o

def bevel(o,width):
    if width:
        activate(o);m=o.modifiers.new('Single facet chamfers','BEVEL');m.width=width;m.segments=1
        bpy.ops.object.modifier_apply(modifier=m.name)

def box(g,name,pos,size,c,b=0):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name
    o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    bevel(o,b);return finish(o,g,c)

def mesh(g,name,verts,faces,c,b=0):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);asset.objects.link(o);bevel(o,b);return finish(o,g,c)

def cyl(g,name,pos,r,depth,c,rotation=(0,0,0),b=0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=r,depth=depth,location=pos,rotation=rotation)
    o=bpy.context.object;o.name=name;bevel(o,b);return finish(o,g,c)

def grille(g,name,x,y,z,w,h,cols,rows,c):
    # Actual holes through a chamfered plate, with dark cavity behind it.
    plate=box(g,name,(x,y,z),(w,.009,h),c,.008)
    cuts=[]
    for i in range(cols):
        for j in range(rows):
            bpy.ops.mesh.primitive_cube_add(size=1,location=(x+(i-(cols-1)/2)*w/(cols+.7),y,z+(j-(rows-1)/2)*h/(rows+.8)))
            o=bpy.context.object;o.dimensions=(w/(cols+.7)*.47,.06,h/(rows+.8)*.27)
            bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);cuts.append(o)
    bpy.ops.object.select_all(action='DESELECT')
    for o in cuts:o.select_set(True)
    bpy.context.view_layer.objects.active=cuts[0];bpy.ops.object.join();cut=bpy.context.object
    activate(plate);mod=plate.modifiers.new('Real grille apertures','BOOLEAN');enum_set(mod,'operation','DIFFERENCE');mod.object=cut
    bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
    # Boolean adds loops: assign palette coordinates again without double grouping.
    for layer in list(plate.data.uv_layers):
        if layer.name!='PaletteUV':plate.data.uv_layers.remove(layer)
    for p in plate.data.polygons:
        for li in p.loop_indices:plate.data.uv_layers.active.data[li].uv=((c+.5)/8,.5)
    return plate

# Shelf spans the measured 1.74 m gap with 10 mm clearance per end.
box('Shelf','Steel shelf',(0,0,.393),(1.72,.48,.054),1,.018)
box('Shelf','Yellow front lip',(0,-.235,.388),(1.72,.036,.065),5,.012)
box('Shelf','Rear fixing strip',(0,.226,.415),(1.70,.028,.13),1,.008)
for x in [-.70,.70]:
    verts=[(x+dx,y,z) for dx in [-.018,.018] for y,z in [(-.19,.366),(.225,.366),(.225,.075)]]
    mesh('Supports','Triangular wall bracket',verts,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],1,.005)
    # A single bolt on each visible front-facing mounting shoe.
    box('Supports','Bracket mounting shoe',(x,.209,.18),(.12,.035,.21),1,.004)
    box('Supports','Bracket bolt',(x+(-.038 if x<0 else .038),.186,.18),(.021,.013,.021),3,.004)

# Broad speaker, with thin inset frame and real slotted cream grille.
sx,sy,sz=-.36,.025,.642
box('Speaker','Speaker teal shell',(sx,sy,sz),(.70,.31,.444),0,.028)
box('Speaker','Speaker charcoal frame',(sx,-.139,sz),(.635,.025,.378),1,.020)
box('Speaker','Dark acoustic cavity',(sx,-.155,sz),(.566,.009,.302),2,.012)
grille('Speaker','Cream grille',sx,-.168,sz,.57,.31,7,5,3)
for dx,dz in [(-.294,.165),(.294,-.165)]:
    box('Speaker','Speaker bolt',(sx+dx,-.159,sz+dz),(.023,.015,.023),3,.004)

# Base wedge: top rises toward its rear, making the cap face the player.
mx=.46
verts=[(mx+x,y,z) for zset in [0,1] for x,y,z in
       [(-.205,-.20,.42 if zset==0 else .475),(.205,-.20,.42 if zset==0 else .475),(.205,.17,.42 if zset==0 else .55),(-.205,.17,.42 if zset==0 else .55)]]
mesh('MicBase','Sloped teal microphone base',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],0,.015)
angle=math.atan(.075/.37)
rot=Matrix.Rotation(angle,4,'X')
anchor=Vector((mx,-.035,.475+(.165/.37)*.075))
def topbox(g,name,offset,size,c,b=0):
    o=box(g,name,anchor+rot@Vector(offset),size,c,b);o.rotation_euler[0]=angle;return o
def topcyl(g,name,z,r,depth,c,b=0):
    o=cyl(g,name,anchor+rot@Vector((0,0,z)),r,depth,c,b=b)
    # Spin around the cylinder's local normal BEFORE placing it on the slope.
    # Euler (angle, 0, spin) instead spins the slope normal around world Z.
    o.rotation_euler=(rot@Matrix.Rotation(math.pi/8,4,'Z')).to_euler()
    return o
topbox('ButtonMount','Charcoal button plate',(0,0,.008),(.285,.255,.018),1,.012)
topcyl('ButtonMount','Dark octagonal collar',.026,.123,.027,2,.004)
topcyl('TalkButton','Orange talk cap',.051,.110,.043,4,.005)
for dx,dy in [(-.119,.102),(.119,-.102)]:
    topbox('ButtonMount','Button plate bolt',(dx,dy,.023),(.018,.018,.012),3,.003)
# Cream pixel microphone pictogram, centered on the physical moving cap.
pixels=['000111000','001111100','001111100','001111100','001111100','101111101','100111001','010000010','001111100','000010000','000010000','001111100']
step=.008
for row,line in enumerate(pixels):
    # Merge adjacent horizontal pixels into strips.
    start=None
    for col,ch in enumerate(line+'0'):
        if ch=='1' and start is None:start=col
        elif ch=='0' and start is not None:
            topbox('TalkButton','Cream microphone pixel stroke',((start+(col-start)/2-4.5)*step,(5.5-row)*step,.074),((col-start)*step,step,.0012),3)
            start=None

box('Microphone','Stem',(mx,.112,.655),(.044,.046,.25),1,.003)
box('Microphone','Cream microphone head',(mx,.10,.84),(.19,.085,.205),3,.018)
box('Microphone','Microphone cavity',(mx,.053,.84),(.146,.014,.16),2,.009)
grille('Microphone','Microphone charcoal grille',mx,.042,.84,.146,.159,3,5,1)

# Actual CutePixel font, converted to mesh for a portable Blender/FBX deliverable.
bpy.ops.object.text_add(location=(mx,-.209,.449),rotation=(math.pi/2,0,0))
label=bpy.context.object;label.name='HOLD TO TALK - CutePixel';label.data.body='HOLD TO TALK'
label.data.font=bpy.data.fonts.load(ROOT+'/Assets/Fonts/CutePixel.ttf')
enum_set(label.data,'align_x','CENTER');enum_set(label.data,'align_y','CENTER')
label.data.size=.057;label.data.extrude=0;label.data.resolution_u=1
bpy.context.view_layer.update()
if label.dimensions.x>.34:label.scale*=.34/label.dimensions.x
bpy.ops.object.convert(target='MESH');finish(bpy.context.object,'Label',3)

# Both sockets are on BACK faces; cable runs entirely behind the housings.
cyl('Cable','Speaker rear plug',(sx+.23,.192,.479),.012,.028,2,(math.pi/2,0,0))
cyl('Cable','Mic base rear plug',(mx-.12,.184,.46),.011,.028,2,(math.pi/2,0,0))
points=[(sx+.23,.206,.479),(-.10,.202,.455),(-.035,.196,.438),(.04,.191,.432),(.13,.190,.432),(.23,.193,.435),(.31,.196,.443),(mx-.12,.198,.46)]
curve=bpy.data.curves.new('Rear cable path','CURVE');curve.dimensions='3D';curve.resolution_u=1
curve.bevel_depth=.0055;curve.bevel_resolution=0;curve.resolution_u=1
sp=curve.splines.new('POLY');sp.points.add(len(points)-1)
for p,co in zip(sp.points,points):p.co=(*co,1)
o=bpy.data.objects.new('Short rear cable',curve);asset.objects.link(o);activate(o);bpy.ops.object.convert(target='MESH');finish(bpy.context.object,'Cable',2)

# Join by editable functional component; keep button origin on its press axis.
parts=[]
for name,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();o=bpy.context.object;o.name=name
    o.data.materials.clear();o.data.materials.append(mat)
    for polygon in o.data.polygons:polygon.material_index=0
    bpy.context.scene.cursor.location=anchor if name=='TalkButton' else (0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR');bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    o['part']=name;o['export']=True;parts.append(o)
button=bpy.data.objects['TalkButton'];button['press_direction_blender']=[0,math.sin(angle),-math.cos(angle)];button['suggested_travel_metres']=.012

# Measured wall section as optional review context; never part of the FBX.
box('STAGE','Reference wall',(0,.296,.16),(2,.112,.32),1)
box('STAGE','Reference yellow rail',(0,.296,.35),(2,.128,.06),5)
for x in [-.95,.95]:box('STAGE','Reference yellow end post',(x,.296,.20),(.16,.152,.40),5,.004)
box('STAGE','Reference canopy upright',(-1.05,.296,.68),(.144,.144,1.42),1,.005)
for i in range(13):box('STAGE','Preview deck plank',(-1.56+i*.26,-.25,-.055),(.257,1.8,.05),6 if i%3 else 7)

world=bpy.data.worlds.new('PA neutral world');world.use_nodes=True;scene.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.17,.21,.25,1);bg.inputs['Strength'].default_value=.6
def point_at(o,at):o.rotation_euler=(Vector(at)-o.location).to_track_quat('-Z','Y').to_euler()
for name,pos,power,size in [('Key',(-2,-3,4),220,4),('Fill',(3,-1,2),100,3),('Rim',(0,2,3),130,2)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    o=bpy.data.objects.new(name,data);stage.objects.link(o);o.location=pos;point_at(o,(0,0,.5))
data=bpy.data.cameras.new('Console camera');cam=bpy.data.objects.new('Console camera',data);stage.objects.link(cam)
cam.location=(1.25,-3.9,2.05);point_at(cam,(0,0,.48));data.type='ORTHO';data.ortho_scale=2.35;scene.camera=cam
scene.render.resolution_x=1500;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
try:scene.render.engine='BLENDER_EEVEE'
except TypeError:pass
try:scene.view_settings.view_transform='Standard'
except TypeError:pass
scene.render.filepath=OUT+'/preview.png'

bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=button
bpy.ops.export_scene.fbx(filepath=OUT+'/RadioConsole.fbx',use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
palette.pack()
summary={'shelf_width_m':1.72,'wall_clear_span_m':1.74,'end_clearance_m':.01,'shelf_depth_m':.48,'shelf_top_m':.42,'bolt_count':6,'unity_mapping':'(1.64 + Blender Y, Blender Z, 2 - Blender X)','unity_installed':False,'parts':[]}
for o in parts:
    o.data.calc_loop_triangles();summary['parts'].append({'name':o.name,'triangles':len(o.data.loop_triangles),'vertices':len(o.data.vertices)})
summary['triangles']=sum(p['triangles'] for p in summary['parts'])
with open(OUT+'/model-checks.json','w') as f:json.dump(summary,f,indent=2)
bpy.data.libraries.write(OUT+'/RadioConsole.blend',{scene},fake_user=True,compress=True)
print(json.dumps(summary,indent=2))
# Turn the isolated scene library into a normal file with a working startup UI.
# Only use this final open when the prior file was saved before running the build.
bpy.ops.wm.open_mainfile(filepath=OUT+'/RadioConsole.blend')
bpy.context.window.scene=bpy.data.scenes['Radio Console Review']
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=OUT+'/RadioConsole.blend')
