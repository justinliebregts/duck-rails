# Subway terminal compound - slice 1 (original structure + sealed stairway).
# Run inside Blender: exec(open(r"X:/Work/duck-rails/ArtSource/SubwayTerminal/build_terminal.py").read())
# Blender axes: X across the hall, Y along it (stairs at -Y, platforms gate at +Y), Z up. Metres.
# All surfaces use the two existing Quackville atlases (Town 256x256, Industrial 256x128) at 16 px/m.
import bpy, bmesh, math, random
from mathutils import Vector as V, Matrix

ROOT = "X:/Work/duck-rails/"
PXM = 16.0
ATLAS = {"T": (256, 256), "I": (256, 128)}
MAT_INDEX = {"T": 0, "I": 1}
LENS, GLOW, VOID, WARM, MESH = 2, 3, 4, 5, 6
MESH_TEX = "Assets/DuckRails/Compound/SubwayTerminal/Textures/SubwayMeshFence.png"   # 32 px, one repeat per metre
TILES = {  # name: (atlas, x, y, size) with a top-left pixel origin
    "gravel": ("T", 64, 0, 64), "asphalt": ("T", 128, 0, 64), "plaster": ("T", 0, 64, 64),
    "redwood": ("T", 64, 64, 64), "timber": ("T", 192, 64, 64), "yellowrust": ("T", 0, 128, 64),
    "charcoal": ("T", 128, 128, 64), "concrete": ("T", 192, 128, 64), "olive": ("T", 0, 192, 64),
    "whitewood": ("T", 128, 192, 64), "redband": ("T", 192, 192, 64),
    "lightconc": ("I", 32, 0, 32), "brick": ("I", 64, 0, 32), "corr": ("I", 96, 0, 32),
    "whitepanel": ("I", 128, 0, 32), "louver": ("I", 224, 0, 32), "darkmetal": ("I", 0, 32, 32),
    "yellowworn": ("I", 32, 32, 32), "blueworn": ("I", 64, 32, 32), "redworn": ("I", 96, 32, 32),
    "crate": ("I", 0, 64, 32), "galv": ("I", 32, 64, 32), "teal": ("I", 64, 64, 32),
    "steel": ("I", 224, 64, 32), "panel": ("I", 32, 96, 32), "hazard": ("I", 64, 96, 32),
    "rust": ("I", 96, 96, 32), "greypanel": ("I", 128, 96, 32), "tactile": ("I", 160, 96, 32),
    "ribbed": ("I", 192, 96, 32), "water": ("T", 64, 192, 64), "glass": ("I", 128, 32, 32), "stripes": ("I", 224, 96, 32),
}
# Wall treatment from the concept: grey base, pale wall, red band, pale wall above.
BAND_LO, BAND_HI = 3.0, 3.45
STATION = [(0.45, "concrete"), (BAND_LO, "plaster"), (BAND_HI, "redband"), (99, "plaster")]

# Layout
HX, HY, H = 9.0, 12.0, 7.0          # hall half width, half length, ceiling
TX, TZ = 12.4, -1.1                 # outer tunnel wall, track bed level
ROWS = (-9.0, -3.0, 3.0, 9.0)       # pillar rows (Y)
PX = 4.2                            # pillar line (X)
BEAM_Z = 6.3
ST_Y0, RISE, RUN, STEPS = -9.6, 0.2, 0.36, 15
ST_W, CHEEK = 2.15, 2.6             # stair half width, cheek outer face
ST_TOP_Y = ST_Y0 - (STEPS - 1) * RUN
LAND_Z = STEPS * RISE
REC_Y, REC_H = -18.0, 6.2           # stair recess back wall and ceiling


def splits(a, b, cell):
    xs = [a]
    k = math.floor(a / cell) + 1
    while k * cell < b - 1e-5:
        if k * cell > a + 1e-5:
            xs.append(k * cell)
        k += 1
    xs.append(b)
    return xs


class Builder:
    def __init__(self, name):
        self.name, self.bm = name, bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.M, self.stack = Matrix.Identity(4), []

    def push(self, m):
        self.stack.append(self.M)
        self.M = self.M @ m

    def pop(self):
        self.M = self.stack.pop()

    def _face(self, pts, uvs, tile):
        atlas, x, y, size = TILES[tile]
        W, Hh = ATLAS[atlas]
        u0, u1 = (x + .5) / W, (x + size - .5) / W
        vt, vb = 1 - (y + .5) / Hh, 1 - (y + size - .5) / Hh
        f = self.bm.faces.new([self.bm.verts.new(self.M @ V(p)) for p in pts])
        f.material_index = MAT_INDEX[atlas]
        for l, (a, b) in zip(f.loops, uvs):
            a, b = min(max(a, 0), 1), min(max(b, 0), 1)
            l[self.uv].uv = (u0 + a * (u1 - u0), vb + b * (vt - vb))

    def flat(self, pts, mat):
        f = self.bm.faces.new([self.bm.verts.new(self.M @ V(p)) for p in pts])
        f.material_index = mat

    def mesh(self, p0, du, dv):
        """Wire-mesh rectangle: alpha-clipped tiling texture, one repeat per metre, drawn two-sided in Unity."""
        p0, du, dv = V(p0), V(du), V(dv)
        f = self.bm.faces.new([self.bm.verts.new(p) for p in (p0, p0 + du, p0 + du + dv, p0 + dv)])
        f.material_index = MESH
        w, h = du.length, dv.length
        for l, uv in zip(f.loops, ((0, 0), (w, 0), (w, h), (0, h))):
            l[self.uv].uv = uv

    def quad(self, p0, du, dv, tile, fit=False, swap=False):
        """Rectangle p0 + du + dv, normal du x dv. Metre-scaled UVs split on atlas cells unless fit."""
        p0, du, dv = V(p0), V(du), V(dv)
        w, h = du.length, dv.length
        if w < 1e-5 or h < 1e-5:
            return
        if fit:
            uvs = [(0, 0), (0, 1), (1, 1), (1, 0)] if swap else [(0, 0), (1, 0), (1, 1), (0, 1)]
            self._face([p0, p0 + du, p0 + du + dv, p0 + dv], uvs, tile)
            return
        uh, vh = du / w, dv / h
        cell = TILES[tile][3] / PXM
        s0, t0 = p0.dot(uh), p0.dot(vh)
        us, vs = splits(s0, s0 + w, cell), splits(t0, t0 + h, cell)
        for i in range(len(us) - 1):
            for j in range(len(vs) - 1):
                a0, a1, b0, b1 = us[i], us[i + 1], vs[j], vs[j + 1]
                ku, kv = math.floor((a0 + a1) / 2 / cell), math.floor((b0 + b1) / 2 / cell)
                pts, uvs = [], []
                for a, b in ((a0, b0), (a1, b0), (a1, b1), (a0, b1)):
                    pts.append(p0 + uh * (a - s0) + vh * (b - t0))
                    uv = ((a - ku * cell) / cell, (b - kv * cell) / cell)
                    uvs.append((uv[1], uv[0]) if swap else uv)
                self._face(pts, uvs, tile)

    def poly(self, pts, tile):
        """Planar polygon that fits inside one atlas cell; metre-scaled projection."""
        pts = [V(p) for p in pts]
        n = (pts[1] - pts[0]).cross(pts[2] - pts[0]).normalized()
        if abs(n.z) < 0.9:
            v = (V((0, 0, 1)) - n * n.z).normalized()
        else:
            v = V((0, 1, 0)) * (1 if n.z > 0 else -1)
        u = v.cross(n)
        cell = TILES[tile][3] / PXM
        c = sum(pts, V()) / len(pts)
        ku, kv = math.floor(c.dot(u) / cell), math.floor(c.dot(v) / cell)
        self._face(pts, [((p.dot(u) - ku * cell) / cell, (p.dot(v) - kv * cell) / cell) for p in pts], tile)

    def wall(self, a, b, z0, z1, tile):
        """Vertical face from (x,y) a to b; normal is (b-a) x up. tile may be a band scheme."""
        du = V((b[0] - a[0], b[1] - a[1], 0))
        bands = [(99, tile)] if isinstance(tile, str) else tile
        lo = z0
        for top, t in bands:
            hi = min(top, z1)
            if hi > lo + 1e-5:
                self.quad((a[0], a[1], lo), du, (0, 0, hi - lo), t)
                lo = hi
            if lo >= z1 - 1e-5:
                break

    def wx(self, x, y0, y1, z0, z1, tile, face):
        if face > 0: self.wall((x, y0), (x, y1), z0, z1, tile)
        else: self.wall((x, y1), (x, y0), z0, z1, tile)

    def wy(self, y, x0, x1, z0, z1, tile, face):
        if face > 0: self.wall((x1, y), (x0, y), z0, z1, tile)
        else: self.wall((x0, y), (x1, y), z0, z1, tile)

    def box(self, lo, hi, side, top=None, bottom=None, skip="", fit=False):
        """Axis-aligned box. skip holds any of x+ x- y+ y- t b."""
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        if fit:
            if "x+" not in skip: self.quad((x1, y0, z0), (0, y1 - y0, 0), (0, 0, z1 - z0), side, True)
            if "x-" not in skip: self.quad((x0, y1, z0), (0, y0 - y1, 0), (0, 0, z1 - z0), side, True)
            if "y+" not in skip: self.quad((x1, y1, z0), (x0 - x1, 0, 0), (0, 0, z1 - z0), side, True)
            if "y-" not in skip: self.quad((x0, y0, z0), (x1 - x0, 0, 0), (0, 0, z1 - z0), side, True)
        else:
            if "x+" not in skip: self.wx(x1, y0, y1, z0, z1, side, 1)
            if "x-" not in skip: self.wx(x0, y0, y1, z0, z1, side, -1)
            if "y+" not in skip: self.wy(y1, x0, x1, z0, z1, side, 1)
            if "y-" not in skip: self.wy(y0, x0, x1, z0, z1, side, -1)
        s = side if isinstance(side, str) else side[0][1]
        if "t" not in skip: self.quad((x0, y0, z1), (x1 - x0, 0, 0), (0, y1 - y0, 0), top or s, fit)
        if "b" not in skip: self.quad((x0, y1, z0), (x1 - x0, 0, 0), (0, y0 - y1, 0), bottom or s, fit)

    def beam(self, a, b, w, h, tile, up=(0, 0, 1), fit=False, caps=True, skip=""):
        """Box along a->b. w across (axis x up), h along up. Tile v runs along the length."""
        a, b = V(a), V(b)
        d = b - a
        x = d.normalized()
        side = x.cross(V(up)).normalized()
        upv = side.cross(x)
        sw, uh = side * w, upv * h
        if "t" not in skip: self.quad(a - sw / 2 + uh / 2, sw, d, tile, fit)
        if "b" not in skip: self.quad(a - sw / 2 - uh / 2, d, sw, tile, fit, swap=True)
        if "s+" not in skip: self.quad(a + sw / 2 - uh / 2, d, uh, tile, fit, swap=True)
        if "s-" not in skip: self.quad(b - sw / 2 - uh / 2, -d, uh, tile, fit, swap=True)
        if caps:
            self.quad(a - sw / 2 - uh / 2, sw, uh, tile, fit)
            self.quad(b + sw / 2 - uh / 2, -sw, uh, tile, fit)

    def cyl(self, a, b, r, n, tile, cap_a=False, cap_b=False):
        a, b = V(a), V(b)
        x = (b - a).normalized()
        e1 = x.cross(V((0, 0, 1)) if abs(x.z) < 0.9 else V((1, 0, 0))).normalized()
        e2 = x.cross(e1)
        L = (b - a).length
        cell = TILES[tile][3] / PXM
        uw = min(1.0, 2 * math.pi * r / cell)
        ring = [e1 * (r * math.cos(2 * math.pi * k / n)) + e2 * (r * math.sin(2 * math.pi * k / n)) for k in range(n + 1)]
        vs = splits(0, L, cell)
        for k in range(n):
            for j in range(len(vs) - 1):
                kv = math.floor((vs[j] + vs[j + 1]) / 2 / cell)
                v0, v1 = (vs[j] - kv * cell) / cell, (vs[j + 1] - kv * cell) / cell
                u0, u1 = k / n * uw, (k + 1) / n * uw
                self._face([a + x * vs[j] + ring[k], a + x * vs[j] + ring[k + 1], a + x * vs[j + 1] + ring[k + 1], a + x * vs[j + 1] + ring[k]],
                           [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], tile)
        cw = min(1.0, 2 * r / cell)
        for c, flip, on in ((a, True, cap_a), (b, False, cap_b)):
            if not on: continue
            idx = list(range(n))
            if flip: idx.reverse()
            self._face([c + ring[k] for k in idx],
                       [(.5 + .5 * cw * math.cos(2 * math.pi * k / n), .5 + .5 * cw * math.sin(2 * math.pi * k / n)) for k in idx], tile)

    def finish(self, coll, mats):
        me = bpy.data.meshes.new(self.name)
        self.bm.normal_update()
        self.bm.to_mesh(me)
        tris = sum(len(f.verts) - 2 for f in self.bm.faces)
        self.bm.free()
        for m in mats: me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        coll.objects.link(ob)
        print(f"{self.name}: {len(me.polygons)} faces, {tris} tris, {len(me.vertices)} verts")
        return ob


def zn(y):
    """Stair nosing line height at y."""
    return max(0.0, min(LAND_Z, (ST_Y0 - y) / RUN * RISE))


def build_shell(b):
    # Concourse floor, platform-edge warning strips and coping
    b.quad((-8.4, -HY, 0), (16.8, 0, 0), (0, 2 * HY, 0), "concrete")
    for s in (-1, 1):
        b.quad((8.4 if s > 0 else -8.85, -HY, 0), (.45, 0, 0), (0, 2 * HY, 0), "tactile")
        b.quad((8.85 if s > 0 else -9.0, -HY, 0), (.15, 0, 0), (0, 2 * HY, 0), "lightconc")
        b.wx(s * HX, -HY, HY, TZ, 0, "concrete", s)                         # platform face
        b.quad((HX if s > 0 else -TX, -HY, TZ), (TX - HX, 0, 0), (0, 2 * HY, 0), "gravel")  # ballast
        b.wx(s * TX, -HY, HY, TZ, H, STATION, -s)                           # far tunnel wall
        for r in ROWS:                                                     # pilasters on the tunnel wall
            x0, x1 = (TX - .3, TX) if s > 0 else (-TX, -TX + .3)
            b.box((x0, r - .45, TZ), (x1, r + .45, BEAM_Z), "concrete", skip="tb" + ("x+" if s > 0 else "x-"))
        # rails and sleepers
        cx = s * 10.7
        for g in (-.72, .72):
            b.beam((cx + g, -HY - 5, TZ + .17), (cx + g, HY + 5, TZ + .17), .08, .14, "steel", fit=True, caps=False, skip="b")
        y = -HY + .75
        while y < HY:
            b.beam((cx - 1.1, y, TZ + .05), (cx + 1.1, y, TZ + .05), .24, .1, "timber", up=(0, 0, 1), caps=False, skip="b")
            y += 1.5
    # Ceiling slab
    b.quad((-TX, HY, H), (2 * TX, 0, 0), (0, -2 * HY, 0), "charcoal")
    # End walls with track portals
    for e in (-1, 1):
        y, f = e * HY, -e
        if e < 0:
            b.wy(y, -HX, -CHEEK, 0, H, STATION, f)
            b.wy(y, CHEEK, HX, 0, H, STATION, f)
            b.wy(y, -CHEEK, CHEEK, REC_H, H, STATION, f)
        else:
            b.wy(y, -HX, HX, 0, H, STATION, f)
        for s in (-1, 1):
            xa, xb = (HX, TX) if s > 0 else (-TX, -HX)
            pa, pb = xa + (.4 if s > 0 else .4), xb - .4
            b.wy(y, xa, pa, TZ, 3.4, STATION, f)
            b.wy(y, pb, xb, TZ, 3.4, STATION, f)
            b.wy(y, xa, xb, 3.4, H, STATION, f)
            yo = e * (HY + 5)
            y0, y1 = min(y, yo), max(y, yo)
            b.wx(pa, y0, y1, TZ, 3.4, "charcoal", 1)
            b.wx(pb, y0, y1, TZ, 3.4, "charcoal", -1)
            b.quad((pa, y1, 3.4), (pb - pa, 0, 0), (0, y0 - y1, 0), "charcoal")
            b.quad((pa, y0, TZ), (pb - pa, 0, 0), (0, y1 - y0, 0), "gravel")
            pts = [(pb, yo, TZ), (pa, yo, TZ), (pa, yo, 3.4), (pb, yo, 3.4)]
            b.flat(pts if f > 0 else pts[::-1], VOID)
    # Pillars: wider base, banded shaft, capital under the crossbeam
    for r in ROWS:
        for s in (-1, 1):
            x = s * PX
            b.box((x - .56, r - .56, 0), (x + .56, r + .56, .45), "concrete", skip="b")
            b.box((x - .5, r - .5, .45), (x + .5, r + .5, 6.02), STATION, skip="tb")
            b.box((x - .64, r - .64, 6.02), (x + .64, r + .64, BEAM_Z), "concrete", skip="t")
    # Crossbeams, longitudinal beams and platform-edge beams
    for r in ROWS:
        b.box((-TX, r - .5, BEAM_Z), (TX, r + .5, H), "concrete", skip="tx+x-")
    for x, w in ((-PX, .4), (PX, .4), (-HX, .3), (HX, .3)):
        b.box((x - w, -HY, 6.5), (x + w, HY, H), "concrete", skip="ty+y-")


def build_stairs(b):
    # Steps and landing
    for i in range(STEPS):
        yf, z0 = ST_Y0 - i * RUN, i * RISE
        b.wy(yf, -ST_W, ST_W, z0, z0 + RISE, "charcoal", 1)
        if i < STEPS - 1:
            b.quad((-ST_W, yf - RUN, z0 + RISE), (2 * ST_W, 0, 0), (0, RUN, 0), "asphalt")
    b.quad((-CHEEK, REC_Y, LAND_Z), (2 * CHEEK, 0, 0), (0, ST_TOP_Y - .36 - REC_Y, 0), "asphalt")
    b.quad((-ST_W, ST_TOP_Y - .36, LAND_Z), (2 * ST_W, 0, 0), (0, .36, 0), "asphalt")
    # Recess walls, ceiling, back wall and the night glow behind the barricade
    for s in (-1, 1):
        b.wx(s * CHEEK, REC_Y, -HY, 0, REC_H, STATION, -s)
    b.quad((-CHEEK, -HY, REC_H), (2 * CHEEK, 0, 0), (0, REC_Y + HY, 0), "charcoal")
    b.wy(REC_Y, -CHEEK, CHEEK, LAND_Z, REC_H, "charcoal", 1)
    b.flat([(2.2, REC_Y + .05, 4.6), (-2.2, REC_Y + .05, 4.6), (-2.2, REC_Y + .05, 6.05), (2.2, REC_Y + .05, 6.05)], GLOW)
    # Cheek walls with a low newel, capped, and handrails on posts
    CH = 0.55
    ye = ST_TOP_Y - .36
    for s in (-1, 1):
        xi, xo = s * ST_W, s * CHEEK
        xl, xh = min(xi, xo), max(xi, xo)
        for ya, yb in ((ST_Y0, -HY), (-HY, ye)):
            za, zb = CH + zn(ya), CH + zn(yb)
            shape = [(ya, 0), (ya, za), (yb, zb), (yb, 0)]           # y,z going toward -Y
            if ya == ST_Y0:
                shape = [(ST_Y0 + .3, 0), (ST_Y0 + .3, CH)] + shape[1:]
            inner = [(xi, y, z) for y, z in shape]
            b.poly(inner[::-1] if s > 0 else inner, "concrete")
            if ya == ST_Y0:
                outer = [(xo, y, z) for y, z in shape]
                b.poly(outer if s > 0 else outer[::-1], "concrete")
            b.poly([(xl, ya, za), (xl, yb, zb), (xh, yb, zb), (xh, ya, za)], "charcoal")
        b.quad((xl, ST_Y0, CH), (xh - xl, 0, 0), (0, .3, 0), "charcoal")
        b.wy(ST_Y0 + .3, xl, xh, 0, CH, "concrete", 1)
        b.wy(ye, xl, xh, LAND_Z, LAND_Z + CH, "concrete", -1)
        xr = s * (ST_W + CHEEK) / 2
        p = [(xr, ST_Y0 + .22, 1.0), (xr, ST_Y0, 1.0), (xr, ye, 1.0 + LAND_Z), (xr, ye - .45, 1.0 + LAND_Z)]
        for a, c in zip(p, p[1:]):
            b.beam(a, c, .07, .07, "darkmetal", fit=True, caps=False)
        y = ST_Y0 + .18
        while y > ye:
            b.beam((xr, y, CH + zn(y)), (xr, y, 1.0 + zn(y)), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
            y -= 1.26
        b.beam((xr, ye + .06, CH + LAND_Z), (xr, ye + .06, 1.0 + LAND_Z), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    # Barricade on the landing: timber frame, salvaged sheets, diagonal planks, stacked crates
    yb = -15.75
    for x in (-2.35, 0.05, 2.35):
        b.beam((x, yb, LAND_Z), (x, yb, REC_H), .16, .16, "timber", up=(0, 1, 0), caps=False)
    for z in (4.25, 5.55):
        b.beam((-CHEEK, yb, z), (CHEEK, yb, z), .1, .16, "timber", up=(0, 0, 1), caps=False)
    sheets = [(-1.85, 3.0, 1.35, 2.25, "corr", 3, 4), (-.55, 3.0, 1.2, 1.95, "rust", -4, 6), (.8, 3.02, 1.3, 2.4, "teal", 2, 3),
              (2.0, 3.0, 1.1, 2.1, "corr", -3, 5), (-1.25, 4.95, 1.55, 1.2, "galv", -7, 2), (.25, 4.85, 1.3, 1.3, "corr", 6, 5),
              (1.65, 5.0, 1.45, 1.15, "rust", -2, 3)]
    for i, (x, z, w, h, t, roll, lean) in enumerate(sheets):
        b.push(Matrix.Translation((x, yb + .1 + .03 * (i % 3), z)) @ Matrix.Rotation(math.radians(roll), 4, "Y") @ Matrix.Rotation(math.radians(-lean), 4, "X"))
        b.quad((w / 2, 0, 0), (-w, 0, 0), (0, 0, h), t, fit=True)
        b.pop()
    for (x0, z0, x1, z1, t) in ((-2.45, 3.45, 1.7, 4.55, "timber"), (-1.1, 5.75, 2.45, 4.35, "whitewood"), (-2.4, 5.95, .5, 5.3, "timber")):
        b.beam((x0, yb + .22, z0), (x1, yb + .22, z1), .28, .05, t, up=(0, 1, 0))
    b.beam((-2.2, yb + .24, 3.9), (-.6, yb + .24, 3.1), .22, .05, "redwood", up=(0, 1, 0))
    crates = [(-1.95, -15.2, 0, .8, .8, .75, "crate", 8), (-1.05, -15.25, 0, .7, .5, .42, "olive", -6), (-1.9, -15.22, .75, .6, .6, .5, "olive", -12),
              (1.75, -15.2, 0, .85, .8, .8, "crate", -5), (.9, -15.2, 0, .6, .6, .55, "galv", 14), (1.8, -15.2, .8, .55, .5, .45, "crate", 20),
              (.15, -15.3, 0, .75, .45, .4, "olive", 3)]
    for x, y, z, sx, sy, sz, t, yaw in crates:
        b.push(Matrix.Translation((x, y, LAND_Z + z)) @ Matrix.Rotation(math.radians(yaw), 4, "Z"))
        b.box((-sx / 2, -sy / 2, 0), (sx / 2, sy / 2, sz), t, skip="b", fit=TILES[t][0] == "I")
        b.pop()


def pallet(b):
    for x in (-.55, 0, .55):
        b.beam((x, -.5, .05), (x, .5, .05), .1, .1, "timber")
    for y in (-.43, -.215, 0, .215, .43):
        b.beam((-.6, y, .1125), (.6, y, .1125), .14, .025, "timber", up=(0, 0, 1))


def crate(b, x, y, z, sx, sy, sz, tile, yaw=0):
    b.push(Matrix.Translation((x, y, z)) @ Matrix.Rotation(math.radians(yaw), 4, "Z"))
    b.box((-sx / 2, -sy / 2, 0), (sx / 2, sy / 2, sz), tile, skip="b", fit=TILES[tile][0] == "I")
    b.pop()


LAMPS = []


def build_dressing(b):
    yw = -HY
    for s in (-1, 1):
        # Service door, vent above, junction box and conduit on the stair end wall
        x = s * 6.2
        b.box((x - .72, yw, 0), (x + .72, yw + .07, 2.27), "darkmetal", skip="by-")
        b.quad((x + .6, yw + .1, .02), (-1.2, 0, 0), (0, 0, 2.13), "greypanel", fit=True)
        b.box((x - .6, yw + .07, .02), (x + .6, yw + .1, 2.15), "darkmetal", skip="by-y+")
        b.quad((x + .38, yw + .105, .22), (-.76, 0, 0), (0, 0, .4), "louver", fit=True)
        b.box((x + s * .42 - .04, yw + .1, 1.0), (x + s * .42 + .04, yw + .16, 1.16), "steel", skip="y-", fit=True)
        b.box((x - .58, yw, 2.45), (x + .58, yw + .06, 2.9), "darkmetal", skip="y-y+")
        b.quad((x + .58, yw + .06, 2.45), (-1.16, 0, 0), (0, 0, .45), "louver", fit=True)
        xj = s * 4.55
        b.box((xj - .22, yw, 1.15), (xj + .22, yw + .15, 1.8), "galv", skip="y-", fit=True)
        b.beam((xj, yw + .04, 1.8), (xj, yw + .04, 3.95), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
        b.beam((s * 2.65, yw + .04, 3.95), (s * 8.95, yw + .04, 3.95), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
        # Control box and conduit on the stair-side pillar, work lamp on a bracket
        px, py = s * PX, ROWS[0]
        b.box((px - .26, py + .5, 1.1), (px + .26, py + .64, 1.78), "galv", skip="y-", fit=True)
        b.quad((px + .22, py + .645, 1.14), (-.44, 0, 0), (0, 0, .6), "panel", fit=True)
        b.beam((px + s * .2, py + .54, 1.78), (px + s * .2, py + .54, 4.5), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
        xin = px - s * .5
        pos = V((xin - s * .5, py + .25, 4.45))
        b.beam((xin, py + .25, 4.55), pos + V((0, 0, .1)), .07, .07, "darkmetal", up=(0, 1, 0), fit=True)
        aim = (V((-s * 1.0, 3.2, 0)) - pos).normalized()
        xa = aim.cross(V((0, 0, 1))).normalized()
        za = xa.cross(aim)
        m = Matrix((xa, aim, za)).transposed().to_4x4()
        m.translation = pos
        b.push(m)
        b.box((-.21, -.16, -.16), (.21, .2, .16), "galv", fit=True, skip="y+")
        b.box((-.26, .2, -.21), (.26, .26, .21), "galv", fit=True, skip="y-y+")
        b.quad((.26, .26, -.21), (-.52, 0, 0), (0, 0, .42), "darkmetal", fit=True)
        b.flat([(.19, .262, -.14), (-.19, .262, -.14), (-.19, .262, .14), (.19, .262, .14)], LENS)
        b.pop()
        LAMPS.append((pos + aim * .3, aim))
    # Twin ceiling pipes with collars and hangers, rising into the slab before the stair portal
    for x in (-.3, .3):
        b.cyl((x, HY, 5.92), (x, -11.2, 5.92), .1, 8, "redworn")
        b.cyl((x, -11.2, 5.82), (x, -11.2, H), .1, 8, "redworn", cap_a=True)
        for y in (-7.5, -1.5, 4.5, 10.5):
            b.cyl((x, y - .07, 5.92), (x, y + .07, 5.92), .135, 8, "darkmetal", True, True)
    for r in ROWS:
        b.beam((-.5, r, 5.78), (.5, r, 5.78), .06, .04, "darkmetal", fit=True)
        for x in (-.47, .47):
            b.beam((x, r, 5.8), (x, r, BEAM_Z), .04, .04, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    # Props, kept asymmetric: left of the stairs
    b.cyl((-7.7, -11.2, 0), (-7.7, -11.2, .9), .3, 10, "teal", cap_b=True)
    crate(b, -6.75, -11.45, 0, .8, .5, .42, "olive", 4)
    crate(b, -6.8, -11.45, .42, .7, .45, .38, "olive", -8)
    b.push(Matrix.Translation((-8.2, -11.86, .5)) @ Matrix.Rotation(math.radians(-78), 4, "X")); pallet(b); b.pop()
    crate(b, -3.25, -11.45, 0, .8, .7, .7, "crate", 6)
    b.push(Matrix.Translation((-3.3, -10.55, .5)) @ Matrix.Rotation(math.radians(90), 4, "Z") @ Matrix.Rotation(math.radians(-80), 4, "X")); pallet(b); b.pop()
    # Right of the stairs
    b.push(Matrix.Translation((2.74, -10.6, .5)) @ Matrix.Rotation(math.radians(-90), 4, "Z") @ Matrix.Rotation(math.radians(-76), 4, "X")); pallet(b); b.pop()
    crate(b, 3.35, -11.5, 0, .7, .55, .5, "galv", -10)
    crate(b, 7.6, -11.4, 0, .9, .8, .8, "crate", -4)
    crate(b, 7.55, -11.4, .8, .6, .55, .5, "olive", 15)
    b.box((2.02, ST_Y0 + .3, .25), (2.5, ST_Y0 + .42, .95), "galv", skip="y-", fit=True)


FENCE_TILES = ["corr", "rust", "teal", "corr", "galv", "timber", "whitewood", "corr", "redwood", "blueworn", "ribbed", "rust"]


def sheet(b, p, n, w, h, tile, roll=0, lean=0, double=False):
    """Salvaged panel standing at p, facing horizontal direction n."""
    b.push(Matrix.Translation(p) @ Matrix.Rotation(math.atan2(-n[0], n[1]), 4, "Z") @ Matrix.Rotation(math.radians(roll), 4, "Y") @ Matrix.Rotation(math.radians(-lean), 4, "X"))
    b.quad((w / 2, 0, 0), (-w, 0, 0), (0, 0, h), tile, fit=True)
    if double: b.quad((-w / 2, -.04, 0), (w, 0, 0), (0, 0, h), tile, fit=True)
    b.pop()


def fence(b, a, c, rng, h=2.6, double=False, slit_every=0):
    """Improvised barricade from a to c (x,y); faces (c-a) x up. Posts and rails sit behind."""
    a, c = V((a[0], a[1], 0)), V((c[0], c[1], 0))
    d = c - a
    L = d.length
    x = d / L
    n = V((x.y, -x.x, 0))
    count = max(1, round(L / 1.35))
    pw = L / count
    for i in range(count):
        gap = .17 if slit_every and i % slit_every == slit_every - 1 else .03
        mid = a + x * (pw * (i + .5))
        sheet(b, mid + n * rng.uniform(0, .04), n, pw - gap, h + rng.uniform(-.25, .2), rng.choice(FENCE_TILES), rng.uniform(-2.5, 2.5), rng.uniform(0, 2), double)
        p = mid - n * .1
        b.beam(p, p + V((0, 0, h + .1)), .12, .12, "timber", up=tuple(n), caps=False)
    for z in (.6, h - .5):
        b.beam(a - n * .07 + V((0, 0, z)), c - n * .07 + V((0, 0, z)), .05, .12, "timber", caps=False)


def lamp(b, attach, out, target):
    """Work lamp on a short bracket, aimed at target. Records a review spot light."""
    attach, out = V(attach), V(out)
    pos = attach + out * .5 - V((0, 0, .1))
    b.beam(attach, pos + V((0, 0, .1)), .07, .07, "darkmetal", up=(0, 0, 1) if abs(out.z) < .5 else (0, 1, 0), fit=True)
    aim = (V(target) - pos).normalized()
    xa = aim.cross(V((0, 0, 1))).normalized()
    m = Matrix((xa, aim, xa.cross(aim))).transposed().to_4x4()
    m.translation = pos
    b.push(m)
    b.box((-.21, -.16, -.16), (.21, .2, .16), "galv", fit=True, skip="y+")
    b.box((-.26, .2, -.21), (.26, .26, .21), "galv", fit=True, skip="y-y+")
    b.quad((.26, .26, -.21), (-.52, 0, 0), (0, 0, .42), "darkmetal", fit=True)
    b.flat([(.19, .262, -.14), (-.19, .262, -.14), (-.19, .262, .14), (.19, .262, .14)], LENS)
    b.pop()
    LAMPS.append((pos + aim * .3, aim))


def bulb(b, x, y, z, top):
    b.beam((x, y, z + .08), (x, y, top), .025, .025, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    s = .08
    for pts in ([(x + s, y - s, z - s), (x + s, y + s, z - s), (x + s, y + s, z + s), (x + s, y - s, z + s)],
                [(x - s, y + s, z - s), (x - s, y - s, z - s), (x - s, y - s, z + s), (x - s, y + s, z + s)],
                [(x + s, y + s, z - s), (x - s, y + s, z - s), (x - s, y + s, z + s), (x + s, y + s, z + s)],
                [(x - s, y - s, z - s), (x + s, y - s, z - s), (x + s, y - s, z + s), (x - s, y - s, z + s)],
                [(x - s, y + s, z - s), (x + s, y + s, z - s), (x + s, y - s, z - s), (x - s, y - s, z - s)]):
        b.flat(pts, WARM)


def canopy(b, x0, x1, y0, y1, z0, z1, n, tile="corr"):
    """Sloped salvaged roof sheets, z0 at x0 to z1 at x1, visible from both sides."""
    w = (y1 - y0) / n
    for i in range(n):
        ya, dz = y0 + i * w, .03 * (i % 2)
        b.quad((x0, ya + w - .03, z0 + dz), (x1 - x0, 0, z1 - z0), (0, -(w - .03), 0), tile, fit=True)
        b.quad((x0, ya, z0 + dz + .02), (x1 - x0, 0, z1 - z0), (0, w - .03, 0), tile, fit=True)


def signboard(b, lo, hi):
    b.box(lo, hi, "olive")
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    along_y = (y1 - y0) > (x1 - x0)
    for z in (z0 - .04, z1 + .04):
        if along_y: b.beam(((x0 + x1) / 2, y0 - .06, z), ((x0 + x1) / 2, y1 + .06, z), (x1 - x0) + .06, .09, "timber")
        else: b.beam((x0 - .06, (y0 + y1) / 2, z), (x1 + .06, (y0 + y1) / 2, z), (y1 - y0) + .06, .09, "timber")


def build_fountain(b):
    R, RW = 1.6, .38
    heights = [[.4, .4, .22, .4], [.4, .15, 0, .3], [.4, .4, .4, .4], [.28, .4, .4, .34]]
    seg = (2 * R - RW) / 4
    for k in range(4):
        b.push(Matrix.Rotation(math.radians(90 * k), 4, "Z"))
        for j, h in enumerate(heights[k]):
            if h: b.box((-R + j * seg, -R, 0), (-R + (j + 1) * seg, -R + RW, h), "plaster", top="concrete", skip="b")
        b.pop()
    i = R - RW
    b.quad((-i, -i, .03), (2 * i, 0, 0), (0, 2 * i, 0), "charcoal")
    b.quad((-i, -i, .13), (2 * i, 0, 0), (0, 2 * i, 0), "water")
    crate(b, .1, -.05, 0, .55, .55, .34, "concrete", 12)
    for x, y, s, yaw in ((1.9, -.5, .3, 25), (2.05, -.15, .2, -40), (.5, -1.25, .22, 60), (-.9, .4, .2, 15)):
        crate(b, x, y, 0, s, s * .8, s * .6, "plaster", yaw)
    # Generator beside the fountain, cabled to the nearest pillar
    b.push(Matrix.Translation((2.95, -1.85, 0)) @ Matrix.Rotation(math.radians(24), 4, "Z"))
    b.box((-.42, -.28, .2), (.42, .28, .68), "yellowworn", fit=True)
    b.box((-.3, -.2, .68), (.3, .2, .8), "darkmetal", fit=True, skip="b")
    b.quad((-.2, -.285, .3), (.4, 0, 0), (0, 0, .3), "panel", fit=True)
    for x in (-.5, .5):
        for y in (-.34, .34):
            b.beam((x, y, 0.05), (x, y, .86), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    for y in (-.34, .34):
        b.beam((-.5, y, .86), (.5, y, .86), .05, .05, "darkmetal", fit=True, caps=False)
        b.beam((-.5, y, .16), (.5, y, .16), .05, .05, "darkmetal", fit=True, caps=False)
    for x in (-.5, .5):
        b.beam((x, -.34, .86), (x, .34, .86), .05, .05, "darkmetal", fit=True, caps=False)
    b.cyl((-.3, -.42, .14), (-.3, .42, .14), .14, 8, "darkmetal", True, True)
    b.pop()
    b.beam((3.3, -2.1, .02), (3.64, -2.6, .02), .05, .04, "darkmetal", fit=True, caps=False)


def build_barricades(b):
    rng = random.Random(11)
    fence(b, (8.9, HY), (8.9, -HY), rng, 2.6, slit_every=3)
    fence(b, (-8.9, -HY), (-8.9, HY), rng, 2.6, slit_every=3)


def build_info(b):
    rng = random.Random(23)
    fence(b, (-4.75, -2.6), (-8.85, -2.6), rng, 2.8, double=True)
    fence(b, (-8.85, 2.6), (-4.75, 2.6), rng, 2.8, double=True)
    signboard(b, (-4.84, -2.45, 2.75), (-4.72, 2.45, 3.5))
    for y in (-2.38, 2.38):
        b.beam((-4.78, y, 0), (-4.78, y, 3.55), .14, .14, "timber", up=(0, 1, 0), caps=False)
    canopy(b, -8.85, -6.5, -2.6, 2.6, 3.08, 2.92, 3)
    # Map board with a warm strip light, bench and pinned notices
    b.box((-8.8, -1.5, .95), (-8.72, 1.5, 2.75), "timber", skip="x-")
    b.quad((-8.715, -1.35, 1.1), (0, 2.7, 0), (0, 0, 1.5), "whitepanel", fit=True)
    b.box((-8.72, -.7, 2.85), (-8.5, .7, 2.93), "darkmetal", fit=True, skip="b")
    b.flat([(-8.72, .7, 2.85), (-8.5, .7, 2.85), (-8.5, -.7, 2.85), (-8.72, -.7, 2.85)], WARM)
    b.box((-8.3, -1.2, .4), (-7.85, 1.2, .47), "timber")
    for y in (-1.0, 1.0):
        b.box((-8.25, y - .05, 0), (-7.9, y + .05, .4), "timber", skip="tb")
    # Notices stay toward the front; the rear of each partition is kept clear for the Discord and rating stations.
    for x, z, t in ((-6.2, 1.15, "yellowworn"), (-5.6, 1.4, "whitepanel")):
        sheet(b, (x, -2.52, z), (0, 1), .5, .62, t, rng.uniform(-5, 5))
    # Ticket machine by the pillar, crates at the nook corners
    b.box((-4.62, -4.6, 0), (-3.95, -3.75, 1.75), "whitepanel", fit=True, skip="b")
    b.quad((-3.945, -4.6, 1.4), (0, .85, 0), (0, 0, .35), "blueworn", fit=True)
    b.quad((-3.945, -4.6, 0), (0, .85, 0), (0, 0, .35), "blueworn", fit=True)
    b.quad((-3.94, -4.42, .9), (0, .5, 0), (0, 0, .42), "glass", fit=True)
    b.quad((-3.94, -4.42, .5), (0, .5, 0), (0, 0, .3), "panel", fit=True)
    crate(b, -5.35, -2.0, 0, .7, .6, .5, "olive", 5)
    crate(b, -5.3, 2.0, 0, .8, .7, .7, "crate", -6)
    crate(b, -5.25, 2.0, .7, .55, .5, .45, "olive", 14)
    crate(b, -5.4, -3.35, 0, .75, .7, .7, "crate", 10)
    crate(b, -6.3, -3.3, 0, .7, .5, .42, "olive", -4)
    lamp(b, (-PX + .5, 3 - .2, 4.3), (1, 0, 0), (-.8, 1.0, 0))


def shelf(b, ox, oy, yaw, L, rng):
    b.push(Matrix.Translation((ox, oy, 0)) @ Matrix.Rotation(math.radians(yaw), 4, "Z"))
    for x in (0.03, L - .03):
        for y in (-.42, -.03):
            b.beam((x, y, 0), (x, y, 2.0), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    stock = ["crate", "olive", "yellowworn", "redworn", "blueworn", "galv", "teal", "whitepanel", "crate"]
    for z in (.15, .65, 1.15, 1.65):
        b.box((0, -.45, z), (L, 0, z + .04), "galv")
        x = .08
        while x < L - .3:
            w, h = rng.uniform(.24, .42), rng.uniform(.18, .36)
            if rng.random() > .22 and x + w < L - .05:
                t = rng.choice(stock)
                b.box((x, -.4, z + .04), (x + w, -.06, z + .04 + h), t, skip="by-", fit=TILES[t][0] == "I")
            x += w + rng.uniform(.03, .12)
    b.pop()


def build_supplies(b):
    rng = random.Random(37)
    fence(b, (8.85, -2.6), (4.75, -2.6), rng, 2.8, double=True)
    fence(b, (4.75, 5.6), (5.3, 5.6), rng, 2.8, double=True)
    fence(b, (6.7, 5.6), (8.85, 5.6), rng, 2.8, double=True)
    fence(b, (4.75, 5.6), (4.75, 1.6), rng, 2.8, double=True)
    fence(b, (4.75, -1.6), (4.75, -2.6), rng, 2.8, double=True)
    signboard(b, (4.72, -2.45, 2.75), (4.84, 2.45, 3.5))
    for y in (-1.68, 1.68):
        b.beam((4.78, y, 0), (4.78, y, 3.55), .14, .14, "timber", up=(0, 1, 0), caps=False)
    b.beam((5.3, 5.6, 2.3), (6.7, 5.6, 2.3), .1, .14, "timber", caps=False)
    canopy(b, 8.85, 6.3, -2.6, 5.6, 3.1, 2.92, 5)
    canopy(b, 6.3, 4.9, -1.9, 1.9, 2.95, 2.72, 2, "plaster")
    shelf(b, 8.72, -2.3, 90, 3.6, rng)
    shelf(b, 8.72, 1.6, 90, 3.7, rng)
    shelf(b, 5.5, -2.05, 0, 2.4, rng)
    shelf(b, 8.2, 5.05, 180, 1.4, rng)
    shelf(b, 6.9, 2.3, 90, 2.5, rng)
    for x, y in ((6.6, -.6), (7.6, 1.2), (7.4, 3.9)):
        bulb(b, x, y, 2.35, 2.95)
    crate(b, 4.2, -1.85, 0, .75, .7, .65, "crate", -8)
    crate(b, 4.25, -1.85, .65, .55, .5, .4, "olive", 10)
    crate(b, 4.15, 1.9, 0, .7, .6, .5, "galv", 6)
    crate(b, 5.5, 1.0, 0, .8, .6, .45, "olive", 80)
    crate(b, 5.45, 1.0, .45, .6, .5, .4, "crate", 70)
    lamp(b, (PX - .5, 3 - .2, 4.3), (-1, 0, 0), (.8, 1.0, 0))


def build_platforms(b):
    rng = random.Random(51)
    Y = ROWS[3]
    fence(b, (-PX + .5, Y), (-1.98, Y), rng, 2.8)
    fence(b, (1.98, Y), (PX - .5, Y), rng, 2.8)
    fence(b, (-8.85, Y), (-PX - .5, Y), rng, 2.7)
    fence(b, (PX + .5, Y), (8.85, Y), rng, 2.7)
    for x in (-1.9, 1.9):
        b.beam((x, Y, 0), (x, Y, 3.1), .16, .16, "darkmetal", up=(0, 1, 0), caps=False)
    b.beam((-PX + .5, Y, 3.05), (PX - .5, Y, 3.05), .16, .16, "darkmetal", caps=False)
    for s in (-1, 1):                                   # double gate: braced frames, sheet below, bars above
        x0, x1 = (s * .03, s * 1.82)
        xa, xb = min(x0, x1), max(x0, x1)
        yg = Y - .02
        for p, q in (((xa, 0.1), (xb, 0.1)), ((xa, 2.75), (xb, 2.75)), ((xa, 1.35), (xb, 1.35)), ((xa, 0.1), (xa, 2.75)), ((xb, 0.1), (xb, 2.75)),
                     ((xa, 1.35), (xb, 2.75)), ((xa, 2.75), (xb, 1.35))):
            b.beam((p[0], yg, p[1]), (q[0], yg, q[1]), .07, .07, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
        b.quad((xb, yg + .02, .12), (xa - xb, 0, 0), (0, 0, 1.2), "ribbed" if s > 0 else "galv", fit=True)
        b.quad((xa, yg + .02, .12), (xb - xa, 0, 0), (0, 0, 1.2), "ribbed" if s > 0 else "galv", fit=True)   # concourse side
        x = xa + .2
        while x < xb - .1:
            b.beam((x, yg + .02, 1.38), (x, yg + .02, 2.72), .03, .03, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
            x += .2
        # Low and outboard, clear of the launch button and prompt that Unity places at the gate centre.
        sheet(b, (s * 1.34, yg - .05, .2), (0, -1), .86, .3, "hazard" if s < 0 else "stripes", -3 * s)
    signboard(b, (-2.4, Y - .45, 3.6), (2.4, Y - .33, 4.35))
    for x in (-2.0, 2.0):
        b.beam((x, Y - .39, 4.39), (x, Y - .39, BEAM_Z), .04, .04, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    b.box((-.8, Y - .2, 3.13), (.8, Y - .1, 3.2), "darkmetal", fit=True, skip="b")
    b.flat([(-.8, Y - .1, 3.13), (.8, Y - .1, 3.13), (.8, Y - .2, 3.13), (-.8, Y - .2, 3.13)], WARM)
    for x, y, t in ((-2.9, Y - .55, "blueworn"), (2.75, Y - .6, "rust"), (3.35, Y - .45, "teal")):
        b.cyl((x, y, 0), (x, y, .9), .29, 10, t, cap_b=True)
    crate(b, -3.2, Y - 1.3, 0, .7, .5, .42, "olive", 20)
    for s in (-1, 1):
        lamp(b, (s * (PX - .2), Y - .5, 4.4), (0, -1, 0), (s * 1.2, Y - 3.2, 0))


def build_mesh_fence(b):
    """Wire mesh from the barricade tops up to the beams: both track sides and the Platforms end.
    Unity blocks these lines with invisible full-height guards; the mesh itself has no collider."""
    post = dict(up=(0, 1, 0), caps=False)
    # Posts and rails stand 3-4 cm behind the mesh plane; a post face in the same plane z-fights.
    for s in (-1, 1):                                   # track sides, up to the platform-edge beam
        x = s * 8.96
        b.mesh((x, -HY, 2.3), (0, 2 * HY, 0), (0, 0, 6.5 - 2.3))
        y = -10.5
        while y < HY:
            b.beam((s * 9.03, y, 2.2), (s * 9.03, y, 6.5), .08, .08, "darkmetal", **post)
            y += 3.0
        for z in (2.4, 4.45):
            b.beam((s * 9.03, -HY, z), (s * 9.03, HY, z), .05, .05, "darkmetal", caps=False)
    Y = ROWS[3] + .06                                   # Platforms end, up to the crossbeam
    for xa, xb in ((-8.85, -PX - .5), (-PX + .5, PX - .5), (PX + .5, 8.85)):
        b.mesh((xa, Y, 2.4), (xb - xa, 0, 0), (0, 0, BEAM_Z - 2.4))
        b.beam((xa, Y + .08, 4.45), (xb, Y + .08, 4.45), .05, .05, "darkmetal", caps=False)
    for x in (-6.8, 6.8):
        b.beam((x, Y + .08, 2.3), (x, Y + .08, BEAM_Z), .08, .08, "darkmetal", **post)
    for x in (-1.9, 1.9):
        b.beam((x, Y + .08, 3.13), (x, Y + .08, BEAM_Z), .08, .08, "darkmetal", **post)


def mesh_texture():
    """Diamond wire mesh, 32 px square, one repeat per metre (8 px cells = 25 cm, 3 cm wire; finer than the 16 px/m atlases so it reads as wire). Written once; delete the PNG to regenerate."""
    import os
    path = ROOT + MESH_TEX
    if os.path.exists(path):
        return
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rng, px = random.Random(7), []
    for y in range(32):
        for x in range(32):
            wire = (x + y) % 8 == 0 or (x - y) % 8 == 0
            k = rng.uniform(.9, 1.05)
            c = (.085 * k, .095 * k, .09 * k)
            px += [c[0], c[1], c[2], 1.0 if wire else 0.0]
    img = bpy.data.images.new("SubwayMeshFence", 32, 32, alpha=True)
    img.pixels = px
    img.filepath_raw, img.file_format = path, "PNG"
    img.save()
    bpy.data.images.remove(img)


def material(name, image=None, emission=None, strength=0.0, color=(0, 0, 0, 1)):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.9
    bsdf.inputs["Base Color"].default_value = color
    if image:
        tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(ROOT + image, check_existing=True)
        tex.interpolation = "Closest"
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if image.endswith(".png"):
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if emission:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = strength
    return m


def main():
    for n in ("SubwayTerminal", "SubwayTerminal_ReviewLights", "SubwayTerminal_ReviewText"):
        c = bpy.data.collections.get(n)
        if c:
            for o in list(c.objects):
                bpy.data.objects.remove(o, do_unlink=True)
        else:
            c = bpy.data.collections.new(n)
            bpy.context.scene.collection.children.link(c)
    for m in list(bpy.data.meshes):
        if m.users == 0: bpy.data.meshes.remove(m)
    mesh_texture()
    coll, lights = bpy.data.collections["SubwayTerminal"], bpy.data.collections["SubwayTerminal_ReviewLights"]
    mats = [material("Quackville Town atlas", "Assets/Models/Quackville/Quackville_Town_Diffuse.jpg"),
            material("Quackville Industrial atlas", "Assets/Models/Quackville/Quackville_Industrial_Diffuse.jpg"),
            material("Subway lamp lens", emission=(1, .97, .9, 1), strength=5, color=(1, 1, 1, 1)),
            material("Subway night glow", emission=(.22, .42, 1, 1), strength=2.5, color=(.1, .2, .5, 1)),
            material("Subway tunnel void"),
            material("Subway warm bulb", emission=(1, .68, .32, 1), strength=6, color=(1, .8, .5, 1)),
            material("Subway mesh fence", MESH_TEX)]
    LAMPS.clear()
    for name, fn in (("Terminal_Shell", build_shell), ("Stairway_Sealed", build_stairs), ("Stair_End_Dressing", build_dressing),
                     ("Fountain", build_fountain), ("Side_Barricades", build_barricades), ("Station_Info", build_info),
                     ("Supplies_Store", build_supplies), ("Platforms_Gate", build_platforms), ("Mesh_Fence", build_mesh_fence)):
        b = Builder(name)
        fn(b)
        b.finish(coll, mats)

    # Sign lettering for review only; in Unity these are TextMeshPro labels in CutePixel SDF.
    font = bpy.data.fonts.load(ROOT + "Assets/Fonts/CutePixel.ttf", check_existing=True)
    ivory = material("Subway sign lettering", emission=(.91, .87, .75, 1), strength=1.2, color=(.91, .87, .75, 1))
    tc = bpy.data.collections["SubwayTerminal_ReviewText"]
    for body, loc, rz in (("STATION INFO", (-4.7, 0, 3.125), 90), ("SUPPLIES", (4.7, 0, 3.125), -90), ("PLATFORMS", (0, ROWS[3] - .47, 3.975), 0)):
        cu = bpy.data.curves.new(body, "FONT")
        cu.body, cu.font, cu.size, cu.align_x, cu.align_y = body, font, .5, "CENTER", "CENTER"
        cu.materials.append(ivory)
        o = bpy.data.objects.new("Text " + body, cu)
        o.location, o.rotation_euler = loc, (math.radians(90), 0, math.radians(rz))
        tc.objects.link(o)

    # Review lighting only (not exported): the two work lamps, warm low fills, blue spill from the surface.
    def light(name, kind, loc, energy, color, **kw):
        d = bpy.data.lights.new(name, kind)
        d.energy, d.color = energy, color
        for k, v in kw.items(): setattr(d, k, v)
        o = bpy.data.objects.new(name, d)
        o.location = loc
        lights.objects.link(o)
        return o
    for i, (p, aim) in enumerate(LAMPS):
        o = light(f"Review work lamp {i}", "SPOT", p, 1100, (.85, .92, 1), spot_size=math.radians(95), spot_blend=.5, shadow_soft_size=.15)
        o.rotation_euler = aim.to_track_quat("-Z", "Y").to_euler()
    for name, loc, e in (("info", (-7.3, 0, 2.3), 300), ("supplies A", (6.6, -.6, 2.2), 260), ("supplies B", (7.5, 3.0, 2.2), 220),
                         ("gate", (0, ROWS[3] - .6, 2.9), 120), ("stair L", (-7.2, -7.5, 1.6), 110), ("stair R", (7.2, -7.5, 1.6), 110)):
        light("Review warm " + name, "POINT", loc, e, (1, .62, .3), shadow_soft_size=.4)
    light("Review barricade fill", "POINT", (0, -13.6, 4.9), 110, (1, .8, .6), shadow_soft_size=.8)
    o = light("Review surface spill", "AREA", (0, -17.6, 5.3), 900, (.3, .5, 1), size=3.5)
    o.rotation_euler = (math.radians(100), 0, 0)
    light("Review cool overhead", "POINT", (0, 0, 5.2), 140, (.6, .75, 1), shadow_soft_size=1.0)
    w = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (.006, .009, .016, 1)
    bg.inputs[1].default_value = 1.0
    for name, loc, target, lens in (("Cam_Forward", (-.3, -10.6, 2.5), (0, 7, 1.5), 19), ("Cam_LowEye", (-1.6, -6.2, .5), (1.5, 6, 1.6), 19),
                                    ("Cam_Info", (-.6, -1.2, 1.2), (-8, .3, 1.7), 19), ("Cam_Supplies", (.6, -1.6, 1.2), (8, 1.2, 1.5), 19),
                                    ("Cam_Reverse", (0, 2.2, 1.45), (0, -14, 3.4), 20), ("Cam_StairClose", (1.6, -6.6, .6), (-.3, -15, 3.6), 22),
                                    ("Cam_TrackEdge", (-3, 6.5, 1.5), (10.5, -9, .8), 20), ("Cam_Upper", (6.5, 10.5, 1.0), (-3, -10, 6.2), 18)):
        cam = bpy.data.objects.new(name, bpy.data.cameras.get(name) or bpy.data.cameras.new(name))
        lights.objects.link(cam)
        cam.data.lens = lens
        cam.location = loc
        cam.rotation_euler = (V(target) - V(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = bpy.data.objects["Cam_Reverse"]


def export_fbx():
    """Write the Unity model. Meshes are turned 180 degrees about Z for the export only, so that in Unity
    Platforms is at +Z and Station Info at -X; the .blend keeps Platforms at +Y."""
    objs = list(bpy.data.collections["SubwayTerminal"].objects)
    R = Matrix.Rotation(math.pi, 4, "Z")
    for o in objs: o.data.transform(R)
    try:
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.fbx(filepath=ROOT + "Assets/DuckRails/Compound/SubwayTerminal/SubwayTerminal.fbx", use_selection=True,
                                 object_types={"MESH"}, axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_UNITS",
                                 bake_space_transform=True, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    finally:
        for o in objs: o.data.transform(R)
        bpy.ops.object.select_all(action="DESELECT")


main()  # call export_fbx() afterwards to refresh the Unity model
