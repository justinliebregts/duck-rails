"""Build the additive Lakeside Campground FBX and Quackville atlas using Blender 5.3 in background mode.
Run from the repository root. terrain.f32z contains the approved generated terrain vertex heights.
The gameplay prefab retains the terrain, all interiors, doorways, rail, and original gameplay components.
"""
from pathlib import Path
import bpy, json, math, random, sys, struct, zlib
from mathutils import Vector, Matrix
from math import sin, cos, pi
from collections import defaultdict
from contextlib import contextmanager
root=Path(__file__).resolve().parents[2]
out=root/"Assets/DuckRails/CampgroundGreybox/Finish"
source_dir=root/"ArtSource/CampgroundFinish"
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene
heights=struct.unpack("<15617f",zlib.decompress((source_dir/"terrain.f32z").read_bytes()))
def ground(x,y):
    ix=math.floor(x);iy=math.floor(y);fx=x-ix;fy=y-iy
    def h(dx,dy):return heights[(iy+dy)*97+ix+dx+48]
    if fy>=fx:return h(0,0)*(1-fy)+h(0,1)*(fy-fx)+h(1,1)*fx
    return h(0,0)*(1-fx)+h(1,0)*(fx-fy)+h(1,1)*fy
random.seed(235+sum(map(ord,"lakeside-campground")))
swatches={}
labels=[]
fixtures=[]
def rgb(h):
    def lin(v):
        v=int(v,16)/255
        return v/12.92 if v<.04045 else ((v+.055)/1.055)**2.4
    return tuple(lin(h[i:i+2]) for i in (0,2,4))
def mat(name,h,texture=None,strength=0,rough=.76):
    m=bpy.data.materials.new(name);m.use_nodes=True
    nt=m.node_tree;b=nt.nodes.get("Principled BSDF")
    c=rgb(h);b.inputs["Base Color"].default_value=(*c,1);b.inputs["Roughness"].default_value=rough
    if strength:
        b.inputs["Emission Color"].default_value=(*c,1);b.inputs["Emission Strength"].default_value=strength
    if texture:
        uv=nt.nodes.new("ShaderNodeTexCoord");v=nt.nodes.new("ShaderNodeVectorMath");v.operation="SCALE";v.inputs["Scale"].default_value=.23;nt.links.new(uv.outputs["Object"],v.inputs[0])
        im=nt.nodes.new("ShaderNodeTexImage");im.image=bpy.data.images.load(str(root/"Assets/DuckRails/CampgroundGreybox/Textures"/("Quackville "+texture+".png")),check_existing=True);im.interpolation="Closest";im.projection="BOX";nt.links.new(v.outputs[0],im.inputs[0])
        a=nt.nodes.new("ShaderNodeMixRGB");a.blend_type="MULTIPLY";a.inputs[0].default_value=.54;a.inputs[1].default_value=(*c,1);nt.links.new(im.outputs[0],a.inputs[2]);nt.links.new(a.outputs[0],b.inputs["Base Color"])
    swatches[name]=dict(color=h,texture=texture,emission=strength)
    return m

wood=mat("Cedar","9e7955","timber")
bark=mat("Pine bark","6a503e","timber")
pale=mat("Fresh timber","c59f72","timber")
cream=mat("Ivory trim","e2d5b8","concrete")
teal=mat("Teal enamel","416c67","weathered paint")
rus=mat("Vermilion","965340","weathered paint")
canvas=mat("Ochre tent canvas","c59b5c","ochre canvas")
tentred=mat("Orange tent canvas","b55c3e","ochre canvas")
metal=mat("Painted frame","3e494b","weathered paint")
silver=mat("Roof seam","9caaab","weathered paint")
terr=mat("Chimney brick","a1614c","concrete")
amber=mat("Warm window","ffc88b",None,1.7)
ochre=mat("Ochre canoe","d3a143","weathered paint")
neon=mat("Sign lettering","ffe6c1",None,3.5)
screen=mat("Water glint","829da3",None,.15)
dark=mat("Ash bed","242a2c")
rock=mat("Shore stone","83868b","rock")
salt=mat("Hearth stones","aaa49b","rock")
loam=mat("Earth","796c5e","gravel")
leaf=[mat("Brush "+str(i),h,"foliage") for i,h in enumerate(["527050","698254","48654b","81905f"])]
batches=defaultdict(lambda:[[],[]]);frame=Matrix.Identity(4)
zone="Campground"
def geo(m,v,f):
    points=[tuple(frame@Vector(t)) for t in v]
    x,y,z=[sum(q[i] for q in points)/len(points) for i in range(3)]
    solid=zone not in ("Shoreline brush","Water ripples","Trail brush") and m not in (amber,screen)
    key=zone,int(x//16),int(y//16),solid,m
    pos,pol=batches[key];idx=len(pos);pos.extend(points)
    for face in f:pol.append([idx+i for i in face])
faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
def box(x,y,z,w,d,h,m):
    geo(m,[(x+a*w*.5,y+b*d*.5,z+c*h*.5) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],faces)
def tube(a,b,r,m,n=8,r2=None):
    a,b=Vector(a),Vector(b);v=(b-a).normalized();ref=Vector((0,0,1)) if abs(v.z)<.9 else Vector((1,0,0));u=v.cross(ref).normalized();w=v.cross(u);r2=r if r2 is None else r2
    pts=[tuple(p+rr*(u*cos(i*2*pi/n)+w*sin(i*2*pi/n))) for p,rr in [(a,r),(b,r2)] for i in range(n)]
    geo(m,pts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])
def slab(x,y,w,d,h,m,z=0):box(x,y,z+h/2,w,d,h,m)
def patch(x,y,rx,ry,z,m,n=10):
    pts=[(x+rx*cos(i*2*pi/n),y+ry*sin(i*2*pi/n),z) for i in range(n)];geo(m,pts,[tuple(range(n))])
def shrub(x,y,z,r,h,m=None):
    m=m or random.choice(leaf);a=random.random()*2*pi;n=7;rs=[random.uniform(.83,1.08) for _ in range(n)]
    pts=[(x+r*rr*q*cos(a+i*2*pi/n),y+r*rr*q*sin(a+i*2*pi/n),z+hh*h) for q,hh in [(.75,.02),(1,.4),(.63,.85)] for i,rr in enumerate(rs)]+[(x+.13*r,y-.12*r,z+h)]
    fs=[]
    for i in range(n):
        j=(i+1)%n;fs += [(i,j,n+j,n+i),(n+i,n+j,2*n+i),(2*n+i,n+j,2*n+j),(2*n+i,2*n+j,3*n)]
    geo(m,pts,fs)
@contextmanager
def local(x,y,z=0,a=0):
    global frame
    old=frame;frame=old@Matrix.Translation((x,y,z))@Matrix.Rotation(a,4,'Z')
    try:yield
    finally:frame=old
def marker(x,y,z,title):
    for xx in [-1.7,1.7]:box(x+xx,y,z+1.4,.21,.24,2.8,wood)
    box(x,y,z+2.42,4.4,.24,1.24,teal);sign(title,x,y-.16,z+2.43,.65,neon)
def tent(x,y,z,w,d,m):
    with local(x,y,z):
        for side in (-1,1):
            geo(m,[(side*w/2,-d/2,0),(side*w/2,d/2,0),(0,d/2,w*.65),(0,-d/2,w*.65)],[(0,1,2,3)])
            geo(m,[(side*w/2,-d/2,0),(side*.28*w,-d/2-.10,.05),(0,-d/2,w*.65)],[(0,1,2)])
            for yy in (-d/2,d/2):tube((side*w*.24,yy,w*.33),(side*w*.68,yy*1.23,0),.035,cream,5)
def sign(txt,x,y,z,size,m,rot=0):labels.append(dict(text=txt,x=x,y=z,z=y,size=size))
def light(name,loc,power,color):fixtures.append(dict(name=name,x=loc[0],y=loc[2],z=loc[1],power=power))
def beam(a,b,r=.12,m=metal):tube(a,b,r,m,5)
# Source meshes for the production pines come from the Convenience Store atlas authoring export.
pine_parts=[p for p in json.loads((root/"ArtSource/ConvenienceStoreAtlas/source.json").read_text())["parts"] if "/Large court pine 0/" in p["path"]]
pts=[(a,c,b) for p in pine_parts for a,b,c in p["vertices"]]
center=[(min(p[i] for p in pts)+max(p[i] for p in pts))/2 for i in range(2)]
bottom=min(p[2] for p in pts)
def pine(x,y,size):
    global zone
    with local(x,y,ground(x,y),random.random()*6):
        for p in pine_parts:
            isTrunk=p["path"].endswith("/Trunk");zone="Pine trunks" if isTrunk else "Trail brush"
            v=[((a-center[0])*size,(c-center[1])*size,(b-bottom)*size) for a,b,c in p["vertices"]];s=p["triangles"]
            geo(bark if isTrunk else leaf[2],v,[(s[i],s[i+2],s[i+1]) for i in range(0,len(s),3)])
def gable(x,y,w,d,z,rise,m):
    for side in [-1,1]:
        geo(m,[(x+side*w/2,y-d/2,z),(x+side*w/2,y+d/2,z),(x,y+d/2,z+rise),(x,y-d/2,z+rise)],[(0,1,2,3)] if side==1 else [(3,2,1,0)])
        geo(wood,[(x-w/2,y+side*d/2,z),(x+w/2,y+side*d/2,z),(x,y+side*d/2,z+rise)],[(0,1,2)] if side==-1 else [(2,1,0)])
        for j in range(int(d/1.3)+1):beam((x+side*w/2,y-d/2+j*1.3,z+.07),(x,y-d/2+j*1.3,z+rise+.07),.05,metal)
def campground():
    global zone
    zone="Cabin porches"
    for x,y in [(34,76),(34,99),(20,121),(18,27),(34,50)]:
        z=ground(x,y);w=12 if y==27 else 10;d={50:8,121:9}.get(y,10)
        for k in range(6):
            for side in [-1,1]:box(x+side*w*.335,y-d*.50-.08,z+.5+k*.46,w*.33,.22,.10,pale)
        for side in [-1,1]:
            box(x+side*w*.31,y-d*.5-.22,z+1.95,2.35,.20,1.35,wood);box(x+side*w*.31,y-d*.5-.36,z+1.95,1.96,.13,1.03,amber);box(x+side*w*.31,y-d*.5-.45,z+1.95,.14,.11,1.1,cream)
            for yy in [-d*.5-2.2,-d*.5]:tube((x+side*w*.45,y+yy,z),(x+side*w*.45,y+yy,z+2.68),.13,bark)
        box(x,y-d*.5-1.1,z+.042,w,2.6,.08,wood);box(x,y-d*.5-1.1,z+2.85,w+.4,3,.28,teal)
        light("Cabin porch",(x,y-d*.5-1.3,z+2.55),370,(1,.62,.29))
    zone="Campsite tents"
    for x,y in [(-22,20),(-35,26),(30,142)]:tent(x,y,ground(x,y),4.6,5.9,tentred if x==-22 else canvas)
    zone="Trail markers"
    marker(19,16,ground(19,19),"PINE LAKE");marker(-33,133,ground(-33,133),"OVERLOOK")
    zone="Camper awning"
    with local(31.6,35.9,ground(31.6,35.9)):
        for i in range(8):box(-3.1+i*.48,0,2.35,.48,4.4,.13,cream if i%2 else rus)
        for y in [-2,2]:tube((-3.3,y,0),(-3.3,y,2.4),.06,silver)
    zone="Canoe racks"
    for x,y in [(-34,112),(-30,115)]:
        with local(x,y,ground(x,y)):
            for yy in [-1.5,1.5]:box(0,yy,.9,2.15,.23,.22,bark);box(0,yy,.48,.28,.29,.92,wood)
            for i in range(3):
                z=1.05+i*.44;pts=[(0,-2.7,z+.21),(.60,-1.1,z),(.63,1.0,z),(0,2.7,z+.23),(-.63,1,z),(-.6,-1.1,z)]
                geo([rus,teal,ochre][i],pts+[(0,0,z-.24)],[(j,(j+1)%6,6) for j in range(6)]);beam((-.5,0,z),(.5,0,z),.1,pale)
    for i in range(28):
        x=random.choice([-44,-39,40,44])+random.uniform(-1,1);y=random.uniform(8,152);pine(x,y,random.uniform(.6,.98))
    zone="Trail brush"
    for x,y in [(-41,43),(-37,120),(26,109),(12,130),(40,64),(-23,48)]:
        for i in range(6):
            u=x+random.uniform(-2,2);v=y+random.uniform(-2,2);shrub(u,v,ground(u,v),random.uniform(.55,1),random.uniform(.6,1.3))
    zone="Water ripples"
    for i in range(15):
        x=random.uniform(-33,-23);y=random.uniform(72,92);box(x,y,-.77,random.uniform(.45,1.9),.08,.025,screen)
def details():
    global zone
    zone='Gabled roofs'
    for x,y,w,d,z in [(34,76,10,10,4),(34,99,10,10,5),(20,121,10,9,4.2),(18,27,12,10,3.64),(34,50,10,8,3.4),(-11,57.5,10,9,3.3),(-11,97,10,12,3.3)]:
        z=ground(x,y)+3.45
        gable(x,y,w+1.1,d+1.1,z,2.15,teal)
        box(x+w*.32,y+d*.17,z+1.4,.85,.84,2.83,terr);box(x+w*.32,y+d*.17,z+2.87,1.06,1.06,.16,metal)
        for dy in [-d*.26,d*.20]:
            box(x-w*.5-.13,y+dy,z-1.4,.22,2.3,1.4,cream);box(x-w*.5-.26,y+dy,z-1.4,.10,2,1.15,amber);box(x-w*.5-.34,y+dy,z-1.4,.10,.12,1.17,wood)
    zone="Dock piles"
    for xx in [-26,-21,-15]:
        for yy in (77.8,81.4):tube((xx,yy,-1.6),(xx,yy,.66),.16,bark);tube((xx,yy,.61),(xx,yy,.73),.20,cream)
    zone="Shoreline brush"
    for i in range(35):
        yy=52+i*1.59;xx=-39+random.uniform(-1.1,2)
        shrub(xx,yy,ground(xx,yy),random.uniform(.5,1.3),random.uniform(.3,.9),random.choice([rock,leaf[0],loam]))
    zone="Fire rings"
    for x,y in [(-19,30),(37,42),(27,143)]:
        z=ground(x,y);patch(x,y,1.55,1.55,z+.06,dark)
        for j in range(10):a=j*2*pi/10;shrub(x+cos(a)*1.35,y+sin(a)*1.35,z,.41,.37,salt)
        for j in range(3):a=j*pi/3;tube((x-cos(a),y-sin(a),z+.30),(x+cos(a),y+sin(a),z+.34),.17,bark)
        light("Embers",(x,y,z+.44),110,(1,.37,.08))
    zone="Trail markers"
    for x,y in [(12,35),(-12,48),(-13,115),(21,141)]:marker(x,y,ground(x,y),"TRAIL")

campground()
details()
# Pack the repeated Quackville textures and colors into a shared atlas. Clip UVs at
# tile boundaries so pavements and long roof members retain meter-scaled repeats.
import numpy as np
from collections import defaultdict
def srgb(linear):
    return np.where(linear<=.0031308,linear*12.92,1.055*np.maximum(linear,0)**(1/2.4)-.055)
atlas_size=1024
base_pixels=np.zeros((atlas_size,atlas_size,4),dtype=np.float32)
glow_pixels=base_pixels.copy()
for img in (base_pixels,glow_pixels):img[:,:,3]=1
used=sorted({key[4] for key in batches},key=lambda m:m.name)
slots={}
for i,m in enumerate(used):
    sw=swatches[m.name];x=(i%8)*128+32;y=(i//8)*128+32;slots[m]=(x,y)
    color=np.array(rgb(sw["color"]))
    tile=np.broadcast_to(color,(64,64,3)).copy()
    if sw['texture']:
        im=bpy.data.images.load(str(root/"Assets/DuckRails/CampgroundGreybox/Textures"/("Quackville "+sw['texture']+".png")),check_existing=True)
        arr=np.array(im.pixels[:]).reshape(im.size[1],im.size[0],4)
        row=np.arange(64)*im.size[1]//64;col=np.arange(64)*im.size[0]//64
        tile*=.46+.54*arr[row[:,None],col[None,:],:3]
    for pixels,t in [(base_pixels,tile),(glow_pixels,np.broadcast_to(color*sw["emission"]/3.5,tile.shape))]:
        pixels[y-32:y+96,x-32:x+96,:3]=np.pad(srgb(t),((32,32),(32,32),(0,0)),mode="wrap")
def save_image(n,arr):
    im=bpy.data.images.new(n,width=atlas_size,height=atlas_size,alpha=False)
    im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(arr.ravel())
    im.filepath_raw=str(out/(n+'.png'));im.file_format='PNG';im.save()
save_image("Campground color",base_pixels);save_image("Campground emission",glow_pixels)
# Preserve the original shell UVs, with campground-local tints and filtered Quackville grain.
surface_colors = {
    "camper paint": "e2d5b8", "concrete": "b1afa1", "dark roofing": "416c67",
    "foliage": "61734f", "grass": "69774b", "gravel": "796c5e",
    "lake water": "487f82", "ochre canvas": "c59b5c", "rock": "aaa49b",
    "timber": "9e7955", "weathered paint": "965340"
}
surface_dir = out / "Surfaces"
surface_dir.mkdir(exist_ok=True)
for surface, color in surface_colors.items():
    path = root / "Assets/DuckRails/CampgroundGreybox/Textures" / ("Quackville " + surface + ".png")
    reference = bpy.data.images.load(str(path), check_existing=True)
    samples = np.array(reference.pixels[:]).reshape(reference.size[1], reference.size[0], 4)[:, :, :3]
    values = samples.mean(axis=2, keepdims=True)
    # Keep a soft painted finish on distant flat areas. Slightly stronger grain reads on close cabin walls.
    contrast = {"lake water": .10, "grass": .18, "foliage": .20}.get(surface, .36)
    tone = .81 + contrast * (values - values.mean())
    tinted = np.ones((reference.size[1], reference.size[0], 4), dtype=np.float32)
    tinted[:, :, :3] = srgb(np.array(rgb(color)) * tone)
    image = bpy.data.images.new("Pine Lake " + surface, width=reference.size[0], height=reference.size[1], alpha=False)
    image.colorspace_settings.name = "Non-Color"
    image.pixels.foreach_set(tinted.ravel())
    image.filepath_raw = str(surface_dir / ("Pine Lake " + surface + ".png"))
    image.file_format = "PNG"
    image.save()
atlas=bpy.data.materials.new("Lakeside atlas")
def clip(poly,axis,k,side):
    res=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da=(a[axis]-k)*side;db=(b[axis]-k)*side
        if da>=0:res.append(a)
        if da*db<0:res.append(a+(b-a)*da/(da-db))
    return res
chunks=defaultdict(lambda:[[],[],[]])
for (zone,cx,cy,solid,m),(verts,polys) in batches.items():
    pos,indices,uvs=chunks[(zone,cx,cy,solid)];u0,v0=slots[m]
    for poly in polys:
        xyz=np.array([verts[idx] for idx in poly]);n=np.cross(xyz[1]-xyz[0],xyz[2]-xyz[0]);axis=np.argmax(np.abs(n))
        if np.linalg.norm(n)<.000001:continue
        tex=xyz[:,[1 if axis==0 else 0,1 if axis==2 else 2]]*.23 if swatches[m.name]["texture"] else np.zeros((len(xyz),2))+.5
        corners=list(np.column_stack((xyz,tex)));lo=np.floor(tex.min(axis=0)).astype(int);hi=np.maximum(lo,np.floor(tex.max(axis=0)-.000001).astype(int))
        for u in range(lo[0],hi[0]+1):
            for v in range(lo[1],hi[1]+1):
                clipped=clip(clip(clip(clip(corners,3,u,1),3,u+1,-1),4,v,1),4,v+1,-1)
                if len(clipped)<3:continue
                # Scrap duplicate vertices left when an edge lands on a tile corner.
                unique=[]
                for pt in clipped:
                    if not unique or np.max(np.abs(pt[:3]-unique[-1][:3]))>.00001:unique.append(pt)
                if len(unique)>1 and np.max(np.abs(unique[0][:3]-unique[-1][:3]))<.00001:unique.pop()
                if len(unique)<3:continue
                ids=list(range(len(pos),len(pos)+len(unique)));indices.append(ids)
                pos.extend(q[:3] for q in unique);uvs.extend(((u0+(q[3]-u)*64)/1024,(v0+(q[4]-v)*64)/1024) for q in unique)
                if m in (canvas,tentred) or zone=="Canoe racks":indices.append(list(reversed(ids)))
for (zone,cx,cy,solid),(verts,polys,uvs) in chunks.items():
    if not polys:continue
    key=f"{zone}_{cx}_{cy}_{'solid' if solid else 'detail'}"
    mesh=bpy.data.meshes.new(key);mesh.from_pydata(verts,[],polys);mesh.materials.append(atlas);mesh.update()
    layer=mesh.uv_layers.new()
    for loop in mesh.loops:layer.data[loop.index].uv=uvs[loop.vertex_index]
    ob=bpy.data.objects.new(key,mesh);s.collection.objects.link(ob)

for o in bpy.context.selected_objects:o.select_set(False)
for o in s.objects:
    if o.type=='MESH':o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/"Lakeside finish.fbx"),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
(source_dir/"fixtures.json").write_text(json.dumps(dict(labels=labels,fixtures=fixtures),indent=2))
