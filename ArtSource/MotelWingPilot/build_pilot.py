"""Render-only east Motel wing. Run in Blender; never writes production Unity assets."""
import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector

BASE = Path('X:/Work/duck-rails/ArtSource/MotelWingPilot')
data = json.loads((BASE/'source.json').read_text())
scene = bpy.data.scenes.new('Motel east wing - atlas review')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
optimized = bpy.data.collections.new('PILOT - one shared material')
scene.collection.children.link(optimized)
reference = bpy.data.collections.new('REFERENCE - original materials (hidden)')
scene.collection.children.link(reference)
reference.hide_viewport = True
reference.hide_render = True

SIZE=1024
pad=4
base=np.ones((SIZE,SIZE,4),dtype=np.float32)
emission=np.zeros_like(base); emission[:,:,3]=1
surface=np.ones_like(base)
tiles=[]; materials=[]
x=y=row=0
def linear(v):
    v=np.asarray(v); return np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
def gamma(v):
    return np.where(v<=.0031308,v*12.92,1.055*np.maximum(v,0)**(1/2.4)-.055)
for i,m in enumerate(data['materials']):
    img=bpy.data.images.load(str(BASE/m['texture']),check_existing=True)
    w,h=img.size
    if x+w+pad*2>SIZE: x=0;y+=row;row=0
    if y+h+pad*2>SIZE: raise RuntimeError('Atlas overflow')
    pixels=np.array(img.pixels[:],dtype=np.float32).reshape(h,w,4)
    tinted=pixels.copy(); tinted[:,:,:3]=gamma(linear(pixels[:,:,:3])*linear(m['color']))
    base[y:y+h+2*pad,x:x+w+2*pad]=np.pad(tinted,((pad,pad),(pad,pad),(0,0)),mode='edge')
    emission[y:y+h+2*pad,x:x+w+2*pad,:3]=np.array(m['emission'])/8
    surface[y:y+h+2*pad,x:x+w+2*pad,:3]=[m['metallic'],1-m['smoothness'],0]
    tiles.append((x+pad,y+pad,w,h)); x+=w+2*pad;row=max(row,h+2*pad)
    mat=bpy.data.materials.new('Reference '+m['name']);mat.use_nodes=True
    nodes=mat.node_tree.nodes; links=mat.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    tex=nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest'
    tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1;tint.inputs[2].default_value=(*linear(m['color']),1)
    links.new(tex.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],bs.inputs['Base Color'])
    bs.inputs['Metallic'].default_value=m['metallic'];bs.inputs['Roughness'].default_value=1-m['smoothness']
    bs.inputs['Emission Color'].default_value=(*m['emission'],1);bs.inputs['Emission Strength'].default_value=1
    materials.append(mat)

def image(name,arr,colorspace):
    im=bpy.data.images.new(name,width=SIZE,height=SIZE,alpha=True)
    im.colorspace_settings.name=colorspace
    im.pixels.foreach_set(arr.ravel());im.filepath_raw=str(BASE/(name+'.png'));im.file_format='PNG';im.save();im.pack();return im
atlas=image('MotelWing_BaseColor',base,'sRGB')
emit=image('MotelWing_Emission',emission,'Non-Color')
orm=image('MotelWing_Surface',surface,'Non-Color')
mat=bpy.data.materials.new('Motel wing - ONE atlas material');mat.use_nodes=True
nodes=mat.node_tree.nodes;links=mat.node_tree.links
bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
for im,socket in [(atlas,'Base Color'),(emit,'Emission Color')]:
    n=nodes.new('ShaderNodeTexImage');n.image=im;n.interpolation='Closest';links.new(n.outputs['Color'],bs.inputs[socket])
bs.inputs['Emission Strength'].default_value=8
n=nodes.new('ShaderNodeTexImage');n.image=orm;n.interpolation='Closest'
sep=nodes.new('ShaderNodeSeparateColor');links.new(n.outputs['Color'],sep.inputs[0]);links.new(sep.outputs[0],bs.inputs['Metallic']);links.new(sep.outputs[1],bs.inputs['Roughness'])

def clip(poly,axis,value,above):
    out=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da=(a[axis]-value)*(1 if above else -1);db=(b[axis]-value)*(1 if above else -1)
        if da>=-1e-7:out.append(a)
        if (da>1e-7 and db< -1e-7) or (da< -1e-7 and db>1e-7):out.append(a+(b-a)*(da/(da-db)))
    return out

groups={}; used=set();source_triangles=0
for part in data['parts']:
    pos=np.array(part['vertices']);uv=np.array(part['uv']);norm=np.array(part['normals']);uv2=np.array(part['uv2'])
    mi=part['material'];m=data['materials'][mi]
    if len(uv)!=len(pos):uv=np.zeros((len(pos),2))
    if len(uv2)!=len(pos):uv2=np.zeros((len(pos),2))
    uv=uv*np.array(m['scale'])+np.array(m['offset'])
    st=part['lightmapST'];uv2=uv2*np.array(st[:2])+np.array(st[2:])
    packed=np.concatenate((pos,norm,uv,uv2),axis=1)
    for tri in np.array(part['triangles']).reshape(-1,3):
        poly=list(packed[tri]);poly=clip(poly,0,22,True);poly=clip(poly,2,94,False)
        if len(poly)<3:continue
        used.add(part['sourceId']);source_triangles+=1
        # Preserve source lightmap identity and floor; limit longitudinal bounds to room bays.
        for bay in range(5):
            p=clip(clip(poly,2,54+8*bay,True),2,62+8*bay,False)
            if len(p)<3:continue
            avg=np.mean(p,axis=0)
            level='Roof' if avg[1]>=7.2 else ('Upper' if avg[1]>=3.4 else 'Ground')
            key=(level,bay,part['lightmap'])
            lo=np.floor(np.min(p,axis=0)[6:8]+1e-6).astype(int);hi=np.floor(np.max(p,axis=0)[6:8]-1e-6).astype(int);hi=np.maximum(hi,lo)
            for u in range(lo[0],hi[0]+1):
                for v in range(lo[1],hi[1]+1):
                    q=clip(clip(clip(clip(p,6,u,True),6,u+1,False),7,v,True),7,v+1,False)
                    if len(q)<3:continue
                    for j in range(1,len(q)-1):
                        t=np.array([q[0],q[j+1],q[j]])
                        if np.linalg.norm(np.cross(t[1,:3]-t[0,:3],t[2,:3]-t[0,:3]))<1e-9:continue
                        groups.setdefault(key,[]).append((t,mi,u,v))

for (level,bay,lm),tris in groups.items():
    verts=[];faces=[];atlasuv=[];originaluv=[];lightuv=[];normals=[];indices=[]
    for t,mi,u,v in tris:
        start=len(verts);faces.append((start,start+1,start+2));indices.append(mi)
        tx,ty,w,h=tiles[mi]
        for a in t:
            verts.append((a[0]-31,a[2]-74,a[1]));normals.append((a[3],a[5],a[4]))
            atlasuv.append(((tx+(a[6]-u)*w)/SIZE,(ty+(a[7]-v)*h)/SIZE));originaluv.append(a[6:8]);lightuv.append(a[8:10])
    mesh=bpy.data.meshes.new(f'{level} bay {bay+1}');mesh.from_pydata(verts,[],faces);mesh.update()
    for name,coords in [('Atlas',atlasuv),('Original lightmap UV',lightuv)]:
        layer=mesh.uv_layers.new(name=name)
        for loop,co in zip(layer.data,coords):loop.uv=co
    mesh.normals_split_custom_set_from_vertices(normals)
    ob=bpy.data.objects.new(mesh.name,mesh);optimized.objects.link(ob);mesh.materials.append(mat)
    ob['Original lightmap']=lm;ob['Unity origin']='31,0,74';ob['Review only']=True
    refmesh=mesh.copy();refmesh.materials.clear()
    for mm in materials:refmesh.materials.append(mm)
    for face,mi in zip(refmesh.polygons,indices):face.material_index=mi
    for loop,co in zip(refmesh.uv_layers[0].data,originaluv):loop.uv=co
    ob=bpy.data.objects.new('Reference '+mesh.name,refmesh);reference.objects.link(ob)

scene.world=bpy.data.worlds.new('Neutral review world');scene.world.use_nodes=True
next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.25,.25,.25,1)
sun=bpy.data.lights.new('Review sun','SUN');sun.energy=2
ob=bpy.data.objects.new('Review sun',sun);scene.collection.objects.link(ob);ob.rotation_euler=(.45,-.5,-.5)
cam=bpy.data.cameras.new('Wing review');ob=bpy.data.objects.new('Wing review',cam);scene.collection.objects.link(ob)
ob.location=(-38,-39,27);target=Vector((0,0,3));ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler();cam.type='ORTHO';cam.ortho_scale=48;scene.camera=ob
scene.render.resolution_x=1500;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_perspective='CAMERA'
for ob in bpy.context.selected_objects:ob.select_set(False)
for ob in optimized.objects:ob.select_set(True)
bpy.context.view_layer.objects.active=next(iter(optimized.objects))
summary={'source_renderers':len(used),'source_triangles_intersecting_wing':source_triangles,'chunks':len(groups),'output_triangles':sum(len(t) for t in groups.values()),'materials':1,'atlas_size':SIZE,'scope':'East wing x>=22, 54<=z<=94; two floors, balcony, Stair A, roof equipment. Render-only review, no production replacement or tests.'}
(BASE/'manifest.json').write_text(json.dumps(summary,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Motel-EastWing-Atlas.blend'))
print(json.dumps(summary))
