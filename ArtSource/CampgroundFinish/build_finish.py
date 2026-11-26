"""Build the additive Lakeside Campground FBX and Quackville atlas using Blender 5.3 in background mode.
Run from the repository root. terrain.f32z contains the approved generated terrain vertex heights.
The Unity shell owns terrain, foundations, door positions, rail, and gameplay components.
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
ferns=[]
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
screen=mat("Water glint","829da3","lake water",.15)
dark=mat("Ash bed","242a2c","gravel")
rock=mat("Shore stone","83868b","rock")
salt=mat("Hearth stones","aaa49b","rock")
loam=mat("Earth","796c5e","gravel")
leaf=[mat("Brush "+str(i),h,"foliage") for i,h in enumerate(["527050","698254","48654b","81905f"])]
batches=defaultdict(lambda:[[],[]]);frame=Matrix.Identity(4)
zone="Campground"
def geo(m,v,f):
    points=[tuple(frame@Vector(t)) for t in v]
    x,y,z=[sum(q[i] for q in points)/len(points) for i in range(3)]
    solid=zone not in ("Shoreline brush","Water ripples","Trail brush","Pine crowns","Gravel trails","Track ballast","Fern thicket","Horsetail","Lamp glass") and m not in (amber,screen)
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
    for xx in [-1.7,1.7]:box(x+xx,y+.21,z+1.4,.21,.24,2.8,wood)
    box(x,y,z+2.42,4.4,.24,1.24,teal);sign(title,x,y-.16,z+2.43,.65,neon)
def tent(x,y,fire,m):
    """A pitched canvas shell with an open entrance facing the central fire."""
    angle=math.atan2(fire[0]-x, -(fire[1]-y))
    w,d,h=3.85,4.4,2.25
    with local(x,y,ground(x,y),angle):
        box(0,0,.03,w,d,.06,loam)
        for side in (-1,1):
            end=side*w/2
            roof=[(end,-d/2,.04),(end,d/2,.04),(0,d/2,h),(0,-d/2,h)]
            # A separate lining remains visible from inside even with backface culling.
            inner=[(u*.98,v,z-.035) for u,v,z in roof]
            face=(0,1,2,3) if side>0 else (3,2,1,0)
            geo(m,roof,[face])
            geo(m,inner,[tuple(reversed(face))])
            for yy in (-d/2,d/2):
                tube((end*.55,yy,h*.45),(end*1.25,yy*1.13,0),.028,cream,5)
            geo(m,[(end,-d/2,.04),(end*.58,-d/2,.04),(0,-d/2,h)],[(0,1,2),(2,1,0)])
        back=[(-w/2,d/2,.04),(w/2,d/2,.04),(0,d/2,h)]
        geo(m,back,[(2,1,0)])
        geo(m,[(u,v-.04,z) for u,v,z in back],[(0,1,2)])
        for yy in (-d/2+.10,d/2-.10):
            tube((0,yy,0),(0,yy,h-.04),.046,pale,6)

# Coordinates are block-local X/Z. The southwest damaged tent remains under its fallen pine.
camps=[
    ((-25.5,24.3),[(-31.9,25.8),(-20.3,20.5),(-19.8,32)]),
    ((15.7,46.5),[(11.7,41.4),(20.8,43.3),(13.1,52.7)]),
    ((30.6,137.7),[(27.8,132.6),(35.5,135.1),(29.8,144.0)])]

def sign(txt,x,y,z,size,m,rot=0):labels.append(dict(text=txt,x=x,y=z,z=y,size=size,yaw=rot))
def light(name,loc,power,color):fixtures.append(dict(name=name,x=loc[0],y=loc[2],z=loc[1],power=power))
def beam(a,b,r=.12,m=metal):tube(a,b,r,m,5)
def gable(x,y,w,d,z,rise,m):
    # Start both gable ends within the ceiling and carry them into a thick roof skin.
    for side in (-1,1):
        yy=y+side*(d-1.1)/2
        section=[(x-w/2,z-.13),(x+w/2,z-.13),(x+w/2,z+.05),
                 (x,z+rise+.05),(x-w/2,z+.05)]
        vertices=[(u,yy+offset,h) for offset in (-.10,.10) for u,h in section]
        sides=[(i,i+5,(i+1)%5+5,(i+1)%5) for i in range(5)]
        geo(wood,vertices,[(0,1,2,3,4),(9,8,7,6,5)]+sides)
        skin=[(x+side*w/2,y-d/2,z),(x+side*w/2,y+d/2,z),
              (x,y+d/2,z+rise),(x,y-d/2,z+rise)]
        top=[(u,v,h+.14) for u,v,h in skin]
        order=[0,1,2,3] if side==1 else [3,2,1,0]
        ring=[skin[i] for i in order]
        geo(m,ring+[top[i] for i in order],faces)
        for j in range(int(d/1.3)+1):
            yy=y-d/2+j*1.3
            beam((x+side*w/2,yy,z+.153),(x,yy,z+rise+.153),.047,metal)
    # A wall plate wraps all four walls at the eaves. Its lower edge overlaps the wall tops.
    for side in (-1,1):
        box(x,y+side*(d-1.1)/2,z-.105,w-.90,.28,.28,bark)
        box(x+side*(w-1.1)/2,y,z-.105,.28,d-.90,.28,bark)
def campground():
    global zone
    zone="Cabin porches"
    for x,y,w,d in buildings[:5]:
        z=ground(x,y)+floor_lift
        for side in [-1,1]:
            for yy in [-d*.5-2.2,-d*.5]:
                tube((x+side*w*.45,y+yy,z-.14),(x+side*w*.45,y+yy,z+2.68),.13,bark)
        box(x,y-d*.5-1.1,z-.045,w,2.6,.10,wood)
        box(x,y-d*.5-1.1,z+2.85,w+.4,3,.28,teal)
    zone="Campsite tents"
    for fire,pitches in camps:
        for i,(x,y) in enumerate(pitches):
            tent(x,y,fire,tentred if i==1 else canvas)
    zone="Trail markers"
    marker(16,7,ground(16,7),"PINE LAKE")
    zone="Camper awning"
    with local(31.6,35.9,ground(31.6,35.9)):
        for i in range(8):box(-3.1+i*.48,0,2.35,.48,4.4,.13,cream if i%2 else rus)
        for y in [-2,2]:tube((-3.3,y,0),(-3.3,y,2.4),.06,silver)
    zone="Water ripples"
    for i in range(15):
        x=random.uniform(-33,-23);y=random.uniform(72,92);box(x,y,-.77,random.uniform(.45,1.9),.08,.025,screen)
def details():
    global zone
    zone='Gabled roofs'
    for x,y,w,d in buildings:
        z=ground(x,y)+3.36+floor_lift
        gable(x,y,w+1.1,d+1.1,z,2.15,teal)
        box(x+w*.32,y+d*.17,z+1.4,.85,.84,2.83,terr)
        box(x+w*.32,y+d*.17,z+2.87,1.06,1.06,.16,metal)
    zone="Dock piles"
    for xx in [-26,-21,-15]:
        for yy in (77.8,81.4):
            toe=min(ground(xx+dx,yy+dy) for dx,dy in [(-.16,0),(.16,0),(0,-.16),(0,.16)])-.40
            tube((xx,yy,toe),(xx,yy,.66),.16,bark)
            tube((xx,yy,.61),(xx,yy,.73),.20,cream)
    zone="Fire rings"
    for (x,y),pitches in camps:
        z=ground(x,y)
        tube((x,y,z-.16),(x,y,z+.055),1.53,dark,10)
        for j in range(10):a=j*2*pi/10;shrub(x+cos(a)*1.35,y+sin(a)*1.35,z,.41,.37,salt)
        for offset in (-.48,.48):
            tube((x-1,y+offset,z+.21),(x+1,y+offset,z+.21),.17,bark)
        tube((x,y-1,z+.52),(x,y+1,z+.52),.17,bark)
        light("Fire pit",(x,y,z+.65),500,(1,.37,.08))

floor_lift = .14
buildings = [(34,76,10,10),(34,99,10,10),(20,121.5,10,9),(18,27,12,10),
             (34,50,10,8),(-11,57.5,10,9),(-11,97,10,12)]
lichen = mat("Lichen granite","85877c","rock")
slate = mat("Blue grey granite","696f74","rock")
blanket = mat("Faded wool","627d87","ochre canvas")
gravel_mix = mat("Trail aggregate","a6957e","gravel")
ballast_mix = mat("Rail aggregate","837e77","rock")
bulb = mat("Lantern glass","ffcd7c","concrete",2.7)
solar = mat("Solar diffuser","c5decb","concrete",1.3)

def spruce(x,y,height,radius,mature=False):
    global zone
    # Each tier has an uneven eave and a shaded, downward-facing cap.
    with local(x,y,ground(x,y),random.uniform(0,2*pi)):
        zone="Pine trunks"
        if mature:
            zone="Old growth trunks"
            foot=.85+(height-18)*.065
            stem=[(-.16,foot*1.26),(1.1,foot*.78),(height*.43,foot*.65),(height*.88,.17)]
            verts=[(r*cos(k*pi/4),r*sin(k*pi/4),z) for z,r in stem for k in range(8)]
            sides=[(8*j+k,8*j+(k+1)%8,8*j+(k+1)%8+8,8*j+k+8) for j in range(3) for k in range(8)]
            geo(bark,verts,[tuple(reversed(range(8))),tuple(range(24,32))]+sides)
            for k in range(3):
                a=k*2.4+height
                z=height*(.24+.08*k)
                tube((0,0,z),(radius*.65*cos(a),radius*.65*sin(a),z+.8),foot*.26,bark,6,.09)
        else:
            tube((0,0,-.05),(0,0,height*.89),min(.31,height*.035),bark,7,.09)
        zone="Pine crowns"
        tiers=[(.23,.44,1),(.41,.41,.81),(.59,.41,.58)]
        if mature:
            start=.28+.06*cos(height)
            tiers=[(start,.42,1),(.48,.37,.84),(.66,.34,.60)]
        for tier,(base,span,r) in enumerate(tiers):
            angle=random.uniform(-.35,.35)
            rim=[]
            for k in range(9):
                t=angle+k*2*pi/9
                rr=radius*r*random.uniform(.87,1.1)
                rim.append((rr*cos(t),rr*sin(t),height*(base+random.uniform(-.028,.028))))
            pts=rim+[(u*.58,v*.58,height*(base+.40*span)) for u,v,_ in rim]
            pts += [(.07*radius,-.08*radius,height*(base+span)),(0,0,height*base+.12)]
            side=[]
            for k in range(9):
                n=(k+1)%9
                side.extend([(k,n,n+9),(k,n+9,k+9),(k+9,n+9,18)])
            geo(leaf[[2,0,1][tier]],pts,side)
            geo(leaf[2],pts,[(k,19,(k+1)%9) for k in range(9)])

def woodland():
    points=json.loads((source_dir/"woodland.json").read_text())
    for i in range(28):
        points.append((random.choice([-44,-39,40,44])+random.uniform(-1,1),random.uniform(8,152)))
    for i,(x,y) in enumerate(points):
        tall=i%5==2
        h=random.uniform(13.0,17.2) if tall else random.uniform(5.2,10.0)
        radius=random.uniform(1.75,2.6) if tall else random.uniform(1.55,2.95)
        mature=i in {4,12,19,22,28,30,32,34,39,43,47,52,55,59,62,72}
        if mature:
            h=18.5+5*(.5+.5*sin(i*2.73))
            radius=3.15+.95*(.5+.5*cos(i*1.82))
        # Clear the true eave/porch footprint, including the irregular lower crown.
        for bx,by,w,d in buildings:
            if abs(x-bx)<w/2+.65+radius*1.12 and by-d/2-3-radius<y<by+d/2+.65+radius:
                x=bx+math.copysign(w/2+1.20+radius*1.12,x-bx)
        for fire,pitches in camps:
            if any(math.hypot(x-px,y-py)<radius+3.5 for px,py in pitches):
                # Shift trees away from occupied pitches toward the outer woodland.
                x=math.copysign(min(44,max(abs(x)+5,abs(fire[0])+9)),x)
        spruce(x,y,h,radius,mature)

def canoe(paint):
    # Stations run from stem to stem. Separate inner and outer skins meet at the gunwales.
    stations=[(-2.65,.045,.91),(-2.1,.37,.67),(-1.3,.62,.57),(0,.76,.54),
              (1.3,.62,.57),(2.1,.37,.67),(2.65,.045,.91)]
    rings=[]
    for y,w,h in stations:
        lift=.045+.30*(abs(y)/2.65)**3
        rings.append([(-w,y,h),(-w*.83,y,lift+.17),(-w*.43,y,lift),(w*.43,y,lift),
                      (w*.83,y,lift+.17),(w,y,h),(max(.01,w-.065),y,h),
                      (w*.68,y,lift+.24),(w*.35,y,lift+.09),(-w*.35,y,lift+.09),
                      (-w*.68,y,lift+.24),(-max(.01,w-.065),y,h)])
    for ix,offset in [(0,.14),(-1,-.14)]:
        rings[ix][6:]=[(x,y+offset,z) for x,y,z in rings[ix][6:]]
    for a,b in zip(rings,rings[1:]):
        for k in range(12):
            n=(k+1)%12
            geo(paint if k<5 else pale if k in (5,11) else wood,
                [a[k],a[n],b[n],b[k]],[(3,2,1,0)])
        for k in (0,5):tube(a[k],b[k],.045,pale,6)
    for end in (0,-1):
        ring=rings[end]
        # A solid stem closes the painted hull and the wooden liner on opposite sides.
        inner=ring[6:]
        outer=ring[:6]
        order=tuple(range(6)) if end==0 else tuple(reversed(range(6)))
        # Outer points run left to right around the keel (normal points south).
        geo(paint,outer,[order])
        geo(wood,inner,[order])
        geo(pale,[outer[0],outer[5],inner[0],inner[-1]],[(0,1,2,3) if end==0 else (3,2,1,0)])
    for y in (-1.18,1.18):
        # Fit each corner into the liner at the height of the seat, within the paint.
        def seat_width(v):
            w=.76+(.62-.76)*min(abs(v),1.3)/1.3
            lift=.045+.30*(abs(v)/2.65)**3
            h=.54+(.57-.54)*min(abs(v),1.3)/1.3
            t=(.44-lift-.24)/(h-lift-.24)
            return w*.68+((w-.065)-w*.68)*t+.012
        yy=[y-.14,y+.14]
        plan=[(-seat_width(yy[0]),yy[0]),(seat_width(yy[0]),yy[0]),
              (seat_width(yy[1]),yy[1]),(-seat_width(yy[1]),yy[1])]
        geo(pale,[(x,v,z) for z in (.405,.475) for x,v in plan],faces)
    tube((-.67,0,.46),(.67,0,.46),.035,wood,6)

def boat_beach():
    global zone
    zone="Canoes"
    for x,y,a,m in [(-28,111,.19,teal),(-24.9,111.4,-.25,ochre)]:
        # Rest the keel at its highest terrain contact along the beach.
        base=max(ground(x-sin(a)*v,y+cos(a)*v)-(.045+.30*(abs(v)/2.65)**3)
                 for v in (-2.65,-2.1,-1.3,0,1.3,2.1,2.65))
        with local(x,y,base-.01,a):canoe(m)
    zone="Canoe rack"
    with local(-30.6,116.7,ground(-30.6,116.7),.82):
        for y in (-1.48,1.48):
            for x in (-.96,.96):box(x,y,.86,.14,.18,1.72,wood)
            for h in (.48,1.57):box(0,y,h,2.12,.18,.15,bark)
        for level,m in [(.45,rus),(1.54,teal)]:
            with local(0,0,level):canoe(m)

def boulder(x,y,r,overlook=False):
    global zone
    zone="Overlook rocks" if overlook else "Shore boulders"
    # The widest ring straddles the bank. Unequal radii and tilted shoulders break the oval.
    with local(x,y,ground(x,y)-r*.09,random.uniform(0,2*pi)):
        n=random.choice([6,7,8])
        radii=[r*random.uniform(.81,1.18) for _ in range(n)]
        width=random.uniform(.52,1.10) if overlook else random.uniform(.74,1.05)
        relief=random.uniform(.68,1.28) if overlook else 1
        pts=[]
        for scale,h,dx in [(.43,-.85,0),(1,-.05,0),(.78,.53,.11),(.35,.88,-.05)]:
            for k,rr in enumerate(radii):
                a=2*pi*k/n
                pts.append((rr*scale*cos(a)+dx*r,width*rr*scale*sin(a),relief*r*(h+random.uniform(-.10,.10))))
        faces=[tuple(reversed(range(n))),tuple(range(3*n,4*n))]
        for ring in range(3):
            for k in range(n):
                a=ring*n+k;b=ring*n+(k+1)%n
                faces.extend([(a,b,b+n),(a,b+n,a+n)])
        geo(random.choice([rock,slate,lichen]),pts,faces)

def horsetail(x,y):
    global zone
    zone="Horsetail"
    h=random.uniform(.60,1.45)
    with local(x,y,ground(x,y),random.uniform(0,2*pi)):
        tube((0,0,0),(0,0,h),.024,leaf[0],4,.012)
        for k in range(1,4):
            z=h*k/4
            verts=[(-.2,0,z),(0,.2,z+.06),(.2,0,z),(0,-.2,z+.06)]
            geo(leaf[1],verts,[(0,1,2,3),(3,2,1,0)])

# Uniform size of each finished fern. Placement and passage spacing use the unscaled values.
FERN_SCALE=.75

def fern(x,y,height,reach):
    global zone,frame
    zone="Fern thicket"
    ferns.append(dict(x=x,y=ground(x,y),z=y,w=height*FERN_SCALE))
    with local(x,y,ground(x,y)-.09*FERN_SCALE,random.uniform(0,2*pi)):
        # Shrinks about the buried crown; local() restores the frame afterwards.
        frame=frame@Matrix.Scale(FERN_SCALE,4)
        # Stagger outer and inner fronds around a broad, buried crown.
        for k in range(5):
            with local(0,0,0,2*pi*k/5+random.uniform(-.16,.16)):
                outer=k in (1,3)
                length=reach*(1 if outer else .81)*random.uniform(.88,1.08)
                lift=height*(.74 if outer else 1)*random.uniform(.90,1.10)
                width=length*random.uniform(.49,.65)
                verts=[]
                profile=[(0,.53,0),(.36,1,.78),(.73,.76,1)]
                for t,w,h in profile:
                    r=(.29 if outer else .14)+t*length
                    z=h*lift
                    edge=r+(0 if t==0 else w*width*.47)
                    verts.extend([(edge,-w*width,z),(r,0,z+w*width*.32),(edge,w*width,z)])
                verts.append((length+(.29 if outer else .14),0,.85*lift))
                polygons=[(n,n+3,n+4,n+1) for row in range(len(profile)-1) for n in (3*row,3*row+1)]
                polygons.extend([(6,9,7),(7,9,8)])
                geo(leaf[(k+(1 if outer else 0))%4],verts,polygons)

def fern_beds():
    beds=[(-33,13,11,7),(-17,14,7,6),(-40,26,6,14),(-38,39,7,10),
          (-27,44,9,7),(-17,40,5,5),(-8.5,23,4,8)]
    turns=[(-40,9),(-41.7,14),(-40.2,18),(-42.6,23),(-39.7,27),(-41.8,31.5),
           (-39.8,35.5),(-42.2,39.5),(-42.4,43.5),(-39.8,46),(-40.7,49)]
    lane=[]
    for (ax,ay),(bx,by) in zip(turns,turns[1:]):
        steps=math.ceil(math.hypot(bx-ax,by-ay)/.35)
        lane.extend((ax+(bx-ax)*j/steps,ay+(by-ay)*j/steps) for j in range(steps))
    lane.append(turns[-1])
    def lane_distance(x,y):
        return min(math.hypot(x-u,y-v) for u,v in lane)
    for i in range(23):
        for j in range(24):
            x=-45+i*1.82+random.uniform(-.32,.32)
            y=7+j*1.82+random.uniform(-.32,.32)
            if not any(((x-cx)/rx)**2+((y-cy)/ry)**2<1 for cx,cy,rx,ry in beds):continue
            if ((x+25)/14.1)**2+((y-27.5)/12.5)**2<1 or trail_distance(x,y)<3.3:continue
            if ground(x,y)<-.38:continue
            reach=random.uniform(.9,1.42)
            # Keep the player's head clear in the winding passage through the western beds.
            if lane_distance(x,y)<.64+reach:continue
            height=random.uniform(.75,1.3) if random.random()<.28 else random.uniform(1.65,2.30)
            fern(x,y,height,reach)
    # Stagger taller stands on each shoulder. Gaps at different points give players
    # exits into the woods without a straight view through both sides of the passage.
    for side in (-1,1):
        for j in range(3 if side<0 else 6,len(lane)-2,5):
            if (j//5+(3 if side<0 else 0))%11==0:continue
            cx,cy=lane[j]
            tx=lane[j+1][0]-lane[j-1][0]
            ty=lane[j+1][1]-lane[j-1][1]
            offset=side*random.uniform(1.78,2.12)/math.hypot(tx,ty)
            x,y=cx+ty*offset,cy-tx*offset
            if ((x+25)/14.1)**2+((y-27.5)/12.5)**2<1 or lane_distance(x,y)<1.6:continue
            fern(x,y,random.uniform(1.72,2.48),random.uniform(.86,1.03))

    # Separate islands in the three marked clearings. Keep open grass at the sign,
    # beside the signed crossing and between neighboring groups.
    pockets=[(10,13),(23,10),(32,14),(8.8,27),(8.5,33),
             (-3.2,66),(-3.1,70.3),(8.5,132),(8.1,141),(6.5,150)]
    for cx,cy in pockets:
        for j in range(7):
            a=j*2.40
            rr=.68*math.sqrt(j)
            x=cx+rr*cos(a)
            y=cy+rr*sin(a)
            if trail_distance(x,y)>2.85:
                fern(x,y,random.uniform(1.05,1.85),random.uniform(.82,1.12))

def crushed_tent():
    global zone
    zone="Collapsed tent"
    # Local X follows the trunk. Its underside slopes toward the eastern end.
    with local(-27.5,32,0,math.atan2(-6,9)):
        verts=[]
        across=[(-1.7,.065),(-1.26,.23),(-.83,.13),(-.48,.36),
                (0,0),(.49,.31),(.79,.16),(1.24,.32),(1.69,.06)]
        for i,x in enumerate([-2.3,-1.24,-.08,.95,2.22]):
            for j,(y,h) in enumerate(across):
                ridge=.62-.0555*x-.332
                z=ridge if y==0 else max(.045,h+.071*sin(2.1*i+.8*j))
                verts.append((x+.12*sin(i+1.7*j),y+.055*cos(2*i+j),z))
        cols=len(across)
        top=[]
        for i in range(4):
            for j in range(cols-1):
                k=i*cols+j
                top.extend([(k,k+cols,k+cols+1),(k,k+cols+1,k+1)])
        n=len(verts)
        rim=list(range(0,n,cols))+list(range(n-cols+1,n))+list(range(n-cols-1,0,-cols))+list(range(cols-2,0,-1))
        underside=[(x,y,z-.035) for x,y,z in verts]
        geo(tentred,verts+underside,top+[tuple(k+n for k in reversed(t)) for t in top]+
            [(k,k+n,l+n,l) for k,l in zip(rim,rim[1:]+rim[:1])])

def shore():
    for x,y,size in [(-33,54,1.35),(-36,64,.75),(-34,72,1.65),(-38.5,86,1.85),
                     (-36,96,.91),(-35,103,1.52),(-30,108,.75),(-22,105,.72)]:
        boulder(x,y,size)
        for j in range(random.randint(2,4)):
            a=random.uniform(0,2*pi);r=size+random.uniform(.45,1.4)
            boulder(x+r*cos(a),y+r*sin(a),random.uniform(.26,.67))
    for cx,cy in [(-31,53),(-35,61),(-33,70),(-35,81),(-37,90),(-34,100),(-30,107),(-22,104),(-21,65)]:
        for j in range(17):
            x=cx+random.uniform(-1.25,1.25);y=cy+random.uniform(-2,2)
            if ground(x,y)>-1.1:horsetail(x,y)
    fern_beds()

def walls(index,w,d):
    global zone
    zone="Cabin walls"
    # Local tangent is the length of this wall. Openings remain clear through both skins and collision.
    for face,(x,y,length,angle) in enumerate([(0,-d/2,w,0),(0,d/2,w,pi),
                                               (-w/2,0,d,-pi/2),(w/2,0,d,pi/2)]):
        door=face==1 or (face==2 if index<2 else face==0)
        with local(x,y,0,angle):
            openings=[(-1.3,1.3,0,2.6)] if door else []
            for pos in (-length*.30,length*.30):
                openings.append((pos-.85,pos+.85,1.0,2.22))
            last=-length/2
            for left,right,low,high in sorted(openings):
                if left>last:box((last+left)/2,0,1.6,left-last,.20,3.2,wood)
                if low:
                    box((left+right)/2,0,low/2,right-left,.20,low,wood)
                    box((left+right)/2,0,low,right-left+.15,.32,.095,pale)
                    if (index+face+(left>0))%3==0:
                        for h,tilt in [(1.33,.09),(1.84,-.12)]:
                            board=[(u,-.145+v,h+dz+(2*(u-left)/(right-left)-1)*tilt)
                                   for u,v,dz in [(left-.1,-.04,-.095),(right+.1,-.04,-.095),
                                       (right+.1,.04,-.095),(left-.1,.04,-.095),
                                       (left-.1,-.04,.095),(right+.1,-.04,.095),
                                       (right+.1,.04,.095),(left-.1,.04,.095)]]
                            geo(bark,board,faces)
                box((left+right)/2,0,(high+3.2)/2,right-left,.20,3.2-high,wood)
                last=right
            if last<length/2:box((last+length/2)/2,0,1.6,length/2-last,.20,3.2,wood)

def table(x,y,w,d,h):
    box(x,y,h,w,d,.11,pale)
    for sx in (-1,1):
        for sy in (-1,1):box(x+sx*(w/2-.11),y+sy*(d/2-.11),h/2,.13,.13,h,wood)

def shelf(x,y,w):
    for sx in (-1,1):box(x+sx*w/2,y,1,.12,.72,2,wood)
    for z in (.24,.88,1.53,2):box(x,y,z,w,.74,.10,pale)

def crate(x,y,z,w):
    box(x,y,z+.32,w,.68,.64,wood)
    for sx in (-1,1):box(x+sx*(w*.5-.08),y-.35,z+.32,.10,.04,.64,pale)

def bed(x,y,h):
    table(x,y,1.32,2.25,h)
    box(x,y,h+.13,1.27,2.18,.19,cream)
    box(x,y-.42,h+.25,1.29,1.18,.09,blanket)

def furnish(i,w,d):
    global zone
    zone="Room furnishings"
    if i==0:
        for h in (.45,1.7):bed(3,1.4,h)
        shelf(-3,3.6,1.9)
    elif i==1:
        table(2.65,1,2.1,1.45,.81)
        for y in (-.10,2.12):table(2.65,y,1.8,.46,.43)
        box(2.6,1,.91,.6,.42,.09,teal)
    elif i==2:
        bed(-3,1.15,.43)
        for x,y,w in [(2.7,2.8,1.25),(2.9,1.9,.84)]:
            box(x,y,.24,w,.72,.48,rus)
            box(x,y,.5,w*.13,.72,.045,pale)
    elif i==3:
        table(2.8,.6,3.5,1.06,1.05)
        box(2.8,.12,.53,3.4,.12,.98,teal)
        shelf(3,3.96,2.55)
        for x in (2.25,3,3.75):box(x,3.96,1.23,.07,.65,.62,bark)
        box(2.4,.6,1.14,.59,.42,.09,blanket)
    elif i==4:
        zone="Washroom fittings"
        for side in (-1,1):
            with local(side*(w/2-.6),0,0,-side*pi/2):
                table(0,0,2.3,.85,.75)
                for dx in (-.55,.55):
                    tube((dx,0,.78),(dx,0,.91),.31,cream)
                    beam((dx,.29,.80),(dx,.25,1.12),.038,metal)
                box(0,.49,1.60,1.68,.08,.73,silver)
    elif i==5:
        shelf(-3,2.9,2.54);shelf(2.9,2.9,2.6)
        for x,z in [(-3.45,.29),(-2.5,.93),(2.6,.29),(3.35,1.58)]:crate(x,2.9,z,.65)
        crate(3,-2,0,1.15)
    else:
        table(2.85,2.65,2.9,1.13,.92)
        zone="Boathouse paddle rack"
        with local(-w/2+.10,0,0,pi/2):
            box(0,-.045,1.55,2.44,.15,.22,bark)
            for j in (-1,0,1):
                x=j*.72
                tube((x,-.18,.44),(x,-.18,2.23),.05,pale,7)
                box(x,-.18,1.61,.19,.11,.10,pale)
                box(x,-.18,.54,.28,.10,.70,ochre)
                for dx in (-.063,.063):
                    tube((x+dx,-.07,1.52),(x+dx,-.24,1.55),.032,metal,6)

def lamp(x,y,kind,angle=0):
    global zone,frame
    z=ground(x,y)
    # Interior lanterns sit directly on the raised foundation.
    for bx,by,w,d in buildings:
        if abs(x-bx)<w/2 and abs(y-by)<d/2:
            z=ground(bx,by)+floor_lift
            break
    zone="Lantern hardware"
    with local(x,y,z,angle):
        stake=kind=="Solar stake"
        fallen=kind=="Dropped lantern"
        if stake:
            tube((0,0,-.18),(0,0,.61),.037,metal,6)
        tip=math.radians(84)
        foot_radius=.16*1.15
        with local(0,0,.59 if stake else foot_radius*sin(tip) if fallen else 0):
            saved=frame
            if fallen:frame=frame@Matrix.Rotation(tip,4,'Y')
            r=.13 if stake else .16
            tube((0,0,0),(0,0,.09),r*1.15,metal)
            tube((0,0,.09),(0,0,.33),r,solar if stake else bulb)
            tube((0,0,.33),(0,0,.40),r*1.24,metal,8,r)
            if not stake:
                for dx in (-r,r):tube((dx,0,.08),(dx,0,.58),.019,metal,5)
                tube((-r,0,.58),(r,0,.58),.022,bark,5)
            # Place the baked emitter outside its opaque diffuser and metal frame.
            pos=frame@Vector((0,0,.73 if fallen else .66))
            frame=saved
    light(kind,tuple(pos),105 if stake else 225,(1,.78,.5))
    fixtures[-1]["yaw"]=90-math.degrees(angle)

def crossing(x,y,angle):
    global zone
    zone="Crossing signs"
    with local(x,y,ground(x,y),angle):
        box(0,.13,.95,.15,.15,1.9,wood)
        box(0,0,1.54,1.65,.14,.86,ochre)
        p=frame@Vector((0,-.082,1.55))
    sign("WATCH FOR\nTRAINS",p.x,p.y,p.z,.26,cream,-math.degrees(angle))

routes=json.loads((source_dir/"routes.json").read_text())
# The saved terrain still follows the old grades. These are the visible/walked trail centerlines.
routes["paths"][0]=routes["paths"][0][1:]
routes["paths"][2]=routes["paths"][2][1:]
# Both trails previously doubled back from (0,106), leaving a stub beside the rails.
routes["paths"][2]=routes["paths"][2][:-1]
routes["paths"][11]=[routes["paths"][2][-1]]+routes["paths"][11][2:]
routes["paths"][8]=routes["paths"][8][:-1]
routes["paths"][10]=routes["paths"][10][:-1]
routes["paths"] += [[(-10,79.5),(.18,79.85)],[(7.82,80),(9,80)],[(24,62),(22,62.6)]]

def trail_distance(x,y):
    best=float("inf")
    for path in routes["paths"]:
        for (ax,ay),(bx,by) in zip(path,path[1:]):
            dx,dy=bx-ax,by-ay
            t=max(0,min(1,((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)))
            best=min(best,math.hypot(x-ax-t*dx,y-ay-t*dy))
    return best

def crossbuck(x,y,angle):
    global zone
    zone="Railway crossbucks"
    with local(x,y,ground(x,y),angle):
        box(0,.15,1.45,.19,.19,2.90,bark)
        for side in (-1,1):
            # Each face is a wide painted board, with grain running across its edge.
            w=.16;d=.07
            outline=[(u*cos(pi/4)-v*side*sin(pi/4),2.46+u*side*sin(pi/4)+v*cos(pi/4))
                     for u,v in [(-1,-w/2),(1,-w/2),(1,w/2),(-1,w/2)]]
            y0=side*.034
            verts=[(u,y0+yoff,h) for yoff in (-d/2,d/2) for u,h in outline]
            geo(cream,verts,[(0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)])

def lookout():
    global zone
    zone="Lookout structure"
    corners=[(22,64),(22,74),(12,74),(12,64),(22,64)]
    start=ground(22,64)+.07
    rise=1.25
    top=start+4*rise
    for x in (13.6,20.4):
        for y in (65.6,72.4):
            bottom=ground(x,y)-.12
            box(x,y,(bottom+top)/2,.32,.32,top-bottom,bark)
    for x in (13.6,20.4):
        beam((x,65.6,ground(x,65.6)+.4),(x,72.4,top-.38),.13,wood)
        beam((x,72.4,ground(x,72.4)+.4),(x,65.6,top-.38),.13,wood)
    for y in (65.6,72.4):
        box(17,y,top-.22,7.10,.35,.32,bark)
    def guard(a,b):
        for fraction in (0,.5,1):
            p=Vector(a).lerp(Vector(b),fraction)
            box(p.x,p.y,p.z+.49,.11,.11,.98,bark)
        for h in (.49,.98):
            beam(tuple(Vector(a)+Vector((0,0,h))),tuple(Vector(b)+Vector((0,0,h))),.063,pale)
    zone="Lookout platforms"
    for i,(x,y) in enumerate(corners):
        h=start+i*rise
        box(x,y,h-.10,2,2,.20,wood)
        # Outer corners of each turning landing; the first landing opens toward the approach.
        sx=1 if x==22 else -1
        sy=1 if y==74 else -1
        guard((x+sx,y-1,h),(x+sx,y+1,h))
        if i>0:guard((x-1,y+sy,h),(x+1,y+sy,h))
    for i,(a,b) in enumerate(zip(corners,corners[1:])):
        dx=(b[0]-a[0])/10;dy=(b[1]-a[1])/10
        ax=a[0]+dx;ay=a[1]+dy;bx=b[0]-dx;by=b[1]-dy
        low=start+i*rise;high=low+rise
        # Top vertices form a single continuous plane. Flat landings use exactly its end heights.
        pts=[(ax+dy,ay-dx,low),(bx+dy,by-dx,high),(bx-dy,by+dx,high),(ax-dy,ay+dx,low)]
        zone="Lookout ramp"
        geo(wood,[(x,y,z-.18) for x,y,z in pts]+pts,faces)
        zone="Lookout railings"
        for side in (-1,1):
            guard((ax+dy*side,ay-dx*side,low),(bx+dy*side,by-dx*side,high))
        for t in (.22,.78):
            x=ax+(bx-ax)*t;y=ay+(by-ay)*t;h=low+rise*t-.18
            for side in (-.82,.82):
                px=x+dy*side;py=y-dx*side;bottom=ground(px,py)-.09
                if h>bottom:box(px,py,(h+bottom)/2,.19,.19,h-bottom,bark)
    # A short level gallery connects the final corner to the square lake-facing deck.
    zone="Lookout deck"
    box(22,66,top-.10,2,2,.20,wood)
    box(20,66,top-.10,2,2,.20,wood)
    box(16.5,69,top-.10,5,8,.20,wood)
    for a,b in [((23,65),(23,67)),((19,65),(21,65)),((19,67),(23,67)),
                ((14,65),(19,65)),((14,65),(14,73)),((14,73),(19,73)),((19,67),(19,73))]:
        guard((*a,top),(*b,top))

def refine():
    global zone
    woodland();boat_beach();shore()
    crushed_tent()
    for x,y,r in [(-32.5,130.3,2.48),(-30,133.8,1.45),(-34.1,135.8,1.94),
                  (-31.3,126.5,.87),(-30.2,128.2,.49),(-34.4,128.5,.63),
                  (-31.7,135.2,.60),(-33.8,138,.42),(-28.5,133,.38)]:
        boulder(x,y,r,overlook=True)
    for i,(x,y,w,d) in enumerate(buildings):
        with local(x,y,ground(x,y)+floor_lift):
            walls(i,w,d);furnish(i,w,d)
    zone="Boundary fence"
    for x in (-47,47):
        for j in range(40):
            y=2+j*4
            box(x,y,.78,.18,.18,1.56,bark)
            if j<39:
                for z in (.48,1.13):box(x,y+2,z,.13,4,.16,wood)
    crossing(-4.5,76.9,-pi/2);crossing(10.5,83.05,pi/2)
    for x,y,angle in [(9,53,0),(9,107,pi)]:
        crossbuck(x,y,angle)
    lookout()
    for x,y,a in [(16.2,24,.4),(36,73,0),(36.2,96,1.2),(17.1,119,0),
                  (31.2,48,.7),(-13.5,55,0),(-13.5,94,.8),(-17.5,35,0)]:
        lamp(x,y,"Dropped lantern" if a else "Ground lantern",a)
    for x,y in [(-13.5,75.8),(-12.4,85),(-22,110),(-34.3,51)]:
        assert ground(x,y)>-.49, "Solar stake requires a dry bank"
        lamp(x,y,"Solar stake")
    gravel_surfaces()

def gravel_surfaces():
    global zone
    zone="Track ballast"
    def profile(x,y):
        return [(x+dx,y,h) for dx,h in [(-2.42,.004),(-2.01,.022),(2.01,.022),(2.42,.004)]]
    for a,b in zip(routes["track"],routes["track"][1:]):
        first,last=profile(*a),profile(*b)
        for k in range(3):
            geo(ballast_mix,[first[k],first[k+1],last[k+1],last[k]],[(0,1,2,3)])
    # Clip the union of all trail footprints against the actual one-metre ground triangles.
    # Every resulting face is parallel to its supporting terrain face, including across slopes.
    zone="Gravel trails"
    distances={}
    tops=[]
    for path in routes["paths"]:
        for (ax,ay),(bx,by) in zip(path,path[1:]):
            dx,dy=bx-ax,by-ay
            for x in range(math.floor(min(ax,bx))-3,math.ceil(max(ax,bx))+4):
                for y in range(math.floor(min(ay,by))-3,math.ceil(max(ay,by))+4):
                    t=max(0,min(1,((x-ax)*dx+(y-ay)*dy)/(dx*dx+dy*dy)))
                    d=math.hypot(x-ax-t*dx,y-ay-t*dy)-1.52
                    distances[x,y]=min(d,distances.get((x,y),10))
    for x,y in sorted(distances):
        for delta in [[(0,0),(1,0),(1,1)],[(0,0),(1,1),(0,1)]]:
            tri=[(x+u,y+v) for u,v in delta]
            clipped=[]
            for a,b in zip(tri,tri[1:]+tri[:1]):
                da,db=distances.get(a,10),distances.get(b,10)
                if da<=0:clipped.append(a)
                if da*db<0:
                    t=da/(da-db)
                    clipped.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
            if len(clipped)>=3:
                # The top stays below each raised floor and the dock footbridge deck.
                geo(gravel_mix,[(u,v,ground(u,v)+PATH_RAISE) for u,v in clipped],
                    [tuple(range(len(clipped)))])
                tops.append(clipped)
    path_banks(tops)

PATH_RAISE=.065
BANK_WIDTH=.24
BANK_SINK=.035

def path_banks(tops):
    """Earth shoulders from every outer path edge down into the soil, so trails read as raised ground."""
    key=lambda p:(round(p[0],4),round(p[1],4))
    edges={}
    for poly in tops:
        for a,b in zip(poly,poly[1:]+poly[:1]):
            if key(a)!=key(b):edges[key(a),key(b)]=(a,b)
    # An edge shared by two top faces appears once in each direction; the rest form the outline.
    outline=[(k,e) for k,e in edges.items() if (k[1],k[0]) not in edges]
    normals=defaultdict(list)
    for (ka,kb),(a,b) in outline:
        dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy)
        # Top faces wind counterclockwise, so the outside is to the right of each edge.
        for k in (ka,kb):normals[k].append((dy/length,-dx/length))
    feet={}
    def foot(k,p):
        if k not in feet:
            nx=sum(n[0] for n in normals[k]);ny=sum(n[1] for n in normals[k]);length=math.hypot(nx,ny)
            if length<.001:nx,ny=normals[k][0];length=1
            nx/=length;ny/=length
            # Mitre neighbouring edges, limited so that sharp junctions do not grow spikes.
            reach=BANK_WIDTH/max(.65,nx*normals[k][0][0]+ny*normals[k][0][1])
            u,v=p[0]+nx*reach,p[1]+ny*reach
            feet[k]=(u,v,ground(u,v)-BANK_SINK)
        return feet[k]
    highest=-1
    for (ka,kb),(a,b) in outline:
        low_a,low_b=foot(ka,a),foot(kb,b)
        for t in (0,.25,.5,.75,1):
            u,v,z=(low_a[i]+(low_b[i]-low_a[i])*t for i in range(3))
            highest=max(highest,z-ground(u,v))
        top_a=(a[0],a[1],ground(*a)+PATH_RAISE);top_b=(b[0],b[1],ground(*b)+PATH_RAISE)
        geo(loam,[top_a,low_a,low_b,top_b],[(0,1,2),(0,2,3)])
    print("Path banks:",len(outline),"outline edges; highest foot above soil",round(highest,5))
    assert highest<0,"A path bank foot floats above the soil"

campground()
details()
refine()
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
def atlas_grain(image):
    # Retain native Quackville patterns at a texel scale visible from duck height.
    arr=np.array(image.pixels[:]).reshape(image.size[1],image.size[0],4)
    y=np.arange(32)*image.size[1]//32
    x=np.arange(32)*image.size[0]//32
    values=arr[y[:,None],x[None,:],:3].dot([.30,.57,.13])
    low,high=np.quantile(values,[.04,.96])
    tone=.57+.71*np.clip((values-low)/max(.001,high-low),0,1)
    return np.repeat(np.repeat(tone,2,axis=0),2,axis=1)[:,:,None]

slots={}
for i,m in enumerate(used):
    sw=swatches[m.name];x=(i%8)*128+32;y=(i//8)*128+32;slots[m]=(x,y)
    color=np.array(rgb(sw["color"]))
    tile=np.broadcast_to(color,(64,64,3)).copy()
    if sw['texture']:
        im=bpy.data.images.load(str(root/"Assets/DuckRails/CampgroundGreybox/Textures"/("Quackville "+sw['texture']+".png")),check_existing=True)
        tile*=atlas_grain(im)
    for pixels,t in [(base_pixels,tile),(glow_pixels,tile*sw["emission"]/3.5)]:
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
    tone = atlas_grain(reference)
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
def contact_surface(zone,m):
    if m in (rock,salt,terr,bulb,solar) or (zone=="Washroom fittings" and m==cream):
        return "Stone"
    if m in (loam,dark):return "Dirt"
    if m in (metal,silver) or (m==teal and zone in ("Cabin porches","Gabled roofs")):
        return "Metal"
    return "Wood"
for (zone,cx,cy,solid,m),(verts,polys) in batches.items():
    surface=contact_surface(zone,m) if solid else ""
    pos,indices,uvs=chunks[(zone,cx,cy,solid,surface)];u0,v0=slots[m]
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
for (zone,cx,cy,solid,surface),(verts,polys,uvs) in chunks.items():
    if not polys:continue
    key=f"{zone}_{cx}_{cy}_{surface+'_solid' if solid else 'detail'}"
    mesh=bpy.data.meshes.new(key);mesh.from_pydata(verts,[],polys);mesh.materials.append(atlas);mesh.update()
    layer=mesh.uv_layers.new()
    for loop in mesh.loops:layer.data[loop.index].uv=uvs[loop.vertex_index]
    ob=bpy.data.objects.new(key,mesh);s.collection.objects.link(ob)

for o in bpy.context.selected_objects:o.select_set(False)
for o in s.objects:
    if o.type=='MESH':o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/"Lakeside finish.fbx"),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False)
trails=[dict(points=[dict(x=x,z=y) for x,y in path]) for path in routes["paths"]]
(source_dir/"fixtures.json").write_text(json.dumps(dict(labels=labels,fixtures=fixtures,trails=trails,ferns=ferns),indent=2))
