"""Offline logo authoring. Run with Blender 4.4 --background --python this_file."""
import bpy, math, random, json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/DuckRails/Branding/Logo'
SOURCE = ROOT / 'ArtSource/Logo'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# Hand-authored polygon outlines, not font substitution for the approved wordmark.
shapes = {
'D': [[(0,0),(.68,.02),(.94,.22),(1,.72),(.79,.97),(.16,1),(.02,.88),(.12,.84)],[(.34,.25),(.39,.75),(.61,.73),(.69,.60),(.65,.38),(.55,.25)]],
'U': [[(.04,1),(.36,.98),(.30,.31),(.40,.21),(.57,.22),(.65,.34),(.70,1),(1,1),(.94,.24),(.76,.04),(.29,0),(.07,.18),(0,.40)]],
'C': [[(.99,.85),(.78,1),(.26,.98),(.07,.78),(0,.26),(.19,.04),(.67,0),(.91,.17),(.98,.37),(.67,.42),(.57,.25),(.37,.25),(.30,.37),(.35,.68),(.45,.76),(.63,.75),(.69,.62),(.96,.66)]],
'K': [[(0,0),(.31,.02),(.35,.37),(.66,.02),(1,0),(.64,.53),(.99,.96),(.67,1),(.38,.65),(.42,1),(.10,.96)]],
'R': [[(0,0),(.30,.05),(.35,.37),(.62,.02),(.98,0),(.64,.45),(.88,.56),(.99,.81),(.81,.98),(.16,1),(.05,.84),(.13,.85)],[(.40,.61),(.43,.78),(.66,.78),(.69,.71),(.62,.62)]],
'A': [[(0,0),(.32,.03),(.44,.24),(.72,.24),(.73,.02),(1,0),(.86,1),(.49,.96)],[(.55,.46),(.69,.47),(.67,.72)]],
'I': [[(0,0),(.34,.03),(.48,1),(.16,.98)]],
'L': [[(0,.03),(.81,0),(.89,.25),(.36,.23),(.49,1),(.17,.96)]],
'S': [[(0,.17),(.20,0),(.69,.01),(.93,.19),(1,.43),(.80,.59),(.42,.66),(.44,.76),(.65,.79),(.76,.68),(.99,.87),(.79,1),(.33,.96),(.11,.78),(.07,.54),(.29,.39),(.65,.31),(.61,.22),(.37,.21),(.23,.32)]]}

# One 256-square atlas: 16 independently painted 64-square tiles.
N=256
palette=[(0.91,.79,.54),(0.87,.025,.015),(.18,.012,.035),(.94,.83,.59)]
pixels=[0.0]*(N*N*4)
rng=random.Random(11062026)
for tile in range(16):
    base=palette[0 if tile<4 else 1 if tile<10 else 2 if tile<12 else 3]
    cell=[[tuple(c for c in base) for x in range(64)] for y in range(64)]
    for patch in range(70):
        x,y=rng.randrange(64),rng.randrange(64)
        w,h=rng.randrange(2,7),rng.randrange(2,6)
        shade=rng.choice([.84,.92,1.02,1.08])
        for yy in range(y,min(64,y+h)):
            for xx in range(x,min(64,x+w)):cell[yy][xx]=tuple(min(1,c*shade) for c in base)
    if tile<9:
        for crack in range(2 if tile<4 else 1):
            x=rng.randrange(14,50); y=rng.randrange(26,49)
            for step in range(rng.randrange(12,22)):
                if 2<=x<62 and 2<=y<62:
                    cell[y][x]=(.055,.025,.025)
                    if step%5==0 and x+1<64:cell[y][x+1]=(.055,.025,.025)
                x+=rng.choice([-1,0,1]);y-=1
    for y in range(64):
        for x in range(64):
            i=(((tile//4)*64+y)*256+(tile%4)*64+x)*4
            pixels[i:i+4]=[*cell[y][x],1]
img=bpy.data.images.new('LogoPixels',N,N,alpha=False)
img.pixels.foreach_set(pixels);img.filepath_raw=str(OUT/'LogoPixels.png');img.file_format='PNG';img.save()
mat=bpy.data.materials.new('Logo Pixel Matte');mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Roughness'].default_value=1
bsdf.inputs['Specular IOR Level'].default_value=0
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img;tex.interpolation='Closest'
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
parts=[]

def finish(obj,tile,depth):
    bpy.context.view_layer.objects.active=obj
    obj.select_set(True);bpy.ops.object.convert(target='MESH')
    obj=bpy.context.object
    obj.data.materials.clear();obj.data.materials.append(mat)
    # Each face uses a tile with padded coordinates; sides sample burgundy.
    for layer in list(obj.data.uv_layers):obj.data.uv_layers.remove(layer)
    uv=obj.data.uv_layers.new(name='LogoAtlas')
    xs=[v.co.x for v in obj.data.vertices];ys=[v.co.y for v in obj.data.vertices]
    loX,hiX=min(xs),max(xs);loY,hiY=min(ys),max(ys)
    for p in obj.data.polygons:
        p.use_smooth=False
        t=tile if p.normal.z>.5 else 10
        for li in p.loop_indices:
            v=obj.data.vertices[obj.data.loops[li].vertex_index].co
            u=(v.x-loX)/max(.001,hiX-loX);v=(v.y-loY)/max(.001,hiY-loY)
            uv.data[li].uv=((t%4*64+2+u*59)/256,(t//4*64+2+v*59)/256)
    tri=obj.modifiers.new('Explicit low-poly triangles','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=tri.name)
    obj.select_set(False);parts.append(obj);return obj

def polygon(name,loops,x,y,w,h,tile,angle=0,depth=.18):
    curve=bpy.data.curves.new(name,'CURVE');curve.dimensions='2D';curve.resolution_u=1
    curve.fill_mode='BOTH';curve.extrude=depth/2;curve.bevel_depth=0
    minx=min(p[0] for p in loops[0]);span=max(p[0] for p in loops[0])-minx
    for loop in loops:
        spl=curve.splines.new('POLY');spl.points.add(len(loop)-1)
        for p,(a,b) in zip(spl.points,loop):p.co=((a-minx)/span*w,b*h,0,1)
        spl.use_cyclic_u=True
    obj=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(obj)
    obj=finish(obj,tile,depth);obj.location=(x,y,0);obj.rotation_euler.z=math.radians(angle)
    return obj

# Overall width approximately six metres. Scale the prefab uniformly for installation.
x=-2.68
for i,(ch,w,ang) in enumerate(zip('DUCK',[1.30,1.28,1.27,1.35],[-3,1,2,-2])):
    polygon('DUCK_'+ch,shapes[ch],x,.30,w,1.75,i,ang,.22);x+=w+.055
x=-2.80
for i,(ch,w,ang) in enumerate(zip('RAILS',[1.30,1.30,.55,1.03,1.23],[-3,0,1,1,3])):
    polygon('RAILS_'+ch,shapes[ch],x,-1.47,w,1.64,4+i,ang,.26);x+=w+.025
for j,y in enumerate([-1.70,-1.96]):
    polygon('Rail_'+str(j),[[(0,.1),(.04,.9),(.23,1),(.55,.91),(.92,.55),(1,0),(.93,.12),(.56,.47),(.23,.57),(.02,.36)]],-2.72,y,5.5,.15,9,depth=.12)
for i,x in enumerate([-1.83,-.68,.56,1.79]):
    polygon('Sleeper_'+str(i),[[(0,0),(.9,0),(1,1),(.1,1)]],x,-2.01,.19,.50,9,depth=.15)

# Use the project's approved CutePixel source for physically extruded subtitle glyphs.
font=bpy.data.fonts.load(str(ROOT/'Assets/Fonts/CutePixel.ttf'))
curve=bpy.data.curves.new('CutePixel subtitle','FONT');curve.body='ESCAPE FROM QUACKVILLE';curve.font=font
curve.align_x='CENTER';curve.size=.5;curve.extrude=.035;curve.resolution_u=1
obj=bpy.data.objects.new('Subtitle_CutePixel',curve);bpy.context.collection.objects.link(obj)
bpy.context.view_layer.update()
scale=5.50/obj.dimensions.x;obj.scale=(scale,scale,scale)
obj=finish(obj,12,.07);obj.location=(0,-2.45,0)

bpy.ops.object.select_all(action='DESELECT')
for ob in parts:ob.select_set(True)
bpy.context.view_layer.objects.active=parts[0]
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
# Blender Z-up: stand the sign upright, with its painted face toward -Y.
for ob in parts:ob.data.transform(Matrix.Rotation(math.pi/2,4,'X'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'DuckRailsLogo.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,add_leaf_bones=False,path_mode='STRIP')
triangles=sum(len(o.data.polygons) for o in parts)
report={'triangles':triangles,'mesh_objects':len(parts),'texture':'256x256 point-filtered atlas','font':'Assets/Fonts/CutePixel.ttf','letters':'custom polygon wordmark approved by user'}
(SOURCE/'build-stats.json').write_text(json.dumps(report,indent=2))
img.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'DuckRailsLogo.blend'))
print('LOGO_BUILD '+json.dumps(report))
