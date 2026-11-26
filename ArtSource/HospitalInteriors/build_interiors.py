# Hospital District interiors: furniture, fixtures and wall/floor finishes for every enterable room.
# Headless:  blender -b --factory-startup --python ArtSource/HospitalInteriors/build_interiors.py -- [--export] [--render]
# Coordinates are the block's own, the same as the campus exterior: Blender X = Unity X, Blender Y = Unity Z
# (departure toward +Y), Blender Z = Unity Y (up). Metres. Hospital floors are at Z 0 and 4; the roof stays as built.
# The playable shell (slabs, outer walls, corridor walls, stairs) belongs to the Unity prefab and is not modelled here.
# Builder, atlas tiles and the stocked shelf are reused from the Compound model so the interiors match the
# Compound and Starting Station. Every room is one render mesh; physics uses the box list written to interiors.json.
import bpy, math, random, json, os, sys
from functools import partial
import numpy as np
from mathutils.geometry import barycentric_transform

from pathlib import Path

ROOT = Path(__file__).resolve().parents[2].as_posix() + "/"
sys.path.insert(0, ROOT + "ArtSource/HospitalCampus")
from planar_faces import PlanarFaces
OUT_DIR = ROOT + "ArtSource/HospitalInteriors/"
FBX = ROOT + "Assets/DuckRails/HospitalDistrict/Interiors/Hospital Interiors.fbx"
RENDER_DIR = ROOT + "output/hospital-interiors-review/"

_src = open(ROOT + "ArtSource/SubwayTerminal/build_terminal.py").read().split("\ndef main():")[0]
T = {}
exec(_src, T)
T["ROOT"] = ROOT
V, Matrix, TILES = T["V"], T["Matrix"], T["TILES"]
shelf = partial(T["shelf"], closed_stock=True)


class Builder(T["Builder"]):
    def trim_overlaps(self, planar_faces):
        # The terminal builder gives each face its own vertices. Keep its atlas
        # UV projection on the exposed strips of shelves, counters and finishes.
        for face in list(self.bm.faces):
            points=[v.co.copy() for v in face.verts]
            xyz=np.array(points)
            pieces=planar_faces.exposed(xyz)
            if len(pieces)==1 and pieces[0] is xyz:
                continue
            uv=[V((*loop[self.uv].uv,0)) for loop in face.loops]
            for piece in pieces:
                new=self.bm.faces.new([self.bm.verts.new(p) for p in piece])
                new.material_index=face.material_index
                for loop in new.loops:
                    projected=barycentric_transform(loop.vert.co,*points[:3],*uv[:3])
                    loop[self.uv].uv=projected.xy
            for vert in list(face.verts):
                self.bm.verts.remove(vert)

    def beam(self, a, b, w, h, tile, up=(0, 0, 1), **kwargs):
        direction=(V(b)-V(a)).normalized()
        if direction.cross(V(up)).length < 1e-5:
            up=(0,1,0) if abs(direction.y)<.9 else (1,0,0)
        super().beam(a,b,w,h,tile,up=up,**kwargs)

    def flat(self, pts, mat):
        # Give the hospital's luminous glass and screens usable UVs too.
        points = [V(p) for p in pts]
        face = self.bm.faces.new([self.bm.verts.new(self.M @ p) for p in points])
        face.material_index = mat
        u = (points[1] - points[0]).normalized()
        v = u.cross(points[-1] - points[0]).normalized().cross(u)
        for loop, p in zip(face.loops, points):
            loop[self.uv].uv = (p.dot(u) / 2, p.dot(v) / 2)
material = T["material"]
LENS, GLOW, WARM = T["LENS"], T["GLOW"], T["WARM"]
# Further cells of the Industrial atlas (32 px).
TILES.update({"red": ("I", 160, 0, 32), "blue": ("I", 192, 0, 32), "plate": ("I", 160, 32, 32), "lamp": ("I", 192, 32, 32),
              "hub": ("I", 224, 32, 32), "tread2": ("I", 96, 64, 32), "tyre": ("I", 128, 64, 32), "slot": ("I", 192, 64, 32),
              "speckle": ("I", 0, 96, 32), "dials": ("I", 32, 96, 32), "grass": ("T", 0, 0, 64)})

LIGHTS, LABELS, BOXES, KEEP, FIXTURES = [], [], [], [], []
rng = random.Random(4711)
CEIL = {0: 3.7, 4: 7.7}              # underside of the slab above each hospital floor
SMALL_CEIL = 3.6                     # clinic, pharmacy and laundry roof underside
WALL_T = .125                        # half thickness of shell walls


def U(p):
    return [round(p[0], 3), round(p[2], 3), round(p[1], 3)]


# ---------------------------------------------------------------- keep-outs
def keep(level, x0, x1, y0, y1, why):
    KEEP.append((level, x0, x1, y0, y1, why))


def keep_spot(level, x, y, r, why):
    keep(level, x - r, x + r, y - r, y + r, why)


def door_keep(level, axis, c, a0, a1, depth=1.4):
    if axis == "x":
        keep(level, c - depth, c + depth, a0 - .15, a1 + .15, "door x=%g" % c)
    else:
        keep(level, a0 - .15, a1 + .15, c - depth, c + depth, "door y=%g" % c)


for L in (0, 4):
    keep(L, 19.35, 20.65, 66.5, 93.5, "west loop line"); keep(L, 31.35, 32.65, 66.5, 93.5, "east loop line")
    keep(L, 19, 33, 67.35, 68.65, "south loop line"); keep(L, 19, 33, 91.35, 92.65, "north loop line")
    keep(L, 31.4, 42, 55, 57.0, "south stair approach"); keep(L, 31.4, 42, 96.0, 98.0, "north stair approach")
    keep(L, 35.6, 42, 55, 63.8, "south stair core"); keep(L, 35.6, 42, 95.5, 105, "north stair core")
    for a, b in ((72, 75), (79, 83), (87, 90)): door_keep(L, "x", 18, a, b)
    for a, b in ((68, 71), (78, 81), (88, 91)): door_keep(L, "x", 34, a, b)
    for a, b in ((19, 22), (29, 32)): door_keep(L, "y", 94, a, b)
    for a, b in ((18, 21), (28, 31)) + (((23, 27),) if L == 0 else ()): door_keep(L, "y", 66, a, b)
keep(0, 23.3, 26.7, 55, 70.5, "ER arrival lane"); door_keep(0, "y", 70, 24, 28); door_keep(0, "y", 55, 23, 27)
keep(0, 9.5, 20.5, 78.3, 81.7, "main reception return"); keep(0, 23.3, 27.7, 94.2, 105, "north exit lane")
keep(0, 18.6, 21.4, 63, 66, "ER west door approach"); keep(0, 27.6, 31.4, 63, 66, "ER east door approach")
keep(4, 18.6, 21.4, 63, 66, "surgery 2 door approach"); keep(4, 27.6, 31.4, 63, 66, "surgery 3 door approach")
for x, y in ((-14, 92.5), (-14, 66.5), (-16, 136)):
    keep(0, x - 4.2, x + .5, y - 2.4, y + 2.4, "small building entry")
# Existing coal, gold and enemy candidates inside buildings (clearance radius around the marker).
for L, x, y in ((0, -20, 65), (0, -33, 75), (0, -20, 67.5), (0, -20, 90), (0, -20, 87.5), (0, 20, 80), (0, 32, 84),
                (4, 32, 92), (4, 14, 76)):
    keep_spot(L, x, y, 1.0, "spawn candidate")


def level_of(z):
    return 4 if z >= 3.9 and z < 7.9 else 0


# ---------------------------------------------------------------- primitives with physics
def wpt(b, p):
    return b.M @ V(p)


def collide(b, lo, hi, check=True):
    """Record an oriented physics box for the local box lo..hi under the builder's current transform."""
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    c = wpt(b, ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))
    ax = b.M.to_3x3() @ V((1, 0, 0))
    yaw = math.degrees(math.atan2(ax.y, ax.x))
    BOXES.append({"c": U(c), "s": [round(abs(x1 - x0), 3), round(abs(z1 - z0), 3), round(abs(y1 - y0), 3)], "yaw": round(-yaw, 2), "room": b.name})
    if check:
        pts = [wpt(b, (x, y, z0)) for x in (x0, x1) for y in (y0, y1)]
        bx0, bx1 = min(p.x for p in pts), max(p.x for p in pts)
        by0, by1 = min(p.y for p in pts), max(p.y for p in pts)
        lv = level_of(min(p.z for p in pts) + .05) if b.name.startswith("H") else 0
        for L, kx0, kx1, ky0, ky1, why in KEEP:
            if L == lv and bx0 < kx1 and bx1 > kx0 and by0 < ky1 and by1 > ky0:
                raise RuntimeError(f"{b.name}: solid box {lo}-{hi} at ({bx0:.2f}..{bx1:.2f}, {by0:.2f}..{by1:.2f}) blocks {why}")


def solid(b, lo, hi, side, top=None, skip="b", fit=False, check=True):
    b.box(lo, hi, side, top, skip=skip, fit=fit)
    collide(b, lo, hi, check)


def place(b, x, y, z=0, yaw=0):
    b.push(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, "Z"))


def fitbox(b, lo, hi, tile, top=None, skip="b"):
    b.box(lo, hi, tile, top, skip=skip, fit=TILES[tile][0] == "I")


def disc(b, c, r, n, mat_or_tile, normal=(0, 0, -1)):
    """Flat polygon disc; mat_or_tile is a tile name or an emissive material index."""
    c, nv = V(c), V(normal).normalized()
    e1 = nv.cross(V((0, 0, 1)) if abs(nv.z) < .9 else V((1, 0, 0))).normalized()
    e2 = nv.cross(e1)
    pts = [c + (e1 * math.cos(2 * math.pi * k / n) + e2 * math.sin(2 * math.pi * k / n)) * r for k in range(n)]
    if (pts[1] - pts[0]).cross(pts[2] - pts[0]).dot(nv) < 0: pts.reverse()
    if isinstance(mat_or_tile, str): b.poly(pts, mat_or_tile)
    else: b.flat(pts, mat_or_tile)


def glowquad(b, p0, du, dv, mat):
    p0, du, dv = V(p0), V(du), V(dv)
    b.flat([p0, p0 + du, p0 + du + dv, p0 + dv], mat)


def ring(b, c, r0, r1, depth, n, outer, inner, face):
    """Gantry ring around the local Y axis through c: outer shell, inner bore and two annular faces."""
    c = V(c)
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        p = lambda r, a, y: c + V((r * math.cos(a), y, r * math.sin(a)))
        y0, y1 = -depth / 2, depth / 2
        b.poly([p(r1, a0, y0), p(r1, a1, y0), p(r1, a1, y1), p(r1, a0, y1)][::-1], outer)
        b.poly([p(r0, a0, y0), p(r0, a0, y1), p(r0, a1, y1), p(r0, a1, y0)][::-1], inner)
        b.poly([p(r0, a0, y0), p(r0, a1, y0), p(r1, a1, y0), p(r1, a0, y0)][::-1], face)
        b.poly([p(r0, a0, y1), p(r1, a0, y1), p(r1, a1, y1), p(r0, a1, y1)][::-1], face)


# ---------------------------------------------------------------- finishes
DADO = [(.12, "charcoal"), (1.05, "teal"), (99, "plaster")]


def skin_floor(b, x0, x1, y0, y1, z, tile):
    b.quad((x0, y0, z + .012), (x1 - x0, 0, 0), (0, y1 - y0, 0), tile)


def threshold(b, axis, c, a0, a1, z, tile="greypanel"):
    """Floor patch through a door gap in a wall (axis x: wall at X=c running along Y)."""
    if axis == "x": b.quad((c - .13, a0, z + .016), (.26, 0, 0), (0, a1 - a0, 0), tile)
    else: b.quad((a0, c - .13, z + .016), (a1 - a0, 0, 0), (0, .26, 0), tile)


def dado(b, axis, c, a0, a1, z, face, doors=(), top=1.12, scheme=None):
    """Skirting and dado band on one face of a shell wall: axis 'x' means the wall runs along Y at X=c.
    face is +1/-1: the side of the wall that is dressed. Door gaps are skipped."""
    off = c + face * (WALL_T + .012)
    cuts, cur = [], a0
    for d0, d1 in sorted(doors):
        if d1 <= a0 or d0 >= a1: continue
        cuts.append((cur, max(a0, d0))); cur = min(a1, d1)
    cuts.append((cur, a1))
    if scheme and scheme[-1][0] < 50: top = scheme[-1][0]
    scheme = scheme or [(.12, "charcoal"), (top, "teal")]
    for s0, s1 in cuts:
        if s1 - s0 < .05: continue
        if axis == "x": b.wx(off, s0, s1, z, z + top, scheme, face)
        else: b.wy(off, s0, s1, z, z + top, scheme, face)
        # Bumper rail along the top of the dado.
        if axis == "x": b.beam((off + face * .03, s0, z + top), (off + face * .03, s1, z + top), .07, .09, "whitewood", caps=True, skip="b")
        else: b.beam((s0, off + face * .03, z + top), (s1, off + face * .03, z + top), .07, .09, "whitewood", caps=True, skip="b")


def troffer(b, x, y, ceil, w=1.2, d=.6, lit=True):
    FIXTURES.append(U((x, y, ceil)))
    b.box((x - w / 2 - .04, y - d / 2 - .04, ceil - .07), (x + w / 2 + .04, y + d / 2 + .04, ceil), "whitepanel", skip="t b", fit=True)
    glowquad(b, (x - w / 2, y + d / 2, ceil - .07), (w, 0, 0), (0, -d, 0), LENS if lit else T["VOID"])


def light(name, p, intensity=16, rng_=10, color=(.72, .86, 1.0)):
    LIGHTS.append({"name": name, "pos": U(p), "intensity": intensity, "range": rng_, "color": list(color)})


def plate(b, text, p, yaw, w=2.4, h=.5, tile="teal", hang=None, two_sided=False):
    """Room sign: a backing plate here, CutePixel lettering added in Unity. p is the plate face centre.
    The face looks along local -Y: yaw 0 faces -Y (south), 180 faces +Y, 90 faces +X, -90 faces -X.
    hang: ceiling height to suspend the plate from on two rods."""
    place(b, p[0], p[1], p[2], yaw)
    b.box((-w / 2, 0, -h / 2), (w / 2, .05, h / 2), tile, fit=True, skip="" if hang else "y+")
    if hang:
        for sx in (-w / 2 + .15, w / 2 - .15):
            b.beam((sx, .025, h / 2), (sx, .025, hang - p[2]), .02, .02, "galv", fit=True, caps=False)
    b.pop()
    f = (math.sin(math.radians(yaw)), -math.cos(math.radians(yaw)))
    face = V((p[0], p[1], p[2])) + V((f[0], f[1], 0)) * .006
    # Raised CutePixel text reads from its -Z side, so the Unity yaw is the negated Blender yaw.
    label = {"text": text, "pos": U(face), "yaw": round(-yaw, 2), "width": round(w - .2, 3), "height": round(h - .12, 3)}
    LABELS.append(label)
    if two_sided:
        back = face - V((f[0], f[1], 0)) * .062
        LABELS.append(dict(label, pos=U(back), yaw=180-yaw))


# ---------------------------------------------------------------- furniture
def bed(b, x, y, z, yaw=0, blanket="blue", tilt=0):
    """Hospital bed 1.0 x 2.1, head toward local +Y."""
    place(b, x, y, z, yaw)
    if tilt: b.push(Matrix.Rotation(math.radians(tilt), 4, "Z"))
    for sx in (-.38, .38):
        for sy in (-.88, .88):
            b.beam((sx, sy, .1), (sx, sy, .42), .05, .05, "galv", up=(0, 1, 0), fit=True, caps=False)
            b.cyl((sx - .04, sy, .08), (sx + .04, sy, .08), .08, 6, "tyre", True, True)
    b.box((-.46, -1.0, .38), (.46, 1.0, .5), "steel", fit=True, skip="")
    b.box((-.44, -.98, .5), (.44, .98, .64), "whitepanel", fit=True, skip="b")
    b.box((-.47, -.2, .64), (.47, .96, .69), blanket, fit=True, skip="b") if blanket else None
    b.box((-.3, .6, .64), (.3, .92, .76), "whitepanel", fit=True, skip="b")
    b.box((-.5, .98, .38), (.5, 1.05, 1.05), "whitepanel", fit=True)
    b.box((-.5, -1.05, .38), (.5, -.98, .86), "whitepanel", fit=True)
    for sx in (-.5, .5):
        b.beam((sx, -.6, .82), (sx, .55, .82), .04, .05, "galv", fit=True)
        for sy in (-.5, .45):
            b.beam((sx*.89, sy, .45), (sx, sy, .48), .035, .035, "galv", fit=True)
            b.beam((sx, sy, .48), (sx, sy, .82), .03, .03, "galv", fit=True, caps=False)
    collide(b, (-.5, -1.05, 0), (.5, 1.05, .82))
    if tilt: b.pop()
    b.pop()


def gurney(b, x, y, z, yaw=0, sheet_on=True):
    place(b, x, y, z, yaw)
    for sy in (-.8, .8):
        b.beam((0, sy, .12), (0, sy, .78), .05, .05, "galv", fit=True, caps=False)
        b.beam((-.3, sy, .12), (.3, sy, .12), .04, .04, "galv", fit=True)
        for sx in (-.3, .3): b.cyl((sx - .03, sy, .07), (sx + .03, sy, .07), .07, 6, "tyre", True, True)
    b.box((-.34, -.98, .78), (.34, .98, .88), "steel", fit=True, skip="")
    b.box((-.32, -.95, .88), (.32, .95, .96), "whitepanel" if sheet_on else "blue", fit=True, skip="b")
    b.box((-.35, -.6, .83), (-.32, .6, 1.12), "galv", fit=True)
    collide(b, (-.36, -.98, 0), (.36, .98, .96))
    b.pop()


def iv(b, x, y, z, fallen=False):
    if fallen:
        b.cyl((x, y, z + .05), (x + 1.7, y + .6, z + .05), .02, 5, "galv")
        b.box((x + 1.55, y + .45, z), (x + 1.75, y + .62, z + .05), "whitepanel", fit=True, skip="b")
        return
    b.cyl((x, y, z + .05), (x, y, z + 2.0), .02, 5, "galv", cap_b=True)
    for a in range(0, 360, 72):
        r = math.radians(a)
        b.beam((x, y, z + .07), (x + .32 * math.cos(r), y + .32 * math.sin(r), z + .015), .03, .03, "galv", fit=True)
    b.beam((x - .18, y, z + 1.95), (x + .18, y, z + 1.95), .02, .02, "galv", fit=True)
    b.cyl((x+.14,y,z+1.85),(x+.14,y,z+1.95),.012,5,"galv")
    b.box((x + .08, y - .05, z + 1.55), (x + .2, y + .05, z + 1.85), "whitepanel", fit=True, skip="")


def monitor_cart(b, x, y, z, yaw=0, on=True):
    place(b, x, y, z, yaw)
    b.cyl((0, 0, .05), (0, 0, 1.15), .03, 5, "galv")
    for a in range(45, 360, 90):
        r = math.radians(a)
        b.beam((0, 0, .08), (.28 * math.cos(r), .28 * math.sin(r), .015), .04, .03, "darkmetal", fit=True)
    b.box((-.25, -.08, 1.15), (.25, .12, 1.55), "darkmetal", fit=True, skip="")
    glowquad(b, (-.21, -.085, 1.19), (.42, 0, 0), (0, 0, .32), GLOW if on else T["VOID"])
    b.pop()


def chair(b, x, y, z, yaw=0, seat="blue", fallen=False):
    """Waiting chair facing local -Y."""
    place(b, x, y, z, yaw)
    if fallen: b.push(Matrix.Translation((0, 0, .27)) @ Matrix.Rotation(math.radians(90), 4, "X"))
    for sx in (-.2, .2):
        for sy in (-.2, .2):
            b.beam((sx, sy, 0), (sx, sy, .42), .03, .03, "darkmetal", fit=True, caps=False)
    b.box((-.24, -.24, .42), (.24, .22, .5), seat, fit=True, skip="")
    b.box((-.24, .18, .5), (.24, .25, .95), seat, fit=True)
    if not fallen: collide(b, (-.24, -.25, 0), (.24, .25, .5))
    if fallen: b.pop()
    b.pop()


def seat_row(b, x, y, z, yaw, n, seat="blue"):
    """Linked beam seating, n seats along local X, facing local -Y."""
    place(b, x, y, z, yaw)
    L = n * .56
    b.beam((-L / 2, 0, .36), (L / 2, 0, .36), .08, .08, "darkmetal", fit=True)
    for sx in (-L / 2 + .2, L / 2 - .2):
        b.box((sx - .03, -.25, 0), (sx + .03, .25, .36), "darkmetal", fit=True, skip="b")
    for i in range(n):
        sx = -L / 2 + .28 + i * .56
        b.box((sx - .25, -.24, .4), (sx + .25, .2, .48), seat, fit=True, skip="")
        b.box((sx - .25, .16, .48), (sx + .25, .24, .92), seat, fit=True)
    collide(b, (-L / 2, -.25, 0), (L / 2, .25, .48))
    b.pop()


def counter(b, lo, hi, front="whitepanel", top="greypanel", check=True):
    solid(b, lo, (hi[0], hi[1], hi[2] - .04), front, skip="b", fit=True, check=check)
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    b.box((x0 - .04, y0 - .04, z1 - .04), (x1 + .04, y1 + .04, z1), top, skip="", fit=False)


def pc(b, x, y, z, yaw=0, on=False):
    place(b, x, y, z, yaw)
    b.box((-.24, -.02, .08), (.24, .04, .4), "darkmetal", fit=True, skip="")
    glowquad(b, (-.2, -.025, .11), (.4, 0, 0), (0, 0, .26), GLOW if on else T["VOID"])
    b.box((-.04, .02, 0), (.04, .08, .1), "darkmetal", fit=True)
    b.box((-.2, -.3, 0), (.2, -.16, .02), "darkmetal", fit=True)
    b.pop()


def cabinet(b, x, y, z, yaw, w, d, h, tile="whitepanel", drawers=4, check=True):
    """Cabinet against a wall; local -Y is its front."""
    place(b, x, y, z, yaw)
    solid(b, (-w / 2, -d / 2, 0), (w / 2, d / 2, h), tile, fit=True, check=check)
    for i in range(1, drawers):
        zz = h * i / drawers
        b.beam((-w / 2 + .03, -d / 2 - .005, zz), (w / 2 - .03, -d / 2 - .005, zz), .012, .02, "darkmetal", fit=True, caps=False)
    for i in range(drawers):
        zz = h * (i + .5) / drawers
        b.box((-.08, -d / 2 - .03, zz - .015), (.08, -d / 2, zz + .015), "galv", fit=True, skip="y+")
    b.pop()


def stocked(b, x, y, z, yaw, L, check=True):
    """The Compound's steel shelving unit (depth .45, height 2) with its back on local Y 0."""
    place(b, x, y, z, yaw)
    shelf(b, -L / 2, 0, 0, L, rng)
    collide(b, (-L / 2, -.45, 0), (L / 2, 0, 2.0), check)
    b.pop()


def wheelchair(b, x, y, z, yaw=0, tipped=False):
    place(b, x, y, z, yaw)
    if tipped: b.push(Matrix.Translation((0, 0, .32)) @ Matrix.Rotation(math.radians(85), 4, "Y"))
    for sx in (-.3, .3):
        b.cyl((sx - .02, .05, .3), (sx + .02, .05, .3), .3, 10, "tyre", True, True)
        b.cyl((sx, -.38, .07), (sx + .01, -.38, .07), .07, 6, "tyre", True, True)
        b.beam((sx * .85, -.38, .1), (sx * .85, .2, .5), .03, .03, "galv", fit=True)
        b.beam((sx * .85, .2, .45), (sx * .85, .25, 1.0), .03, .03, "galv", fit=True)
    b.box((-.24, -.25, .46), (.24, .2, .52), "blue", fit=True, skip="")
    b.box((-.24, .2, .52), (.24, .24, .92), "blue", fit=True)
    if not tipped: collide(b, (-.32, -.45, 0), (.32, .3, .55))
    if tipped: b.pop()
    b.pop()


def crash_cart(b, x, y, z, yaw=0):
    place(b, x, y, z, yaw)
    solid(b, (-.38, -.28, .1), (.38, .28, 1.0), "red", fit=True)
    for i in range(1, 5):
        b.beam((-.35, -.285, .1 + .18 * i), (.35, -.285, .1 + .18 * i), .012, .02, "darkmetal", fit=True, caps=False)
    for sx in (-.3, .3):
        for sy in (-.2, .2): b.cyl((sx - .02, sy, .06), (sx + .02, sy, .06), .06, 6, "tyre", True, True)
    b.box((-.3, -.2, 1.0), (.1, .2, 1.18), "lamp", fit=True)
    b.pop()


def trolley(b, x, y, z, yaw=0, load=True):
    place(b, x, y, z, yaw)
    for sx in (-.32, .32):
        for sy in (-.22, .22):
            b.beam((sx, sy, .05), (sx, sy, .92), .025, .025, "galv", fit=True, caps=False)
            b.cyl((sx-.02,sy,.05),(sx+.02,sy,.05),.05,8,"tyre",True,True)
    for zz in (.3, .9):
        b.box((-.36, -.25, zz), (.36, .25, zz + .03), "galv", fit=True, skip="")
    if load:
        for i in range(3):
            b.box((-.28 + i * .2, -.15, .93), (-.14 + i * .2, .1, .99 + .04 * i), rng.choice(["whitepanel", "blue", "galv"]), fit=True, skip="b")
    collide(b, (-.36, -.25, 0), (.36, .25, .93))
    b.pop()


def plant(b, x, y, z):
    b.cyl((x, y, z), (x, y, z + .45), .22, 8, "concrete", cap_b=True)
    for k in range(5):
        a = k * 1.3 + rng.random()
        b.push(Matrix.Translation((x + .1 * math.cos(a), y + .1 * math.sin(a), z + .4)) @ Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(.5, 4, "X"))
        b.box((-.12, -.03, 0), (.12, .03, .65 + .15 * rng.random()), "grass", skip="b")
        b.pop()
    BOXES.append({"c": U((x, y, z + .22)), "s": [.44, .45, .44], "yaw": 0, "room": b.name})


def papers(b, x0, x1, y0, y1, z, n):
    for i in range(n):
        x, y, a = rng.uniform(x0, x1), rng.uniform(y0, y1), rng.uniform(0, math.pi)
        w, h = .21, .3
        c, s = math.cos(a), math.sin(a)
        p0 = V((x - c * w / 2 + s * h / 2, y - s * w / 2 - c * h / 2, z + .018))
        b.quad(p0, V((c * w, s * w, 0)), V((-s * h, c * h, 0)), "whitepanel", fit=True)


def box_pile(b, x, y, z, n, tiles=("crate", "olive", "galv", "whitepanel")):
    zz = 0
    for i in range(n):
        s = rng.uniform(.35, .55)
        T["crate"](b, x + rng.uniform(-.06, .06), y + rng.uniform(-.06, .06), z + zz, s, s * .85, s * .7, rng.choice(tiles), rng.uniform(-20, 20))
        zz += s * .7
    BOXES.append({"c": U((x, y, z + zz / 2)), "s": [.5, round(zz, 3), .45], "yaw": 0, "room": b.name})


def curtain(b, a, c, z0, z1, tile="blue", pleats=.35, rail=True):
    """Pleated privacy curtain from (x,y) a to c, hanging between z0 and z1, both sides drawn."""
    a, c = V((a[0], a[1], 0)), V((c[0], c[1], 0))
    d = c - a
    L = d.length
    n = max(2, int(L / pleats))
    side = V((-d.y, d.x, 0)).normalized() * .07
    pts = [a + d * (i / n) + (side if i % 2 else V()) for i in range(n + 1)]
    for p, q in zip(pts, pts[1:]):
        b.quad(V((p.x, p.y, z0)), q - p, V((0, 0, z1 - z0)), tile, fit=True)
        b.quad(V((q.x, q.y, z0)), p - q, V((0, 0, z1 - z0)), tile, fit=True)
    if rail:
        b.beam((a.x, a.y, z1 + .02), (c.x, c.y, z1 + .02), .04, .05, "galv", fit=True)
        for anchor in (a, c):
            ceiling = CEIL[4 if z1 > 4 else 0]
            b.cyl((anchor.x, anchor.y, z1 + .02), (anchor.x, anchor.y, ceiling), .025, 6, "galv")


def bay_rail(b, x0, x1, y0, y1, z):
    """Ceiling-hung U-rail with drop rods."""
    for p, q in (((x0, y0), (x0, y1)), ((x0, y1), (x1, y1)), ((x1, y1), (x1, y0))):
        b.beam((p[0], p[1], z), (q[0], q[1], z), .04, .04, "galv", fit=True)
    for x in (x0, x1):
        for y in (y0, y1):
            b.cyl((x, y, z), (x, y, CEIL[0]), .025, 6, "galv")


def wall_tv(b, x, y, z, yaw):
    place(b, x, y, z, yaw)
    b.box((-.45, -.06, -.28), (.45, 0, .28), "darkmetal", fit=True)
    glowquad(b, (-.41, -.062, -.24), (.82, 0, 0), (0, 0, .48), T["VOID"])
    b.pop()


def board(b, x, y, z, yaw, w=2.0, h=1.0, tile="whitepanel"):
    place(b, x, y, z, yaw)
    b.box((-w / 2 - .04, -.04, -h / 2 - .04), (w / 2 + .04, 0, h / 2 + .04), "galv", fit=True, skip="y+")
    b.quad((-w / 2, -.045, -h / 2), (w, 0, 0), (0, 0, h), tile, fit=True)
    b.pop()


def sink(b, x, y, z, yaw, n=1):
    place(b, x, y, z, yaw)
    w = .7 * n
    solid(b, (-w / 2, -.28, 0), (w / 2, .28, .82), "whitepanel", fit=True, check=False)
    b.box((-w / 2, -.3, .82), (w / 2, .3, .9), "steel", fit=True, skip="b")
    for i in range(n):
        sx = -w / 2 + .35 + .7 * i
        b.cyl((sx, .18, .9), (sx, .18, 1.15), .02, 5, "galv")
        b.beam((sx, .18, 1.15), (sx, .02, 1.12), .025, .025, "galv", fit=True)
    b.box((-w / 2, .24, .9), (w / 2, .3, 1.5), "whitepanel", fit=True, skip="b")
    b.pop()


def x_ray_box(b, x, y, z, yaw, lit=True):
    place(b, x, y, z, yaw)
    b.box((-.7, -.08, -.4), (.7, 0, .4), "whitepanel", fit=True, skip="y+")
    glowquad(b, (-.6, -.085, -.3), (1.2, 0, 0), (0, 0, .6), LENS if lit else T["VOID"])
    b.pop()


def op_table(b, x, y, z, yaw=0):
    place(b, x, y, z, yaw)
    b.box((-.35, -.5, 0), (.35, .5, .08), "darkmetal", fit=True, skip="b")
    b.box((-.15, -.2, .08), (.15, .2, .7), "galv", fit=True, skip="b")
    b.box((-.32, -1.05, .7), (.32, 1.05, .8), "steel", fit=True, skip="")
    b.box((-.3, -1.02, .8), (.3, 1.02, .88), "teal", fit=True, skip="b")
    for sy in (-.95, .95):
        b.box((-.36, sy - .03, .7), (.36, sy + .03, .82), "galv", fit=True)
    collide(b, (-.36, -1.05, 0), (.36, 1.05, .88))
    b.pop()


def op_lamp(b, x, y, ceil, reach=(.6, .4)):
    """Ceiling surgical light: mount, articulated arm and two lamp heads."""
    b.cyl((x, y, ceil - .1), (x, y, ceil), .2, 10, "whitepanel", cap_a=True)
    b.cyl((x, y, ceil - .1), (x, y, ceil - .9), .05, 6, "whitepanel")
    for k, (dx, dy) in enumerate(((reach[0], reach[1]), (-reach[1], reach[0] * .6))):
        hx, hy, hz = x + dx, y + dy, ceil - 1.6 - .12 * k
        b.beam((x, y, ceil - .9), (hx, hy, hz + .14), .06, .06, "whitepanel", fit=True)
        b.cyl((hx, hy, hz), (hx, hy, hz + .16), .32, 12, "whitepanel", cap_b=True)
        disc(b, (hx, hy, hz - .001), .26, 12, LENS)


def anaesthesia(b, x, y, z, yaw):
    place(b, x, y, z, yaw)
    solid(b, (-.4, -.3, 0), (.4, .3, 1.25), "whitepanel", fit=True, check=False)
    b.quad((-.36, -.305, .65), (.72, 0, 0), (0, 0, .5), "dials", fit=True)
    b.box((-.3, -.1, 1.25), (.3, .1, 1.62), "darkmetal", fit=True)
    glowquad(b, (-.26, -.105, 1.29), (.52, 0, 0), (0, 0, .29), GLOW)
    for sx, t in ((-.25, "blue"), (.05, "teal"), (.3, "galv")):
        b.cyl((sx, .4, 0), (sx, .4, .9), .09, 8, t, cap_b=True)
    b.pop()


def ct_scanner(b, x, y, z, yaw=0):
    place(b, x, y, z, yaw)
    b.box((-1.2, -.5, 0), (1.2, .5, .5), "whitepanel", fit=True, skip="b")
    ring(b, V((0, 0, 1.25)), .38, 1.05, .9, 16, "whitepanel", "galv", "whitepanel")
    b.box((-.95, -.42, 0), (.95, .42, .35), "whitepanel", fit=True, skip="b")
    collide(b, (-1.2, -.5, 0), (1.2, .5, 2.3), check=True)
    # Patient table through the bore.
    b.box((-.22, -2.6, 0), (.22, -1.1, .62), "galv", fit=True, skip="b")
    b.box((-.3, -2.8, .62), (.3, .3, .76), "whitepanel", fit=True, skip="")
    b.box((-.26, -2.75, .76), (.26, -.6, .8), "teal", fit=True, skip="b")
    collide(b, (-.3, -2.8, 0), (.3, -.5, .8))
    b.pop()


def washer(b, x, y, z, yaw, h=1.25, w=1.05, d=.95, open_door=False):
    """Industrial washer; door on local -Y."""
    place(b, x, y, z, yaw)
    solid(b, (-w / 2, -d / 2, 0), (w / 2, d / 2, h), "whitepanel", top="greypanel", fit=True, check=False)
    b.quad((-w / 2 + .04, -d / 2 - .006, h - .22), (w - .08, 0, 0), (0, 0, .17), "dials", fit=True)
    b.cyl((0, -d / 2 + .01, h * .45), (0, -d / 2 - .05, h * .45), .33, 14, "galv", cap_b=False)
    disc(b, (0, -d / 2 - .052, h * .45), .26, 14, "glass", normal=(0, -1, 0))
    b.pop()


def folding_table(b, x, y, z, yaw, L=2.4, D=.9):
    place(b, x, y, z, yaw)
    for sx in (-L / 2 + .1, L / 2 - .1):
        for sy in (-D / 2 + .08, D / 2 - .08):
            b.beam((sx, sy, 0), (sx, sy, .86), .05, .05, "galv", fit=True, caps=False)
    b.box((-L / 2, -D / 2, .86), (L / 2, D / 2, .92), "greypanel", fit=True, skip="")
    collide(b, (-L / 2, -D / 2, 0), (L / 2, D / 2, .92))
    for i in range(int(L / .6)):
        if rng.random() < .65:
            sx = -L / 2 + .3 + i * .6
            b.box((sx - .2, -.25, .92), (sx + .2, .25, .92 + .04 * rng.randint(1, 4)), rng.choice(["whitepanel", "blue", "whitepanel"]), fit=True, skip="b")
    b.pop()


def laundry_cart(b, x, y, z, yaw=0, full=True, tipped=False):
    place(b, x, y, z, yaw)
    if tipped: b.push(Matrix.Translation((0, 0, .35)) @ Matrix.Rotation(math.radians(-90), 4, "X"))
    for sx in (-.4, .4):
        for sy in (-.3, .3): b.cyl((sx - .02, sy, .06), (sx + .02, sy, .06), .06, 6, "tyre", True, True)
    b.box((-.45, -.33, .12), (.45, .33, .85), "blue", fit=True, skip="t")
    b.box((-.42, -.3, .14), (.42, .3, .14), "darkmetal", fit=True, skip="b")
    b.box((-.47, -.35, .83), (.47, .35, .87), "galv", fit=True, skip="b")
    if full:
        for i in range(4):
            b.box((-.38 + .19 * i, -.25, .55), (-.2 + .19 * i, .2, .9 + .05 * rng.random()), "whitepanel", fit=True, skip="b")
    if tipped:
        b.pop()
        for i in range(5):
            sx = rng.uniform(-.6, .6)
            b.box((sx - .25, -1.2 - .3 * i, 0), (sx + .25, -.9 - .3 * i, .06), "whitepanel", fit=True, skip="b")
        collide(b, (-.45, -.33, 0), (.45, .45, .7))
    else:
        collide(b, (-.47, -.35, 0), (.47, .35, .87))
    b.pop()


def linen_shelf(b, x, y, z, yaw, L=2.0):
    place(b, x, y, z, yaw)
    for sx in (-L / 2 + .03, L / 2 - .03):
        for sy in (-.45, -.03):
            b.beam((sx, sy, 0), (sx, sy, 2.0), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    for zz in (.15, .65, 1.15, 1.65):
        b.box((-L / 2, -.45, zz), (L / 2, 0, zz + .04), "galv")
        sx = -L / 2 + .08
        while sx < L / 2 - .4:
            w = rng.uniform(.3, .42)
            if rng.random() > .2:
                b.box((sx, -.4, zz + .04), (sx + w, -.06, zz + .04 + rng.uniform(.14, .3)), rng.choice(["whitepanel", "whitepanel", "blue", "teal"]), skip="b", fit=True)
            sx += w + .05
    collide(b, (-L / 2, -.45, 0), (L / 2, 0, 2.0))
    b.pop()


def exam_table(b, x, y, z, yaw):
    place(b, x, y, z, yaw)
    solid(b, (-.35, -.9, 0), (.35, .9, .55), "whitepanel", fit=True, check=False)
    b.box((-.36, -.9, .55), (.36, .5, .68), "teal", fit=True, skip="b")
    b.push(Matrix.Translation((0, .5, .62)) @ Matrix.Rotation(math.radians(-30), 4, "X"))
    b.box((-.36, 0, 0), (.36, .55, .12), "teal", fit=True)
    b.pop()
    b.box((-.3, -.88, .69), (.3, .2, .695), "whitepanel", fit=True, skip="b")
    b.cyl((-.3, .78, .5), (.3, .78, .5), .06, 8, "whitepanel", True, True)
    b.box((-.14, -1.15, .1), (.14, -.9, .2), "darkmetal", fit=True)
    b.pop()


def gondola(b, x, y, z, yaw, L):
    """Double-sided pharmacy shelving along local X, end caps at both ends."""
    place(b, x, y, z, yaw)
    for side in (0, 180):
        b.push(Matrix.Rotation(math.radians(side), 4, "Z"))
        shelf(b, -L / 2, -.005, 0, L, rng)
        b.pop()
    for sx in (-L / 2 - .05, L / 2):
        b.box((sx, -.5, 0), (sx + .05, .5, 2.05), "whitepanel", fit=True, skip="b")
    b.box((-L / 2 - .05, -.5, 2.05), (L / 2 + .05, .5, 2.1), "whitepanel", fit=True)
    collide(b, (-L / 2 - .05, -.5, 0), (L / 2 + .05, .5, 2.1))
    b.pop()


def vending(b, x, y, z, yaw, tile="red"):
    place(b, x, y, z, yaw)
    solid(b, (-.5, -.4, 0), (.5, .4, 1.9), tile, fit=True, check=False)
    b.quad((-.42, -.405, .5), (.6, 0, 0), (0, 0, 1.25), "glass", fit=True)
    glowquad(b, (-.4, -.41, 1.55), (.56, 0, 0), (0, 0, .15), GLOW)
    b.quad((.24, -.405, .8), (.14, 0, 0), (0, 0, .5), "dials", fit=True)
    b.pop()


def water_cooler(b, x, y, z, yaw):
    place(b, x, y, z, yaw)
    solid(b, (-.17, -.17, 0), (.17, .17, 1.0), "whitepanel", fit=True, check=False)
    b.cyl((0, 0, 1.0), (0, 0, 1.42), .14, 8, "blue", cap_b=True)
    b.pop()


def pipes(b, a, c, z, r=.08, tile="galv", n=2, gap=.25):
    for i in range(n):
        o = (i - (n - 1) / 2) * gap
        d = V((c[0] - a[0], c[1] - a[1], 0)).normalized()
        s = V((-d.y, d.x, 0)) * o
        b.cyl((a[0] + s.x, a[1] + s.y, z), (c[0] + s.x, c[1] + s.y, z), r, 8, tile)
        for t in (.06, .5, .94):
            x, y = a[0] + (c[0]-a[0])*t + s.x, a[1] + (c[1]-a[1])*t + s.y
            b.cyl((x, y, z), (x, y, SMALL_CEIL), .028, 6, "galv")


# ---------------------------------------------------------------- hospital
def corridor_level(b, z):
    ceil = CEIL[z]
    up = z > 0
    # Vinyl corridor floor: loop and the stair halls.
    for x0, x1, y0, y1 in ((18.13, 21.87, 66.13, 93.87), (30.13, 33.87, 66.13, 93.87), (21.87, 30.13, 66.13, 69.87), (21.87, 30.13, 90.13, 93.87)):
        skin_floor(b, x0, x1, y0, y1, z, "greypanel")
    # Dado on both faces of the courtyard walls is not possible (they are window walls); dress the room side of the corridor walls.
    dado(b, "x", 18, 66.13, 93.87, z, 1, ((72, 75), (79, 83), (87, 90)))
    dado(b, "x", 34, 66.13, 93.87, z, -1, ((68, 71), (78, 81), (88, 91)))
    dado(b, "y", 94, 18.13, 33.87, z, -1, ((19, 22), (29, 32)))
    dado(b, "y", 66, 18.13, 33.87, z, 1, ((18, 21), (28, 31)) + (((23, 27),) if not up else ()))
    for c, gaps in ((18, ((72, 75), (79, 83), (87, 90))), (34, ((68, 71), (78, 81), (88, 91)))):
        for a0, a1 in gaps: threshold(b, "x", c, a0, a1, z)
    for c, gaps in ((94, ((19, 22), (29, 32))), (66, ((18, 21), (28, 31)) + (((23, 27),) if not up else ()))):
        for a0, a1 in gaps: threshold(b, "y", c, a0, a1, z)
    if not up:
        threshold(b, "x", 10, 78, 82, z); threshold(b, "y", 55, 23, 27, z); threshold(b, "y", 105, 24, 27, z)
    for x, y in ((25, 68), (25, 92), (20, 74), (20, 86), (32, 80), (32, 72), (32, 88)):
        troffer(b, x, y, ceil, lit=(x, y) in ((25, 68), (25, 92), (32, 80)) or rng.random() > .35)
    if not up:
        gurney(b, 18.75, 84.6, z, 0)
        wheelchair(b, 18.7, 77.5, z, 90)
        wheelchair(b, 33.25, 86.3, z, -90, tipped=True)
        seat_row(b, 25.0, 93.4, z, 0, 4, "blue")
        plant(b, 22.6, 93.3, z); plant(b, 33.3, 93.3, z)
        crash_cart(b, 33.45, 75.6, z, -90)
        iv(b, 33.4, 82.6, z, fallen=True)
        papers(b, 19.0, 21.2, 70, 88, z, 9)
        plate(b, "WARDS", (34 - WALL_T - .02, 79.5, z + 2.75), 90 + 180, 2.2)
        plate(b, "SUPPLIES", (25.5, 94 - WALL_T - .02, z + 2.75), 0, 2.6)
        plate(b, "WAITING", (18.025, 81.0, z + 2.42), 90, 2.4, hang=z+2.74, two_sided=True)
    else:
        gurney(b, 33.25, 74.5, z, 0, sheet_on=False)
        trolley(b, 18.65, 70.2, z, 90)
        wheelchair(b, 18.7, 84.7, z, 90)
        seat_row(b, 25.0, 93.4, z, 0, 3, "teal")
        plant(b, 33.3, 93.3, z)
        iv(b, 33.35, 84.9, z)
        papers(b, 30.6, 33.4, 70, 90, z, 7)
        plate(b, "RECOVERY", (25.5, 94 - WALL_T - .02, z + 2.75), 0, 2.8)
        plate(b, "NURSES", (18 + WALL_T + .02, 85.0, z + 2.75), -90 + 180, 2.2)
        plate(b, "ROOMS 201-203", (34 - WALL_T - .02, 79.5, z + 2.75), 90 + 180, 3.2)


def er(b):
    z, ceil = 0, CEIL[0]
    skin_floor(b, 10.13, 35.6, 55.13, 65.87, z, "greypanel")
    dado(b, "y", 66, 10.13, 34, z, -1, ((18, 21), (23, 27), (28, 31)))
    # Four curtained bays along the south wall, west of the arrival lane.
    for i, x0 in enumerate((10.4, 13.2, 16.0, 18.8)):
        x1 = x0 + 2.75
        bay_rail(b, x0, x1, 55.2, 58.6, 2.45)
        if i < 3: curtain(b, (x1, 55.4), (x1, 58.4), .35, 2.4, "blue", rail=False)
        open_to = x0 + (0.7 if i % 2 else 1.9)
        curtain(b, (x0, 58.6), (open_to, 58.6), .35, 2.4, "blue", rail=False)
        bed(b, x0 + 1.37, 56.65, z, 180, "blue" if i != 2 else None, tilt=rng.uniform(-6, 6))
        monitor_cart(b, x0 + .35, 55.6, z, 180, on=i % 2 == 0)
        if i != 1: iv(b, x0 + 2.35, 57.9, z)
    curtain(b, (10.4, 55.2), (10.4, 58.6), .35, 2.4, "blue", rail=False)
    # Two bays along the west end of the corridor wall, facing the open floor.
    for x0 in (10.4, 14.3):
        bay_rail(b, x0, x0 + 3.4, 65.8, 62.6, 2.45)
        curtain(b, (x0, 65.6), (x0, 62.6), .35, 2.4, "teal", rail=False)
        bed(b, x0 + 1.7, 64.65, z, 0, "teal")
        iv(b, x0 + .5, 63.2, z)
    curtain(b, (17.7, 65.6), (17.7, 62.6), .35, 2.4, "teal", rail=False)
    monitor_cart(b, 16.0, 62.9, z, 180, on=False)
    # Triage desk east of the arrival lane, with chairs and a crash cart.
    counter(b, (28.2, 60.0, 0), (32.4, 61.0, 1.05))
    counter(b, (31.4, 61.0, 0), (32.4, 63.0, 1.05))
    pc(b, 29.4, 60.55, 1.05, 180, on=True); pc(b, 31.0, 60.55, 1.05, 180)
    chair(b, 29.6, 61.8, z, 180, "red"); chair(b, 30.8, 62.2, z, 160, "red", fallen=True)
    plate(b, "TRIAGE", (30.3, 59.96, .62), 0, 1.8, .36)
    crash_cart(b, 27.9, 57.2, z, 0)
    gurney(b, 30.9, 57.6, z, 90, sheet_on=False)
    wheelchair(b, 33.6, 61.6, z, 200)
    cabinet(b, 10.4, 60.8, z, 90, 1.6, .5, 1.9)
    papers(b, 17.0, 23.0, 59.5, 62.5, z, 8)
    papers(b, 26.8, 31.0, 61.6, 64.8, z, 5)
    iv(b, 22.0, 61.0, z, fallen=True)
    for x, y in ((15, 60), (32, 60)): troffer(b, x, y, ceil)
    for x, y in ((12, 57), (17.5, 57), (12, 64.2), (21, 60.4)): troffer(b, x, y, ceil, lit=rng.random() > .3)
    light("ER bays", (16.0, 57.2, 3.2), 14, 9)
    troffer(b, 26.0, 61.0, ceil); light("ER arrival", (26.0, 61.0, 3.2), 14, 10)


def reception(b):
    z, ceil = 0, CEIL[0]
    skin_floor(b, 10.13, 17.87, 66.13, 93.87, z, "greypanel")
    dado(b, "x", 18, 66.13, 93.87, z, -1, ((72, 75), (79, 83), (87, 90)))
    dado(b, "y", 66, 10.13, 17.87, z, 1)
    dado(b, "y", 94, 10.13, 17.87, z, -1)
    # Front desk north of the entrance lane.
    counter(b, (11.6, 82.7, 0), (16.4, 83.6, 1.1), "teal", "whitewood")
    counter(b, (11.6, 83.6, 0), (12.5, 86.6, 1.1), "teal", "whitewood")
    b.box((11.2, 82.5, 1.1), (16.8, 82.9, 1.18), "whitewood", fit=False)
    for x in (13.3, 15.2): pc(b, x, 84.9, .76, 180, on=x < 14)
    solid(b, (12.6, 84.6, 0), (16.4, 85.4, .76), "whitepanel", top="whitewood", fit=True)
    chair(b, 14.2, 86.1, z, 20, "blue")
    plate(b, "INFORMATION", (14.0, 82.66, .62), 0, 3.0, .36)
    # Waiting rows south of the lane, facing the desk.
    for y in (70.5, 73.2, 75.9):
        seat_row(b, 13.9, y, z, 180, 5, "blue")
    b.box((15.9, 77.0, 0), (17.0, 77.7, .4), "whitewood", fit=False, skip="b")
    collide(b, (15.9, 77.0, 0), (17.0, 77.7, .4))
    papers(b, 15.9, 17.0, 77.0, 77.7, .4, 3)
    vending(b, 11.0, 66.55, z, 180, "red"); vending(b, 12.15, 66.55, z, 180, "blue")
    water_cooler(b, 17.4, 67.0, z, 0)
    plant(b, 10.8, 77.4, z); plant(b, 17.2, 77.4, z)
    chair(b, 16.9, 72.0, z, 120, "blue", fallen=True)
    papers(b, 11.0, 17.0, 68.5, 77.5, z, 10)
    # Records and wheelchair park north of the desk.
    for y in (88.0, 89.0, 90.0, 91.0): cabinet(b, 10.45, y, z, 90, .9, .6, 1.35, "galv")
    wheelchair(b, 16.9, 92.6, z, -90); wheelchair(b, 16.9, 91.4, z, -90)
    board(b, 14.0, 93.85, 1.7, 0, 2.4, 1.1)
    for x, y in ((15, 80), (13, 72), (13, 88)): troffer(b, x, y, ceil, lit=(x, y) != (13, 88) or rng.random() > .3)
    light("Waiting area", (13.0, 72.0, 3.2), 13, 9)


def east_ground(b):
    z, ceil = 0, CEIL[0]
    skin_floor(b, 34.13, 41.87, 64.0, 95.5, z, "greypanel")
    # Imaging suite (south) is closed off from the ward by a new partition at Y 77.
    partition(b, (34.13, 77), (41.87, 77), (), h=3.7)
    dado(b, "x", 34, 64.13, 95.5, z, 1, ((68, 71), (78, 81), (88, 91)))
    ct_scanner(b, 38.6, 70.2, z, 90)
    # Control desk and lead apron rail along the partition.
    counter(b, (35.0, 75.6, 0), (38.6, 76.6, .8), "whitepanel", "greypanel")
    pc(b, 35.9, 76.1, .8, 0, on=True); pc(b, 37.3, 76.1, .8, 0, on=True)
    chair(b, 36.6, 75.0, z, 0, "teal")
    x_ray_box(b, 41.85, 74.5, 1.7, -90)
    cabinet(b, 41.5, 67.3, z, -90, 1.4, .7, 1.1, "whitepanel", 3)
    plate(b, "CT SCAN", (42 - WALL_T - .02, 70.2, 2.8), -90, 2.0, .45, "lamp")
    # General ward: four beds head-on to the east wall, curtains between.
    for i, y in enumerate((79.6, 83.3, 87.0, 90.7)):
        bed(b, 40.8, y, z, -90, ("blue", "teal", None, "blue")[i], tilt=rng.uniform(-8, 8))
        cabinet(b, 41.6, y + 1.25, z, -90, .5, .45, .8, "whitepanel", 2)
        if i < 3: curtain(b, (39.0, y + 1.85), (41.85, y + 1.85), .35, 2.4, "teal")
        if i != 2: iv(b, 39.4, y - .9, z)
        else: iv(b, 37.8, y + .4, z, fallen=True)
    chair(b, 38.6, 84.3, z, -70, "blue")
    chair(b, 37.4, 92.4, z, 30, "blue", fallen=True)
    trolley(b, 35.0, 93.6, z, 90)
    papers(b, 35.5, 39.0, 79, 94, z, 7)
    for y in (70.5, 85.0): troffer(b, 38.0, y, ceil)
    troffer(b, 38.0, 91.5, ceil, lit=False)
    light("Imaging suite", (38.0, 70.5, 3.2), 14, 9); light("General ward", (38.0, 85.0, 3.2), 14, 10)


def supplies(b):
    z, ceil = 0, CEIL[0]
    skin_floor(b, 10.13, 35.6, 94.13, 104.87, z, "greypanel")
    dado(b, "y", 94, 10.13, 35.6, z, 1, ((19, 22), (29, 32)))
    # Racking on the north wall, leaving the north door lane clear, and a free-standing run.
    for x in (11.2, 13.35, 15.5, 17.65, 19.8, 21.95): stocked(b, x, 104.85, z, 0, 2.1)
    for x in (29.0, 31.2): stocked(b, x, 104.85, z, 0, 2.1)
    for x in (12.4, 14.6, 16.8):
        stocked(b, x, 99.4, z, 0, 2.1); stocked(b, x, 99.4, z, 180, 2.1)
    cabinet(b, 10.45, 95.8, z, 90, 1.2, .6, 1.9, "galv", 5)
    # Oxygen cylinder cage.
    for i in range(6):
        b.cyl((33.0 + .32 * (i % 3), 103.8 - .32 * (i // 3), 0), (33.0 + .32 * (i % 3), 103.8 - .32 * (i // 3), 1.35), .13, 8, ("teal", "galv")[i % 2], cap_b=True)
    b.box((32.75, 103.1, .9), (33.95, 104.1, .95), "darkmetal", fit=True, skip="")
    collide(b, (32.8, 103.3, 0), (33.9, 104.1, 1.35))
    for x, y in ((20.3, 97.0), (21.0, 101.6), (31.6, 99.6)): box_pile(b, x, y, z, rng.randint(1, 3))
    laundry_cart(b, 28.6, 101.4, z, 15)
    trolley(b, 18.0, 96.4, z, 0)
    papers(b, 11.0, 22.0, 95.5, 98.5, z, 6)
    for x, y in ((15, 99), (32, 99)): troffer(b, x, y, ceil)
    troffer(b, 25.5, 101.5, ceil, lit=False)


def surgery(b):
    z, ceil = 4, CEIL[4]
    skin_floor(b, 10.13, 17.87, 66.13, 78.87, z, "whitepanel")
    skin_floor(b, 10.13, 35.6, 55.13, 65.87, z, "whitepanel")
    dado(b, "y", 66, 10.13, 34, z, -1, ((18, 21), (28, 31)), scheme=[(.12, "charcoal"), (1.12, "lightconc")])
    dado(b, "x", 18, 66.13, 78.87, z, -1, ((72, 75),), scheme=[(.12, "charcoal"), (1.12, "lightconc")])
    # Surgery 1 (west, between Y 66 and 79): the gold marker at (14, 76) stays clear.
    op_table(b, 13.8, 70.8, z, 0)
    op_lamp(b, 13.8, 70.8, ceil)
    anaesthesia(b, 11.0, 70.0, z, 90)
    trolley(b, 16.0, 69.6, z, 0)
    monitor_cart(b, 15.8, 72.6, z, 160)
    sink(b, 12.0, 66.45, z, 180, 2)
    cabinet(b, 10.45, 74.0, z, 90, 1.6, .55, 2.0, "whitepanel", 5)
    # Surgery 2 (X 10..24) and Surgery 3 (X 24..34).
    for cx, w in ((16.4, 2), (28.6, 3)):
        op_table(b, cx, 60.2, z, 0)
        op_lamp(b, cx, 60.2, ceil, (.7, .3) if w == 2 else (-.6, .4))
        anaesthesia(b, cx - 2.3, 61.0, z, 90)
        trolley(b, cx + 1.7, 59.0, z, 0)
        trolley(b, cx + 1.6, 62.2, z, 20, load=w == 2)
        monitor_cart(b, cx - 1.6, 58.3, z, 200, on=w == 3)
        iv(b, cx + .9, 61.9, z)
    sink(b, 11.0, 55.45, z, 180, 2)
    cabinet(b, 22.6, 55.45, z, 180, 2.0, .55, 2.0, "whitepanel", 5)
    cabinet(b, 25.2, 55.45, z, 180, 1.6, .55, 2.0, "whitepanel", 5)
    gurney(b, 12.2, 63.6, z, 90)
    x_ray_box(b, 10.15, 60.0, 5.7, 90)
    iv(b, 26.0, 63.7, z, fallen=True)
    for x, y in ((15, 60), (32, 60)): troffer(b, x, y, ceil)
    for x, y in ((21, 63.5), (26.2, 57.5), (14, 66.8 + 7)): troffer(b, x, y, ceil, lit=False)
    light("Surgery 1", (13.8, 72.2, 7.2), 13, 8)


def nurses(b):
    z, ceil = 4, CEIL[4]
    skin_floor(b, 10.13, 17.87, 79.13, 93.87, z, "greypanel")
    dado(b, "x", 18, 79.13, 93.87, z, -1, ((79, 83), (87, 90)))
    dado(b, "y", 79, 10.13, 17.87, z, 1)
    counter(b, (14.6, 83.6, z), (15.5, 88.0, z + 1.1), "teal", "whitewood")
    counter(b, (12.6, 87.1, z), (14.6, 88.0, z + 1.1), "teal", "whitewood")
    solid(b, (12.6, 83.6, z), (14.6, 84.4, z + .76), "whitepanel", top="whitewood", fit=True)
    for y in (84.6, 86.2): pc(b, 15.05, y, z + 1.1, -90, on=y < 85)
    pc(b, 13.6, 84.0, z + .76, 180)
    chair(b, 13.7, 85.4, z, -110, "teal"); chair(b, 12.4, 82.2, z, 140, "teal", fallen=True)
    plate(b, "NURSE STATION", (15.54, 85.8, z + .62), 90, 2.6, .36)
    for y in (90.8, 91.8, 92.8): cabinet(b, 10.45, y, z, 90, .9, .6, 1.35, "galv")
    cabinet(b, 10.45, 81.2, z, 90, 1.4, .55, 1.9, "whitepanel", 4)
    crash_cart(b, 17.2, 92.9, z, 0)
    board(b, 10.15, 86.0, z + 1.7, 90, 2.6, 1.1)
    papers(b, 11, 17, 80, 93, z, 12)
    for x, y in ((15, 80.8), (13, 90.0)): troffer(b, x, y, ceil, lit=y < 85)


def patient_rooms(b):
    z, ceil = 4, CEIL[4]
    skin_floor(b, 34.13, 41.87, 64.0, 95.5, z, "greypanel")
    dado(b, "x", 34, 64.13, 95.5, z, 1, ((68, 71), (78, 81), (88, 91)))
    for y0, y1, beds in ((64.0, 77.0, (66.6, 74.2)), (77.0, 87.0, (79.6, 84.4)), (87.0, 95.5, (91.0,))):
        for k, y in enumerate(beds):
            bed(b, 40.8, y, z, -90, ("blue", "teal")[k % 2], tilt=rng.uniform(-5, 5))
            cabinet(b, 41.6, y + (1.25 if k == 0 else -1.25), z, -90, .5, .45, .8, "whitepanel", 2)
            b.box((37.9, y - .45, z + .8), (38.6, y + .45, z + .84), "greypanel", fit=True)
            b.cyl((38.5, y, z), (38.5, y, z + .8), .03, 5, "galv")
            wall_tv(b, 41.85, y, z + 2.2, -90)
        if len(beds) > 1:
            curtain(b, (38.6, (beds[0] + beds[1]) / 2), (41.85, (beds[0] + beds[1]) / 2), z + .35, z + 2.4, "teal")
        chair(b, 36.8, (y0 + y1) / 2 + 1.0, z, -90, "blue", fallen=y0 > 76 and y0 < 78)
        troffer(b, 38, (y0 + y1) / 2, ceil, lit=y0 < 80)
    iv(b, 39.1, 66.0, z); iv(b, 39.1, 85.3, z)
    board(b, 34.3, 66.2, z + 1.7, 90, 1.4, .9)
    papers(b, 35, 39, 87.5, 94, z, 5)
    light("Patient room 201", (38.0, 70.5, 7.2), 12, 8); light("Patient room 202", (38.0, 82.0, 7.2), 12, 8)


def recovery(b):
    z, ceil = 4, CEIL[4]
    skin_floor(b, 10.13, 35.6, 94.13, 104.87, z, "greypanel")
    dado(b, "y", 94, 10.13, 35.6, z, 1, ((19, 22), (29, 32)))
    xs = (11.9, 15.3, 18.7, 22.1, 25.5, 28.9, 32.3)
    for i, x in enumerate(xs):
        if i == 4: gurney(b, x + .3, 102.7, z, 12)
        else: bed(b, x, 103.6, z, 0, ("blue", "teal", "blue", None, "", "teal", "blue")[i] or None, tilt=rng.uniform(-6, 6))
        if i < len(xs) - 1:
            curtain(b, (x + 1.7, 101.5), (x + 1.7, 104.85), z + .35, z + 2.4, "blue")
        if i % 2 == 0: monitor_cart(b, x - 1.05, 104.4, z, 0, on=i == 2)
        elif i != 5: iv(b, x + 1.05, 102.0, z)
    trolley(b, 15.0, 96.6, z, 0); laundry_cart(b, 11.8, 96.5, z, 0, full=True)
    wheelchair(b, 23.6, 98.6, z, 35, tipped=True)
    papers(b, 12, 33, 96, 101, z, 9)
    for x in (15, 32): troffer(b, x, 99, ceil)
    troffer(b, 24.0, 99, ceil, lit=False)


# ---------------------------------------------------------------- street buildings
def small_dado(b, x0, x1, y0, y1, entry, scheme, back=True):
    """Dado on the solid back/entry walls of a shell street building (north and south walls are windows)."""
    if back: dado(b, "x", x0, y0 + .13, y1 - .13, 0, 1, (), scheme=scheme)
    dado(b, "x", x1, y0 + .13, y1 - .13, 0, -1, (entry,), scheme=scheme)


def partition(b, a, c, doors=(), h=SMALL_CEIL, tile="plaster", scheme=None):
    """Full-height interior wall from (x,y) a to c (axis aligned) with door gaps; dado on both faces."""
    ax = "y" if abs(a[1] - c[1]) < 1e-6 else "x"
    if ax == "y":
        y = a[1]; lo, hi = min(a[0], c[0]), max(a[0], c[0])
    else:
        x = a[0]; lo, hi = min(a[1], c[1]), max(a[1], c[1])
    cur = lo
    for d0, d1 in sorted(doors) + [(hi, hi)]:
        if d0 > cur + .01:
            if ax == "y": solid(b, (cur, y - .12, 0), (d0, y + .12, h), tile, top=tile, check=False)
            else: solid(b, (x - .12, cur, 0), (x + .12, d0, h), tile, top=tile, check=False)
        if d1 > d0 and d1 < hi:
            if ax == "y": b.box((d0, y - .12, 2.4), (d1, y + .12, h), tile, skip="t")
            else: b.box((x - .12, d0, 2.4), (x + .12, d1, h), tile, skip="t")
        cur = d1
    for d0, d1 in doors: threshold(b, "y" if ax == "y" else "x", y if ax == "y" else x, d0, d1, 0, "whitepanel")
    for face in (1, -1):
        if ax == "y": dado(b, "y", y + face * (.12 - WALL_T), lo, hi, 0, face, doors, scheme=scheme)
        else: dado(b, "x", x + face * (.12 - WALL_T), lo, hi, 0, face, doors, scheme=scheme)


def clinic(b):
    X0, X1, Y0, Y1 = -40, -14, 82, 103
    sch = [(.12, "charcoal"), (1.05, "blue"), (99, "plaster")]
    skin_floor(b, X0 + .13, X1 - .13, Y0 + .13, Y1 - .13, 0, "whitepanel")
    threshold(b, "x", X1, 90.5, 94.5, 0, "whitepanel")
    small_dado(b, X0, X1, Y0, Y1, (90.5, 94.5), [(.12, "charcoal"), (1.05, "blue")])
    # Three exam rooms along the back wall behind a corridor.
    partition(b, (-31, 82.13), (-31, 102.87), ((84.6, 86.2), (91.7, 93.3), (98.8, 100.4)), scheme=[(.12, "charcoal"), (1.05, "blue")])
    for yy in (89.0, 96.0):
        partition(b, (-39.87, yy), (-31.12, yy), (), scheme=[(.12, "charcoal"), (1.05, "blue")])
    for k, (y0, y1) in enumerate(((82, 89), (89, 96), (96, 103))):
        cy = (y0 + y1) / 2
        exam_table(b, -36.6, cy - .4, 0, 0 if k != 1 else 180)
        cabinet(b, -39.55, cy + 2.2, 0, 90, 1.4, .6, .9, "whitepanel", 2)
        sink(b, -39.55, cy - 2.2, 0, 90)
        chair(b, -34.0, cy + 1.6, 0, 200, "blue", fallen=k == 2)
        pc(b, -39.55, cy + 2.2, .9, 90, on=k == 0)
        plate(b, "EXAM %d" % (k + 1), (-30.86, cy, 2.75), -90 + 180, 1.8, .42)
        light("Clinic exam %d" % (k + 1), (-36.0, cy, 3.2), 12, 8)
        troffer(b, -36.0, cy, SMALL_CEIL, lit=k != 2)
    # Reception counter facing the entrance, waiting rows along the window walls.
    counter(b, (-29.0, 88.0, 0), (-28.0, 97.0, 1.08), "blue", "whitewood")
    pc(b, -28.5, 90.4, 1.08, -90, on=True); pc(b, -28.5, 94.4, 1.08, -90)
    chair(b, -29.9, 90.9, 0, 90, "blue")
    plate(b, "CHECK IN", (-27.96, 92.5, .62), 90, 2.0, .36)
    for y, yaw in ((83.0, 180), (102.0, 0)):
        seat_row(b, -21.6, y, 0, yaw, 6, "teal")
    seat_row(b, -25.6, 86.2, 0, 180, 3, "teal")
    seat_row(b, -25.6, 98.8, 0, 0, 3, "teal")
    plant(b, -16.9, 83.0, 0); plant(b, -16.9, 102.0, 0); plant(b, -29.4, 83.2, 0)
    b.box((-25.2, 89.3, 0), (-23.8, 90.6, .42), "whitewood", skip="b")
    collide(b, (-25.2, 89.3, 0), (-23.8, 90.6, .42))
    papers(b, -25.2, -23.8, 89.3, 90.6, .42, 3)
    water_cooler(b, -29.6, 101.9, 0, 0)
    chair(b, -22.6, 98.4, 0, 40, "teal", fallen=True)
    papers(b, -30, -16, 84, 101, 0, 14)
    board(b, -14.15, 85.6, 1.7, -90, 2.0, 1.0)
    for x, y in ((-22.0, 87.0), (-22.0, 98.0)):
        light("Clinic waiting", (x, y, 3.2), 14, 10); troffer(b, x, y, SMALL_CEIL)


def pharmacy(b):
    X0, X1, Y0, Y1 = -40, -14, 57, 76
    skin_floor(b, X0 + .13, X1 - .13, Y0 + .13, Y1 - .13, 0, "greypanel")
    threshold(b, "x", X1, 64.5, 68.5, 0)
    small_dado(b, X0, X1, Y0, Y1, (64.5, 68.5), [(.12, "charcoal"), (1.05, "redband")])
    # Retail aisles either side of the entrance lane.
    for y in (59.7, 61.9): gondola(b, -24.5, y, 0, 0, 4.2)
    for y in (71.0, 73.2): gondola(b, -24.5, y, 0, 0, 4.2)
    for x in (-19.0, -16.8): stocked(b, x, 57.15, 0, 180, 2.1)
    for x in (-19.0, -16.8): stocked(b, x, 75.85, 0, 0, 2.1)
    counter(b, (-20.2, 69.9, 0), (-16.2, 70.8, 1.0), "red", "whitewood")
    pc(b, -18.0, 70.35, 1.0, 180, on=False)
    plate(b, "CHECKOUT", (-18.2, 69.86, .6), 0, 2.2, .36)
    box_pile(b, -21.0, 63.2, 0, 2); box_pile(b, -27.6, 69.6, 0, 1)
    # Pharmacist counter with a pass-through near the north wall; dispensary behind.
    counter(b, (-29.6, 58.4, 0), (-28.6, 72.4, 1.1), "whitepanel", "greypanel")
    for y in (61.0, 66.0, 70.5): pc(b, -29.1, y, 1.1, -90, on=y == 66.0)
    plate(b, "PRESCRIPTIONS", (-29.1, 65.0, 2.6), 90, 3.4, .5, "red", hang=SMALL_CEIL)
    plate(b, "PICK UP", (-28.56, 70.5, .62), 90, 1.6, .36)
    for y in (58.5, 60.7, 62.9, 65.1, 67.3, 69.5, 71.7): stocked(b, -39.87, y, 0, 90, 2.1)
    for x in (-35.4, -32.5):
        for y0 in (59.5, 65.0):
            place(b, x, y0 + 2.2, 0, 90); shelf(b, -2.2, -.02, 0, 4.4, rng); b.pop()
            place(b, x, y0 + 2.2, 0, -90); shelf(b, -2.2, -.02, 0, 4.4, rng); b.pop()
            BOXES.append({"c": U((x, y0 + 2.2, 1.0)), "s": [.9, 2.0, 4.4], "yaw": 0, "room": b.name})
    cabinet(b, -38.0, 75.55, 0, 0, 1.8, .6, 1.8, "whitepanel", 5)
    b.box((-31.0, 74.6, 0), (-30.1, 75.7, 1.6), "whitepanel", fit=True, skip="b")
    collide(b, (-31.0, 74.6, 0), (-30.1, 75.7, 1.6), check=False)
    box_pile(b, -30.6, 57.8, 0, 2)
    papers(b, -38, -30, 58, 74, 0, 10); papers(b, -27, -15, 58, 75, 0, 8)
    for x, y in ((-21.0, 61.5), (-21.0, 71.5)): troffer(b, x, y, SMALL_CEIL)
    light("Pharmacy shop", (-21.0, 66.5, 3.2), 15, 11)
    light("Dispensary", (-34.0, 66.5, 3.2), 12, 9); troffer(b, -34.0, 66.5, SMALL_CEIL)
    troffer(b, -34.0, 60.0, SMALL_CEIL, lit=False); troffer(b, -34.0, 73.0, SMALL_CEIL, lit=False)


def laundry(b):
    X0, X1, Y0, Y1 = -40, -16, 126, 146
    skin_floor(b, X0 + .13, X1 - .13, Y0 + .13, Y1 - .13, 0, "concrete")
    threshold(b, "x", X1, 134, 138, 0, "concrete")
    small_dado(b, X0, X1, Y0, Y1, (134, 138), [(1.3, "lightconc")])
    # Washers on the back wall, dryer stacks on the north window wall.
    for y in (132.55, 133.95, 135.35, 136.75, 139.8, 141.2, 142.6, 144.0):
        washer(b, -39.25, y, 0, 90)
    for x in (-36.5, -35.0, -33.5, -24.5, -23.0, -21.5):
        washer(b, x, 145.3, 0, 0, h=.9, w=1.0, d=.85)
        washer(b, x, 145.3, .9, 0, h=.9, w=1.0, d=.85)
    pipes(b, (-39.6, 127.5), (-39.6, 144.5), 2.9, .07, "galv", 2, .3)
    pipes(b, (-38.5, 145.6), (-20.0, 145.6), 3.2, .1, "rust", 1)
    # Boiler and water heater in the south-west corner.
    b.cyl((-37.2, 127.6, 0), (-37.2, 127.6, 2.4), .75, 12, "galv", cap_b=True)
    b.cyl((-37.2, 127.6, 2.4), (-37.2, 127.6, 3.6), .12, 6, "rust")
    b.box((-38.0, 127.0, 0), (-36.4, 128.2, .15), "darkmetal", skip="b")
    collide(b, (-37.95, 126.85, 0), (-36.45, 128.35, 2.4), check=False)
    b.cyl((-35.3, 127.0, 0), (-35.3, 127.0, 1.6), .32, 10, "whitepanel", cap_b=True)
    BOXES.append({"c": U((-35.3, 127.0, .8)), "s": [.64, 1.6, .64], "yaw": 0, "room": b.name})
    # Folding tables, carts and linen racks.
    for y in (131.2, 140.8): folding_table(b, -28.5, y, 0, 0, 4.0, 1.1)
    for x in (-33.4, -30.9, -26.0, -23.5): linen_shelf(b, x, 126.15, 0, 180, 2.3)
    laundry_cart(b, -31.5, 136.9, 0, 20); laundry_cart(b, -34.8, 134.2, 0, -10, full=False)
    laundry_cart(b, -23.2, 133.0, 0, 110, tipped=True); laundry_cart(b, -20.6, 143.0, 0, 5)
    # Ironing press.
    solid(b, (-21.2, 128.0, 0), (-18.6, 129.0, .95), "whitepanel", top="greypanel", fit=True)
    b.box((-21.0, 128.2, .95), (-18.8, 128.8, 1.05), "red", fit=True, skip="b")
    b.box((-21.1, 128.75, 1.05), (-18.7, 128.95, 1.4), "whitepanel", fit=True)
    # Hanging sheets on a rail in the middle of the room.
    b.beam((-34.0, 138.2, 2.45), (-27.5, 138.2, 2.45), .04, .04, "galv", fit=True)
    for x in (-34.0, -27.5): b.beam((x, 138.2, 2.45), (x, 138.2, SMALL_CEIL), .03, .03, "galv", fit=True, caps=False)
    for i in range(5):
        x = -33.6 + i * 1.3
        if i != 2: curtain(b, (x, 138.2), (x + 1.0, 138.2), .9, 2.4, "whitepanel", .5, rail=False)
    plate(b, "WASH", (-40 + WALL_T + .02, 136.0, 2.4), 90, 1.4, .4)
    plate(b, "DRY", (-29.0, 146 - WALL_T - .02, 3.0), 0, 1.2, .4)
    papers(b, -38, -18, 128, 145, 0, 6)
    for x, y in ((-34.0, 131.0), (-34.0, 141.0), (-22.0, 136.0)):
        light("Laundry", (x, y, 3.2), 13, 10); troffer(b, x, y, SMALL_CEIL)


PARTS = [("H0_Corridors", lambda b: corridor_level(b, 0)), ("H0_ER", er), ("H0_Reception", reception), ("H0_East", east_ground),
         ("H0_Supplies", supplies), ("H1_Corridors", lambda b: corridor_level(b, 4)), ("H1_Surgery", surgery), ("H1_Nurses", nurses),
         ("H1_Patients", patient_rooms), ("H1_Recovery", recovery), ("Clinic", clinic), ("Pharmacy", pharmacy), ("Laundry", laundry)]


def main():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for n in ("HospitalInteriors", "HospitalInteriors_Review"):
        c = bpy.data.collections.get(n) or bpy.data.collections.new(n)
        if c.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(c)
    for me in list(bpy.data.meshes):
        if me.users == 0: bpy.data.meshes.remove(me)
    coll = bpy.data.collections["HospitalInteriors"]
    mats = [material("Quackville Town atlas", "Assets/Models/Quackville/Quackville_Town_Diffuse.jpg"),
            material("Quackville Industrial atlas", "Assets/Models/Quackville/Quackville_Industrial_Diffuse.jpg"),
            material("Subway lamp lens", emission=(1, .97, .9, 1), strength=5, color=(1, 1, 1, 1)),
            material("Subway night glow", emission=(.22, .42, 1, 1), strength=2.5, color=(.1, .2, .5, 1)),
            material("Subway tunnel void"),
            material("Subway warm bulb", emission=(1, .68, .32, 1), strength=6, color=(1, .8, .5, 1))]
    del LIGHTS[:], LABELS[:], BOXES[:], FIXTURES[:]
    total = 0
    planar_faces = PlanarFaces()
    for name, fn in PARTS:
        b = Builder(name)
        fn(b)
        b.trim_overlaps(planar_faces)
        total += sum(len(f.verts) - 2 for f in b.bm.faces)
        b.finish(coll, mats)
    print(f"TOTAL triangles: {total}; lights {len(LIGHTS)}, labels {len(LABELS)}, physics boxes {len(BOXES)}")
    print("Interior coplanar rectangles trimmed:",planar_faces.trimmed)
    with open(OUT_DIR + "interiors.json", "w") as f:
        json.dump({"lights": LIGHTS, "labels": LABELS, "boxes": BOXES, "fixtures": FIXTURES}, f, indent=1)


def export_fbx():
    objs = list(bpy.data.collections["HospitalInteriors"].objects)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    # Same axis convention as the campus exterior; Unity turns the instance 180 degrees about Y.
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False, path_mode="STRIP", use_custom_props=False)


main()
_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--export" in _args: export_fbx()
if bpy.app.background:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "HospitalInteriors.blend")
