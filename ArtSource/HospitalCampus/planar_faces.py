"""Trim overlapping, same-facing rectangles before Unity unwraps the lightmap UVs."""
from collections import defaultdict

import numpy as np


def subtract(rect, cut):
    x0, y0, x1, y1 = rect
    a, b = max(x0, cut[0]), max(y0, cut[1])
    c, d = min(x1, cut[2]), min(y1, cut[3])
    if a >= c or b >= d:
        return [rect]
    strips = [(x0,y0,a,y1), (c,y0,x1,y1), (a,y0,c,b), (a,d,c,y1)]
    return [r for r in strips if r[0] < r[2] and r[1] < r[3]]


class PlanarFaces:
    def __init__(self):
        self.planes = defaultdict(list)
        self.trimmed = 0

    def exposed(self, xyz):
        if len(xyz) != 4:
            return [xyz]
        normal = np.cross(xyz[1] - xyz[0], xyz[2] - xyz[0])
        axis = int(np.argmax(np.abs(normal)))
        if abs(normal[axis]) < 1e-8 or np.ptp(xyz[:, axis]) > 1e-5:
            return [xyz]
        u, v = [a for a in range(3) if a != axis]
        a, b, c, d = rect = tuple(round(float(n), 5) for n in
                                  (min(xyz[:,u]), min(xyz[:,v]), max(xyz[:,u]), max(xyz[:,v])))
        corners = []
        for p in xyz:
            if min(abs(p[u]-a), abs(p[u]-c)) > 1e-5 or min(abs(p[v]-b), abs(p[v]-d)) > 1e-5:
                return [xyz]
            corners.append((abs(p[u]-c) < 1e-5, abs(p[v]-d) < 1e-5))
        if len(set(corners)) != 4:
            return [xyz]
        previous = self.planes[axis, round(float(xyz[0, axis]), 5), normal[axis] > 0]
        pieces = [rect]
        for cut in previous:
            if cut[0] < c and cut[2] > a and cut[1] < d and cut[3] > b:
                pieces = [part for piece in pieces for part in subtract(piece, cut)]
        previous.append(rect)
        if pieces == [rect]:
            return [xyz]
        self.trimmed += 1
        result = []
        for left, bottom, right, top in pieces:
            p = xyz.copy()
            for row, (high_u, high_v) in enumerate(corners):
                p[row,u] = right if high_u else left
                p[row,v] = top if high_v else bottom
            result.append(p)
        return result
