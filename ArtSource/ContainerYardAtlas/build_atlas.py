"""Render-only full ContainerYard. Run in Blender; never writes production Unity assets."""
import bpy, json, math
import numpy as np
from pathlib import Path
from mathutils import Vector

BASE = Path('X:/Work/duck-rails/ArtSource/ContainerYardAtlas')
data = json.loads((BASE/'source.json').read_text())
if (BASE/'ContainerYard-FullMap-Atlas.blend').exists() and not globals().get('ALLOW_AUTHORING_REBUILD',False): raise RuntimeError('Review already exists; preserve review edits')

scene = bpy.data.scenes.new('ContainerYard full map - atlas review')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
optimized = bpy.data.collections.new('OPTIMIZED - one shared material')
scene.collection.children.link(optimized)
reference = bpy.data.collections.new('REFERENCE - original materials (hidden)')
scene.collection.children.link(reference)
reference.hide_viewport = True
reference.hide_render = True

SIZE=1024
EMISSION_SCALE=max(1.0,max(max(m["emission"]) for m in data["materials"]))
pad=4
base=np.ones((SIZE,SIZE,4),dtype=np.float32)
emission=np.zeros_like(base); emission[:,:,3]=1
surface=np.ones_like(base)
tiles=[]; materials=[]
# Skyline pack padded tiles without resampling; fills narrow gaps between large tiles.
source_images=[bpy.data.images.load(str(BASE/m['texture']),check_existing=True) if m.get('texture') else None for m in data['materials']]
sizes=[tuple(im.size) if im else (1,1) for im in source_images]
skyline=np.zeros(SIZE,dtype=int);locations={}
for index in sorted(range(len(sizes)),key=lambda i:(-sizes[i][1],-sizes[i][0])):
    w,h=sizes[index];ww=w+2*pad;hh=h+2*pad
    candidates=[(int(skyline[xx:xx+ww].max()),xx) for xx in range(SIZE-ww+1)]
    yy,xx=min(candidates)
    if yy+hh>SIZE:raise RuntimeError('Atlas does not fit at native resolution')
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
    base[y:y+h+2*pad,x:x+w+2*pad]=np.pad(tinted,((pad,pad),(pad,pad),(0,0)),mode='edge')
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
    im=bpy.data.images.new(name,width=SIZE,height=SIZE,alpha=True)
    im.colorspace_settings.name=colorspace
    im.pixels.foreach_set(arr.ravel());im.filepath_raw=str(BASE/(name+'.png'));im.file_format='PNG';im.save();im.pack();return im
atlas=image('ContainerYard_BaseColor',base,'sRGB')
emit=image('ContainerYard_Emission',emission,'Non-Color')
orm=image('ContainerYard_Surface',surface,'Non-Color')
mat=bpy.data.materials.new('ContainerYard full map - ONE atlas material');mat.use_nodes=True
nodes=mat.node_tree.nodes;links=mat.node_tree.links
bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
for im,socket in [(atlas,'Base Color'),(emit,'Emission Color')]:
    n=nodes.new('ShaderNodeTexImage');n.image=im;n.interpolation='Closest';links.new(n.outputs['Color'],bs.inputs[socket])
bs.inputs['Emission Strength'].default_value=EMISSION_SCALE
n=nodes.new('ShaderNodeTexImage');n.image=orm;n.interpolation='Closest'
sep=nodes.new('ShaderNodeSeparateColor');links.new(n.outputs['Color'],sep.inputs[0]);links.new(sep.outputs[0],bs.inputs['Metallic']);links.new(sep.outputs[1],bs.inputs['Roughness'])

def clip(poly,axis,value,above):
    out=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da=(a[axis]-value)*(1 if above else -1);db=(b[axis]-value)*(1 if above else -1)
        if da>=-1e-7:out.append(a)
        if (da>1e-7 and db< -1e-7) or (da< -1e-7 and db>1e-7):out.append(a+(b-a)*(da/(da-db)))
    return out

groups={}; used=set();source_triangles=0; max_error=0; total_before=total_after=0
for part_index,part in enumerate(data['parts']):
    pos=np.array(part['vertices']);uv=np.array(part['uv']);norm=np.array(part['normals']);uv2=np.array(part['uv2'])
    mi=part['material'];m=data['materials'][mi]
    if len(uv)!=len(pos):uv=np.zeros((len(pos),2))
    uv=uv*np.array(m['scale'])+np.array(m['offset'])
    st=part['lightmapST'];uv2=uv2*np.array(st[:2])+np.array(st[2:])
    packed=np.concatenate((pos,norm,uv,uv2),axis=1)
    bounds_min=pos.min(axis=0);bounds_max=pos.max(axis=0);center=(bounds_min+bounds_max)*.5
    size=bounds_max-bounds_min
    cell=('large',part['sourceKey']) if max(size[0],size[2])>20 or size[1]>12 else tuple(np.floor(center/np.array([12,6,12])).astype(int))
    key=(str(cell),part['lightmap'],part['settings'])
    before=after=0
    for tri in np.array(part['triangles']).reshape(-1,3):
        p=list(packed[tri]);area=np.linalg.norm(np.cross(p[1][:3]-p[0][:3],p[2][:3]-p[0][:3]))*.5
        if area<1e-10:continue
        before+=area;source_triangles+=1;used.add(part['sourceKey'])
        lo=np.floor(np.min(p,axis=0)[6:8]+1e-6).astype(int);hi=np.maximum(lo,np.floor(np.max(p,axis=0)[6:8]-1e-6).astype(int))
        if (hi[0]-lo[0]+1)*(hi[1]-lo[1]+1)>100000:raise RuntimeError('Excessive UV repetition: '+part['path'])
        for u in range(lo[0],hi[0]+1):
            for v in range(lo[1],hi[1]+1):
                q=clip(clip(clip(clip(p,6,u,True),6,u+1,False),7,v,True),7,v+1,False)
                for j in range(1,len(q)-1):
                    t=np.array([q[0],q[j+1],q[j]])
                    area2=np.linalg.norm(np.cross(t[1,:3]-t[0,:3],t[2,:3]-t[0,:3]))*.5
                    if area2<1e-10:continue
                    after+=area2
                    groups.setdefault(key,[]).append((t,mi,u,v))
    error=abs(before-after)/max(before,1e-9);max_error=max(max_error,error);total_before+=before;total_after+=after
    if error>1e-4:raise RuntimeError('Surface preservation failed '+part['path']+': '+str(error))
    if part_index%100==0:(BASE/'progress.txt').write_text(f'Processed {part_index}/{len(data["parts"])} source parts')
export_parts=[]
for group_index,((cell,lm,settings),tris) in enumerate(groups.items()):
    verts=[];faces=[];atlasuv=[];originaluv=[];lightuv=[];normals=[];indices=[];lookup={}
    for t,mi,u,v in tris:
        tx,ty,w,h=tiles[mi];face=[]
        for a in t:
            at=((tx+(a[6]-u)*w)/SIZE,(ty+(a[7]-v)*h)/SIZE)
            n=np.array((a[3],a[5],a[4]));length=np.linalg.norm(n);n=n/max(length,1e-15)
            point=(a[0],a[2]-80,a[1]);key=tuple(point)+tuple(n)+tuple(at)+tuple(a[8:10])
            if key not in lookup:
                lookup[key]=len(verts);verts.append(point);normals.append(tuple(n))
            face.append(lookup[key]);atlasuv.append(at);originaluv.append(a[6:8]);lightuv.append(a[8:10])
        faces.append(face);indices.append(mi)
    name=f'ContainerYard atlas chunk {group_index:03d}'
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    for name_uv,coords in [('Atlas',atlasuv),('Original lightmap UV',lightuv)]:
        layer=mesh.uv_layers.new(name=name_uv)
        for loop,co in zip(layer.data,coords):loop.uv=co
    mesh.normals_split_custom_set_from_vertices(normals)
    # Preserve interpolated source normals independently of Blender's tiny-face corner-normal calculation.
    attr=mesh.attributes.new(name='Unity source normal',type='FLOAT_VECTOR',domain='POINT')
    attr.data.foreach_set('vector',np.array(normals,dtype=np.float32).ravel())
    ob=bpy.data.objects.new(name,mesh);optimized.objects.link(ob);mesh.materials.append(mat)
    ob['Original lightmap']=lm;ob['Unity origin']='0,0,80';ob['Renderer settings']=settings;ob['Cell']=cell
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
    export_parts.append(dict(name=name,lightmap=lm,settings=settings,vertices=vv,normals=nn,uv=uu,uv2=ll,triangles=ii))
(BASE/'unity-meshes.json').write_text(json.dumps(dict(sourceHash=data['sourceHash'],sourceKeys=sorted(used),parts=export_parts)))
a=surface.copy();a[:,:,3]=1-a[:,:,1];a[:,:,1:3]=0
image('ContainerYard_MetallicSmoothness',a,'Non-Color')
scene.world=bpy.data.worlds.new('Neutral review world');scene.world.use_nodes=True
next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.25,.25,.25,1)
sun=bpy.data.lights.new('Review sun','SUN');sun.energy=2
ob=bpy.data.objects.new('Review sun',sun);scene.collection.objects.link(ob);ob.rotation_euler=(.45,-.5,-.5)
cam=bpy.data.cameras.new('Full ContainerYard review');ob=bpy.data.objects.new('Full ContainerYard review',cam);scene.collection.objects.link(ob)
ob.location=(-115,-130,125);target=Vector((0,0,2));ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler();cam.type='ORTHO';cam.ortho_scale=180;scene.camera=ob
scene.render.resolution_x=1500;scene.render.resolution_y=1050;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard'
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.shading.type='MATERIAL';area.spaces.active.region_3d.view_perspective='CAMERA'
for ob in bpy.context.selected_objects:ob.select_set(False)
for ob in optimized.objects:ob.select_set(True)
bpy.context.view_layer.objects.active=next(iter(optimized.objects))
summary={'source_renderers':len(used),'source_triangles':source_triangles,'chunks':len(groups),'output_triangles':sum(len(t) for t in groups.values()),'output_vertices':sum(len(p['vertices']) for p in export_parts),'materials':1,'atlas_size':SIZE,'emission_scale':EMISSION_SCALE,'source_materials':len(data['materials']),'tiles':tiles,'source_surface_area':float(total_before),'output_surface_area':float(total_after),'max_part_area_relative_error':float(max_error),'scope':'Full ContainerYard compatible opaque static scenery; production unchanged; no new device run.'}
(BASE/'manifest.json').write_text(json.dumps(summary,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(BASE/'ContainerYard-FullMap-Atlas.blend'))
(BASE/'progress.txt').write_text('Complete')
print(json.dumps(summary))


