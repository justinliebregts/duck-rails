# Render-matched exterior detail for the existing playable Nuclear Power Plant.
# Run with Blender in background mode. The authoring mesh uses metres, +Y north.
from pathlib import Path
from collections import defaultdict
from contextlib import contextmanager
import bpy, json, math, random
from mathutils import Vector, Matrix
from math import sin, cos, pi

root=Path(__file__).resolve().parents[2]
out=root/'Assets/DuckRails/NuclearPlant/FullBlock'
out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene
random.seed(235+sum(map(ord,'nuclear-power-plant')))
materials={}
def mat(name,color,texture='',emission=0):
    m=bpy.data.materials.new(name)
    materials[name]=dict(name=name,color=color,texture=texture,emission=emission)
    return m
cream=mat("Plant ivory","e2d5b8","concrete")
teal=mat("Oxidized enamel","416c67","weathered paint")
metal=mat("Gunmetal","3e494b","weathered paint")
silver=mat("Galvanized steel","9caaab","weathered paint")
dark=mat("Rubber","242a2c")
wood=mat("Cedar","9e7955","timber")
asphalt=mat("Aggregate","51525a","gravel")
walk=mat("Concrete","b1afa1","concrete")
stripe=mat("Worn line","d9cba0","concrete")
ochre=mat("Work equipment","d3a143","weathered paint")
aqua=mat("Cold glazing","58777a","",.15)
mint=mat("Lit glazing","b3d8cd","",.52)
neon=mat("Lamp lens","ffe6c1","",2.5)
water=mat("Settling water","487f82","lake water")
leaf=[mat("Foliage "+str(i),h,"foliage") for i,h in enumerate(["527050","698254","48654b","81905f"])]
bark=mat("Tree bark","6a503e","timber")
# Materials on the original playable shells share the exterior art palette.
mat("Plant ground","69774b","grass")
mat("Ceramic","9fbab1")
mat("Groundcover","495d42","grass")
mat("Red marker","ff6456","",2.3)

# Blend 54% of each original Quackville texture into white in linear space,
# matching the Multiply material nodes in the accepted Blender study.
texture_dir = out / "Textures"
texture_dir.mkdir(exist_ok=True)
for texture in sorted({swatch["texture"] for swatch in materials.values() if swatch["texture"]}):
    original = bpy.data.images.load(str(root/"Assets/DuckRails/CampgroundGreybox/Textures"/("Quackville "+texture+".png")))
    width, height = original.size
    pixels = list(original.pixels)
    for i in range(0, len(pixels), 4):
        for channel in range(3):
            value = pixels[i+channel]
            linear = value / 12.92 if value <= .04045 else ((value + .055) / 1.055) ** 2.4
            pixels[i+channel] = 1.055 * (.46 + .54 * linear) ** (1 / 2.4) - .055
    blended = bpy.data.images.new(texture, width, height, alpha=False)
    blended.pixels = pixels
    blended.filepath_raw = str(texture_dir/(texture+".png"))
    blended.file_format = "PNG"
    blended.save()
    bpy.data.images.remove(blended)
    bpy.data.images.remove(original)
frame=Matrix.Identity(4)
groups=defaultdict(lambda:[[],[]])
labels=[]
lights=[]
group="Arrival"
solid=True
def geo(m,verts,faces):
    p,polys=groups[(group,m,solid)]
    offset=len(p)
    p.extend(tuple(frame@Vector(v)) for v in verts)
    polys.extend(tuple(offset+i for i in f) for f in faces)
faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]

def sign(text,x,y,z,size,m,rot=0):
    labels.append(dict(text=text,x=x,y=z,z=y,size=size))
def light(name,loc,power,color,size=.45):
    lights.append(dict(name=name,x=loc[0],y=loc[2],z=loc[1],power=power,color=list(color)))
def box(x,y,z,w,d,h,m):
    geo(m,[(x+a*w*.5,y+b*d*.5,z+c*h*.5) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],faces)

def tube(a,b,r,m,n=8,r2=None):
    a,b=Vector(a),Vector(b);v=(b-a).normalized();ref=Vector((0,0,1)) if abs(v.z)<.9 else Vector((1,0,0));u=v.cross(ref).normalized();w=v.cross(u);r2=r if r2 is None else r2
    pts=[tuple(p+rr*(u*cos(i*2*pi/n)+w*sin(i*2*pi/n))) for p,rr in [(a,r),(b,r2)] for i in range(n)]
    geo(m,pts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

def slab(x,y,w,d,h,m,z=0):box(x,y,z+h/2,w,d,h,m)

def shrub(x,y,z,r,h,m=None):
    m=m or random.choice(leaf);a=random.random()*2*pi;n=7;rs=[random.uniform(.83,1.08) for _ in range(n)]
    pts=[(x+r*rr*q*cos(a+i*2*pi/n),y+r*rr*q*sin(a+i*2*pi/n),z+hh*h) for q,hh in [(.75,.02),(1,.4),(.63,.85)] for i,rr in enumerate(rs)]+[(x+.13*r,y-.12*r,z+h)]
    fs=[]
    for i in range(n):
        j=(i+1)%n;fs += [(i,j,n+j,n+i),(n+i,n+j,2*n+i),(2*n+i,n+j,2*n+j),(2*n+i,2*n+j,3*n)]
    geo(m,pts,fs)

def tree(x,y,h=6):
    tube((x,y,.1),(x+.15,y,h*.68),h*.042,wood,7,h*.023)
    for i in range(3):
        a=i*2.1+.6;u=x+cos(a)*h*.19;v=y+sin(a)*h*.19
        tube((x,y,h*.35),(u,v,h*.69),h*.023,wood,6,h*.012);shrub(u,v,h*.48,h*.23,h*.38)
    shrub(x+.1,y,h*.63,h*.26,h*.4)

def lamp(x,y,angle):
    dx=cos(angle);dy=sin(angle);tube((x,y,.12),(x,y,.8),.30,cream);tube((x,y,.8),(x,y,5.9),.13,metal,8,.095);tube((x,y,5.5),(x+dx*1.35,y+dy*1.35,6.1),.095,metal)
    box(x+dx*1.35,y+dy*1.35,6.1,1.05,.74,.23,metal);box(x+dx*1.35,y+dy*1.35,5.96,.88,.60,.08,neon);light("Street pool",(x+dx*1.35,y+dy*1.35,5.72),920,(1,.66,.36))

def hvac(x,y,z,w=3.7,d=2.6):
    box(x,y,z+.2,w+.25,d+.24,.36,dark);box(x,y,z+.86,w,d,1.25,silver)
    for i in range(7):box(x,y-d/2-.01,z+.44+i*.13,w*.83,.08,.055,metal)
    for dx in [-.24*w,.24*w]:
        tube((x+dx,y,z+1.49),(x+dx,y,z+1.57),min(w*.205,d*.37),metal,12)
        for i in range(5):
            a=i*pi/5;rr=min(w*.175,d*.32);tube((x+dx-cos(a)*rr,y-sin(a)*rr,z+1.6),(x+dx+cos(a)*rr,y+sin(a)*rr,z+1.6),.038,silver,4)
@contextmanager
def local(x,y,z=0,a=0):
    global frame
    old=frame;frame=old@Matrix.Translation((x,y,z))@Matrix.Rotation(a,4,'Z')
    try:yield
    finally:frame=old

def yard(x,y,w,d):slab(x,y,w+.5,d+.5,.1,walk);slab(x,y,w,d,.12,asphalt)

def marker(x,y,z,title):
    for xx in [-1.7,1.7]:box(x+xx,y,z+1.4,.21,.24,2.8,wood)
    box(x,y,z+2.42,4.4,.24,1.24,teal);sign(title,x,y-.16,z+2.43,.65,neon)

def worklight(x,y,z):
    for dx,dy in [(-.7,-.4),(.65,-.4),(0,.7)]:tube((x+dx,y+dy,z),(x,y,z+1.25),.07,ochre)
    tube((x,y,z+.65),(x,y,z+2.45),.095,ochre);box(x,y,z+2.48,1.12,.38,.49,metal);box(x,y-.22,z+2.48,.93,.07,.34,neon);light("Worklight",(x,y-.45,z+2.4),320,(1,.66,.34))

def beam(a,b,r=.12,m=metal):tube(a,b,r,m,5)

def bus(x,y,z=0,empty=False):
    with local(x,y,z):
        body=ochre if empty else teal
        box(0,0,1.05,2.8,9,1.25,body);box(0,0,3,2.9,9.12,.22,cream)
        for side in [-1,1]:
            for j in range(8):
                box(side*1.33,-4.35+j*1.23,2.21,.13,.15,1.6,body)
                if not empty and j<7:box(side*1.32,-3.75+j*1.23,2.27,.08,1.09,1.13,aqua)
            for yy in [-2.9,2.9]:tube((side*1.08,yy,.62),(side*1.47,yy,.62),.60,dark,12)
            box(side*1.41,0,1.25,.06,8.8,.15,cream)
        for j in range(5):box(0,-3.5+j*1.4,1.51,2.32,.46,.32,wood)

def bollard(x,y,z=.13):
    tube((x,y,z),(x,y,z+1.28),.14,ochre)
    for h in (.47,.95):tube((x,y,z+h),(x,y,z+h+.16),.148,dark)

# West settling water and east flume sit outside the tower precinct.
group="Shift bus turnaround"
yard(-23,20,29,19);bus(-20,21)
for x in [-31,-27,-15]:box(x,21,.16,.13,7,.05,stripe)
for x in [-39,-34,-29,-24,-19]:box(x,29,.18,3,.16,.05,stripe)
group="Pipe delivery racks"
for x in [12,19,26,33]:
    for y in [8,14]:box(x,y,.77,.45,.67,1.53,metal)
    for j in range(4):tube((x-2+j*1.2,7,2.02),(x-2+j*1.2,16,2.02),.48,silver,12)
group="Cooling tower manifolds"
# Trunk lines and access ladders give the tower pair a legible scale.
for y in [67,94]:
    for side in [-1,1]:
        xx=26+side*10.6
        for yy in [y-4,y+4]:box(xx,yy,1.1,1.4,1.3,2.2,walk)
        tube((xx,y-6,3.2),(xx,y+6,3.2),.64,teal,14)
    for i in range(12):
        a=(i+.5)*2*pi/12;u=26+10.4*cos(a);v=y+10.4*sin(a)
        if i%3==0:worklight(u,v,0)
group="Hall facade and roof plant"
for x in range(-38,-11,4):
    box(x,54.76,3.65,.32,.48,7.1,cream);box(x+1.7,54.74,4.5,2.9,.32,2.3,aqua)
box(-26,54.45,7.01,20,.32,1.4,teal);sign("TURBINE HALL",-26,54.23,7.0,1.35,neon)
for y in (62,73,83):hvac(-31,y,8.0);tube((-20,y,8),(-20,y,10.7),.5,metal)
for j in range(3):
    xx=-42.5+j*.60;tube((xx,65,3.35),(xx,105,3.35),.27,silver,10)
group="Reed settling pond"
slab(-24,131,20,13,.16,walk);slab(-24,131,18.7,11.8,.21,water)
solid=False
for i in range(48):
    x=random.uniform(-33,-15);y=random.choice([125.3,136.7])+random.uniform(-.4,.4);tube((x,y,.2),(x+.09,y+.12,random.uniform(.7,1.35)),.045,leaf[2],4)
solid=True
group="Stepped discharge flume"
for i in range(4):
    z=.25+i*.23;y=146+i*2;box(23,y,z,8,2.2,.34,water)
    for x in (18.8,27.2):box(x,y,z+.34,.48,2.2,1,walk)
marker(-15,11,0,"NORTH GRID")
group="Cooling shell joint bands"
solid=False
for cy in (67,94):
    for zz,rr in [(3.2,11),(8,9.8),(16,7.6),(24,6.8),(30,7.3),(35,8.5)]:
        for k in range(32):
            a=k*2*pi/32;b=(k+1)*2*pi/32
            beam((26+(rr+.03)*cos(a),cy+(rr+.03)*sin(a),zz),(26+(rr+.03)*cos(b),cy+(rr+.03)*sin(b),zz),.105 if zz==35 else .052,silver)
group="Supply trunk and collars"
solid=True
for k in range(3):
    xx=14.3+k*.94;tube((xx,49,3.25),(xx,112,3.25),.35,teal if k%2 else silver,14)
    for yy in range(50,111,5):tube((xx,yy,3.25),(xx,yy+.16,3.25),.40,metal,12)
for yy in [50,57,62,73,80,87,100,109]:box(15.2,yy,1.54,4,.47,3.08,walk)
group="Turbine clerestory"
for y in (61,80,87):
    box(-40.18,y,4.32,.40,7.3,3.2,cream);box(-40.44,y,4.34,.14,6.88,2.71,aqua)
    for j in range(4):box(-40.55,y-2.8+j*1.9,4.32,.14,.13,2.8,silver)
group="Transformer fins"
for x,y in [(21.5,126.5),(31.5,126.5),(21.5,135.5),(31.5,135.5)]:
    for side in [-1,1]:
        for k in range(8):box(x+side*2.65,y-1.75+k*.5,1.67,.55,.14,2,metal)
group="Service buildings"
for x,y in [(-31,38.45),(33.5,40.45),(-32,143.45)]:
    for j in range(3):box(x-2.5+j*2.1,y-3.6,2.2,1.67,.24,1.08,mint)
for u in [-15,-10,10,15]:bollard(u,38)
group="Perimeter planting"
for x,y in [(-39,17),(-36,146),(38,144),(8,148)]:tree(x,y,4.8)
group="Rail verge lights"
for x,y in [(9,47),(-10,48),(10,115),(-10,115)]:lamp(x,y,pi if x>0 else 0)
# Reuse the source map's faceted roadside pine silhouette. All crowns remain
# visual, while the trunk is a solid prop on the environment collision layer.
for side in [-1,1]:
    for yy in [9,23,47,65,103,117,137,150]:
        x=side*random.uniform(43.1,45.1);h=random.uniform(4.3,7.2)
        group="Pine verge";solid=True
        tube((x,yy,0),(x,yy,h*.54),.18,bark,8)
        solid=False
        for j in range(3):
            z=h*(.20+j*.19)
            tube((x,yy,z),(x,yy,z+h*(.42-j*.03)),h*(.29-j*.054),leaf[(j+1)%4],7,0.02)
# Parked fleet car beside the shift bus.
group="Employee car";solid=True
with local(-32,19,.13,.10):
    box(0,0,.82,2.1,4.6,.82,teal);box(0,.2,1.45,1.78,2.44,.68,aqua)
    box(0,.2,1.84,1.83,2.04,.18,teal)
    for side in [-1,1]:
        for yy in [-1.3,1.27]:
            tube((side*.88,yy,.45),(side*1.12,yy,.45),.44,dark,12)
        box(side*.72,-2.34,.94,.43,.09,.18,cream)
# Store spatially separate mesh pieces for lightmap packing and collision.
for (label,m,collision),(pts,polys) in groups.items():
    mesh=bpy.data.meshes.new(label);mesh.from_pydata(pts,[],polys);mesh.materials.append(m);mesh.update()
    layer=mesh.uv_layers.new()
    for face in mesh.polygons:
        axis=max(range(3),key=lambda j:abs(face.normal[j]))
        for idx in face.loop_indices:
            q=mesh.vertices[mesh.loops[idx].vertex_index].co
            layer.data[idx].uv=((q.y if axis==0 else q.x)*.23,(q.y if axis==2 else q.z)*.23)
    obj=bpy.data.objects.new(label+" "+m.name+(" solid" if collision else " detail"),mesh)
    s.collection.objects.link(obj)
bpy.ops.export_scene.fbx(filepath=str(out/'Plant site.fbx'),object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
(root/'ArtSource/NuclearFullBlock/site.json').write_text(json.dumps(dict(materials=list(materials.values()),lights=lights,labels=labels),indent=2))
