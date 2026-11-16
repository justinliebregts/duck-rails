"""Solid CutePixel source outlines; Unity coordinates face negative Z."""
import bpy, json
from pathlib import Path
root = Path(__file__).resolve().parents[2]
font = bpy.data.fonts.load(str(root / 'Assets/Fonts/CutePixel.ttf'))
vertices, triangles = [], []
for label, width, height, y in [('FUTURE HOME OF',5.7,.48,.83), ('QUACK HEIGHTS!',8.6,.72,0), ('Luxury nests. Coming soon.',8,.43,-.85)]:
    curve=bpy.data.curves.new(label,'FONT'); curve.body=label; curve.font=font
    curve.extrude=.02; curve.resolution_u=1
    obj=bpy.data.objects.new(label,curve); bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.convert(target='MESH'); mesh=obj.data; mesh.calc_loop_triangles()
    lo=[min(v.co[a] for v in mesh.vertices) for a in range(3)]
    hi=[max(v.co[a] for v in mesh.vertices) for a in range(3)]
    for tri in mesh.loop_triangles:
        for index in reversed(tri.vertices):
            p=mesh.vertices[index].co
            vertices.extend([((p.x-lo[0])/(hi[0]-lo[0])-.5)*width,y+((p.y-lo[1])/(hi[1]-lo[1])-.5)*height,-.03-(p.z-lo[2])/(hi[2]-lo[2])*.09])
            triangles.append(len(triangles))
    bpy.data.objects.remove(obj,do_unlink=True)
(Path(__file__).parent/'letters.json').write_text(json.dumps(dict(vertices=vertices,triangles=triangles)))
print('Triangles:',len(triangles)//3)
