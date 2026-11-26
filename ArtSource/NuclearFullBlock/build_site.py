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
mat("Plant ground","787875","gravel")
mat("Ceramic","9fbab1")
mat("Groundcover","495d42","grass")
mat("Red marker","ff6456","",2.3)

waste = mat("Luminous waste","6ad51f","",1.15)
waste_core = mat("Waste sheen","c3ff58","",2.4)
waste_edge = mat("Waste edge","357a12","",.3)
waste_shoulder = mat("Waste shoulder","55b01c","",.7)
foam = mat("Aerated water","9cd4d1","",.25)
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
# Drum bands, rivets and three stencilled trefoils share one low-resolution wrap.
swatch = mat("Waste drum paint","ffffff","drum wrap")
width,height=192,64
pixels=[]
for py in range(height):
    for px in range(width):
        local_x=px%64-32
        local_y=py-31
        shade=((px*17+py*47+(px//3)*13)%13)-6
        rgb=(186+shade,129+shade,46+shade)
        if py in (3,4,11,12,50,51,59,60):
            rgb=(100+shade,83+shade,60+shade)
        elif py in (5,13,52,61):
            rgb=(215,172,87)
        if abs(local_x)<=12 and abs(local_y)<=12:
            rgb=(226+shade,183+shade,66+shade)
            radius=math.hypot(local_x,local_y)
            angle=(math.degrees(math.atan2(local_y,local_x))+90)%120
            if radius<2.5 or (4<=radius<=10.7 and angle<60):rgb=(44,42,35)
        pixels.extend([v/255 for v in rgb]+[1])
img=bpy.data.images.new("drum wrap",width,height,alpha=False)
img.pixels=pixels
img.filepath_raw=str(texture_dir/"drum wrap.png")
img.file_format="PNG"
img.save()
bpy.data.images.remove(img)


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
    labels.append(dict(text=text,x=x,y=z,z=y,size=size,yaw=rot))
def light(name,loc,power,color,size=.45):
    lights.append(dict(name=name,x=loc[0],y=loc[2],z=loc[1],power=power,color=list(color)))
def box(x,y,z,w,d,h,m):
    geo(m,[(x+a*w*.5,y+b*d*.5,z+c*h*.5) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]],faces)

def tube(a,b,r,m,n=8,r2=None):
    a,b=Vector(a),Vector(b);v=(b-a).normalized();ref=Vector((0,0,1)) if abs(v.z)<.9 else Vector((1,0,0));u=v.cross(ref).normalized();w=v.cross(u);r2=r if r2 is None else r2
    pts=[tuple(p+rr*(u*cos(i*2*pi/n)+w*sin(i*2*pi/n))) for p,rr in [(a,r),(b,r2)] for i in range(n)]
    geo(m,pts,[tuple(reversed(range(n))),tuple(range(n,n*2))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)])

def socket(center,r,m):
    """Faceted cast fitting around a bend or branch. The tube ends sit inside it."""
    cx,cy,cz=center
    joints.append(dict(x=cx,y=cz,z=cy))
    seg=10
    vertices=[(cx,cy,cz-r)]
    for j in range(1,6):
        lat=-pi/2+j*pi/6
        vertices.extend((cx+r*cos(lat)*cos(i*2*pi/seg),cy+r*cos(lat)*sin(i*2*pi/seg),cz+r*sin(lat)) for i in range(seg))
    north=len(vertices);vertices.append((cx,cy,cz+r))
    polygons=[]
    for i in range(seg):
        k=(i+1)%seg
        polygons.append((0,1+k,1+i))
        for row in range(4):
            a=1+row*seg
            polygons.append((a+i,a+k,a+k+seg,a+i+seg))
        polygons.append((north,1+4*seg+i,1+4*seg+k))
    geo(m,vertices,polygons)

joints=[]
def pipe_run(points,r,m):
    points=list(map(Vector,points))
    # Slightly recessed flat ends are hidden even between facets of the socket.
    for a,b in zip(points,points[1:]):
        v=(b-a).normalized()
        tube(a+(v*r*.32 if a!=points[0] else Vector((0,0,0))),
             b-(v*r*.32 if b!=points[-1] else Vector((0,0,0))),r,m,10)
    for corner in points[1:-1]:socket(corner,1.25*r,m)

def slab(x,y,w,d,h,m,z=0):box(x,y,z+h/2,w,d,h,m)
def water_face(verts,quads,m):
    """Open water visible from both sides; swimmers stand on the basin floor."""
    global solid
    old=solid;solid=False
    geo(m,verts,quads)
    geo(m,[(x,y,z-.002) for x,y,z in verts],[tuple(reversed(q)) for q in quads])
    solid=old
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

def yard(x,y,w,d):
    slab(x,y,w,d,.12,asphalt)
    for side in (-1,1):
        slab(x+side*(w/2+.125),y,.25,d+.5,.10,walk)
        slab(x,y+side*(d/2+.125),w,.25,.10,walk)

def marker(x,y,z,title):
    for xx in [-1.7,1.7]:box(x+xx,y,z+1.4,.21,.24,2.8,wood)
    box(x,y,z+2.42,4.4,.24,1.24,teal);sign(title,x,y-.16,z+2.43,.65,neon)

def worklight(x,y,z):
    for dx,dy in [(-.7,-.4),(.65,-.4),(0,.7)]:tube((x+dx,y+dy,z),(x,y,z+1.25),.07,ochre)
    tube((x,y,z+.65),(x,y,z+2.45),.095,ochre);box(x,y,z+2.48,1.12,.38,.49,metal);box(x,y-.22,z+2.48,.93,.07,.34,neon);light("Worklight",(x,y-.45,z+2.4),320,(1,.66,.34))

def square_beam(start,end,width,material):
    a,b=Vector(start),Vector(end)
    along=(b-a).normalized()
    side=along.cross(Vector((0,0,1))).normalized()*(width/2)
    up=side.cross(along).normalized()*(width/2)
    # column 0..3 and 4..7 are the two end rings
    geo(material,[p+side*u+up*v for p in (a,b) for u,v in ((-1,-1),(-1,1),(1,1),(1,-1))],faces)

def beam(a,b,r=.12,m=metal):tube(a,b,r,m,5)

def bus(x,y,z=0,empty=False):
    with local(x,y,z):
        body=ochre if empty else teal
        box(0,0,1.05,2.8,9,1.25,body);box(0,0,3,2.9,9.12,.22,cream)
        for side in [-1,1]:
            box(side*1.33,0,2.86,.17,8.98,.25,body)
            for j in range(8):
                box(side*1.33,-4.35+j*1.23,2.21,.13,.15,1.6,body)
                if not empty and j<7:box(side*1.32,-3.75+j*1.23,2.27,.08,1.09,1.13,aqua)
            for yy in [-2.9,2.9]:tube((side*1.08,yy,.62),(side*1.47,yy,.62),.60,dark,12)
            box(side*1.41,0,1.25,.06,8.8,.15,cream)
        for yy in (-4.4,4.4):
            box(0,yy,2.86,2.73,.16,.26,body)
            box(0,yy,2.18,2.45,.08,1.24,aqua)
        for j in range(5):box(0,-3.5+j*1.4,1.51,2.32,.46,.32,wood)

def bollard(x,y,z=.13):
    tube((x,y,z),(x,y,z+1.28),.14,ochre)
    for h in (.47,.95):tube((x,y,z+h),(x,y,z+h+.16),.148,dark)

# West settling water and east flume sit outside the tower precinct.
group="Ground perimeter edge"
for x in (-48,48):box(x,80,-.155,.018,160,.29,asphalt)
for y in (0,160):box(0,y,-.155,96,.018,.29,asphalt)
group="Shift bus turnaround"
yard(-23,20,29,19);bus(-20,21)
for x in [-31,-27,-15]:box(x,21,.123,.13,7,.006,stripe)
for x in [-34,-29,-24,-19]:box(x,29,.123,3,.16,.006,stripe)
group="Pipe delivery racks"
for x in (19,26,33):
    for y in (8.5,14.5):
        for dx in (-1.46,1.46):
            box(x+dx,y,.12,.70,.87,.24,walk)
            box(x+dx,y,.48,.26,.38,.54,metal)
            box(x+dx*1.30,y,1.39,.14,.32,1.60,ochre)
        box(x,y,.79,3.92,.43,.24,metal)
    # An open annulus at each end leaves the bore visible. The lower row
    # bears on both cross-members; successive rows nest between neighbours.
    if x==33:continue
    radius=3.64/6
    for row,count in enumerate((3,2)):
        for j in range(count):
            xx=x+(j-(count-1)/2)*2*radius;h=.91+radius+row*radius*math.sqrt(3)
            n=12
            pts=[(xx+r*cos(k*2*pi/n),yy,h+r*sin(k*2*pi/n))
                 for yy,r in ((7,radius),(16,radius),(7,radius*.8),(16,radius*.8)) for k in range(n)]
            quads=[]
            for k in range(n):
                l=(k+1)%n
                quads.extend([(k,n+k,n+l,l),(2*n+k,2*n+l,3*n+l,3*n+k),
                              (k,l,2*n+l,2*n+k),(n+l,n+k,3*n+k,3*n+l)])
            geo(silver,pts,quads)
group="Tower entrance worklights"
# Four paved positions per tower, outside the shell, entrance crosswalk and spiral.
for cx in (-26,26):
    for dx in (-7.8,7.8):
        for dy in (-10.5,10.5):
            worklight(cx+dx,103+dy,0)

# Wall plates straddle the inside face; a short arm carries the tilting head.
group="Hall wall floodlights"
dead_lens=mat("Burnt lamp glass","101111")
def hall_flood(x,y,nx,ny,height,working=False):
    normal=Vector((nx,ny,0))
    up=Vector((0,0,1))
    side=up.cross(normal)
    anchor=Vector((x,y,height))
    aim=(normal*.78-up*.63).normalized()
    head=anchor+normal*.55
    def housing(center,right,vertical,front,width,height,depth,material):
        geo(material,[center+right*(a*width/2)+vertical*(b*height/2)+front*(c*depth/2)
                      for c in (-1,1) for a,b in ((-1,-1),(1,-1),(1,1),(-1,1))],faces)
    housing(anchor,side,up,normal,.37,.47,.12,metal)
    tube(anchor,head,.095,silver,8)
    vertical=aim.cross(side)
    housing(head,side,vertical,aim,.94,.55,.24,metal)
    housing(head+aim*.135,side,vertical,aim,.80,.40,.035,neon if working else dead_lens)
    if working:
        light("Hall wall flood",head+aim*.19,650,(1,.92,.80))
        lights[-1].update(spot=True,dx=aim.x,dy=aim.z,dz=aim.y)

for y in (60,69,88):
    hall_flood(-39.875,y,1,0,6.75,working=(y==69))
for y in (60,73,85):
    hall_flood(-12.125,y,-1,0,6.75,working=(y==85))
for x in (-20,-31):
    hall_flood(x+2,55.125,0,1,6.60)
    hall_flood(x,90.875,0,-1,6.60)

group="Hall facade and roof plant"
for x in range(-38,-11,4):
    box(x,54.66,3.88,.50,.72,7.76,cream)
for x in (-36,-16):
    box(x,54.10,.35,1.35,1.45,.70,walk)
    box(x,54.12,4.50,.84,1.06,8.30,teal)
    box(x,54.12,8.63,1.26,1.40,.24,ochre)
# Rail-facing nameboard sits above the east door, below the canopy.
box(-11.76,71.8,6.79,.55,16.4,1.92,metal)
box(-11.45,71.8,7.84,1.20,17.0,.18,silver)
box(-11.44,71.8,5.87,.12,16.4,.13,ochre)
sign("TURBINE HALL",-11.445,71.8,6.80,3.1,neon,-90)
for y in (65.8,71.8,77.8):
    box(-10.99,y,7.71,.29,.84,.12,neon)
    light("Hall facade spotlight",(-10.99,y,7.62),950,(1,.84,.61))
    lights[-1].update(spot=True,dx=-.16,dy=-.987,dz=0)
for y in (62,73,83):hvac(-31,y,8.0);tube((-20,y,8),(-20,y,10.7),.5,metal)
group="Reed settling pond"
# Raised settling tank with a sloped maintenance walk on the rail-facing bank.
slab(-26,130,27,19,1.33,walk)
for x in (-39.345,-12.655):box(x,130,1.79,.31,19,.92,walk)
for y in (120.655,139.345):box(-26,y,1.79,26.38,.31,.92,walk)
water_face([(-39.19,120.81,2.33),(-12.81,120.81,2.33),(-12.81,139.19,2.33),(-39.19,139.19,2.33)],[(0,1,2,3)],water)
# Way out: seven treads climb from the tank floor to the east bank landing.
for k in range(1,8):
    h=1.33+1.34*k/7
    box(-12.81-.34*(7-k)-.17,134,(1.33+h)/2,.34,2.2,h-1.33,walk)
for x in (-33,-19):
    for y in (125.5,134.5):
        light("Settling pond aqua bounce",(x,y,6.05),1750,(.06,.81,1))
for x in (-39.35,-12.65):box(x,130,2.46,.32,19,.42,cream)
for y in (120.65,139.35):box(-26,y,2.46,27,.32,.42,cream)
group="Settling pond corner floods"
for x in (-39.35,-12.65):
    for y in (120.65,139.35):
        box(x,y,2.73,.30,.30,.12,metal)
        box(x,y,3.69,.15,.15,1.84,silver)
        center=Vector((x,y,4.65))
        aim=Vector(((-26-x)*.55,(130-y)*.55,-2.4)).normalized()
        tube(center-aim*.16,center+aim*.12,.34,metal,8)
        tube(center+aim*.12,center+aim*.15,.27,mint,8)
        light("Pond corner flood",center+aim*.19,640,(.70,.86,1))
        lights[-1].update(spot=True,dx=aim.x,dy=aim.z,dz=aim.y)
group="Reed settling pond"
def ramp(x0,x1,y0,y1,high):
    geo(walk,[(x0,y0,0),(x1,y0,0),(x1,y1,0),(x0,y1,0),
              (x0,y0,.04),(x1,y0,.04),(x1,y1,high),(x0,y1,high)],faces)
ramp(-12.45,-9.1,116,130,2.66)
slab(-10.78,134.5,3.35,9,2.66,walk)
solid=False
for i in range(48):
    x=random.uniform(-38,-14);y=random.choice([121.8,138])+random.uniform(-.25,.25)
    tube((x,y,1.33),(x+.09,y+.12,random.uniform(2.8,3.5)),.045,leaf[2],4)
solid=True
group="Stepped discharge flume"
# Profile extrusions give each sidewall a single stepped silhouette down to grade.
def stepped_bank(x0,x1,z0,step,heights,m):
    end=z0+step*len(heights)
    profile=[(z0,0),(end,0),(end,heights[-1])]
    for i in reversed(range(len(heights))):
        profile.append((z0+i*step,heights[i]))
        if i:profile.append((z0+i*step,heights[i-1]))
    n=len(profile)
    verts=[(x,y,h) for x in (x0,x1) for y,h in profile]
    polys=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    for i in range(n):
        j=(i+1)%n
        polys.append((i,j,j+n,i+n))
    geo(m,verts,polys)

start=140.95;step=3.1;end=start+4*step
levels=[1.23+i*.9 for i in range(4)]
for i,x in ((0,21),(0,33),(3,21),(3,33)):
    light("Discharge flume aqua bounce",(x,start+(i+.5)*step,levels[i]+3.55),1900,(.045,.92,1))
for x0,x1 in ((15.25,15.80),(38.20,38.75)):
    stepped_bank(x0,x1,start,step,[h+.64 for h in levels],cream)
stepped_bank(15.80,38.20,start+.085,step,[h-1 for h in levels],walk)
# West-bank stairs: between each pool floor, then up to the rear deck.
for i in range(1,4):
    floor=levels[i-1]-1;edge=start+i*step+.085
    for k in range(1,5):box(17.0,edge-.35*(4-k)-.175,floor+.09*k,2.4,.35,.18*k,walk)
floor=levels[-1]-1;rise=(4.12-floor)/7
for k in range(1,7):box(17.0,end-.30*(6-k)-.15,floor+rise*k/2,2.4,.30,rise*k,walk)
for i,h in enumerate(levels):
    y=start+i*step
    low=levels[i-1] if i else .035
    water_face([(15.80,y,low),(38.20,y,low),(38.20,y,h),(15.80,y,h),
                (38.20,y+step,h),(15.80,y+step,h)],[(0,1,2,3),(3,2,4,5)],water)
    solid=False
    for k in range(15):
        x=16.29+k*1.49
        box(x,y-.022,h-.30,.12+(k%3)*.07,.036,.47+(k%4)*.07,foam)
    box(27,y+.085,h+.009,22.24,.17,.013,foam)
    solid=True
# The ramp and broad landing meet on one line with no doubled top surfaces.
ramp(12.03,15.25,131,end,4.12)
slab(25.40,(end+157.6)/2,26.74,157.6-end,4.12,walk)
for x in (12.13,38.67):
    for y in (end+.15,157.42):box(x,y,4.65,.11,.11,1.06,metal)
    tube((x,end+.13,5.17),(x,157.47,5.17),.060,ochre)
for x in (16,20,24,28,32,36):box(x,157.42,4.65,.11,.11,1.06,metal)
tube((12.13,157.42,5.17),(38.67,157.42,5.17),.060,ochre)
group="Generating station sign"
for x in (-31.8,-18.2):
    box(x,8.76,.36,1.4,1.5,.72,walk)
    box(x,8.76,3.15,.52,.62,5.7,metal)
box(-25,8.15,4.2,16.8,.68,4.0,teal)
for h in (2.19,6.21):box(-25,8.14,h,17,.76,.14,ochre)
box(-25,7.77,3.53,14.9,.05,.045,ochre)
sign("QUACKWATT",-25,7.75,4.85,4.15,neon)
sign("NUCLEAR GENERATING STATION",-25,7.75,2.99,1.40,neon)
for x in (-31,-19):light("Entrance sign wash",(x,6.45,2.8),460,(1,.79,.48))

group="Process pipe bridges"
# A north/south header feeds three overhead pipe bridges. Only the towers'
# outside faces receive downcomers, keeping the interior crosswalks open.
for y in (48,81,95):
    for x in (-10.15,10.15):
        for off in (-1.65,1.65):
            box(x,y+off,.28,1.1,1.05,.56,walk)
            box(x,y+off,4.43,.40,.43,8.5,ochre)
        box(x,y,8.68,.72,4.0,.54,metal)
        beam((x,y-1.6,6.5),(x,y+1.6,8.45),.14,ochre)
    for off in (-1.48,1.48):
        box(0,y+off,8.65,21,.26,.34,ochre)
        box(0,y+off,9.65,21,.16,.17,ochre)
        for k in range(10):
            u=-10+k*2
            beam((u,y+off,8.75),(u+1,y+off,9.63),.078,ochre)
            beam((u+1,y+off,9.63),(u+2,y+off,8.75),.078,ochre)
    for idx,off in enumerate((-.92,0,.95)):
        h=9.11; r=(.34,.25,.29)[idx]; m=(silver,ochre,teal)[idx]
        left=-16+(idx-1)*.8;right=16+(idx-1)*.8
        pipe_run([(left,y+off,6.1),(left,y+off,h),(right,y+off,h),(right,y+off,6.1)],r,m)
        for x in (left,right):socket((x,y+off,6.1),.44,m)
        for x in (-12,-6,0,6,12):
            tube((x-.085,y+off,h),(x+.085,y+off,h),r+.075,metal,10)
for x in (-16,16):
    for idx,dx in enumerate((-.8,0,.8)):
        xx=x+dx; end=95+(-.92,0,.95)[idx]
        tube((xx,46,6.1),(xx,end,6.1),.31,silver if idx!=1 else teal,10)
        for y in range(47,95,7):tube((xx,y,6.1),(xx,y+.17,6.1),.38,metal,10)
    for y in ((47,92) if x<0 else (47,56,89,93)):
        box(x,y,3.0,.45,.65,6,metal)
        box(x,y,5.85,2.8,.72,.32,ochre)

group="Containment service annex"
# The small exchanger buildings and their couplings terminate the pipe runs.
# Front wall has four punched windows and a central open doorway.
def frontwall(x0,x1,y,h,sill,lintel,openings,door=None):
    cursor=x0
    for a,b in sorted(openings+([door] if door else [])):
        if a>cursor:box((cursor+a)/2,y,h/2,a-cursor,.28,h,teal)
        if (a,b)!=door:
            box((a+b)/2,y,sill/2,b-a,.28,sill,teal)
            box((a+b)/2,y,sill,b-a+.12,.42,.10,cream)
        top=3.05 if (a,b)==door else lintel
        box((a+b)/2,y,(h+top)/2,b-a,.28,h-top,teal)
        cursor=b
    if cursor<x1:box((cursor+x1)/2,y,h/2,x1-cursor,.28,h,teal)
frontwall(19,35,52,4.40,1.80,3.15,[(19.8,21.2),(22.0,23.8),(30.2,32),(32.8,34.2)],(25,29))
for x in (19.13,34.87):box(x,56,2.2,.26,8,4.4,teal)
box(27,59.87,2.2,15.5,.26,4.4,teal)
box(27,56,4.5,16.4,8.3,.36,silver)
slab(27,56,15.6,7.6,.07,walk)
for x in (21.5,32.3):
    box(x,58.6,1.05,2.6,1.15,2.1,silver)
    for j in range(5):box(x-.9+j*.45,57.99,1.04,.18,.12,1.6,metal)
light("Annex inspection lamp",(27,57.7,3.8),520,(.70,.86,1))
hvac(30,56,4.7,3.5,2.8)
for j in range(3):
    # Separate the parallel branches by more than their collar diameters.
    for side in (-1,1):
        trunk=side*16+(j-1)*.80
        x=side*(20.0+j*1.45)
        y=52.35-j*1.28
        h=7.12
        start=(trunk,y,6.1)
        points=[start,(trunk,y,h),(x,y,h),(x,55.15 if side<0 else 54.65,h)]
        if side>0:points.append((x,54.65,4.52))
        pipe_run(points,.32,silver)
        socket(start,.44,metal)
# Dome bands, low-facing trefoil and pipe connections on the forecourt.
group="Containment markings"
solid=False
for h in (4.7,9.85):
    for k in range(32):
        a=k*2*pi/32;b=(k+1)*2*pi/32
        def point(angle,z):return (30+10.06*cos(angle),72+10.06*sin(angle),z)
        geo(ochre,[point(a,h),point(b,h),point(b,h+.46),point(a,h+.46)],[(0,1,2,3)])
def dome_stencil(x,z):
    return (30+x,72-math.sqrt(10.12**2-x*x),7.45+z)
for sector in range(3):
    for k in range(4):
        a=sector*2*pi/3+k*pi/12;b=a+pi/12
        geo(metal,[dome_stencil(r*cos(t),r*sin(t)) for r,t in ((.51,a),(1.94,a),(1.94,b),(.51,b))],[(0,1,2,3)])
geo(metal,[dome_stencil(.35*cos(k*pi/5),.35*sin(k*pi/5)) for k in range(10)],[tuple(range(10))])
solid=True
group="Condenser feed pipes"
for j in range(3):
    x=31.0+j*1.25; h=2.15+j*.55
    pipe_run([(x,59.6,h),(x,65.45,h),(x,65.45,.75),(x,67.37,.75)],.455,teal)
    for y in (60.3,62.6):tube((x,y,h),(x,y+.19,h),.55,metal,12)
    y=81.9+j*1.28; endx=26+j*.96;trunk=16+(j-1)*.8
    start=(trunk,y,6.1)
    pipe_run([start,(trunk,y,7.22),(endx,y,7.22),(endx,80,7.22),(endx,80,4.06)],.34,silver)
    socket(start,.43,metal)
    socket((endx,80,4.06),.54,teal)
group="Waste spill"
solid=False
def puddle(x,y,rx,ry,level,material,sides=()):
    count=16; phase=random.random()*pi
    base,height,bevel=level
    # Rings run from the floor contact up a rounded shoulder to a flat top.
    rings=[(0,base),(.28,base+height*.55),(.62,base+height*.88),(1,base+height)]
    verts=[]
    for inset,z in rings:
        for n in range(count):
            a=n*2*pi/count;rad=1+.14*sin(3*a+phase)+.09*sin(5*a-.7)
            verts.append((x+max(rx-bevel*inset,rx*.4)*rad*cos(a),y+max(ry-bevel*inset,ry*.4)*rad*sin(a),z))
    top=len(verts);verts.append((x,y,base+height))
    # Darker side bands keep the thickness readable on a self-lit surface.
    for ring in range(len(rings)-1):
        polys=[]
        for n in range(count):
            lo=ring*count;k=(n+1)%count
            polys.append((lo+count+n,lo+n,lo+k,lo+count+k))
        geo(sides[ring] if ring<len(sides) else material,verts,polys)
    polys=[(top,(len(rings)-1)*count+n,(len(rings)-1)*count+(n+1)%count) for n in range(count)]
    geo(material,verts,polys)
for i,(x,y,rx,ry) in enumerate([(17.1,21.1,2.45,1.3),(20.1,22,1.75,.74),(23.2,25.6,1.94,1.2),(27,18.8,2.36,.86),(23.4,19,.86,.4)]):
    # Slightly different heights keep overlapping tops from flickering.
    height=.115+.006*i
    puddle(x,y,rx,ry,(-.006,height,.24),waste,(waste_edge,waste_shoulder))
    puddle(x-rx*.12,y+ry*.12,rx*.45,ry*.28,(height-.008,.030,.08),waste_core)
# Treat adjoining puddles as one source so their falloffs overlap gently.
for x,y,power in ((18.5,21.5,1100),(23.2,25.6,1300),(26.8,18.8,1050)):
    light("Waste spill green bounce",(x,y,1.9),power,(.17,1,.045))
solid=True
group="Cooling shell joint bands"
solid=False
for cx in (-26,26):
    cy=103
    for zz,rr in [(3.2,11),(8,9.8),(16,7.6),(24,6.8),(30,7.3),(35,8.5)]:
        for k in range(32):
            a=k*2*pi/32;b=(k+1)*2*pi/32
            beam((cx+(rr+.03)*cos(a),cy+(rr+.03)*sin(a),zz),(cx+(rr+.03)*cos(b),cy+(rr+.03)*sin(b),zz),.105 if zz==35 else .052,silver)
# Wide painted maintenance band follows the shell taper.
    for k in range(32):
        a=k*pi/16;b=(k+1)*pi/16
        def band(t,r,h):return (cx+r*cos(t),cy+r*sin(t),h)
        geo(ochre,[band(a,9.06,10.8),band(b,9.06,10.8),band(b,8.66,12.25),band(a,8.66,12.25)],[(0,1,2,3)])
group="Supply trunk and collars"
solid=True
for k in range(3):
    xx=14.3+k*.94;tube((xx,49,3.25),(xx,90,3.25),.35,teal if k%2 else silver,14)
    for yy in range(50,90,5):tube((xx,yy,3.25),(xx,yy+.16,3.25),.40,metal,12)
for yy in [50,57,62,77,89]:box(15.2,yy,1.54,4,.47,3.08,walk)

group="Transformer yard floodlights"
for x,y,tx,ty in ((17.7,122,24,129),(38.5,129.5,30,130.5),(18,139,23.5,133)):
    dx,dy=tx-x,ty-y
    box(x,y,.16,.68,.68,.32,walk)
    tube((x,y,.30),(x,y,4.65),.12,metal)
    direction=Vector((dx,dy,-4.35)).normalized()
    center=Vector((x,y,4.74))
    # Swivel housing and luminous face share the aiming axis of the baked spot.
    tube(center-direction*.14,center+direction*.12,.44,metal,8)
    tube(center+direction*.12,center+direction*.15,.36,neon,8)
    light("Transformer floodlight",center+direction*.19,780,(.80,.86,1))
    lights[-1].update(spot=True,dx=direction.x,dy=direction.z,dz=direction.y)
group="Transformer fins"
for x,y in [(21.5,126.5),(31.5,126.5),(21.5,135.5),(31.5,135.5)]:
    for side in [-1,1]:
        for k in range(8):box(x+side*2.65,y-1.75+k*.5,1.67,.55,.14,2,metal)
group="Service buildings"

# Pairs mark the approach to crossings and leave rail and walkway widths clear.
group="Striped verge bollards"
for x in (-7.55,7.55):
    for y in (9,22,34.4,41.6,53,62.8,71.2,84,98.8,107.2,119.8,128.2,140,151):
        bollard(x,y,0)
for y in (35.92,40.08):
    for x in (-21,-15,15,21):
        bollard(x,y,0)

# Mount arrival and waste-yard lights just below the eaves, facing the yards.
group="Yard wall lights"
for x,y,dx,dy,name,color,power in (
    (-32.5,35,0,-1,"Bus yard wall light",(1,.86,.68),390),
    (27,39.8,-1,0,"Workshop entry light",(.87,.91,1),310)):
    u,v=x+dx*.58,y+dy*.58
    tube((x,y,3.18),(u,v,3.45),.075,metal)
    box(u,v,3.41,.65,.56,.20,metal)
    box(u,v,3.29,.54,.45,.055,neon)
    light(name,(u,v,3.16),power,color)
# A light at the loading apron faces the staged drums and the truck.
group="Waste loading apron light"
box(25.2,32.6,.16,.7,.7,.32,walk)
tube((25.2,32.6,.30),(25.2,32.6,5.35),.13,metal)
tube((25.2,32.6,5.2),(25.2,31.85,5.49),.085,metal)
box(25.2,31.85,5.44,.87,.53,.23,metal)
box(25.2,31.85,5.30,.74,.42,.065,neon)
light("Loading apron floodlight",(25.2,31.62,5.18),900,(1,.83,.60))
lights[-1].update(spot=True,dx=.07,dy=-.54,dz=-.84)
group="Rail verge lights"
for x,y in [(8.05,44.8),(-8.05,45.6),(8.05,112.0),(-8.05,112.0)]:lamp(x,y,pi if x>0 else 0)

# Parked fleet car beside the shift bus.
group="Employee car";solid=True
with local(-32,19,.13,.10):
    box(0,0,.82,2.1,4.6,.82,teal);box(0,.2,1.45,1.78,2.44,.68,aqua)
    box(0,.2,1.84,1.83,2.04,.18,teal)
    for side in [-1,1]:
        for yy in [-1.3,1.27]:
            tube((side*.88,yy,.45),(side*1.12,yy,.45),.44,dark,12)
        box(side*.72,-2.34,.94,.43,.09,.18,cream)
# Site coordinates use x across the tracks, y north and z above grade.
group="Security receiving"
frontwall(-38,-24,35,3.6,1.25,2.65,[(-35.8,-34),(-32.9,-31.1),(-30,-28.2)])
group="Spill workshop"
frontwall(27,40,37,3.6,1.25,2.65,[(29,30.8),(31.9,33.7),(34.8,36.6)])
group="Pump house frontage"
box(-32,140,1.80,10,.28,3.60,teal)
group="Pond pump skid"
# A horizontal motor, coupling guard and centrifugal pump share a bolted bed.
x=-32.2
slab(x,143.58,2.12,4.90,.20,walk,.035)
for xx in (x-.65,x+.65):box(xx,143.55,.32,.23,4.55,.18,metal)
for y in (142.35,143.62,144.90):
    box(x,y,.52,1.63,.36,.28,teal)
tube((x,143.50,1.14),(x,145.34,1.14),.56,teal,12)
for i in range(6):
    y=143.65+.27*i
    tube((x,y,1.14),(x,y+.075,1.14),.62,teal,12)
tube((x,145.32,1.14),(x,145.55,1.14),.51,metal,12)
tube((x,142.72,1.14),(x,143.51,1.14),.27,ochre,10)
tube((x,142.04,1.14),(x,142.74,1.14),.72,teal,12)
tube((x,141.78,1.14),(x,142.08,1.14),.44,silver,12)
tube((x,139.87,1.14),(x,141.82,1.14),.27,silver,10)
pipe_run([(x-.34,142.36,1.40),(x-.34,142.36,2.30),(-36.94,142.36,2.30)],.21,silver)
for xx in (-33.60,-35.78):
    tube((xx,142.36,2.30),(xx+.11,142.36,2.30),.32,teal,12)
box(x+.24,144.6,1.70,.50,.53,.28,metal)
box(-31.0,143.6,3.68,1.20,.44,.14,metal)
box(-31.0,143.6,3.59,1.05,.34,.055,neon)
light("Pump hut task light",(-31,143.6,3.49),450,(1,.83,.61))
# Two short, visible footing-and-post pairs for the storage placard.
group="Waste storage placard"
for x in (26.10,30.90):
    slab(x,33.3,.70,.70,.26,walk)
    box(x,33.3,1.28,.17,.22,2.24,metal)
box(28.50,33.13,2.13,6,.20,.98,teal)
sign("WASTE STORAGE",28.50,33.00,2.13,1.17,neon)

group="Security receiving"
def worktable(x,y,w,d,h):
    for dx in (-w/2+.13,w/2-.13):
        for dy in (-d/2+.13,d/2-.13):box(x+dx,y+dy,h/2,.13,.13,h,metal)
    box(x,y,h,w,d,.15,wood)
worktable(-31.3,42.4,4,1.55,1.04)
box(-31.7,42.6,1.26,.52,.42,.38,metal)
box(-31.7,42.6,1.77,1.44,.18,.92,dark)
box(-31.7,42.49,1.77,1.24,.045,.70,aqua)
for j in range(3):
    box(-32.10+j*.25,42.453,1.58,.15,.024,.22,ochre)
box(-30.2,42.3,1.30,.56,.41,.36,teal)
tube((-30,42.4,1.48),(-29.94,42.4,1.95),.024,silver,6)
box(-33.5,46.73,1.72,2.95,.15,1.90,wood)
box(-33.5,46.63,1.72,2.48,.045,1.59,teal)
for text,z,h in (("DRUM DELIVERY",2.15,.44),("02:10   ARRIVED",1.74,.36),("DEPARTED   ____",1.33,.34)):
    sign(text,-33.5,46.57,z,h,metal)
for x in (-36.5,-35.5):
    box(x,44.7,1,.85,.92,2,teal)
    for h in (.42,1.06,1.70):box(x,44.22,h,.21,.06,.06,silver)
box(-31,43.85,.54,.83,.80,.15,wood)
box(-31,44.20,.99,.83,.12,.85,wood)
for dx in (-.29,.29):
    for dy in (-.27,.27):box(-31+dx,43.85+dy,.26,.09,.09,.52,metal)
group="Spill workshop"
worktable(33.6,46.0,4.35,1.65,1.03)
for x in (32.15,33.23):box(x,46,1.20,.25,1.05,.29,metal)
tube((31.96,46,1.68),(33.47,46,1.68),.42,ochre,8)
for x in (32.10,33.24):tube((x,46,1.68),(x+.09,46,1.68),.45,metal,8)
tube((34.07,45.92,1.115),(34.07,45.92,1.18),.41,silver,10)
box(35.1,46.25,1.16,.20,.64,.08,metal)
box(35.1,46.61,1.17,.32,.23,.10,silver)
box(29,48.6,1.35,2.3,.22,2.70,ochre)
for x in (27.9,30.1):box(x,48.34,1.35,.16,.65,2.70,ochre)
for h in (.16,.87,2.61):box(29,48.36,h,2.3,.7,.13,ochre)
for x in (28.6,29.5):tube((x,48.43,1.90),(x,48.05,1.84),.055,silver)
box(29,48.30,2.33,1.88,.08,.39,teal);sign("SPILL KIT",29,48.22,2.33,.48,neon)
tube((27.65,39.43,.38),(27.35,39.7,1.86),.057,wood)
box(27.65,39.43,.29,.36,.10,.51,metal);box(27.65,39.37,.18,.32,.024,.20,waste)
for x in (32.6,33.2,33.8):tube((x,45.32,1.13),(x+.20,45.32,1.13),.045,silver)
light("Receiving task light",(-31.4,42.1,2.91),210,(1,.83,.58))
light("Workbench task light",(33.3,45.6,3.0),260,(.68,.82,1))

group="Transformer gravel apron"
for y,d in ((121.75,1.50),(133.25,15.50)):
    box(27.5,y,-.13,25,d,.30,asphalt)
group="Boundary concrete"
for x in (-47,47):
    for start,end in ((24,60),(72,108)):
        box(x,(start+end)/2,1.49,.34,end-start,2.98,walk)
        for y in range(start,end,6):
            box(x,y+3,1.50,.45,5.98,3,walk)
            box(x,y+3,3.02,.54,5.98,.15,cream)
    for start,end in ((0,24),(60,72),(108,160)):
        for y in (start+.14,end-.14):box(x,y,1.28,.46,.28,2.56,walk)
        for y in range(start+3,end,3):box(x,y,1.18,.12,.12,2.36,silver)
        for h in (.14,2.29):box(x,(start+end)/2,h,.08,end-start,.10,silver)

group="Roof stairwell"
# The existing roof is replaced by four pieces around one continuous stair.
xmin,xmax,ymin,ymax=-40.2,-11.8,54.8,91.2
a,b,c,d=-39.75,-37.24,74.75,84.2
# One welded planar ring gives the whole roof a single connected upper chart.
rings=[[(xmin,ymin),(xmax,ymin),(xmax,ymax),(xmin,ymax)],
       [(a,c),(b,c),(b,d),(a,d)]]
verts=[(x,y,h) for h in (7.80,8.10) for ring in rings for x,y in ring]
quads=[]
for i in range(4):
    j=(i+1)%4
    quads.extend([(8+i,8+j,12+j,12+i),(4+i,4+j,j,i),
                  (i,j,8+j,8+i),(4+j,4+i,12+i,12+j)])
geo(silver,verts,quads)
for k in range(30):
    y=89.2-(k+.5)*.46;h=3+(k+1)*.17
    box(-38.50,y,h-.10,2.06,.46,.20,walk)
for x in (-39.48,-37.52):
    tube((x,89.1,4.05),(x,75.62,9.12),.075,ochre,8)
    for k in range(0,30,4):
        y=89.2-(k+.5)*.46;h=3+(k+1)*.17
        box(x,y,h+.50,.085,.085,1.0,metal)
last_step=89.2-30*.46
box(-38.50,(c+last_step)/2,8.0,2.06,last_step-c,.20,walk)
# Stair headhouse. The low eave and gutter drain west, away from the doorway.
group="Roof access headhouse"
west,east=-40.00,-37.02
front,back=73.45,84.49
def roof_z(x):return 10.96+(x-west)*.14
def pitched_panel(x0,x1,y0,y1,bottom,top,material):
    plan=[(x0,y0),(x1,y0),(x1,y1),(x0,y1)]
    geo(material,[(x,y,f(x)) for f in (bottom,top) for x,y in plan],faces)
base=lambda x:8.10
for x0,x1 in ((west,west+.16),(east-.16,east)):
    pitched_panel(x0,x1,front,back,base,roof_z,teal)
pitched_panel(west+.16,east-.16,back-.16,back,base,roof_z,teal)
for x0,x1 in ((west+.16,-39.55),(-37.45,east-.16)):
    pitched_panel(x0,x1,front,front+.18,base,roof_z,teal)
pitched_panel(-39.55,-37.45,front,front+.18,lambda x:10.50,roof_z,teal)
pitched_panel(west-.19,east+.19,front-.55,back+.22,roof_z,lambda x:roof_z(x)+.18,silver)
box(west-.18,(front+back)/2,10.96,.13,back-front+.4,.15,metal)
# Door is propped against the interior wall, leaving a full-width walk-out.
box(east-.23,front+1.18,9.26,.085,1.93,2.32,metal)
box(-38.50,front+.65,10.77,.75,.30,.10,neon)
light("Roof stair landing",(-38.50,74.7,10.28),440,(1,.75,.42))
# Hollow containment shell, with reveals at the west access opening.
group="Refueling hall shell"
cx,cy=30,72
profile=[(0,10),(3.6,10),(10,10),(13,8.8),(15,5.5),(16,.4)]
def ringpoint(i,r,h):
    angle=i*pi/16
    return (cx+r*cos(angle),cy+r*sin(angle),h)
for i in range(32):
    for j,((h0,r0),(h1,r1)) in enumerate(zip(profile,profile[1:])):
        if j==0 and i in (15,16):continue
        for inset,rev in ((0,False),(.28,True)):
            face=[ringpoint(i,r0-inset,h0),ringpoint(i+1,r0-inset,h0),
                  ringpoint(i+1,r1-inset,h1),ringpoint(i,r1-inset,h1)]
            geo(cream,face,[(3,2,1,0) if rev else (0,1,2,3)])
for i in (15,17):
    geo(metal,[ringpoint(i,r,h) for r,h in ((10,0),(9.72,0),(9.72,3.6),(10,3.6))],[(0,1,2,3),(3,2,1,0)])
tube((30,72,15.91),(30,72,16.1),.44,cream,16)
box(20.08,72,3.60,.62,4.06,.26,ochre)
for y in (69.99,74.01):box(20.12,y,1.82,.58,.22,3.62,metal)
group="Refueling pool"
pool_blue=mat("Refueling water","378db9","lake water",.36)
water_face([(26.26,67.345,1.0),(33.74,67.345,1.0),(33.74,76.655,1.0),(26.26,76.655,1.0)],[(0,1,2,3)],pool_blue)
rim=1.12
for x in (25.58,34.42):box(x,72,rim/2,1.36,11.9,rim,metal)
for y in (66.72,77.28):box(30,y,rim/2,7.5,1.25,rim,metal)
for x in (25.58,34.42):
    for j in range(39):box(x,66.25+j*.30,rim+.022,1.36,.065,.044,silver)
for y in (66.72,77.28):
    for j in range(25):box(26.45+j*.295,y,rim+.022,.065,1.25,.044,silver)
for j in range(6):
    h=.19*(j+1)
    box(22.86+j*.37,72,h/2,.37,2.65,h,walk)
    box(26.44+j*.36,72,(1.14-.19*j)/2,.36,1.56,1.14-.19*j,walk)
for x in (26.17,33.83):
    for start,end in ((66.4,70.95),(73.05,77.6)):
        tube((x,start,2.18),(x,end,2.18),.065,ochre)
        for y in (start,(start+end)/2,end):box(x,y,1.66,.09,.09,1.03,metal)
for y in (67.3,76.7):
    tube((26.17,y,2.18),(33.83,y,2.18),.06,ochre)
solid=False
for x in (27,30,33):
    box(x,72,.006,.32,7.9,.006,aqua)
solid=True
for x in (24.1,35.9):
    for y in (67.7,76.4):box(x,y,3.64,.38,.40,7.28,ochre)
    box(x,72,7.32,.55,10.7,.40,metal)
for y in (75.7,76.8):box(30,y,7.68,12.5,.27,.54,ochre)
for i in range(9):
    x=24.4+i*1.3;beam((x,75.7,7.9),(x+1.1,76.8,7.9),.075,metal)
box(30.8,76.25,7.49,1.55,1.5,.64,teal)
tube((30.8,76.25,7.23),(30.8,76.25,4.0),.035,metal,6)
tube((30.8,76.25,4.0),(31.06,76.25,3.78),.08,ochre)
for j in range(4):
    h=rim+(1.62-rim)*(j+1)/4
    box(35.28+j*.39,76,h/2,.39,1.38,h,walk)
box(37.22,76,.81,.83,1.8,1.62,metal)
box(37.48,76.46,2.1,.38,.51,.96,teal)
for x in (28,32):
    light("Submerged pool light",(x,72,.31),650,(.13,.69,1))
light("Crane service light",(31,75,7.17),820,(1,.72,.38))
light("Reactor doorway light",(22,72,3.3),580,(.65,.85,1))

# Both maintenance ramps rise almost four turns, finishing toward the tracks.
# Render and collision share the sloping deck; floor samples are exported for QA.
ramp_samples=[]
tower_profile=[(0,11),(3.2,11),(8,9.8),(16,7.6),(24,6.8),(30,7.3),(35,8.5),(36,8.5)]
def shell_radius(h):
    for (a,r),(b,sr) in zip(tower_profile,tower_profile[1:]):
        if h<=b:return r+(sr-r)*(h-a)/(b-a)
    return 8.5
for tx in (-26,26):
    facing=1 if tx<0 else -1
    def ramp_section(i):
        turn,step=divmod(i,40)
        h=.09+35.41*(turn+min(step/37.0,1.0))/4
        angle=(.02+i/160*3.98)*2*pi
        ro=shell_radius(h)-.58
        def pt(r,z):return (tx+facing*r*cos(angle),103+facing*r*sin(angle),z)
        return h,ro,pt
    group="West tower ramp" if tx<0 else "East tower ramp"
    for i in range(160):
        h,ro,pt=ramp_section(i);hh,rr,other=ramp_section(i+1)
        ri=ro-2.23;rj=rr-2.23
        quad=[pt(ri,h),pt(ro,h),other(rr,hh),other(rj,hh)]
        geo(metal,[(x,y,z-.14) for x,y,z in quad]+quad,faces)
        if i%2==0:
            for r in ((ri,ro) if h<3.5 else (ri,)):
                x,y,_=pt(r,h)
                box(x,y,h+.51,.095,.095,1.02,metal)
        if i%2==0 and 3.55<=h<=35.0:
            # Seat the end within the shell wall, inside its polygonal outer face.
            level=h-.225
            square_beam(pt(ri+.05,level),pt(shell_radius(level)-.17,level),.18,silver)
        if i%5==0:
            a=pt(ri+.12,h+.012);b=pt(ro-.12,h+.012)
            tube(a,b,.018,stripe,4)
        q=tuple((a+b)/2 for a,b in zip(pt((ro+ri)/2,h),other((rr+rj)/2,hh)))
        ramp_samples.append(dict(x=q[0],y=q[2],z=q[1]))
    # Continuous rectangular rails share a cross-section between consecutive spans.
    for outside in (False,True):
        count=next((i for i in range(161) if outside and ramp_section(i)[0]>=3.5),160)
        for lift,half,material in ((1.04,.058,ochre),(.51,.037,silver)):
            verts=[];quads=[]
            for i in range(count+1):
                h,ro,pt=ramp_section(i)
                r=ro if outside else ro-2.23
                verts.extend(pt(r+dr,h+lift+dz) for dr,dz in ((-half,-half),(half,-half),(half,half),(-half,half)))
                if i:
                    for j in range(4):
                        lo=(i-1)*4
                        k=(j+1)%4
                        quads.append((lo+j,lo+4+j,lo+4+k,lo+k))
            quads.extend([(0,1,2,3),tuple(reversed(range(count*4,count*4+4)))])
            geo(material,verts,quads)
    # Last sector meets the open end of a level, rail-facing overlook.
    x=tx+facing*7.7;y=103+facing*1.51
    box(x,y,35.40,4.3,3.05,.20,metal)
    for yy in (103+facing*3,):
        square_beam((x-2.1,yy,36.56),(x+2.1,yy,36.56),.14,ochre)
    for xx in (x-2.10,x+2.10):
        # The rail facing the other tower parts around the pipe walkway.
        gap=(xx-x)*facing>0
        spans=((0,.85),(2.15,3)) if gap else ((0,3),)
        for a,b in spans:square_beam((xx,103+facing*a,36.56),(xx,103+facing*b,36.56),.14,ochre)
        for along in ((.1,.85,2.15,2.96) if gap else (.1,2.96)):
            box(xx,103+facing*along,36.02,.11,.11,1.04,metal)
    group="Cooling tower luminaires"
    for i in range(6):
        a=(i+.55)*pi/3;ux=cos(a);uy=sin(a)
        x=tx+12.4*ux;y=103+12.4*uy
        box(x,y,.22,.97,.94,.44,walk);box(x,y,.57,.75,.60,.28,metal)
        box(x,y,.73,.59,.46,.06,mint)
        light("Shell uplight",(x,y,1.02),1550,(.44,.76,1))
        lights[-1].update(spot=True,dx=-ux*.27,dy=.96,dz=-uy*.27)
    for h in (6,15,24,33):
        x=tx+facing*(shell_radius(h)-1.0)
        light("Ramp safety lamp",(x,104,h+1.53),390,(.51,.78,1))


group="Tower lookout pipe walkway"
a=Vector((-16.40,104.5,35.37));b=Vector((16.40,101.5,35.37))
across=Vector((-(b-a).y,(b-a).x,0)).normalized()
for offset,m in zip((-.245,0,.245),(silver,ochre,teal)):
    tube(a+across*offset,b+across*offset,.12,m,10)

group="Annex heat exchangers"
for x in (26.35,27.65):
    tube((x,58.95,.55),(x,58.95,2.75),.55,silver,12)
    tube((x,58.95,.38),(x,58.95,.55),.34,silver,12,.55)
    tube((x,58.95,2.75),(x,58.95,2.96),.55,silver,12,.22)
    for h in (.72,1.65,2.58):tube((x,58.95,h),(x,58.95,h+.09),.59,metal,12)
    for dx in (-.34,.34):box(x+dx,58.95,.28,.12,.50,.46,metal)
    pipe_run([(x,58.95,2.90),(x,58.95,3.55),(x,59.75,3.55)],.13,ochre)
tube((26.35,58.36,1.20),(27.65,58.36,1.20),.075,teal,8)
for x in (26.35,27.65):tube((x,58.33,1.20),(x,58.52,1.20),.10,teal,8)
tube((27,58.36,1.20),(27,58.17,1.20),.035,metal,6)
tube((27,58.19,1.20),(27,58.14,1.20),.20,ochre,8)
group="Annex control panel"
box(19.54,56.3,.55,.56,2.4,1.10,teal)
box(19.40,56.3,1.65,.28,2.4,1.10,teal)
box(19.83,56.3,1.12,.10,2.5,.08,silver)
for i,y in enumerate((55.45,56.02,56.59,57.16)):
    tube((19.54,y,1.83),(19.60,y,1.83),.21,cream,10)
    box(19.615,y+.04,1.87,.015,.17,.028,dark)
    for j,m in enumerate((mint,neon,aqua)):box(19.57,y-.15+j*.15,1.37,.07,.08,.08,m)
    box(19.85,y,.70,.07,.09,.28,ochre)
group="Annex lockers"
for i in range(4):
    y=54.75+i*.68
    box(34.43,y,1.16,.62,.64,2.18,teal if i%2 else metal)
    box(34.105,y,1.16,.03,.55,2.02,silver if i%2 else teal)
    for h in (1.78,1.88,1.98):box(34.08,y,h,.03,.36,.035,dark)
    box(34.07,y+.18,1.15,.06,.06,.22,ochre)
group="Annex parts shelves"
for x in (30.15,32.55):
    for y in (52.22,52.74):box(x,y,.92,.09,.09,1.70,silver)
for h in (.30,.95,1.66):box(31.35,52.48,h,2.5,.60,.08,metal)
for x,h,w,tall,m in ((30.55,.34,.55,.36,ochre),(31.35,.34,.70,.28,teal),(32.12,.34,.46,.44,wood),
                     (30.65,.99,.46,.30,teal),(31.55,.99,.62,.40,wood),(32.20,.99,.34,.24,ochre)):
    box(x,52.48,h+tall/2,w,.44,tall,m)
for x in (30.65,31.15,31.65):tube((x,52.30,1.82),(x,52.66,1.82),.12,silver,10)
tube((32.15,52.48,1.70),(32.15,52.48,2.02),.17,ochre,10)
group="Annex shift desk"
worktable(22.8,52.62,1.8,.82,.86)
box(22.55,52.45,1.20,.78,.14,.50,dark)
box(22.55,52.53,1.20,.66,.025,.39,aqua)
box(22.55,52.45,.97,.13,.13,.08,metal)
box(22.55,52.82,.955,.62,.22,.035,metal)
tube((23.35,52.72,.94),(23.35,52.72,1.13),.075,ochre,8)
box(23.15,52.45,.95,.34,.24,.025,cream)
tube((22.7,53.50,.07),(22.7,53.50,.52),.06,metal,8)
tube((22.7,53.50,.52),(22.7,53.50,.62),.27,wood,8)

group="Receiving lockers and shift bench"
for x in (-36.6,-35.6,-34.6):
    box(x,36.65,1.15,.96,.68,2.30,teal)
    box(x,37.01,1.15,.85,.06,2.10,silver)
    for h in (1.86,1.95,2.04):box(x,37.05,h,.54,.03,.035,metal)
    box(x+.28,37.08,1.1,.065,.08,.25,ochre)
worktable(-35.65,38.52,3.24,.82,.53)
box(-27.0,46.80,1.65,3.0,.22,1.55,metal)
box(-27.0,46.65,2.11,2.66,.09,.30,teal)
sign("SHIFT KEYS",-27.0,46.57,2.11,.39,neon)
for j in range(7):
    x=-28.07+j*.35
    box(x,46.60,1.70,.075,.13,.16,silver)
    if j not in (2,5):
        tube((x,46.50,1.71),(x,46.50,1.38),.048,ochre,6)
        box(x+.07,46.50,1.4,.16,.07,.06,ochre)
group="Workshop tool storage"
for x in (37.2,39.2):
    for y in (44.4,47.5):box(x,y,1.25,.11,.11,2.5,silver)
for h in (.22,1.10,1.95):box(38.2,45.95,h,2.12,3.26,.11,metal)
for y in (44.85,45.70,46.64):
    box(38.1,y,.57,1.34,.65,.56,ochre)
    box(38.1,y,1.42,1.28,.62,.49,teal)
box(36.5,38.0,.77,1.57,.83,1.02,ochre)
for h in (.53,.77,1.0):box(36.5,38.44,h,1.22,.07,.065,metal)
for x in (35.93,37.07):
    for y in (37.76,38.24):tube((x-.08,y,.16),(x+.08,y,.16),.17,dark,8)
group="Pool manifolds"
for x in (31.0,32.4,33.8):
    tube((x,65.45,1.13),(x,65.45,1.27),.51,silver,12)
tube((26,80,4.06),(27.92,80,4.06),.47,teal,12)
for x in (26,27.92):
    tube((x,80,4.06),(x,80,.40),.30,teal,10)
    box(x,80,.25,1.06,1.14,.50,walk)
for tx in (-26,26):
    facing=1 if tx<0 else -1
    for i in (2,5,8,11):
        a=(.02+i/160*3.98)*2*pi;h=.09+35.41*(i/37)/4
        x=tx+facing*9.62*cos(a);y=103+facing*9.62*sin(a)
        top=h-.14
        box(x,y,(.035+top)/2,.18,.18,top-.035,silver)


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
(root/'ArtSource/NuclearFullBlock/site.json').write_text(json.dumps(dict(materials=list(materials.values()),lights=lights,labels=labels,pipeJoints=joints,rampSamples=ramp_samples),indent=2))
