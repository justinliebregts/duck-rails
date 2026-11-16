"""Generate only the three raised CutePixel sign meshes. Run with Blender --background --python.
Exports Unity-local coordinates (front faces toward -Z) without touching terminal source.
"""
import bpy, bmesh, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'ArtSource/SubwayTerminal/SignMeshes'
OUT.mkdir(exist_ok=True)
font = bpy.data.fonts.load(str(ROOT / 'Assets/Fonts/CutePixel.ttf'))
for name, width in [('PLATFORMS', 1.96), ('STATION INFO', 2.56), ('SUPPLIES', 1.76)]:
    curve = bpy.data.curves.new(name, 'FONT')
    curve.body, curve.font, curve.size = name, font, 1
    curve.extrude = .03
    curve.resolution_u = 1
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    mesh = obj.data
    xs, ys, zs = [[v.co[i] for v in mesh.vertices] for i in range(3)]
    for v in mesh.vertices:
        v.co.x = (v.co.x - (min(xs)+max(xs))/2) * width/(max(xs)-min(xs))
        v.co.y = (v.co.y - (min(ys)+max(ys))/2) * .26/(max(ys)-min(ys))
        v.co.z = -.002 - (v.co.z-min(zs))/(max(zs)-min(zs))*.022
    if name == 'PLATFORMS':
        # Two solid upward arrows, with diagonal heads as in the supplied reference.
        bm = bmesh.new()
        bm.from_mesh(mesh)
        outline = [(-.035,-.13),(.035,-.13),(.035,.025),(.09,-.03),(.13,.01),(0,.14),(-.13,.01),(-.09,-.03),(-.035,.025)]
        for cx in [-1.30, 1.30]:
            rings = [[bm.verts.new((cx+x,y,z)) for x,y in outline] for z in [-.002,-.024]]
            bm.faces.new(rings[0])
            bm.faces.new(list(reversed(rings[1])))
            for i in range(len(outline)):
                j=(i+1)%len(outline)
                bm.faces.new([rings[0][i],rings[1][i],rings[1][j],rings[0][j]])
        bm.to_mesh(mesh)
        bm.free()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    # Coordinates are exported unchanged, so preserve outward triangle winding.
    data={'vertices':[{'x':v.co.x,'y':v.co.y,'z':v.co.z} for v in mesh.vertices],
          'triangles':[i for p in mesh.polygons for i in p.vertices]}
    (OUT/(name.replace(' ','-')+'.json')).write_text(json.dumps(data))
    print(name, len(mesh.vertices), 'vertices;',len(mesh.polygons),'triangles')
    obj.select_set(False)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SignMeshes.blend'))
