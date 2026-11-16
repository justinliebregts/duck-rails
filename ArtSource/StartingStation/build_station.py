# Starting station v3 - city subway platforms, departure tunnel, retaining-wall portal and fenced yard.
# Headless:  blender -b --factory-startup --python ArtSource/StartingStation/build_station.py -- [--render] [--export]
# Coordinates are the starting block's own: Blender X = Unity X (across), Blender Y = Unity Z (along the track,
# departure toward +Y), Blender Z = Unity Y (up). Track datum is Z 0, platforms are Z 1. Metres.
# Rails, sleepers and the ground slab stay in the Unity prefab and are not modelled here.
# Builder, atlas tiles and prop helpers are reused from the Compound model so both halves of the station match.
import bpy, math, random, json, os, sys

ROOT = "X:/Work/duck-rails/"
OUT_DIR = ROOT + "ArtSource/StartingStation/"
FBX = ROOT + "Assets/DuckRails/StartingStationV3/StartingStationV3.fbx"
BULB_FBX = ROOT + "Assets/DuckRails/StartingStationV3/SuppliesDoorBulb.fbx"
RENDER_DIR = ROOT + "output/starting-station-v3/"

_src = open(ROOT + "ArtSource/SubwayTerminal/build_terminal.py").read().split("\ndef main():")[0]
T = {}
exec(_src, T)
Builder, V, Matrix, TILES = T["Builder"], T["V"], T["Matrix"], T["TILES"]
crate, pallet, sheet, signboard, lamp, material, mesh_texture = (T[k] for k in ("crate", "pallet", "sheet", "signboard", "lamp", "material", "mesh_texture"))
LENS, GLOW, VOID, WARM, MESH = T["LENS"], T["GLOW"], T["VOID"], T["WARM"], T["MESH"]

PZ = 1.0                              # platform top
WX, Y0, Y1 = 13.0, 68.0, 92.0         # station half width, rear wall, tunnel-end wall
SPR, CHX, CEIL = 7.8, 11.2, 9.0       # wall spring height, chamfer inner edge, ceiling
TW, TSPR, TCX, TH = 4.0, 5.2, 2.6, 7.0   # tunnel half width, spring, chamfer inner edge, height
YP = 140.0                            # retaining wall
RISE, RUN = 0.17, 0.34
LIGHTS, LABELS, GUARDS = [], [], []


def scheme(base):
    """Station wall: grey base, olive dado, pale plaster above."""
    return [(base + .35, "concrete"), (base + 1.5, "olive"), (99, "plaster")]


def U(p):
    """Blender point -> Unity point."""
    return [round(p[0], 3), round(p[2], 3), round(p[1], 3)]


def light(name, p, kind="lantern"):
    LIGHTS.append({"name": name, "pos": U(p), "kind": kind})


def guard(a, b, h=1.2):
    GUARDS.append({"a": U(a), "b": U(b), "h": h})


def polyn(b, pts, tile, n):
    p = [V(q) for q in pts]
    if (p[1] - p[0]).cross(p[2] - p[0]).dot(V(n)) < 0: pts = pts[::-1]
    b.poly(pts, tile)


def flatbox(b, lo, hi, mat, skip=""):
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    f = {"x+": [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)], "x-": [(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)],
         "y+": [(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)], "y-": [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)],
         "t": [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)], "b": [(x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0)]}
    for k, pts in f.items():
        if k not in skip: b.flat(pts, mat)


def wall_open(b, axis, c, a0, a1, z0, z1, tile, face, holes=()):
    """Wall on a constant-x or constant-y plane with rectangular openings (a0, a1, z0, z1)."""
    f = b.wx if axis == "x" else b.wy
    a = a0
    for h0, h1, hz0, hz1 in sorted(holes):
        if h0 > a: f(c, a, h0, z0, z1, tile, face)
        if hz0 > z0: f(c, h0, h1, z0, hz0, tile, face)
        if hz1 < z1: f(c, h0, h1, hz1, z1, tile, face)
        a = h1
    if a < a1: f(c, a, a1, z0, z1, tile, face)


def reveal_x(b, xa, xb, y0, y1, z0, z1, tile="concrete"):
    """Jambs, head and sill of an opening through a wall that runs along Y."""
    xa, xb = min(xa, xb), max(xa, xb)
    b.wy(y0, xa, xb, z0, z1, tile, 1)
    b.wy(y1, xa, xb, z0, z1, tile, -1)
    b.quad((xa, y1, z1), (xb - xa, 0, 0), (0, y0 - y1, 0), tile)
    b.quad((xa, y0, z0), (xb - xa, 0, 0), (0, y1 - y0, 0), tile)


def reveal_y(b, x0, x1, ya, yb, z0, z1, tile="concrete"):
    """Jambs and head of a recess in a wall that runs along X."""
    ya, yb = min(ya, yb), max(ya, yb)
    b.wx(x0, ya, yb, z0, z1, tile, 1)
    b.wx(x1, ya, yb, z0, z1, tile, -1)
    b.quad((x0, yb, z1), (x1 - x0, 0, 0), (0, ya - yb, 0), tile)
    b.quad((x0, ya, z0), (x1 - x0, 0, 0), (0, yb - ya, 0), tile)


def flood(b, attach, out, target, name, kind="flood"):
    """The Compound's bracketed floodlight, aimed at target. kind sets the baked light in Unity:
    flood (neutral, platforms and tunnel), small (neutral, low mounts), yard (neutral, long throw), warm (work lamp)."""
    T["LAMPS"].clear()
    lamp(b, attach, out, target)
    p, aim = T["LAMPS"][0]
    LIGHTS.append({"name": name, "pos": U(p), "kind": kind, "dir": U(aim)})


def bulb(b, x, y, z, top, name):
    T["bulb"](b, x, y, z, top)
    light(name, (x, y, z - .15), "bulb")


def sign(b, text, lo, hi, n):
    signboard(b, lo, hi)
    c = [(lo[i] + hi[i]) / 2 for i in range(3)]
    face = [c[0] + n[0] * (abs(hi[0] - lo[0]) / 2 + .01), c[1] + n[1] * (abs(hi[1] - lo[1]) / 2 + .01), c[2]]
    LABELS.append({"text": text, "pos": U(face), "normal": U((n[0], n[1], 0)), "height": round((hi[2] - lo[2]) * .62, 3),
                   "width": round(max(abs(hi[0] - lo[0]), abs(hi[1] - lo[1])) * .9, 3)})


def patch_x(b, x, face, y, z, w, h):
    """Exposed brick where the plaster has come away, on a wall along Y."""
    # L-shaped pair that does not overlap itself, stood 3 cm off the wall so neither fights the plaster
    b.wx(x + face * .03, y, y + w, z, z + h, "brick", face)
    b.wx(x + face * .03, y + w * .35, y + w * 1.25, z + h, z + h * 1.4, "brick", face)


def patch_y(b, y, face, x, z, w, h):
    b.wy(y + face * .03, x, x + w, z, z + h, "brick", face)
    b.wy(y + face * .03, x + w * .35, x + w * 1.25, z + h, z + h * 1.4, "brick", face)


def door_leaf(b, hx, hy, deg, tile="greypanel", w=1.16, h=2.36):
    b.push(Matrix.Translation((hx, hy, PZ)) @ Matrix.Rotation(math.radians(deg), 4, "Z"))
    b.box((0, -.03, .02), (w, .03, h), tile, fit=True)
    b.box((w - .16, -.07, 1.0), (w - .08, .07, 1.08), "steel", fit=True)
    b.pop()


def bench(b, x, y, yaw=0, L=3.0):
    """Timber slat bench; back toward local +Y."""
    b.push(Matrix.Translation((x, y, PZ)) @ Matrix.Rotation(math.radians(yaw), 4, "Z"))
    for lx in (-L / 2 + .25, L / 2 - .25):
        b.box((lx - .05, -.3, 0), (lx + .05, -.2, .45), "darkmetal", fit=True, skip="b")
        b.box((lx - .05, .2, 0), (lx + .05, .3, 1.0), "darkmetal", fit=True, skip="b")
        b.box((lx - .05, -.3, .4), (lx + .05, .3, .46), "darkmetal", fit=True)
    for k in (-.21, 0, .21):
        b.beam((-L / 2, k, .49), (L / 2, k, .49), .17, .05, "timber")
    for z in (.7, .92):
        b.beam((-L / 2, .25, z), (L / 2, .25, z), .05, .17, "timber")
    b.pop()


# ---------------------------------------------------------------- station shell
def build_shell(b, m):
    S = scheme(PZ)
    L = Y1 - Y0
    for s in (-1, 1):
        for xa, xb, tile in ((3, 3.15, "lightconc"), (3.15, 3.5, "yellowworn"), (3.5, WX, "concrete")):
            x0, x1 = sorted((s * xa, s * xb))
            b.quad((x0, Y0, PZ), (x1 - x0, 0, 0), (0, L, 0), tile)
        b.wx(s * 3, Y0, Y1, 0, PZ, "concrete", -s)                       # platform face
        x0, x1 = sorted((s * 3, s * TW))
        b.wy(Y1, x0, x1, 0, PZ, "concrete", 1)                           # platform end inside the tunnel mouth
        holes = [(86.5, 89.5, PZ, 4.0)] if s < 0 else [(84.1, 85.3, PZ, 3.4), (87.6, 89.4, 2.0, 3.3), (90.0, 91.2, PZ, 3.4)]
        wall_open(b, "x", s * WX, Y0, Y1, PZ, SPR, S, -s, holes)
        for h0, h1, hz0, hz1 in holes:
            reveal_x(b, s * WX, s * (WX + .4), h0, h1, hz0, hz1)
        # vault chamfer
        if s > 0: b.quad((WX, Y0, SPR), (CHX - WX, 0, CEIL - SPR), (0, L, 0), "charcoal")
        else: b.quad((-WX, Y0, SPR), (0, L, 0), (WX - CHX, 0, CEIL - SPR), "charcoal")
        # ribs: wall pier, chamfer beam
        n = V((-s * (CEIL - SPR), 0, -(WX - CHX))).normalized()
        for yr in (68.3, 74, 80, 86, 91.7):
            x0, x1 = sorted((s * WX, s * (WX - .45)))
            b.box((x0, yr - .3, PZ), (x1, yr + .3, SPR), "concrete", skip="tb" + ("x+" if s > 0 else "x-"))
            b.beam(V((s * WX, yr, SPR)) + n * .225, V((s * CHX, yr, CEIL)) + n * .225, .6, .45, "concrete", up=tuple(n), caps=False)
        # end walls
        x0, x1 = sorted((s * 3, s * WX))
        b.wy(Y1, *sorted((s * TW, s * WX)), PZ, SPR, S, -1)
        polyn(b, [(s * WX, Y1, SPR), (s * CHX, Y1, SPR), (s * CHX, Y1, CEIL)], "plaster", (0, -1, 0))
        polyn(b, [(s * WX, Y0, SPR), (s * CHX, Y0, SPR), (s * CHX, Y0, CEIL)], "plaster", (0, 1, 0))
        polyn(b, [(s * TW, Y1, TSPR), (s * TW, Y1, TH), (s * TCX, Y1, TH)], "plaster", (0, -1, 0))
        # tunnel mouth frame
        xa, xb = sorted((s * TW, s * (TW + .7)))
        b.box((xa, Y1 - .3, PZ), (xb, Y1, TSPR + .3), "concrete", skip="by+")
        b.beam((s * (TW + .3), Y1 - .15, TSPR + .2), (s * (TCX + .25), Y1 - .15, TH + .35), .3, .7, "concrete", up=(0, -1, 0))
    b.quad((-CHX, Y1, CEIL), (2 * CHX, 0, 0), (0, -L, 0), "charcoal")     # ceiling
    for yr in (68.3, 74, 80, 86, 91.7):
        b.box((-CHX, yr - .3, CEIL - .45), (CHX, yr + .3, CEIL), "concrete", skip="tx+x-")
    b.wy(Y1, -TW, TW, TH, SPR, "plaster", -1)
    b.wy(Y1, -CHX, CHX, SPR, CEIL, "plaster", -1)
    b.box((-TCX - .2, Y1 - .3, TH), (TCX + .2, Y1, TH + .7), "concrete", skip="y+")
    # rear wall: gate on the west platform, rolling doors across the track
    wall_open(b, "y", Y0, -WX, -3, PZ, SPR, S, 1, [(-9.5, -6.5, PZ, 3.9)])
    b.wy(Y0, 3, WX, PZ, SPR, S, 1)
    b.wy(Y0, -3, 3, 5.2, SPR, "plaster", 1)
    b.wy(Y0, -CHX, CHX, SPR, CEIL, "plaster", 1)
    b.wx(-3, Y0 - .4, Y0, 0, 5.2, "concrete", 1)                          # door recess: jambs and head only; the floor
    b.wx(3, Y0 - .4, Y0, 0, 5.2, "concrete", -1)                          # is the prefab's ground slab (a sill here z-fights it)
    b.quad((-3, Y0, 5.2), (6, 0, 0), (0, -.4, 0), "concrete")
    b.wy(Y0 - .4, -3, 3, .4, 5.2, "ribbed", 1)
    b.wy(Y0 - .4, -3, 3, 0, .4, "hazard", 1)
    b.beam((0, Y0 - .33, 0), (0, Y0 - .33, 5.2), .22, .14, "darkmetal", up=(0, 1, 0), caps=False)
    for z in (1.9, 3.6):
        b.beam((-3, Y0 - .35, z), (3, Y0 - .35, z), .1, .14, "darkmetal", caps=False)
    b.box((-3.3, Y0, 5.2), (3.3, Y0 + .25, 5.6), "rust", skip="y-")
    reveal_y(b, -9.5, -6.5, Y0 - .6, Y0, PZ, 3.9)
    b.flat([(-6.5, Y0 - .6, PZ), (-9.5, Y0 - .6, PZ), (-9.5, Y0 - .6, 3.9), (-6.5, Y0 - .6, 3.9)], VOID)   # faces the platform
    bulb(b, -8.0, Y0 - .34, 3.45, 3.9, "Concourse glow")                 # warm light behind the bars
    yg = Y0 - .08
    for s in (-1, 1):                                    # sealed gate, same build as the Compound's Platforms gate
        xa, xb = sorted((-8 + s * .03, -8 + s * 1.47))
        for p, q in (((xa, 1.1), (xb, 1.1)), ((xa, 3.8), (xb, 3.8)), ((xa, 2.35), (xb, 2.35)), ((xa, 1.1), (xa, 3.8)), ((xb, 1.1), (xb, 3.8)),
                     ((xa, 2.35), (xb, 3.8)), ((xa, 3.8), (xb, 2.35))):
            b.beam((p[0], yg, p[1]), (q[0], yg, q[1]), .07, .07, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
        b.quad((xb, yg + .03, 1.12), (xa - xb, 0, 0), (0, 0, 1.2), "ribbed" if s > 0 else "galv", fit=True)   # faces the platform
        x = xa + .2
        while x < xb - .1:
            b.beam((x, yg, 2.38), (x, yg, 3.77), .03, .03, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
            x += .2
    b.beam((-9.4, yg, 2.3), (-6.6, yg - .02, 2.5), .12, .06, "rust", up=(0, 1, 0), fit=True)      # chained shut
    sign(b, "CONCOURSE", (-9.3, Y0 + .04, 4.15), (-6.7, Y0 + .16, 4.85), (0, 1))
    # buffer stop in front of the doors
    for x in (-1.5, 1.5):
        b.beam((x, 69.0, 0), (x, 69.0, 1.3), .2, .2, "rust", up=(0, 1, 0), caps=False)
        b.beam((x, 68.2, 0), (x, 69.0, 1.2), .14, .14, "rust")
    b.box((-1.9, 68.95, .85), (1.9, 69.2, 1.3), "hazard")
    # brick showing through the plaster
    patch_x(b, -WX, 1, 84.9, 4.6, 1.1, .8)
    patch_x(b, -WX, 1, 90.2, 2.6, .9, .7)
    patch_x(b, WX, -1, 82.6, 5.0, 1.2, .9)
    patch_x(b, WX, -1, 89.5, 3.9, .5, .6)
    patch_y(b, Y1, -1, 6.0, 3.1, 1.3, .9)
    patch_y(b, Y1, -1, -11.5, 5.0, 1.4, 1.0)
    patch_y(b, Y0, 1, 4.6, 2.7, 1.2, .9)
    patch_y(b, Y0, 1, -5.6, 4.4, 1.0, .8)


# ---------------------------------------------------------------- rooms behind the platform walls
def room(b, x0, x1, y0, y1, h, S, floor, door_wall_x, holes):
    b.quad((x0, y0, PZ), (x1 - x0, 0, 0), (0, y1 - y0, 0), floor)
    b.quad((x0, y1, h), (x1 - x0, 0, 0), (0, y0 - y1, 0), "charcoal")
    for x, f in ((x0, 1), (x1, -1)):
        if abs(x - door_wall_x) < 1e-6: wall_open(b, "x", x, y0, y1, PZ, h, S, f, holes)
        else: b.wx(x, y0, y1, PZ, h, S, f)
    b.wy(y0, x0, x1, PZ, h, S, 1)
    b.wy(y1, x0, x1, PZ, h, S, -1)


def build_rooms(b, m):
    S = scheme(PZ)
    # Supplies: 7 x 12 m, west of the platform wall. Walls are left clear for the shared store.
    room(b, -20.4, -13.4, 80, 92, 4.4, S, "concrete", -13.4, [(86.5, 89.5, PZ, 4.0)])
    b.beam((-13.2, 86.3, 4.08), (-13.2, 89.7, 4.08), .5, .16, "timber")
    # Unity places the shared store (a 2.8 m wide U) at (-19.02, 1, 85.38) facing +Z, against the west and north walls.
    flood(b, (-16.9, 82.6, 4.4), (0, 0, -1), (-17.4, 84.2, PZ), "Supplies flood S", "small")
    flood(b, (-16.4, 89.4, 4.4), (0, 0, -1), (-17.6, 88.6, PZ), "Supplies flood N", "small")
    bulb(b, -14.6, 81.6, 3.6, 4.4, "Supplies corner bulb")
    crate(b, -14.0, 84.9, PZ, .9, .8, .8, "crate", -6)
    crate(b, -14.05, 84.9, PZ + .8, .7, .6, .5, "galv", 14)
    crate(b, -14.0, 83.7, PZ, .8, .6, .5, "olive", 4)
    for x in (-19.7, -19.2):                                  # ceiling pipes
        b.cyl((x, 80, 4.15), (x, 92, 4.15), .08, 6, "redworn")
    crate(b, -14.3, 80.7, PZ, .9, .8, .8, "crate", 5)
    crate(b, -14.3, 80.7, PZ + .8, .7, .6, .5, "olive", -12)
    crate(b, -15.4, 80.6, PZ, .8, .6, .5, "galv", 8)
    b.cyl((-14.1, 91.4, PZ), (-14.1, 91.4, PZ + .9), .29, 10, "blueworn", cap_b=True)
    b.push(Matrix.Translation((-14.9, 91.86, PZ + .5)) @ Matrix.Rotation(math.radians(-80), 4, "X")); pallet(b); b.pop()
    sign(b, "SUPPLIES", (-12.96, 86.7, 4.25), (-12.84, 89.3, 4.95), (1, 0))
    # Washroom
    W = [(PZ + 1.6, "whitepanel"), (99, "plaster")]
    room(b, 13.4, 18.4, 80, 86.8, 4.0, W, "lightconc", 13.4, [(84.1, 85.3, PZ, 3.4)])
    for k in range(3):                                        # stalls on the east wall
        y = 80.3 + k * 1.5
        b.box((16.9, y + 1.45, PZ + .2), (18.4, y + 1.5, PZ + 2.1), "galv", fit=True, skip="x+")
        ajar = (8, 55, 0)[k]
        b.push(Matrix.Translation((16.9, y + .05, PZ + .2)) @ Matrix.Rotation(math.radians(90 + ajar), 4, "Z"))
        b.box((0, -.02, 0), (1.3, .02, 1.9), "teal", fit=True)
        b.pop()
        b.box((17.75, y + .5, PZ), (18.25, y + .95, PZ + .45), "whitepanel", fit=True, skip="b")
        b.box((18.1, y + .45, PZ + .45), (18.4, y + 1.0, PZ + .95), "whitepanel", fit=True, skip="b")
    b.box((16.9, 80.25, PZ + .2), (18.4, 80.3, PZ + 2.1), "galv", fit=True, skip="x+")
    b.box((13.8, 80, PZ + .75), (16.3, 80.5, PZ + .9), "lightconc", skip="y-")           # sink shelf
    for x in (14.3, 15.05, 15.8):
        b.box((x - .22, 80.06, PZ + .9), (x + .22, 80.44, PZ + 1.0), "whitepanel", fit=True, skip="b")
        b.beam((x, 80.08, PZ + 1.0), (x, 80.08, PZ + 1.2), .04, .04, "steel", up=(0, 1, 0), fit=True)
    b.quad((13.9, 80.03, PZ + 1.3), (0, 0, .9), (2.3, 0, 0), "glass", fit=True, swap=True)           # mirror
    bulb(b, 15.6, 83.4, 3.5, 4.0, "Washroom bulb")
    door_leaf(b, 13.4, 84.1, 18, "teal")
    sign(b, "WASHROOM", (12.84, 83.9, 3.65), (12.96, 85.5, 4.15), (-1, 0))
    # Station office
    room(b, 13.4, 18.4, 87.2, 92, 4.0, S, "timber", 13.4, [(87.6, 89.4, 2.0, 3.3), (90.0, 91.2, PZ, 3.4)])
    for z in (2.0, 3.3):                                      # window frame and mullions
        b.beam((13.2, 87.55, z), (13.2, 89.45, z), .46, .08, "timber")
    b.beam((13.2, 88.5, 2.0), (13.2, 88.5, 3.3), .05, .05, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    b.beam((13.2, 87.6, 2.65), (13.2, 89.4, 2.65), .05, .05, "darkmetal", fit=True, caps=False)
    b.box((16.9, 88.0, PZ), (17.8, 90.0, PZ + .78), "timber", skip="b")                   # desk
    b.box((17.2, 88.3, PZ + .78), (17.5, 88.6, PZ + .82), "darkmetal", fit=True)
    flatbox(b, (17.26, 88.36, PZ + .82), (17.44, 88.54, PZ + 1.0), WARM, skip="b")       # desk lamp
    crate(b, 16.2, 89.0, PZ, .5, .5, .5, "redwood", 12)                                   # stool
    b.box((14.0, 91.45, PZ), (14.6, 92, PZ + 1.5), "galv", fit=True, skip="by+")
    b.box((14.7, 91.45, PZ), (15.3, 92, PZ + 1.5), "galv", fit=True, skip="by+")           # filing cabinets
    b.box((15.4, 91.45, PZ), (16.0, 92, PZ + 1.2), "teal", fit=True, skip="by+")
    signboard(b, (15.0, 87.24, 2.3), (17.4, 87.34, 3.6))                                  # route board
    bulb(b, 15.9, 89.5, 3.4, 4.0, "Office bulb")
    door_leaf(b, 13.4, 90.0, 24, "redwood")
    sign(b, "OFFICE", (12.84, 89.95, 3.65), (12.96, 91.25, 4.15), (-1, 0))


# ---------------------------------------------------------------- stairs and footbridge
def build_stairs_bridge(b, m):
    S = scheme(PZ)
    BT, BU = 6.1, 5.8                                          # bridge top and underside
    for s in (-1, 1):
        xa, xb = sorted((s * 10.1, s * WX))
        xi = s * 10.1

        def step(yf, z0, z1):
            b.wy(yf, xa, xb, z0, z1, "concrete", 1)
            b.quad((xa, yf - RUN, z1), (xb - xa, 0, 0), (0, RUN, 0), "concrete")
            b.wx(xi, yf - RUN, yf, PZ, z1, S, -s)
        z = PZ
        for i in range(14):
            step(82.32 - i * RUN, z, z + RISE); z += RISE
        b.wy(77.56, xa, xb, z, z + RISE, "concrete", 1); z += RISE     # 3.55 landing
        b.quad((xa, 75.76, z), (xb - xa, 0, 0), (0, 1.8, 0), "concrete")
        b.wx(xi, 75.76, 77.56, PZ, z, S, -s)
        for j in range(14):
            step(75.76 - j * RUN, z, z + RISE); z += RISE
        b.wy(71.0, xa, xb, z, BT, "concrete", 1)
        b.wy(71.0, xa, xb, PZ, BU, S, -1)                              # back of the stair under the bridge
        # handrail on the open side
        xr = xi + s * .06
        line = [(82.32, PZ), (77.56, 3.55), (75.76, 3.55), (71.0, BT)]
        for (ya, za), (yb, zb) in zip(line, line[1:]):
            for dz in (1.1, .58):
                b.beam((xr, ya, za + dz), (xr, yb, zb + dz), .06, .06, "teal", fit=True, caps=False)
            m.mesh((xr, ya, za + .12), (0, yb - ya, zb - za), (0, 0, .95))
            guard((xr, ya, za), (xr, yb, zb))
            for t in (0, .5):
                y, zz = ya + (yb - ya) * t, za + (zb - za) * t
                b.beam((xr, y, zz), (xr, y, zz + 1.13), .07, .07, "teal", up=(0, 1, 0), fit=True, caps=False)
        b.beam((s * 3.6, 69.5, PZ), (s * 3.6, 69.5, BU), .26, .26, "rust", up=(0, 1, 0), caps=False)   # bridge posts
        b.beam((s * 3.6, 69.5, 4.6), (s * 5.0, 69.5, BU), .12, .12, "rust", up=(0, 1, 0), caps=False)
        flood(b, (s * 7.4, 69.6, BU), (0, 0, -1), (s * 7.0, 71.6, PZ), f"Under bridge {'W' if s < 0 else 'E'}", "small")
    # bridge deck
    b.quad((-WX, 68, BT), (2 * WX, 0, 0), (0, 3, 0), "concrete")
    b.quad((-WX, 71, BU), (2 * WX, 0, 0), (0, -3, 0), "darkmetal")
    b.wy(71, -10.1, 10.1, BU, BT, "rust", 1)
    for y in (68.35, 70.65):
        b.beam((-WX, y, BU - .12), (WX, y, BU - .12), .24, .24, "rust", caps=False, skip="t")
    yr = 70.9
    for z in (BT + 1.1, BT + .58):
        b.beam((-10.1, yr, z), (10.1, yr, z), .06, .06, "teal", fit=True, caps=False)
    m.mesh((-10.1, yr, BT + .08), (20.2, 0, 0), (0, 0, 1.0))
    guard((-10.1, yr, BT), (10.1, yr, BT))
    x = -10.1
    while x < 10.2:
        b.beam((x, yr, BT), (x, yr, BT + 1.13), .07, .07, "teal", up=(0, 1, 0), fit=True, caps=False)
        x += 2.02


# ---------------------------------------------------------------- platform dressing
def tripod_lamp(b, x, y, height, target, name, kind):
    for a in (90, 210, 330):
        angle = math.radians(a)
        foot = (x + .5 * math.cos(angle), y + .5 * math.sin(angle), PZ)
        b.beam(foot, (x, y, PZ + height * .58), .05, .05, "darkmetal", up=(0, 0, 1), fit=True, caps=False)
    b.beam((x, y, PZ + height * .54), (x, y, PZ + height), .06, .06, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    flood(b, (x, y, PZ + height), (0, 0, 1), target, name, kind)


def supplies_tripods(b):
    tripod_lamp(b, -11.5, 85.95, 1.15, (-13, 86.05, 1.45), "Supplies tripod south", "warm_tripod")
    tripod_lamp(b, -12, 90.25, 1.15, (-13, 89.85, 1.45), "Supplies tripod north", "warm_tripod")


def build_dressing(b, m):
    rng = random.Random(23)
    # Neutral wall floods, the Compound's fixture: three a side, two over the bridge, two on the tunnel-end wall.
    for s in (-1, 1):
        side = "W" if s < 0 else "E"
        for y in (76.0, 82.5, 89.0):                             # aimed steeply so each also washes its own wall
            flood(b, (s * WX, y, 6.7), (-s, 0, 0), (s * 8.5, y, PZ), f"Wall flood {side} {y:.0f}")
        flood(b, (s * 6.0, Y0, 8.0), (0, 1, 0), (s * 6.0, 71.5, 6.1), f"Bridge flood {side}", "small")
        flood(b, (s * 8.6, Y1, 6.7), (0, -1, 0), (s * 8.0, 86.5, PZ), f"Tunnel-end flood {side}")
        bench(b, s * 9.78, 78.8, -90 * s)                        # beside the stair, back to its side wall
    # Survivor camp, west platform
    b.box((-9.6, 75.6, PZ), (-8.4, 76.3, PZ + .8), "galv", fit=True, skip="b")               # generator
    b.quad((-9.6 + 1.1, 75.58, PZ + .15), (-1.0, 0, 0), (0, 0, .5), "louver", fit=True)
    b.cyl((-9.3, 75.95, PZ + .8), (-8.7, 75.95, PZ + .98), .14, 8, "rust", True, True)
    b.cyl((-8.55, 76.15, PZ + .8), (-8.55, 76.15, PZ + 1.25), .04, 6, "darkmetal", cap_b=True)
    px, py = -7.2, 76.4                                                                     # work lamp on a tripod
    for a in (90, 210, 330):
        b.beam((px + .5 * math.cos(math.radians(a)), py + .5 * math.sin(math.radians(a)), PZ), (px, py, PZ + 1.5), .05, .05, "darkmetal", up=(0, 0, 1), fit=True, caps=False)
    b.beam((px, py, PZ + 1.4), (px, py, PZ + 2.6), .06, .06, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
    flood(b, (px, py, PZ + 2.6), (0, 0, 1), (-6.2, 73.4, PZ), "Camp work lamp", "warm")
    supplies_tripods(b)
    crate(b, -9.2, 73.4, PZ, .9, .8, .8, "crate", 8)
    crate(b, -9.25, 73.45, PZ + .8, .7, .55, .45, "olive", -14)
    crate(b, -8.1, 73.0, PZ, .8, .55, .5, "olive", 22)
    crate(b, -6.5, 74.7, PZ, .6, .6, .42, "crate", 40)                                      # something to sit on
    crate(b, -7.6, 74.3, PZ, .55, .5, .4, "galv", -20)
    b.cyl((-6.0, 76.3, PZ), (-6.0, 76.3, PZ + .9), .29, 10, "rust", cap_b=True)              # fire barrel
    flatbox(b, (-6.16, 76.14, PZ + .86), (-5.84, 76.46, PZ + .92), WARM, skip="b")
    light("Fire barrel", (-6.0, 76.3, PZ + 1.2), "bulb")
    b.push(Matrix.Translation((-5.5, 72.9, PZ))); pallet(b); b.pop()
    crate(b, -5.5, 72.9, PZ + .14, .9, .7, .5, "olive", 6)                                  # tarp stack
    # Salvage, east platform
    for x, y, t in ((6.2, 73.0, "blueworn"), (6.85, 73.2, "rust"), (6.5, 73.75, "teal")):
        b.cyl((x, y, PZ), (x, y, PZ + .9), .29, 10, t, cap_b=True)
    b.push(Matrix.Translation((9.3, 76.6, PZ))); pallet(b); b.pop()
    b.push(Matrix.Translation((9.3, 76.6, PZ + .14)) @ Matrix.Rotation(math.radians(9), 4, "Z")); pallet(b); b.pop()
    crate(b, 9.25, 76.6, PZ + .28, .9, .8, .7, "crate", -6)
    crate(b, 8.0, 73.1, PZ, 1.0, .8, .8, "crate", 12)
    crate(b, 8.05, 73.1, PZ + .8, .7, .6, .5, "galv", -18)
    crate(b, 9.2, 73.4, PZ, .8, .55, .45, "olive", 30)
    for i, t in enumerate(("corr", "galv", "corr")):                                         # sheets leaning near the stair
        sheet(b, (9.75, 74.6 + i * .45, PZ), (-1, 0), 1.2, 2.2, t, rng.uniform(-3, 3), 14 + i * 3, True)
    # loose bits by the doors and bench ends
    crate(b, -12.2, 85.4, PZ, .7, .6, .55, "crate", 10)
    crate(b, 12.1, 88.6, PZ, .7, .55, .5, "olive", -8)
    b.cyl((11.6, 91.4, PZ), (11.6, 91.4, PZ + .9), .29, 10, "teal", cap_b=True)
    b.cyl((-12.3, 91.4, PZ), (-12.3, 91.4, PZ + .75), .25, 10, "darkmetal", cap_b=True)      # bin
    # conduit along the east wall
    b.beam((12.93, 71.5, 5.6), (12.93, 91.4, 5.6), .06, .06, "darkmetal", up=(1, 0, 0), fit=True, caps=False)


# ---------------------------------------------------------------- departure tunnel
def build_tunnel(b, m):
    S = scheme(0)
    ye = YP + .5
    L = ye - Y1
    b.wx(TW, Y1, ye, 0, TSPR, S, -1)
    b.wx(-TW, Y1, ye, 0, TSPR, S, 1)
    b.quad((TW, Y1, TSPR), (TCX - TW, 0, TH - TSPR), (0, L, 0), "charcoal")
    b.quad((-TW, Y1, TSPR), (0, L, 0), (TW - TCX, 0, TH - TSPR), "charcoal")
    b.quad((-TCX, ye, TH), (2 * TCX, 0, 0), (0, -L, 0), "charcoal")
    y = 96.0
    while y < YP:
        for s in (-1, 1):
            xa, xb = sorted((s * TW, s * (TW - .25)))
            b.box((xa, y - .25, 0), (xb, y + .25, TSPR), "concrete", skip="tb" + ("x+" if s > 0 else "x-"))
            n = V((-s * (TH - TSPR), 0, -(TW - TCX))).normalized()
            b.beam(V((s * TW, y, TSPR)) + n * .125, V((s * TCX, y, TH)) + n * .125, .5, .25, "concrete", up=tuple(n), caps=False)
        b.box((-TCX, y - .25, TH - .25), (TCX, y + .25, TH), "concrete", skip="tx+x-")
        y += 8.0
    for y, s in ((100.0, -1), (116.0, 1), (132.0, -1)):        # sparse wall floods, alternating sides
        flood(b, (s * TW, y, 4.7), (-s, 0, 0), (-s * 1.5, y + 2.5, 0), f"Tunnel flood {y:.0f}")
    b.beam((-TW + .06, Y1, 4.6), (-TW + .06, ye, 4.6), .07, .07, "darkmetal", up=(1, 0, 0), fit=True, caps=False)
    patch_x(b, TW, -1, 109, 2.4, 1.2, .8)
    patch_x(b, -TW, 1, 125, 3.0, 1.1, .9)


# ---------------------------------------------------------------- portal, retaining wall and city backdrop
def build_portal(b, m):
    S = scheme(0)
    yf = YP + .5
    for s in (-1, 1):
        b.wy(yf, *sorted((s * TW, s * 6.5)), 0, 7.9, S, 1)
        polyn(b, [(s * TW, yf, TSPR), (s * TW, yf, TH), (s * TCX, yf, TH)], "plaster", (0, 1, 0))
        b.wx(s * 6.5, YP, yf, 0, 7.9, "plaster", s)
        b.wy(YP, *sorted((s * 6.5, s * 48)), 0, 9, "concrete", 1)
        # wing wall
        xo = s * 10.5
        polyn(b, [(s * 6.5, YP + 1.2, 0), (xo, YP + 1.2, 0), (xo, YP + 1.2, 1.0), (s * 6.5, YP + 1.2, 2.8)], "concrete", (0, 1, 0))
        polyn(b, [(s * 6.5, YP, 2.8), (s * 6.5, YP + 1.2, 2.8), (xo, YP + 1.2, 1.0), (xo, YP, 1.0)], "concrete", (0, 0, 1))
        b.wx(xo, YP, YP + 1.2, 0, 1.0, "concrete", s)
        b.wx(s * 6.5, yf, YP + 1.2, 0, 2.8, "concrete", -s)
    b.wy(yf, -TW, TW, TH, 7.9, "plaster", 1)
    b.quad((-6.5, YP, 7.9), (13, 0, 0), (0, .5, 0), "concrete")
    b.wy(YP, -6.5, 6.5, 7.9, 9, "concrete", 1)
    b.box((-48, YP - .4, 9), (48, YP + .2, 9.3), "concrete", skip="y-")
    patch_y(b, yf, 1, -6.2, 2.2, 1.0, .8)
    patch_y(b, yf, 1, 4.7, 1.9, .9, .7)
    patch_y(b, YP, 1, 14, 5.2, 1.6, 1.1)
    patch_y(b, YP, 1, -27, 3.4, 1.8, 1.2)
    # street railing and lamp on top of the wall
    x = -46.0
    while x <= 46:
        b.beam((x, YP - .1, 9.3), (x, YP - .1, 10.4), .08, .08, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
        x += 4.0
    for z in (9.85, 10.4):
        b.beam((-48, YP - .1, z), (48, YP - .1, z), .06, .06, "darkmetal", fit=True, caps=False)
    b.beam((9, YP - .1, 9.3), (9, YP - .1, 14.2), .16, .16, "darkmetal", up=(0, 1, 0), caps=False)
    b.beam((9, YP - .1, 14.1), (9, YP + 1.7, 14.1), .1, .1, "darkmetal", fit=True)
    flood(b, (9, YP + 1.7, 14.1), (0, 1, 0), (12, 153, 0), "Street flood", "yard")
    flood(b, (0, yf, 7.4), (0, 1, 0), (0, 150, 0), "Portal flood", "yard")


def build_city(b, m):
    """Unreachable derelict blocks standing on the street above the wall. Front and side faces only."""
    rng = random.Random(8)
    for x0, y1, w, h, tile in ((-45, 134, 13, 15, "charcoal"), (-29, 136, 10, 10, "concrete"), (-16, 132, 9, 21, "charcoal"),
                               (-5, 112, 14, 27, "concrete"), (24, 134, 12, 17, "charcoal"), (38, 132, 9, 12, "concrete"),
                               (20, 104, 18, 31, "charcoal"), (-42, 98, 16, 35, "concrete")):
        z0, z1, d = 9.0, 9.0 + h, 12.0
        b.wy(y1, x0, x0 + w, z0, z1, tile, 1)
        b.wx(x0, y1 - d, y1, z0, z1, tile, -1)
        b.wx(x0 + w, y1 - d, y1, z0, z1, tile, 1)
        b.box((x0 - .15, y1 - d, z1), (x0 + w + .15, y1 + .15, z1 + .5), "concrete", skip="by-")
        cols, floors = int((w - 1.5) // 2.8), int((h - 2) // 3.4)
        gap = (w - cols * 1.3) / (cols + 1)
        for f in range(floors):
            for c in range(cols):
                r = rng.random()
                if r < .12: continue                           # boarded or missing
                wx0, wz0 = x0 + gap + c * (1.3 + gap), z0 + 1.6 + f * 3.4
                mat = WARM if r > .93 else VOID
                b.flat([(wx0 + 1.3, y1 + .04, wz0), (wx0, y1 + .04, wz0), (wx0, y1 + .04, wz0 + 1.7), (wx0 + 1.3, y1 + .04, wz0 + 1.7)], mat)


def build_deck(b, m):
    """Street deck and skirts over the buried station. Never seen from the yard; it stops baked moonlight
    reaching the one-sided interior walls from behind."""
    b.quad((-48, 60, 9.0), (96, 0, 0), (0, YP - .4 - 60, 0), "asphalt", fit=True)
    b.quad((-48, 60, 0), (0, YP - 60, 0), (0, 0, 9), "charcoal", fit=True)        # faces -X
    b.quad((48, YP, 0), (0, 60 - YP, 0), (0, 0, 9), "charcoal", fit=True)         # faces +X
    b.quad((48, 60, 0), (-96, 0, 0), (0, 0, 9), "charcoal", fit=True)             # faces -Y


# ---------------------------------------------------------------- fenced yard outside the portal
def build_yard(b, m):
    rng = random.Random(31)
    for s in (-1, 1):
        x = s * 47.0
        m.mesh((x, YP + .1, 0), (0, 19.9, 0), (0, 0, 2.7))
        guard((x, YP, 0), (x, 160, 0), 4.0)
        y = YP + .15
        while y < 160.1:
            b.beam((x + s * .05, y, 0), (x + s * .05, y, 2.85), .09, .09, "darkmetal", up=(0, 1, 0), fit=True, caps=False)
            y += 3.3
        for z in (2.7, .1):
            b.beam((x + s * .05, YP + .1, z), (x + s * .05, 160, z), .06, .06, "darkmetal", fit=True, caps=False)
        b.beam((s * 44, 150.5, 0), (s * 44, 150.5, 6.2), .16, .16, "darkmetal", up=(0, 1, 0), caps=False)     # dead lamp post
        b.beam((s * 44, 150.5, 6.1), (s * 42.6, 150.5, 6.1), .1, .1, "darkmetal", fit=True)
    flood(b, (42.6, 150.5, 6.1), (-1, 0, 0), (28, 149, 0), "Yard mast flood E", "yard")       # the west mast stays dead
    # signal hut
    b.box((-34, 145, 0), (-29.5, 148.6, 2.8), "brick", skip="tb")
    b.box((-34.3, 144.7, 2.8), (-29.2, 148.9, 2.98), "corr")
    b.box((-29.5, 146.2, 0), (-29.42, 147.3, 2.15), "greypanel", fit=True, skip="x-b")
    b.flat([(-29.48, 147.7, 1.2), (-29.48, 148.3, 1.2), (-29.48, 148.3, 1.9), (-29.48, 147.7, 1.9)], VOID)
    b.box((-29.4, 145.2, 0), (-28.9, 145.9, 1.3), "galv", fit=True, skip="b")                # relay cabinet
    flood(b, (-29.5, 146.75, 2.6), (1, 0, 0), (-23, 148.5, 0), "Signal hut flood", "small")
    # stacked sleepers
    for layer in range(3):
        for k in range(5):
            if layer % 2 == 0: b.beam((12.2, 151.2 + k * .55, .1 + layer * .2), (14.8, 151.2 + k * .55, .1 + layer * .2), .26, .18, "timber")
            else: b.beam((12.4 + k * .55, 151.0, .1 + layer * .2), (12.4 + k * .55, 153.6, .1 + layer * .2), .26, .18, "timber")
    for k in range(3):
        b.beam((16.0, 151.6 + k * .45, .12 + k * .03), (18.5, 152.2 + k * .5, .12), .26, .18, "timber")
    # rubble
    for k in range(7):
        x, y = -20 + rng.uniform(0, 6), 150 + rng.uniform(0, 3)
        sx, sy, sz = rng.uniform(.5, 1.4), rng.uniform(.4, 1.0), rng.uniform(.25, .7)
        b.push(Matrix.Translation((x, y, 0)) @ Matrix.Rotation(rng.uniform(0, 3.1), 4, "Z") @ Matrix.Rotation(rng.uniform(-.25, .25), 4, "X"))
        b.box((-sx / 2, -sy / 2, -.1), (sx / 2, sy / 2, sz), "concrete" if k % 3 else "brick", skip="b")
        b.pop()
    b.cyl((-13.4, 151.2, 0), (-13.4, 151.2, .9), .29, 10, "rust", cap_b=True)
    b.push(Matrix.Translation((-12.6, 152.0, .29)) @ Matrix.Rotation(math.radians(90), 4, "X")); b.cyl((0, 0, -.45), (0, 0, .45), .29, 10, "blueworn", True, True); b.pop()
    # dead bus
    b.push(Matrix.Translation((31, 146.2, 0)) @ Matrix.Rotation(math.radians(8), 4, "Z"))
    b.box((-4.6, -1.25, .5), (4.6, 1.25, 1.5), "whitepanel", skip="t")
    b.box((-4.6, -1.25, 1.5), (4.6, 1.25, 2.5), "darkmetal", skip="tb")
    b.box((-4.6, -1.25, 2.5), (4.6, 1.25, 3.0), "whitepanel", skip="b")
    b.box((-4.62, -1.27, 1.2), (4.62, 1.27, 1.45), "blueworn", skip="tb")
    for x in (-4.0, -2.6, -1.2, .2, 1.6, 3.0):
        for y, f in ((-1.26, -1), (1.26, 1)):
            pts = [(x, y, 1.6), (x + 1.15, y, 1.6), (x + 1.15, y, 2.4), (x, y, 2.4)]
            b.flat(pts if f < 0 else pts[::-1], VOID)
    for x in (-3.2, 3.0):
        for y in (-1.15, 1.15):
            b.cyl((x, y - .14, .48), (x, y + .14, .48), .48, 10, "darkmetal", True, True)
    b.pop()
    # skips
    for x, y, yaw, t in ((-11, 154, -12, "teal"), (9.5, 156, 20, "rust"), (37.5, 152.5, 80, "blueworn")):
        b.push(Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(yaw), 4, "Z"))
        b.box((-1.6, -.9, 0), (1.6, .9, 1.3), t, skip="tb")
        b.quad((-1.5, -.8, .9), (3.0, 0, 0), (0, 1.6, 0), "darkmetal")
        for yy in (-.94, .94):
            b.beam((-1.7, yy, 1.3), (1.7, yy, 1.3), .1, .08, "darkmetal", fit=True)
        b.pop()
    crate(b, -26.5, 144.2, 0, 1.0, .8, .8, "crate", 15)
    crate(b, -25.4, 144.6, 0, .8, .6, .5, "olive", -20)
    b.push(Matrix.Translation((22, 142.3, .5)) @ Matrix.Rotation(math.radians(-78), 4, "X")); pallet(b); b.pop()
    b.push(Matrix.Translation((23.4, 142.3, .5)) @ Matrix.Rotation(math.radians(-74), 4, "X")); pallet(b); b.pop()


PARTS = (("Station_Shell", build_shell), ("Station_Rooms", build_rooms), ("Station_StairsBridge", build_stairs_bridge),
         ("Station_Dressing", build_dressing), ("Tunnel", build_tunnel), ("Portal_Wall", build_portal),
         ("City_Backdrop", build_city), ("City_Deck", build_deck), ("Yard", build_yard))
CAMS = (("Cam_Rear", (1.6, 89.0, 2.7), (-.5, 68, 4.2), 18), ("Cam_Tunnel", (-9.0, 72.6, 2.5), (2.5, 96, 3.0), 18),
        ("Cam_Portal", (-7.5, 157.5, 1.7), (1.0, 140, 5.0), 20), ("Cam_Supplies", (-14.0, 91.3, 2.7), (-19.5, 82.0, 1.8), 15),
        ("Cam_East", (5.0, 83.0, 2.4), (15.0, 88.6, 2.4), 18), ("Cam_Bridge", (-11.0, 69.6, 7.6), (7.0, 88, 1.5), 18),
        ("Cam_LowEye", (-6.0, 79.0, 1.5), (3.0, 92, 3.0), 18), ("Cam_Office", (14.0, 87.8, 2.7), (18, 91.5, 1.6), 15),
        ("Cam_Yard", (-30, 158.5, 2.2), (10, 140, 6.0), 18))


def build_supplies_door_bulb(coll, mats):
    bulb_mesh = Builder("SuppliesDoorBulb")
    T["bulb"](bulb_mesh, -14.6, 90.6, 3.6, 4.4)
    return bulb_mesh.finish(coll, mats)


def main():
    for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)
    for n in ("StartingStation", "StartingStation_Review"):
        c = bpy.data.collections.get(n) or bpy.data.collections.new(n)
        if c.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(c)
    for me in list(bpy.data.meshes):
        if me.users == 0: bpy.data.meshes.remove(me)
    mesh_texture()
    coll, rev = bpy.data.collections["StartingStation"], bpy.data.collections["StartingStation_Review"]
    mats = [material("Quackville Town atlas", "Assets/Models/Quackville/Quackville_Town_Diffuse.jpg"),
            material("Quackville Industrial atlas", "Assets/Models/Quackville/Quackville_Industrial_Diffuse.jpg"),
            material("Subway lamp lens", emission=(1, .97, .9, 1), strength=5, color=(1, 1, 1, 1)),
            material("Subway night glow", emission=(.22, .42, 1, 1), strength=2.5, color=(.1, .2, .5, 1)),
            material("Subway tunnel void"),
            material("Subway warm bulb", emission=(1, .68, .32, 1), strength=6, color=(1, .8, .5, 1)),
            material("Subway mesh fence", T["MESH_TEX"])]
    del LIGHTS[:], LABELS[:], GUARDS[:]
    m = Builder("Station_WireMesh")
    total = 0
    for name, fn in PARTS:
        b = Builder(name)
        fn(b, m)
        total += sum(len(f.verts) - 2 for f in b.bm.faces)
        b.finish(coll, mats)
    total += sum(len(f.verts) - 2 for f in m.bm.faces)
    m.finish(coll, mats)
    build_supplies_door_bulb(coll, mats)
    print(f"TOTAL triangles: {total}; lights {len(LIGHTS)}, labels {len(LABELS)}, guards {len(GUARDS)}")
    with open(OUT_DIR + "station_data.json", "w") as f:
        json.dump({"lights": LIGHTS, "labels": LABELS, "guards": GUARDS}, f, indent=1)

    # Review stand-in for the ground slab, rails and sleepers that the Unity prefab already owns. Not exported.
    g = Builder("Review_GroundAndTrack")
    g.quad((-48, 60, 0), (96, 0, 0), (0, 100, 0), "gravel")
    for x in (-1.5, 1.5):
        g.beam((x, 60, .05), (x, 160, .05), .2, .1, "steel", fit=True, caps=False, skip="b")
    y = 60.8
    while y < 160:
        g.beam((-1.9, y, .01), (1.9, y, .01), .4, .02, "timber", caps=False, skip="b")
        y += 2.0
    g.finish(rev, mats)

    # Review lighting and cameras only; nothing here is exported.
    def rl(name, kind, loc, energy, color, **kw):
        d = bpy.data.lights.new(name, kind)
        d.energy, d.color = energy, color
        for k, v in kw.items(): setattr(d, k, v)
        o = bpy.data.objects.new(name, d)
        o.location = loc
        rev.objects.link(o)
        return o
    power = {"flood": 1600, "small": 700, "yard": 6000, "warm": 900, "warm_tripod": 220, "bulb": 160}
    for L in LIGHTS:
        p = (L["pos"][0], L["pos"][2], L["pos"][1])
        if "dir" in L:
            o = rl("Review " + L["name"], "SPOT", p, power[L["kind"]], (1, .62, .3) if L["kind"] in ("warm", "warm_tripod") else (.88, .94, 1),
                   spot_size=math.radians(110), spot_blend=.5, shadow_soft_size=.15)
            o.rotation_euler = V((L["dir"][0], L["dir"][2], L["dir"][1])).to_track_quat("-Z", "Y").to_euler()
        else:
            rl("Review " + L["name"], "POINT", p, power[L["kind"]], (1, .62, .3), shadow_soft_size=.25)
    o = rl("Review moon", "SUN", (0, 150, 40), .25, (.45, .6, 1))
    o.rotation_euler = (math.radians(50), 0, math.radians(200))
    w = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = w
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (.012, .02, .05, 1)
    bg.inputs[1].default_value = 1.0
    for name, loc, target, lens in CAMS:
        cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
        rev.objects.link(cam)
        cam.data.lens, cam.data.clip_end = lens, 400
        cam.location = loc
        cam.rotation_euler = (V(target) - V(loc)).to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = bpy.data.objects["Cam_Rear"]


def render(names=None):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            pass
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.view_settings.exposure = 1.0
    try: sc.eevee.shadow_pool_size = "1024"
    except Exception: pass
    os.makedirs(RENDER_DIR, exist_ok=True)
    for name, *_ in CAMS:
        if names and name not in names: continue
        sc.camera = bpy.data.objects[name]
        sc.render.filepath = RENDER_DIR + name.replace("Cam_", "blender-").lower() + ".png"
        bpy.ops.render.render(write_still=True)


def export_meshes(objs, path):
    """Blender (x, y, z) lands at Unity (x, z, y): the meshes are turned 180 degrees about Z for the export only,
    the same recipe as the Compound model."""
    R = Matrix.Rotation(math.pi, 4, "Z")
    for o in objs: o.data.transform(R)
    try:
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs: o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                                 apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, mesh_smooth_type="FACE",
                                 add_leaf_bones=False, bake_anim=False)
    finally:
        for o in objs: o.data.transform(R)
        bpy.ops.object.select_all(action="DESELECT")


def export_fbx():
    objs = bpy.data.collections["StartingStation"].objects
    door_bulb = objs["SuppliesDoorBulb"]
    export_meshes([o for o in objs if o != door_bulb], FBX)
    export_meshes([door_bulb], BULB_FBX)


main()
_args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if "--export" in _args: export_fbx()
if bpy.app.background:
    bpy.ops.wm.save_as_mainfile(filepath=OUT_DIR + "StartingStation.blend")
if "--render" in _args: render([a for a in _args if a.startswith("Cam_")] or None)
