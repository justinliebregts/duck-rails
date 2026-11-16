"""Build approved exit sign only; reuse terminal board construction and CutePixel mesh recipe."""
import bpy, bmesh, json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'ArtSource/SubwayTerminal/QuackvilleSign'
OUT.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
ns = {'__name__':'sign_source'}
exec((ROOT/'ArtSource/SubwayTerminal/build_terminal.py').read_text().rsplit('main()  #',1)[0], ns)
b = ns['Builder']('Quackville board')
ns['signboard'](b, (-2.4,-.06,-.31),(2.4,.06,.31))
for x in (-2.2,2.2):
    b.box((x-.045,-.025,.35),(x+.045,.025,.95), 'timber')
bmesh.ops.triangulate(b.bm, faces=list(b.bm.faces))
verts=[]; uv=[]; tris=[]
for f in b.bm.faces:
    for loop in reversed(list(f.loops)):
        p=loop.vert.co
        verts.append({'x':p.x,'y':p.z,'z':p.y})
        t=loop[b.uv].uv
        uv.append({'x':t.x,'y':t.y})
        tris.append(len(tris))
(OUT/'Board.json').write_text(json.dumps({'vertices':verts,'triangles':tris,'uv':uv}))
b.bm.free()
# Execute the existing lettering recipe with a single, offset arrow and wider text.
src=(ROOT/'ArtSource/SubwayTerminal/build_sign_meshes.py').read_text()
src=src.replace("OUT = ROOT / 'ArtSource/SubwayTerminal/SignMeshes'", "OUT = ROOT / 'ArtSource/SubwayTerminal/QuackvilleSign'")
src=src.replace("[('PLATFORMS', 1.96), ('STATION INFO', 2.56), ('SUPPLIES', 1.76)]", "[('QUACKVILLE', 2.95)]")
src=src.replace("* width/(max(xs)-min(xs))", "* width/(max(xs)-min(xs)) + .3")
src=src.replace("* .26/(max(ys)-min(ys))", "* .36/(max(ys)-min(ys))")
src=src.replace("if name == 'PLATFORMS':", "if name == 'QUACKVILLE':")
src=src.replace('[-1.30, 1.30]', '[-1.63]')
src=src.replace('cx+x,y,z', 'cx+x*1.3,y*1.3,z')
src=src.replace("OUT/'SignMeshes.blend'", "OUT/'QuackvilleSign.blend'")
exec(compile(src, __file__, 'exec'))
