#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// V3 Passenger Station 3D sign lettering (Justin, Nov 26 batch 5: text should take the lighting). Glyphs come
/// straight from Assets/Fonts/CutePixel.ttf, the project font: its outlines sit on a 64-unit grid (8 px caps),
/// so each glyph is rasterised on that grid and every lit pixel run becomes a small extruded box.
/// </summary>
public static partial class RailAbandonedStation
{
    sealed class PixelGlyph { public bool[,] bits; public int x0, y0, w, h, advance; }
    static Dictionary<char, PixelGlyph> pixelFont;
    const int FontGrid = 64, CapPixels = 8;

    static PixelGlyph Glyph(char ch)
    {
        if (pixelFont == null) LoadPixelFont();
        return pixelFont.TryGetValue(ch, out var g) ? g : pixelFont.TryGetValue(char.ToUpperInvariant(ch), out g) ? g : pixelFont[' '];
    }

    static void LoadPixelFont()
    {
        var d = System.IO.File.ReadAllBytes("Assets/Fonts/CutePixel.ttf");
        int U16(int o) => (d[o] << 8) | d[o + 1];
        int S16(int o) => (short)U16(o);
        int U32(int o) => (d[o] << 24) | (d[o + 1] << 16) | (d[o + 2] << 8) | d[o + 3];
        var tables = new Dictionary<string, int>();
        for (int i = 0; i < U16(4); i++) { int r = 12 + i * 16; tables[System.Text.Encoding.ASCII.GetString(d, r, 4)] = U32(r + 8); }
        int locFmt = S16(tables["head"] + 50), metrics = U16(tables["hhea"] + 34), loca = tables["loca"], glyf = tables["glyf"], hmtx = tables["hmtx"];
        int cmap = tables["cmap"], sub = -1;
        for (int i = 0; i < U16(cmap + 2); i++) { int pid = U16(cmap + 4 + i * 8), off = U32(cmap + 8 + i * 8); if (U16(cmap + off) == 4 && (pid == 3 || pid == 0)) sub = cmap + off; }
        if (sub < 0) throw new System.Exception("CutePixel.ttf has no format 4 cmap");
        int segX2 = U16(sub + 6), ends = sub + 14, starts = ends + segX2 + 2, deltas = starts + segX2, ranges = deltas + segX2;
        int GlyphIndex(int c)
        {
            for (int s = 0; s < segX2 / 2; s++)
                if (U16(ends + 2 * s) >= c)
                {
                    if (U16(starts + 2 * s) > c) return 0;
                    int ro = U16(ranges + 2 * s); if (ro == 0) return (c + S16(deltas + 2 * s)) & 0xffff;
                    int gi = U16(ranges + 2 * s + ro + 2 * (c - U16(starts + 2 * s))); return gi == 0 ? 0 : (gi + S16(deltas + 2 * s)) & 0xffff;
                }
            return 0;
        }
        int Offset(int g) => locFmt == 0 ? U16(loca + g * 2) * 2 : U32(loca + g * 4);
        pixelFont = new Dictionary<char, PixelGlyph>();
        for (int c = 32; c < 127; c++)
        {
            int g = GlyphIndex(c); var pg = new PixelGlyph { advance = U16(hmtx + 4 * Mathf.Min(g, metrics - 1)) / FontGrid, bits = new bool[0, 0] };
            pixelFont[(char)c] = pg;
            int o = glyf + Offset(g); if (g == 0 || Offset(g + 1) == Offset(g) || S16(o) <= 0) continue;   // empty or composite
            int contours = S16(o), p = o + 10, points = U16(p + 2 * (contours - 1)) + 1;
            var endPts = Enumerable.Range(0, contours).Select(k => U16(o + 10 + 2 * k)).ToArray();
            p += 2 * contours; p += 2 + U16(p);
            var flags = new List<int>();
            while (flags.Count < points) { int f = d[p++]; flags.Add(f); if ((f & 8) != 0) { int rep = d[p++]; for (int k = 0; k < rep; k++) flags.Add(f); } }
            var xs = new int[points]; var ys = new int[points]; int x = 0, y = 0;
            for (int k = 0; k < points; k++) { int f = flags[k]; if ((f & 2) != 0) { int v = d[p++]; x += (f & 16) != 0 ? v : -v; } else if ((f & 16) == 0) { x += S16(p); p += 2; } xs[k] = x; }
            for (int k = 0; k < points; k++) { int f = flags[k]; if ((f & 4) != 0) { int v = d[p++]; y += (f & 32) != 0 ? v : -v; } else if ((f & 32) == 0) { y += S16(p); p += 2; } ys[k] = y; }
            int xMin = Mathf.FloorToInt(S16(o + 2) / (float)FontGrid), yMin = Mathf.FloorToInt(S16(o + 4) / (float)FontGrid), xMax = Mathf.CeilToInt(S16(o + 6) / (float)FontGrid), yMax = Mathf.CeilToInt(S16(o + 8) / (float)FontGrid);
            pg.x0 = xMin; pg.y0 = yMin; pg.w = xMax - xMin; pg.h = yMax - yMin; pg.bits = new bool[pg.w, pg.h];
            for (int cx = 0; cx < pg.w; cx++)
                for (int cy = 0; cy < pg.h; cy++)
                {
                    float px = (xMin + cx + .5f) * FontGrid, py = (yMin + cy + .5f) * FontGrid; int wind = 0, start = 0;
                    foreach (int end in endPts)   // nonzero winding over each contour's polygon
                    {
                        for (int k = start; k <= end; k++)
                        {
                            int k2 = k == end ? start : k + 1; float ax = xs[k], ay = ys[k], bx = xs[k2], by = ys[k2];
                            if (ay <= py) { if (by > py && (bx - ax) * (py - ay) - (px - ax) * (by - ay) > 0) wind++; }
                            else if (by <= py && (bx - ax) * (py - ay) - (px - ax) * (by - ay) < 0) wind--;
                        }
                        start = end + 1;
                    }
                    pg.bits[cx, cy] = wind != 0;
                }
        }
    }

    static int TextPixels(string text) => Mathf.Max(0, text.Sum(ch => Glyph(ch).advance) - 1);
    static float PixelTextWidth(string text, float letterH) => TextPixels(text) * letterH / CapPixels;

    // Adds pixel rectangles (rows merged into runs, equal runs merged downwards) as extruded boxes.
    static void AddPixels(MB mb, bool[,] bits, float ox, float oy, float px, float depth, int sub = 0)
    {
        int w = bits.GetLength(0), h = bits.GetLength(1); var used = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!bits[x, y] || used[x, y]) continue;
                int x1 = x; while (x1 + 1 < w && bits[x1 + 1, y] && !used[x1 + 1, y]) x1++;
                int y1 = y; while (y1 + 1 < h && Enumerable.Range(x, x1 - x + 1).All(k => bits[k, y1 + 1] && !used[k, y1 + 1])) y1++;
                for (int a = x; a <= x1; a++) for (int b = y; b <= y1; b++) used[a, b] = true;
                Cube(mb, new Vector3(ox + (x + x1 + 1) * .5f * px, oy + (y + y1 + 1) * .5f * px, -depth / 2), new Vector3((x1 - x + 1) * px, (y1 - y + 1) * px, depth), sub);
            }
    }

    // Raised CutePixel lettering, centred on the origin (caps centred vertically), reading from -Z.
    static Mesh PixelTextMesh(string text, float letterH, float depth = .02f) => SaveProc("sign-text-" + Hash128.Compute(text).ToString().Substring(0, 12) + "-" + letterH.ToString("F3"), () =>
    {
        var mb = new MB(); float px = letterH / CapPixels, cursor = -TextPixels(text) * px / 2;
        foreach (char ch in text)
        {
            var g = Glyph(ch);
            if (g.w > 0) AddPixels(mb, g.bits, cursor + g.x0 * px, g.y0 * px - letterH / 2, px, depth);
            cursor += g.advance * px;
        }
        return mb;
    }, true);

    // Bold 7x7 pixel arrow pointing up (rotate the holder for other directions), cap height tall.
    static Mesh PixelArrowMesh(float letterH, float depth = .02f) => SaveProc("sign-arrow-" + letterH.ToString("F3"), () =>
    {
        string[] rows = { "...X...", "..XXX..", ".XXXXX.", "XXXXXXX", "..XXX..", "..XXX..", "..XXX.." };   // top row first
        var bits = new bool[7, 7];
        for (int y = 0; y < 7; y++) for (int x = 0; x < 7; x++) bits[x, 6 - y] = rows[y][x] == 'X';
        float px = letterH / 7; var mb = new MB(); AddPixels(mb, bits, -3.5f * px, -3.5f * px, px, depth);
        return mb;
    }, true);
}
#endif
