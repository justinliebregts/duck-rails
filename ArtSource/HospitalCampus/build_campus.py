from pathlib import Path
import bpy, json, math, random, sys
from mathutils import Vector, Matrix
from math import sin, cos, pi
from collections import defaultdict
random.seed(173)
root=Path(__file__).resolve().parents[2]
out=root/"Assets/DuckRails/HospitalDistrict/Campus"
swatches={}
labels=[]
fixtures=[]
zone="Campus"
bpy.ops.wm.read_factory_settings(use_empty=True)
s=bpy.context.scene
s.world=bpy.data.worlds.new("Blue hour")
s.world.use_nodes=True
s.world.node_tree.nodes["Background"].inputs[0].default_value=(.21,.31,.53,1)
s.world.node_tree.nodes["Background"].inputs[1].default_value=.60
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
    swatches[name]={"name":name,"color":h,"texture":texture or "","emission":strength,"original":""}
    return m
cream=mat("Aged ivory precast","e2d5b8","concrete"); lime=mat("Limestone coping","ede1cb","concrete");red=mat("Emergency enamel","a84036","weathered paint");terr=mat("Fired brick","a1614c","concrete"); brick2=mat("Brick variation","875444","concrete");teal=mat("Oxidised blue green enamel","416c67","weathered paint");metal=mat("Gunmetal frames","3e494b","weathered paint");silver=mat("Galvanised sheet","9caaab","weathered paint");roof=mat("Tar and gravel","525658","dark roofing");dark=mat("Rubber and shadow gaps","242a2c");wood=mat("Old cedar","9e7955","timber");grass=mat("Mown verge","69774b","grass");moss=mat("Planting bed","495d42","grass");asphalt=mat("Aggregate road","51525a","gravel");ballast=mat("Rail ballast","77716b","gravel");walk=mat("Sidewalk","b1afa1","concrete");stripe=mat("Worn road paint","d9cba0","concrete");yellow=mat("Amber safety enamel","d6aa4c");white=mat("Ambulance enamel","dedbd0");aqua=mat("Muted ward glazing","58777a",None,.18,.32); amber=mat("Lit ward blinds","ffc88b",None,1.7); mint=mat("Cool ward glazing","b3d8cd",None,.65); neon=mat("Warm ivory lettering","ffe6c1",None,3.5);led=mat("Emergency marker","ff6456",None,2.3);leaf=[mat("Foliage "+str(i),h,"foliage") for i,h in enumerate(["527050","698254","48654b","81905f"])];muted=mat("Lilac planting","a79095");screen=mat("Window reflection","829da3",None,.15);sheet=mat("Washed linen","e1d9c6","ochre canvas")
# Batch static props by material in world coordinates.
batches=defaultdict(lambda:[[],[]]); frame=Matrix.Identity(4)
def geo(m,v,f):
    points=[tuple(frame@Vector(t)) for t in v]
    cx,cy,cz=[sum(q[i] for q in points)/len(points) for i in range(3)]
    # Preserve circulation openings in the playable shell. The review model
    # depicted these entrances as closed glazing.
    if zone in ("Ward facade","Facade masonry","Ward fins") and min(q[2] for q in points)<2.76:
        xs=[q[0] for q in points];ys=[q[1] for q in points]
        for door in [(22.85,27.15,54,55.6),(9,10.6,77.85,82.15),(23.85,27.15,104.4,106),(23.85,28.15,69,70.6)]:
            if min(xs)<door[1] and max(xs)>door[0] and min(ys)<door[3] and max(ys)>door[2]:return
    # Paint and foliage should not catch hands or loose items.
    solid=m not in (stripe,screen,muted,moss,sheet) and m not in leaf
    if zone in ("Road markings","Pavement patina","Facade masonry","Roof seams"):solid=False
    if zone=="Street paving" and cx>0 and 53<cy<107 and min(q[2] for q in points)<.26 and max(q[2] for q in points)<.26:return
    if zone in ("Roof plant","Roof equipment"):points=[(x,y,z-.91) for x,y,z in points]
    key=(zone,int(cx//16),int(cy//16),solid,m)
    p,pol=batches[key];n=len(p);p.extend(points);pol.extend(tuple(n+i for i in face) for face in f)
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
def tree(x,y,h=6):
    tube((x,y,.1),(x+.15,y,h*.68),h*.042,wood,7,h*.023)
    for i in range(3):
        a=i*2.1+.6;u=x+cos(a)*h*.19;v=y+sin(a)*h*.19
        tube((x,y,h*.35),(u,v,h*.69),h*.023,wood,6,h*.012);shrub(u,v,h*.48,h*.23,h*.38)
    shrub(x+.1,y,h*.63,h*.26,h*.4)
def bench(x,y):
    for i in range(4):box(x,y+i*.13,.60,2.4,.1,.14,wood)
    for z in (.96,1.2):box(x,y+.48,z,2.4,.12,.19,wood)
    for u in (x-.9,x+.9):box(u,y+.16,.34,.16,.56,.52,metal);box(u,y+.48,.85,.12,.10,.85,metal)
def light(name,loc,power,color,size=.45):
    fixtures.append(dict(name=name,x=loc[0],y=loc[2],z=loc[1],power=power,color=list(color)))
    d=bpy.data.lights.new(name,"POINT");d.energy=power;d.color=color;d.shadow_soft_size=size;o=bpy.data.objects.new(name,d);s.collection.objects.link(o);o.location=loc
def lamp(x,y,angle):
    dx=cos(angle);dy=sin(angle);tube((x,y,.12),(x,y,.8),.30,cream);tube((x,y,.8),(x,y,5.9),.13,metal,8,.095);tube((x,y,5.5),(x+dx*1.35,y+dy*1.35,6.1),.095,metal)
    box(x+dx*1.35,y+dy*1.35,6.1,1.05,.74,.23,metal);box(x+dx*1.35,y+dy*1.35,5.96,.88,.60,.08,neon);light("Street pool",(x+dx*1.35,y+dy*1.35,5.72),920,(1,.66,.36))
font=bpy.data.fonts.load(str(root/"Assets/Fonts/CutePixel.ttf"))
def sign(txt,x,y,z,size,m,rot=0):
    labels.append(dict(text=txt,x=x,y=z,z=y,size=size,yaw=-rot*180/pi))
    cv=bpy.data.curves.new(txt,"FONT");cv.body=txt;cv.font=font;cv.size=size;cv.align_x="CENTER";cv.align_y="CENTER";cv.extrude=.025
    o=bpy.data.objects.new(txt,cv);s.collection.objects.link(o);o.location=(x,y,z);o.rotation_euler=(pi/2,0,rot);cv.materials.append(m)
def line(a,b,w,z,m):
    v=Vector(b)-Vector(a);n=Vector((-v.y,v.x)).normalized()*w/2;a,b=Vector(a),Vector(b)
    geo(m,[(p.x,p.y,z) for p in (a-n,b-n,b+n,a+n)],[(0,1,2,3)])
def path(points,w,z,m):
    for a,b in zip(points,points[1:]):line(a,b,w,z,m)
    for x,y in points[1:-1]:patch(x,y,w/2,w/2,z+.002,m)
# Ground and civic avenues
zone="Street paving"
for x in [-8,8]:slab(x,80,3,150,.023,walk)
for y in [45.5,115]:
    for x in [-24,24]:slab(x,y,38,6,.025,asphalt)
    for x in [-8,8]:
        for i in range(6):box(x-1.25+i*.5,y,.034,.24,3.8,.014,stripe)
def paved(x,y,w,d):
    slab(x,y,w+.65,d+.65,.15,walk);slab(x,y,w,d,.17,asphalt)
paved(27,29,33,31);paved(26.5,129,34,32);paved(-27,129,34,35)
slab(43,79,3.1,132,.12,asphalt)
for x in [-26.5,26]:slab(x,80,36,55,.025,walk)
zone="Staff garden"
# Campus side paths: a curved garden walk, staggered planters and planting.
path([(-8,12),(-19,14),(-29,20),(-29,30),(-21,37),(-8,39)],2.25,.16,walk)
path([(-29,25),(-41,25),(-41,44)],2,.14,walk)
for x,y in [(-22,19),(-35,35),(-16,29),(-40,13)]:
    patch(x,y,4,2.9,.12,moss)
    for i in range(10):
        a=i*2.4;rr=random.uniform(.3,2.5);shrub(x+cos(a)*rr,y+sin(a)*rr*.75,.14,random.uniform(.36,.7),random.uniform(.45,1))
    for i in range(10):
        a=random.random()*6.28;rr=random.uniform(.1,1.9);shrub(x+cos(a)*rr,y+sin(a)*rr*.65,.52,.19,.3,muted)
for x,y in [(-36,19),(-17,36),(-38,39),(-26,10),(40,9),(8,145),(-12,108),(40,149),(-11,148)]:tree(x,y,random.uniform(4.8,7.8))
# Architectural elevations. Local u follows frontage, local v goes inward.
def elev(x,y,angle,length,floors,bays,store=False):
    global frame
    frame=Matrix.Translation((x,y,0))@Matrix.Rotation(angle,4,'Z')
    w=length/bays
    for i in range(bays):
        u=-length/2+w*(i+.5)
        for level in range(floors):
            base=level*4.0;wid=w-.66
            box(u,-.12,base+3.12,wid,.25,.62,metal)
            for k in range(3):
                col=random.choices([aqua,amber,mint],[.61,.20,.19])[0]
                px=u+(k-1)*wid/3
                if (i+k)%3!=1:box(px,-.27,base+2.24,wid/3-.12,.09,2.15,col)
                else:box(px,-.27,base+3.05,wid/3-.12,.09,.49,col)
                if col==amber:
                    blind=random.choice([.4,.68,.92]);box(px,-.34,base+3.26-blind/2,wid/3-.14,.04,blind,cream)
                elif col==aqua:box(px,-.325,base+3.02,wid/3-.14,.03,.24,screen)
            box(u,-.30,base+1.02,wid+.27,.46,.17,lime)
            box(u+w/2,-.23,base+2.12,.42,.47,4.23,cream)
            box(u,-.23,base+3.71,wid+.03,.28,.24,teal)
    box(0,-.2,floors*4.25,length+.48,.62,.33,lime)
    frame=Matrix.Identity(4)
def rooftop(x,y,w,d,h):
    slab(x,y,w+.4,d+.4,.26,roof,h)
    for v in [-1,1]:
        box(x,y+v*d/2,h+.47,w,.32,.68,cream);box(x,y+v*d/2,h+.85,w+.28,.49,.13,silver)
        box(x+v*w/2,y,h+.47,.32,d,.68,cream);box(x+v*w/2,y,h+.85,.49,d+.3,.13,silver)
def hvac(x,y,z,w=3.7,d=2.6):
    box(x,y,z+.2,w+.25,d+.24,.36,dark);box(x,y,z+.86,w,d,1.25,silver)
    for i in range(7):box(x,y-d/2-.01,z+.44+i*.13,w*.83,.08,.055,metal)
    for dx in [-.24*w,.24*w]:
        tube((x+dx,y,z+1.49),(x+dx,y,z+1.57),min(w*.205,d*.37),metal,12)
        for i in range(5):
            a=i*pi/5;rr=min(w*.175,d*.32);tube((x+dx-cos(a)*rr,y-sin(a)*rr,z+1.6),(x+dx+cos(a)*rr,y+sin(a)*rr,z+1.6),.038,silver,4)
def ring(x,y,w,d,h):
    for v in [-1,1]:box(x,y+v*d/2,h,w,.25,.23,metal);box(x+v*w/2,y,h,.25,d,.23,metal)
# Four hollow wings preserve the 8 x 20 m open court.
zone="Ward facade"
for x,y,a,l,n in [(26,55,0,32,7),(10,80,-pi/2,50,11),(42,80,pi/2,50,11),(26,105,pi,32,7),(22,80,pi/2,20,4),(30,80,-pi/2,20,4),(26,70,pi,8,2),(26,90,0,8,2)]:elev(x,y,a,l,2,n)
zone="Roof plant"
for x in (15.7,20.3):
    for dx in (-1.18,1.18):
        for dy in (-1.18,1.18):box(x+dx,99+dy,9.7,.21,.21,1.55,metal)
    tube((x,99,10.12),(x,99,13.2),1.83,silver,16);tube((x,99,13.2),(x,99,13.85),1.93,teal,16,.22)
    for z in (10.3,11.75,13.05):tube((x,99,z),(x,99,z+.09),1.87,metal,16)
    tube((x,99,9),(x,99,10.4),.26,metal)
for y in (73,81,89):hvac(38,y,8.93)
for y in range(63,96,3):box(13.2,y,9.52,1.12,2.94,.86,silver);box(13.2,y-1.4,9.53,1.2,.1,.92,metal)
for y in [73,86]:tree(26,y,5.1);bench(26,y+2)
zone="Emergency canopy"
# Raised south architectural sign and illuminated covered casualty intake
box(26,54.68,7.2,19.7,.65,2.25,teal);sign("MEDICAL CENTER",26,54.29,7.23,1.52,neon)
for x in [19.5,32.5]:
    for y in [43,52]:box(x,y,2.25,.42,.42,4.5,cream);box(x,y,.8,.6,.60,1.45,terr)
slab(26,48.4,14.8,12.4,.36,cream,4.4);box(26,42.12,4.72,14.8,.28,.72,red);sign("EMERGENCY",26,41.94,4.74,1.03,neon)
for x in [21,26,31]:box(x,45,4.37,1.9,.42,.09,neon);light("ER downlight",(x,45,4.1),500,(1,.75,.55))
for x in [20.7,29.7]:box(x,54.46,1.52,2,.18,2.8,metal);box(x,54.32,1.72,1.78,.07,2.08,mint)
zone="Emergency pylon"
# Deep red wayfinding pylon at arrival with inset illuminated face.
for x in [12.1,13.3]:box(x,17,2.4,.26,.32,4.6,metal)
box(12.7,17,4.45,3.7,.52,3.7,red);box(12.7,16.67,4.65,2.3,.14,.68,neon);box(12.7,16.65,4.65,.68,.14,2.3,neon)
zone="Clinic and pharmacy"
# Smaller street buildings with separate architectural identities.
def small(x,y,w,d,name,base,awning):
    rooftop(x,y,w,d,3.92)
    elev(x,y-d/2,0,w,1,max(3,round(w/4.6)));elev(x-w/2,y,-pi/2,d,1,max(3,round(d/4.6)))
    for i in range(int(w)):
        box(x-w/2+i+.5,y-d/2-1.05,3.37,1,2.2,.18,awning if i%2==0 else cream);box(x-w/2+i+.5,y-d/2-2.07,3.21,1,.16,.38,awning if i%2==0 else cream)
    box(x,y-d/2-.24,4.43,w*.76,.34,1.08,awning);sign(name,x,y-d/2-.43,4.43,1.06,neon)
    hvac(x+w*.23,y+d*.18,4.88);hvac(x+w*.05,y+d*.18,4.88,2.35,2.1)
small(-27,66.5,26,19,"PHARMACY",terr,teal);small(-27,92.5,26,21,"OUTPATIENT CLINIC",cream,metal)
zone="Facade masonry"
# Exterior masonry accents and service annex detailing
for y in [57,82,55]:
    east=y==55;x0=10 if east else -40
    for row in range(4):
        for i in range(24):
            x=x0+.65+i*1.04+(row%2)*.52
            if random.random()<.65:box(x,y-.045,.3+row*.18,.99,.1,.15,random.choice([terr,brick2]))
zone="Laundry yard"
# Laundry loading bays, ridged roof and working yard

for q in (-1,1):
    geo(teal,[(-40.6,136+q*10,4.5),(-15.4,136+q*10,4.5),(-15.4,136,6.55),(-40.6,136,6.55)],[(0,1,2,3)] if q==-1 else [(3,2,1,0)])
for x in range(-40,-14,2):
    for q in [-1,1]:tube((x,136+q*10,4.57),(x,136,6.59),.065,silver,5)
for x in [-34,-25]:
    box(x,126.41,2.15,6.3,.17,3.5,metal)
    for i in range(12):box(x,126.28,.59+i*.27,5.85,.09,.23,silver)
slab(-28,124.8,23,3.2,.78,walk);
for i in range(4):slab(-28,121.75+i*.4,3.4,.4,.195*(i+1),walk)
sign("CENTRAL LAUNDRY",-28,126.28,4.02,1,neon)
for x in [-40,-31,-22]:
    for y in [148,151]:box(x,y,1.55,.13,.13,3.1,metal)
for y in [148,151]:
    tube((-40,y,3),(-22,y,3),.032,metal,5)
    for i in range(7):
        x=-39+i*2.6;geo(sheet,[(x,y,3),(x+2.1,y,3),(x+2.08,y+.18,1.2),(x+.12,y-.14,1.38)],[(0,1,2,3)])
for x in [-36,-31,-25]:slab(x,120,2.4,1.5,1.35,teal,.2);box(x,120,1.58,2.51,1.62,.18,dark)
zone="Garden pergola"
# Pergola, gardens, benches and lit routes
for x in [-32,-26]:
    for y in [20,29]:box(x,y,1.85,.28,.28,3.7,wood);box(x,y,.45,.62,.62,.75,terr)
    box(x,24.5,3.72,.38,10.3,.35,wood)
for y in range(19,31):box(-29,y,4.0,7.4,.30,.25,wood)
for x,y in [(-29,22),(-29,27),(-19,37),(-38,35),(-32,52),(-22,52),(-19,106)]:bench(x,y)
zone="Visitor shelter"
# Visitor lay-by and asymmetric sheltered waiting area
slab(24,139,14.2,4.9,.16,walk,.12)
for x in [17.8,22,26,30.2]:
    box(x,140.5,1.98,.16,.19,3.2,teal);box(x,137.1,1.98,.16,.19,3.2,teal)
for x in [20,24,28]:
    box(x,140.5,1.69,3.8,.12,2.28,aqua);box(x,140.39,1.4,3.8,.05,.12,stripe);bench(x,139.15)
box(24,138.8,3.68,14.5,5.15,.31,teal);box(24,136.17,3.65,14.5,.12,.46,silver);sign("VISITOR TRANSIT",24,136.07,3.66,.64,neon)
for x in [19,29]:light("Shelter",(x,138,3.35),375,(.62,.85,1))
zone="Road markings"
for y in [18,21.4,24.8,28.2,31.6,35,38.4,117,120.4,123.8,127.2,130.6,134]:
    for x in [15.4,37.6]:box(x,y,.192,6.5,.14,.027,stripe)
for y in [12,40,116,144]:
    box(27,y,.20,6,.16,.04,stripe)
# Actual production meshes with original UVs and material settings.
src=root/"ArtSource/ConvenienceStoreAtlas";data=json.loads((src/"source.json").read_text());orig={}
def srcmat(i):
    if i in orig:return orig[i]
    desc=data['materials'][i];m=mat("Map asset - "+desc['name'],"ffffff");nt=m.node_tree;b=nt.nodes.get("Principled BSDF");c=tuple(max(0,v)**2.2 for v in desc['color']);b.inputs["Base Color"].default_value=(*c,1)
    if desc['texture']:
        im=nt.nodes.new("ShaderNodeTexImage");im.image=bpy.data.images.load(str(src/desc['texture']),check_existing=True);im.interpolation="Closest";mul=nt.nodes.new("ShaderNodeMixRGB");mul.blend_type="MULTIPLY";mul.inputs[0].default_value=1;mul.inputs[2].default_value=(*c,1);nt.links.new(im.outputs[0],mul.inputs[1]);nt.links.new(mul.outputs[0],b.inputs["Base Color"])
    swatches[m.name]["original"]=desc["name"];orig[i]=m;return m
templates={}
def asset(key,x,y,yaw=0,size=1):
    if key not in templates:
        parts=[p for p in data['parts'] if ('/'+key+'/') in p['path']]
        verts=[(a,c,b) for p in parts for a,b,c in p['vertices']];cx=(max(v[0] for v in verts)+min(v[0] for v in verts))/2;cy=(max(v[1] for v in verts)+min(v[1] for v in verts))/2;bottom=min(v[2] for v in verts)
        objs=[]
        for p in parts:
            mesh=bpy.data.meshes.new(p['path'].split('/')[-1]);tri=p['triangles'];mesh.from_pydata([(a-cx,c-cy,b-bottom) for a,b,c in p['vertices']],[],[(tri[i],tri[i+2],tri[i+1]) for i in range(0,len(tri),3)]);mesh.update();desc=data['materials'][p['material']];uv=mesh.uv_layers.new()
            if len(p['uv'])==len(p['vertices']):
                for l in mesh.loops:
                    u,v=p['uv'][l.vertex_index];uv.data[l.index].uv=(u*desc['scale'][0]+desc['offset'][0],v*desc['scale'][1]+desc['offset'][1])
            mesh.materials.append(srcmat(p['material']));objs.append(mesh)
        templates[key]=objs
    for mesh in templates[key]:
        o=bpy.data.objects.new(key,mesh);s.collection.objects.link(o);o.location=(x,y,.19);o.rotation_euler[2]=yaw;o.scale=(size,)*3
for x,y,k,a in [(37,120,0,.2),(37,130.5,2,.1),(16,126,3,-.2),(16,133,1,.1),(-37,111,5,1.7),(-20,110,4,1.6)]:
    asset("Scattered car "+str(k),x,y,a)
for x in [-45,45]:
    for y in [7,18,47,60,77,104,119,145,154]:
        if random.random()<.78:asset("Large court pine 0",x,y,random.random()*6,random.uniform(.49,.83))
for x,y in [(-40,151),(-33,155),(36,152),(-39,5),(40,5)]:asset("Large court pine 0",x,y,random.random()*6,.70)
# Ambulance models: shaped cab, two-tone patient module, inset doors and wheel assemblies
def ambulance(x,y,yaw,roll):
    global frame
    lift=.24 if roll==0 else (1.54 if abs(roll)<3 else 2.98)
    frame=Matrix.Translation((x,y,lift))@Matrix.Rotation(yaw,4,'Z')@Matrix.Rotation(roll,4,'Y')
    box(0,.67,1.79,2.58,4.3,2.30,white);box(0,.67,2.96,2.62,4.34,.14,silver);box(0,0,.58,2.1,5.8,.33,dark)
    box(0,-2.12,1.12,2.43,2.26,1.05,white);box(0,-2.92,1.42,2.46,.93,.42,white)
    verts=[(-1.16,-2.99,1.57),(1.16,-2.99,1.57),(1.16,-1.33,1.57),(-1.16,-1.33,1.57),(-1.09,-2.45,2.46),(1.09,-2.45,2.46),(1.09,-1.34,2.46),(-1.09,-1.34,2.46)];geo(aqua,verts,faces);box(0,-1.9,2.49,2.3,1.29,.16,white)
    for side in (-1,1):
        box(side*1.303,.68,1.61,.04,4.15,.32,red);box(side*1.322,.9,2.17,.045,1.08,.26,red);box(side*1.335,.9,2.17,.045,.28,.94,red)
        for yy in [-2.02,1.85]:
            tube((side*1.06,yy,.62),(side*1.36,yy,.62),.57,dark,12);tube((side*1.36,yy,.62),(side*1.39,yy,.62),.3,silver,10)
        box(side*.85,-3.42,1.44,.46,.07,.24,neon);box(side*1.03,-1.95,2.62,.35,.52,.22,led)
    box(0,-3.45,.88,2.40,.12,.25,metal);box(0,-3.44,1.23,.95,.07,.33,dark)
    frame=Matrix.Identity(4)
zone="Ambulance on side"
ambulance(17,32,-.34,pi/2)
zone="Ambulance inverted"
ambulance(34,23,.45,pi)
zone="Ambulance at barrier"
ambulance(36,39,-.40,0)
zone="Triage equipment"
# Stretcher frames, bollards, oxygen carts and wheel stops
for x in [22,29]:
    box(x,47,1.12,1.03,2.35,.19,teal)
    for y in [46.2,47.8]:
        for dx in [-.38,.38]:tube((x+dx,y,.24),(x-dx,y,1.04),.055,silver,6);tube((x+dx-.065,y,.21),(x+dx+.065,y,.21),.13,dark,8)
    box(x,47.7,1.3,.85,.50,.18,cream)
for x in [18.2,20.3,31.7,33.8]:tube((x,42,.17),(x,42,1.2),.14,yellow,8)
zone="Campus lamps"
for x,y in [(-9,20),(-9,53),(-9,101),(-10,139),(10,35),(9,75),(10,118),(34,133),(-36,116),(-35,28)]:
    lamp(x,y,0 if x<0 else pi)
zone="Perimeter"
# Interspersed service details and campus boundary fencing
for x in [-47,47]:
    for y in range(5,157,4):box(x,y,1.05,.18,.18,2.05,metal)
    for z in [.36,1.05,1.80]:box(x,80,z,.09,151,.09,metal)
for x,y in [(-10,51),(-11,104),(11,109),(34,13)]:
    box(x,y,1,.14,.14,2,metal);box(x,y,1.84,1.7,.13,.85,teal);box(x,y-.08,1.85,.96,.035,.09,cream)
for x,y in [(-42,70),(-42,92),(-40,121),(41,107),(34,146)]:
    for j in range(3):tube((x+j*.6,y,.16),(x+j*.6,y,1.24),.23,metal,9);tube((x+j*.6,y,1.24),(x+j*.6,y,1.36),.26,silver,9)
zone="Hospital crown"
# Architectural crown framing the main entrance. Glass stair lantern and ribs
# break the roof line while retaining the two-storey ward wings.
for x in [18.8,33.2]:
    box(x,54.72,5.25,1.65,.82,10.35,cream)
    for j in range(3):box(x-.52+j*.52,54.14,5.55,.19,.19,8.5,lime)
    box(x,54.72,10.52,2.05,1.08,.39,teal)
box(26,55.05,9.85,12.8,.52,2.0,terr)
box(26,54.95,10.05,12.82,.34,1.91,teal)
sign("HOSPITAL",26,54.74,10.03,2.10,neon)
box(26,55.05,10.97,13.55,.94,.30,lime)
zone="Ward fins"
# Ward soffit ribs and inset lower brick wainscot
for y in range(57,105,4):
    box(9.18,y,2.42,.96,.19,3.92,cream)
    box(9.28,y+1.9,4.09,1.2,3.85,.24,teal)
    box(9.72,y+1.9,.65,.30,3.45,.68,terr)
for y in [62,95]:
    box(9.50,y,4.4,1.4,3.32,8.35,cream)
    for k in range(3):box(8.72,y-.94+k*.94,4.65,.22,.36,7.6,teal)
for x in [15.5,38.4]:
    box(x,54.60,5.8,2.75,.30,3,cream)
    box(x,54.36,5.85,1.98,.14,.48,red);box(x,54.32,5.85,.52,.14,1.94,red)
zone="Roof equipment"
# Mechanical roof organized into service aisles, membrane seams and pipe bridges
for x in [34.1,34.7]:
    tube((x,66,9.48),(x,94,9.48),.15,silver,8)
    for y in range(67,95,4):box(x,y,9.18,.46,.26,.58,metal)
for y in [69,77,85]:
    tube((33.1,y,9.46),(37.9,y,9.46),.18,silver)
    tube((37.9,y,9.46),(37.9,y+2,9.46),.18,silver)
for x,y in [(16,65),(28.5,96)]:
    box(x,y,9.18,4.3,2.6,.55,metal)
    for i in range(4):box(x-1.55+i*1.03,y,9.55,.91,2.35,.28,mint)
zone="West campus signs"
# Distinct west-side silhouettes: clinic clerestory and pharmacy blade sign.
for y in [87,93,99]:
    box(-27.5,y,5.22,16,2.45,.85,teal)
    geo(silver,[(-35.7,y-1.45,5.75),(-19.3,y-1.45,5.75),(-19.3,y+1.45,6.32),(-35.7,y+1.45,6.32)],[(0,1,2,3)])
    for i in range(8):box(-34.55+i*2,y-1.28,5.48,1.8,.12,.49,mint)
for x in [-38,-16]:
    box(x,57,3.09,1.85,2.2,6.1,terr);box(x,56,5.48,2.4,.5,1.9,teal)
    box(x,55.68,5.52,1.3,.1,.36,neon);box(x,55.65,5.51,.36,.12,1.3,neon)
zone="Parking islands"
# East parking islands, bus timetable, bike racks and trash cans
for x,y in [(12,112),(39,112),(14,144),(38,144),(13,13),(40,38)]:
    slab(x,y,4.7,1.9,.22,walk,.17);slab(x,y,4.3,1.55,.26,moss,.19)
    for i in range(5):shrub(x-1.55+i*.78,y,.43,.54,.69)
for x,y in [(18,135),(30.6,135),(-16,55),(11,51)]:
    tube((x,y,.18),(x,y,1.13),.34,teal,10);tube((x,y,1.13),(x,y,1.22),.36,metal,10)
box(31.6,136,1.9,.15,.18,3.45,metal);box(31.6,136,2.29,1.26,.2,1.86,cream);box(31.6,135.86,2.75,.99,.03,.45,teal)
for i in range(5):box(31.6,135.85,2.36-i*.13,.82,.04,.04,metal)
for x in [-37,-35.8,-34.6,-33.4,-32.2]:
    for y in [49,50.8]:tube((x,y,.14),(x,y,1.11),.09,teal)
    tube((x,49,1.11),(x,50.8,1.11),.09,teal)
zone="Pavement patina"
# Asphalt archaeology: patched trenches, tar seams, storm grates, crosswalks
for x,y,w,d in [(24,21,2,10),(33,32,4.4,2),(-22,117,6,2),(33,123,3,6)]:
    box(x,y,.182,w,d,.012,roof)
for x,y in [(28,28),(42,17),(-32,121),(26,126),(32,138),(-18,48)]:
    path([(x-3,y+.2),(x-1.2,y+.45),(x,y),(x+.8,y+.21),(x+2.8,y-.45)],.055,.205,metal)
    box(x+2.3,y,.208,.8,.52,.025,metal)
    for i in range(5):box(x+2.04+i*.12,y,.23,.05,.42,.03,dark)
for y in [16,26,36,120,131]:
    box(27.2,y,.205,.13,2,.04,stripe)
for x,y in [(23.7,18),(27.8,116)]:
    box(x,y,.22,.22,1.6,.03,stripe)
    geo(stripe,[(x-.65,y+.36,.24),(x+.65,y+.36,.24),(x,y+1.45,.24)],[(0,1,2)])
zone="Garden entrance"
# Low-rise sign at garden mouth. All lettering uses the project's CutePixel outlines.
box(-18,10,.83,9.6,1.52,1.65,terr);box(-18,10,1.72,9.9,1.75,.22,cream)
box(-18,10,2.65,8.8,.69,1.88,teal);sign("MEDICAL CENTER",-18,9.61,2.66,.97,neon)
for x in [-22,-14]:box(x,10,1.65,.68,.99,3.3,cream)
for i in range(18):shrub(-23+random.random()*10,7.9+random.random()*.6,.13,.38,.46)
for x,y in [(-17,10),(12.7,17),(-26,54),(26,53)]:
    light("Sign wash",(x,y-1.4,1.9),235,(1,.57,.32))
zone="Laundry service"
# Roof and wall character in the service court
for x in [-36,-32]:
    tube((x,139,5.6),(x,139,10.3),.47,terr,10,.34)
    tube((x,139,10.23),(x,139,10.5),.51,silver,10)
for x,y in [(-38,115),(-38,118),(-34,116),(-15,137)]:
    for yy in [-.65,.65]:box(x,y+yy,.33,2.35,.16,.32,wood)
    for j in range(4):box(x-.9+j*.60,y,.52,.50,1.8,.14,wood)
    box(x,y,1.24,2,1.45,1.38,wood)
    for dx in [-.77,.77]:box(x+dx,y,1.25,.1,1.5,1.44,metal)
zone="Visitor shuttle"
# Hospital shuttle at the north shelter, with a sloped windshield, wheelchair door and roof vent.
frame=Matrix.Translation((24.3,131.7,.2))@Matrix.Rotation(pi/2,4,'Z')
box(0,0,1.46,2.64,8.5,1.9,cream);box(0,.03,2.7,2.52,8.15,.96,teal);box(0,.1,3.26,2.75,8.35,.19,cream)
for side in [-1,1]:
    for j in range(6):box(side*1.275,-3.12+j*1.27,2.62,.06,1.10,.79,aqua if j%3 else mint)
    box(side*1.34,0,1.55,.055,8.4,.32,teal)
    for y in [-2.7,2.8]:
        tube((side*1.08,y,.63),(side*1.41,y,.63),.59,dark,12);tube((side*1.41,y,.63),(side*1.45,y,.63),.29,silver,10)
box(0,-4.17,2.58,2.3,.12,.88,aqua);box(0,-4.25,1,2.64,.18,.23,metal);box(0,.9,3.48,1.5,2,.34,silver)
frame=Matrix.Identity(4)
zone="Campus planting"
# Taller planting groups bookend the ward and vary the two approaches
for x,y,h in [(-43,34,7.4),(-36,9,6),(-14,40,5.9),(10.8,107.7,5.4),(42.1,109,6.2),(38.5,146,7),(16,147.5,5),(-14,150.5,6.8),(-40,106.8,6.2),(-40.5,51,5.8)]:tree(x,y,h)
for x,y in [(-43,43),(-39,11),(39,11),(39,147),(-13,145),(-11,65),(-11,96),(-35,53)]:
    patch(x,y,2.25,1.48,.21,moss)
    for i in range(6):shrub(x+random.uniform(-1.55,1.55),y+random.uniform(-.8,.8),.23,.63,.85)


import re
inventory=(src/'inventory.txt').read_text(encoding='utf-8-sig')
materials={m.group(1):m.group(2) for m in re.finditer(r'^MAT (.*?) \| (Assets/.*?) shader=',inventory,re.M)}
for sw in swatches.values():
    if sw["original"]:sw["original"]=materials[sw["original"]]
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
save_image("Campus color",base_pixels);save_image("Campus emission",glow_pixels)
atlas=bpy.data.materials.new("Hospital Campus Atlas")
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
                if m==sheet:indices.append(list(reversed(ids)))
for (zone,cx,cy,solid),(verts,polys,uvs) in chunks.items():
    if not polys:continue
    key=f"{zone}_{cx}_{cy}_{'solid' if solid else 'detail'}"
    mesh=bpy.data.meshes.new(key);mesh.from_pydata(verts,[],polys);mesh.materials.append(atlas);mesh.update()
    layer=mesh.uv_layers.new()
    for loop in mesh.loops:layer.data[loop.index].uv=uvs[loop.vertex_index]
    ob=bpy.data.objects.new(key,mesh);s.collection.objects.link(ob)

# Export render meshes only. Unity creates CutePixel SDF labels and baked lights.
for ob in bpy.context.selected_objects:ob.select_set(False)
for ob in s.objects:
    if ob.type=='MESH':ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(out/'Hospital Campus.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=False)
(root/'ArtSource/HospitalCampus/campus.json').write_text(json.dumps(dict(materials=list(swatches.values()),labels=labels,fixtures=fixtures),indent=2))

