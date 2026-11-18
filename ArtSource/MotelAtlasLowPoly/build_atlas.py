"""Render-only full Motel (triangle-preserving). Run headless: blender -b --factory-startup --python build_atlas.py. Never writes production Unity assets."""
import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector

BASE = Path('X:/Work/duck-rails/ArtSource/MotelAtlasLowPoly')
data = json.loads((BASE/'source.json').read_text())
if (BASE/'Motel-LowPoly-Atlas.blend').exists() and not globals().get('ALLOW_AUTHORING_REBUILD',False): raise RuntimeError('Review already exists; preserve review edits')

scene = bpy.data.scenes.new('Motel low-poly - atlas review')
if bpy.context.window: bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
optimized = bpy.data.collections.new('OPTIMIZED - atlas and repeat exceptions')
scene.collection.children.link(optimized)
reference = bpy.data.collections.new('REFERENCE - original materials (hidden)')
scene.collection.children.link(reference)
reference.hide_viewport = True
reference.hide_render = True

# Repeat texture pixels, never tessellate geometry. Native texture resolution is retained.
# Motel: 38 textured materials. The 256-pixel textures alone fill a 1024 square, so one
# 2048 x 1024 atlas is used. Repeats per material come from the source UV spans, capped
# at 256 atlas pixels per axis; parts needing more keep their original material.
W,H=2048,1024
REPEAT_CAP=256
EMISSION_SCALE=max(1.0,max(max(m["emission"]) for m in data["materials"]))
pad=4
base=np.ones((H,W,4),dtype=np.float32)
emission=np.zeros_like(base); emission[:,:,3]=1
surface=np.ones_like(base)
tiles=[]; materials=[]
source_images=[bpy.data.images.load(str(BASE/m['texture']),check_existing=True) if m.get('texture') else None for m in data['materials']]
def triangle_span(uv):
    lo=np.floor(uv.min(axis=1)+1e-6);hi=np.maximum(lo+1,np.ceil(uv.max(axis=1)-1e-6));return (hi-lo).astype(int)
needed={}
for part in data['parts']:
    m=data['materials'][part['material']]
    uv=(np.array(part['uv'])*np.array(m['scale'])+np.array(m['offset']))[np.array(part['triangles']).reshape(-1,3)]
    needed[m['name']]=np.maximum(needed.get(m['name'],np.array([1,1])),triangle_span(uv).max(axis=0))
REPEATS={}
for im,m in zip(source_images,data['materials']):
    size=im.size if im else (1,1)
    REPEATS[m['name']]=tuple(int(max(1,min(needed[m['name']][a],REPEAT_CAP//size[a]))) for a in range(2))
# Skyline pack padded tiles without resampling; fills narrow gaps between large tiles.
sizes=[tuple(im.size[a]*REPEATS[m['name']][a] for a in range(2)) if im else (1,1) for im,m in zip(source_images,data['materials'])]
skyline=np.zeros(W,dtype=int);locations={}
for index in sorted(range(len(sizes)),key=lambda i:(-sizes[i][1],-sizes[i][0])):
    w,h=sizes[index];ww=w+2*pad;hh=h+2*pad
    candidates=[(int(skyline[xx:xx+ww].max()),xx) for xx in range(W-ww+1)]
    yy,xx=min(candidates)
    if yy+hh>H:raise RuntimeError('Atlas does not fit at native resolution')
    locations[index]=(xx,yy);skyline[xx:xx+ww]=yy+hh
def linear(v):
    v=np.asarray(v); return np.where(v<=.04045,v/12.92,((v+.055)/1.055)**2.4)
def gamma(v):
    return np.where(v<=.0031308,v*12.92,1.055*np.maximum(v,0)**(1/2.4)-.055)
for i,m in enumerate(data['materials']):
    img=bpy.data.images.load(str(BASE/m['texture']),check_existing=True) if m.get('texture') else bpy.data.images.new('Solid white source',width=1,height=1)
    if not m.get('texture'): img.pixels[:]=[1,1,1,1]
    w,h=img.size
    x,y=locations[i]
    pixels=np.array(img.pixels[:],dtype=np.float32).reshape(h,w,4)
    tinted=pixels.copy(); tinted[:,:,:3]=gamma(linear(pixels[:,:,:3])*linear(m['color']))
    ru,rv=REPEATS[m['name']];tinted=np.tile(tinted,(rv,ru,1));w*=ru;h*=rv
    base[y:y+h+2*pad,x:x+w+2*pad]=np.pad(tinted,((pad,pad),(pad,pad),(0,0)),mode='wrap')
    emission[y:y+h+2*pad,x:x+w+2*pad,:3]=np.array(m['emission'])/EMISSION_SCALE
    surface[y:y+h+2*pad,x:x+w+2*pad,:3]=[m['metallic'],1-m['smoothness'],0]
    tiles.append((x+pad,y+pad,w,h))
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
    im=bpy.data.images.new(name,width=W,height=H,alpha=True)
    im.colorspace_settings.name=colorspace
    im.pixels.foreach_set(arr.ravel());im.filepath_raw=str(BASE/(name+'.png'));im.file_format='PNG';im.save();im.pack();return im
atlas=image('Motel_BaseColor',base,'sRGB')
emit=image('Motel_Emission',emission,'Non-Color')
orm=image('Motel_Surface',surface,'Non-Color')
mat=bpy.data.materials.new('Motel full map - ONE atlas material');mat.use_nodes=True
nodes=mat.node_tree.nodes;links=mat.node_tree.links
bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
for im,socket in [(atlas,'Base Color'),(emit,'Emission Color')]:
    n=nodes.new('ShaderNodeTexImage');n.image=im;n.interpolation='Closest';links.new(n.outputs['Color'],bs.inputs[socket])
bs.inputs['Emission Strength'].default_value=EMISSION_SCALE
n=nodes.new('ShaderNodeTexImage');n.image=orm;n.interpolation='Closest'
sep=nodes.new('ShaderNodeSeparateColor');links.new(n.outputs['Color'],sep.inputs[0]);links.new(sep.outputs[0],bs.inputs['Metallic']);links.new(sep.outputs[1],bs.inputs['Roughness'])

def triangle_tile(p):
    lo=np.floor(np.min(p,axis=0)[6:8]+1e-6).astype(int)
    hi=np.maximum(lo+1,np.ceil(np.max(p,axis=0)[6:8]-1e-6).astype(int))
    return lo,hi-lo

groups={}; used=set();source_triangles=0; max_error=0; total_before=total_after=0
part_checks=[]
for part_index,part in enumerate(data['parts']):
    pos=np.array(part['vertices']);uv=np.array(part['uv']);norm=np.array(part['normals']);uv2=np.array(part['uv2'])
    mi=part['material'];m=data['materials'][mi]
    uv=uv*np.array(m['scale'])+np.array(m['offset'])
    st=part['lightmapST'];uv2=uv2*np.array(st[:2])+np.array(st[2:])
    packed=np.concatenate((pos,norm,uv,uv2),axis=1)
    triangles=np.array(part['triangles']).reshape(-1,3)
    repeat=np.array(REPEATS[m['name']])
    use_atlas=all(np.all(triangle_tile(packed[tri])[1]<=repeat) for tri in triangles)
    material=-1 if use_atlas else mi
    bounds_min=pos.min(axis=0);bounds_max=pos.max(axis=0);center=(bounds_min+bounds_max)*.5
    size=bounds_max-bounds_min
    cell=('large',part['sourceKey']) if max(size[0],size[2])>20 or size[1]>12 else tuple(int(x) for x in np.floor(center/np.array([12,6,12] if use_atlas else [48,12,48])))  # Few repeat exceptions: coarser cells
    key=(str(cell),part['lightmap'],part['settings'],material)
    before=0
    for tri in triangles:
        t=packed[tri[[0,2,1]]].copy() # Coordinate-system winding only; one source triangle -> one output triangle.
        lo,span=triangle_tile(t)
        before+=np.linalg.norm(np.cross(t[1,:3]-t[0,:3],t[2,:3]-t[0,:3]))*.5
        groups.setdefault(key,[]).append((t,mi,int(lo[0]),int(lo[1])))
    used.add(part['sourceKey']);source_triangles+=len(triangles);total_before+=before;total_after+=before
    part_checks.append(dict(sourceKey=part['sourceKey'],path=part['path'],material=m['name'],atlas=bool(use_atlas),input_triangles=len(triangles),output_triangles=len(triangles),surface_area=before))
export_parts=[]
for group_index,((cell,lm,settings,material),tris) in enumerate(groups.items()):
    verts=[];faces=[];atlasuv=[];originaluv=[];lightuv=[];normals=[];indices=[];lookup={}
    for t,mi,u,v in tris:
        tx,ty,w,h=tiles[mi];face=[]
        for a in t:
            ru,rv=REPEATS[data['materials'][mi]['name']]
            at=((tx+(a[6]-u)*w/ru)/W,(ty+(a[7]-v)*h/rv)/H) if material==-1 else tuple((a[6:8]-np.array(data['materials'][mi]['offset']))/np.array(data['materials'][mi]['scale']))
            n=np.array((a[3],a[5],a[4]));length=np.linalg.norm(n);n=n/max(length,1e-15)
            point=(a[0],a[2]-80,a[1]);key=tuple(point)+tuple(n)+tuple(at)+tuple(a[8:10])
            if key not in lookup:
                lookup[key]=len(verts);verts.append(point);normals.append(tuple(n))
            face.append(lookup[key]);atlasuv.append(at);originaluv.append(a[6:8]);lightuv.append(a[8:10])
        faces.append(face);indices.append(mi)
    name=f'Motel low-poly chunk {group_index:03d}'
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    for name_uv,coords in [('Atlas',atlasuv),('Original lightmap UV',lightuv)]:
        layer=mesh.uv_layers.new(name=name_uv)
        for loop,co in zip(layer.data,coords):loop.uv=co
    mesh.normals_split_custom_set_from_vertices(normals)
    # Preserve interpolated source normals independently of Blender's tiny-face corner-normal calculation.
    attr=mesh.attributes.new(name='Unity source normal',type='FLOAT_VECTOR',domain='POINT')
    attr.data.foreach_set('vector',np.array(normals,dtype=np.float32).ravel())
    ob=bpy.data.objects.new(name,mesh);optimized.objects.link(ob);mesh.materials.append(mat if material==-1 else materials[material])
    ob['Original lightmap']=lm;ob['Unity origin']='0,0,80';ob['Renderer settings']=settings;ob['Cell']=cell;ob['Material mode']='Atlas' if material==-1 else data['materials'][material]['name']
    refmesh=mesh.copy();refmesh.materials.clear()
    for mm in materials:refmesh.materials.append(mm)
    for face,mi in zip(refmesh.polygons,indices):face.material_index=mi
    for loop,co in zip(refmesh.uv_layers[0].data,originaluv):loop.uv=co
    refob=bpy.data.objects.new('Reference '+name,refmesh);reference.objects.link(refob)
    # Export indexed Unity vertices, retaining per-corner normals and both UV sets.
    vv=[];nn=[];uu=[];ll=[];ii=[];index={}
    for face in mesh.polygons:
        for li in [face.loop_indices[0],face.loop_indices[2],face.loop_indices[1]]:
            co=mesh.vertices[mesh.loops[li].vertex_index].co;n=Vector(normals[mesh.loops[li].vertex_index])
            uv_a=mesh.uv_layers[0].data[li].uv;uv_l=mesh.uv_layers[1].data[li].uv
            k=tuple(co)+tuple(n)+tuple(uv_a)+tuple(uv_l)
            if k not in index:
                index[k]=len(vv);vv.append(dict(x=co.x,y=co.z,z=co.y+80));nn.append(dict(x=n.x,y=n.z,z=n.y))
                uu.append(dict(x=uv_a.x,y=uv_a.y));ll.append(dict(x=uv_l.x,y=uv_l.y))
            ii.append(index[k])
    export_parts.append(dict(name=name,lightmap=lm,settings=settings,materialPath='' if material==-1 else data['materials'][material]['assetPath'],vertices=vv,normals=nn,uv=uu,uv2=ll,triangles=ii))
(BASE/'unity-meshes.json').write_text(json.dumps(dict(sourceHash=data['sourceHash'],sourceKeys=sorted(used),parts=export_parts)))
a=surface.copy();a[:,:,3]=1-a[:,:,1];a[:,:,1:3]=0
image('Motel_MetallicSmoothness',a,'Non-Color')
scene.world=bpy.data.worlds.new('Neutral review world');scene.world.use_nodes=True
next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.25,.25,.25,1)
sun=bpy.data.lights.new('Review sun','SUN');sun.energy=2
ob=bpy.data.objects.new('Review sun',sun);scene.collection.objects.link(ob);ob.rotation_euler=(.45,-.5,-.5)
cam=bpy.data.cameras.new('Full Motel review');ob=bpy.data.objects.new('Full Motel review',cam);scene.collection.objects.link(ob)
ob.location=(-115,-130,125);target=Vector((0,0,2));ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler();cam.type='ORTHO';cam.ortho_scale=180;scene.camera=ob
scene.render.resolution_x=1500;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
for area in (bpy.context.screen.areas if bpy.context.screen else []):
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_perspective='CAMERA'
# Runs headless (blender -b --factory-startup): drop the empty factory scene so the
# saved file opens on the review scene. Never run this against a user's open file.
if not bpy.context.window:
    for other in [s for s in bpy.data.scenes if s!=scene]:bpy.data.scenes.remove(other)
layer=scene.view_layers[0]
for ob in optimized.objects:ob.select_set(True,view_layer=layer)
layer.objects.active=next(iter(optimized.objects))
# Measure actual exported float32 triangle area independently of source accumulation.
total_after=0.0
for part in export_parts:
    v=np.array([[v['x'],v['y'],v['z']] for v in part['vertices']])[part['triangles']].reshape(-1,3,3)
    total_after+=np.linalg.norm(np.cross(v[:,1]-v[:,0],v[:,2]-v[:,0]),axis=1).sum()*.5
summary={'source_renderers':len(used),'source_triangles':source_triangles,'chunks':len(groups),'output_triangles':sum(len(t) for t in groups.values()),'output_vertices':sum(len(p['vertices']) for p in export_parts),'materials':len(set(k[3] for k in groups)),'atlas_size':[W,H],'repeat_cap_pixels':REPEAT_CAP,'emission_scale':EMISSION_SCALE,'source_materials':len(data['materials']),'tiles':tiles,'source_surface_area':float(total_before),'output_surface_area':float(total_after),'export_area_relative_error':float(abs(total_after-total_before)/total_before),'repeats':REPEATS,'atlas_source_parts':sum(p['atlas'] for p in part_checks),'repeat_exception_parts':sum(not p['atlas'] for p in part_checks),'atlas_chunks':sum(k[3]==-1 for k in groups),'repeat_exception_chunks':sum(k[3]!=-1 for k in groups),'scope':'Current saved Motel after 2026-11-17 light fixes; texture-pixel repetition only, no triangle subdivision; isolated review; production unchanged.'}
assert summary['source_triangles']==summary['output_triangles']==79948, 'Triangle preservation required'
(BASE/'part-checks.json').write_text(json.dumps(part_checks,indent=2))
(BASE/'manifest.json').write_text(json.dumps(summary,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'Motel-LowPoly-Atlas.blend'))
(BASE/'progress.txt').write_text('Complete')
print(json.dumps(summary))
