"""Extrude the CutePixel SDF source font into Unity-local solid sign lettering.
Run with Blender --background --factory-startup --python this_file.
layout.tsv preserves the installed TMP label bounds; JSON is consumed by the
station sign authoring utility. No station FBX or Blender scene is overwritten.
"""
import bpy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
font = bpy.data.fonts.load(str(ROOT / "Assets/Fonts/CutePixel.ttf"))
for line in (HERE / "layout.tsv").read_text().splitlines():
    label, *values = line.split("\t")
    width, height, cx, cy = map(float, values)
    curve = bpy.data.curves.new(label, "FONT")
    curve.body = label
    curve.font = font
    curve.size = 1
    curve.extrude = 0.02
    curve.resolution_u = 1
    curve.bevel_depth = 0
    obj = bpy.data.objects.new(label, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    mesh = obj.data
    mesh.calc_loop_triangles()
    lo = [min(v.co[a] for v in mesh.vertices) for a in range(3)]
    hi = [max(v.co[a] for v in mesh.vertices) for a in range(3)]
    vertices, triangles = [], []
    for tri in mesh.loop_triangles:
        # Reflection across Z makes the front face point toward TMP's -Z side.
        for index in reversed(tri.vertices):
            p = mesh.vertices[index].co
            vertices.extend([
                cx + ((p.x - lo[0]) / (hi[0] - lo[0]) - 0.5) * width,
                cy + ((p.y - lo[1]) / (hi[1] - lo[1]) - 0.5) * height,
                -0.004 - (p.z - lo[2]) / (hi[2] - lo[2]) * 0.02,
            ])
            triangles.append(len(triangles))
    (HERE / (label + ".json")).write_text(json.dumps({"vertices": vertices, "triangles": triangles}))
    print(label, "triangles", len(triangles) // 3)
    bpy.data.objects.remove(obj, do_unlink=True)
