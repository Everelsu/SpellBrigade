# Обводим вырезанные буквы (slice_alphabet.py) в контуры и собираем шрифт на основе Irish Grover:
# латиница остаётся его, кириллица — обведённая. Каждая строка картинки нарисована своим
# размером, поэтому масштаб считаем по строке: заглавные — до высоты заглавных Irish Grover,
# строчные — до его высоты строчных.
# usage: python trace_alphabet.py <папка букв> <выход.ttf>
import sys, os, json
import numpy as np
from PIL import Image
import potrace
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.boundsPen import BoundsPen

letters_dir, OUT = sys.argv[1], sys.argv[2]
SRC = sys.argv[3] if len(sys.argv) > 3 else 'IrishGrover-Regular.ttf'
UP = 4  # увеличение перед обводкой — глаже кривые

f = TTFont(SRC)
gs = f.getGlyphSet()
CAP, XH = f['OS/2'].sCapHeight, f['OS/2'].sxHeight
SB = 30
meta = json.load(open(os.path.join(letters_dir, 'meta.json'), encoding='utf-8'))

# масштаб строки: медиана (низ − верх) букв без выносных и надстрочных элементов
def row_key(m): return m['base']
rows = {}
for ch, m in meta.items(): rows.setdefault(row_key(m), []).append((ch, m))
row_scale = {}
for base, items in rows.items():
    upper = items[0][0].isupper()
    plain = [m for ch, m in items if ch not in 'ЁЙДЦЩёйдцщбрфуз']
    h = float(np.median([m['base'] - m['top'] for m in plain]))
    row_scale[base] = (CAP if upper else XH) / h

def trace(ch):
    m = meta[ch]
    im = Image.open(os.path.join(letters_dir, f'u{ord(ch):04X}.png')).convert('L')
    big = im.resize((im.width * UP, im.height * UP), Image.BICUBIC)
    ink = np.asarray(big) < 128
    bm = potrace.Bitmap(~ink)   # potracer обводит «ложные» пиксели — отдаём ему инверсию
    paths = bm.trace(turdsize=40, alphamax=1.0, opticurve=True, opttolerance=0.3)
    s = row_scale[m['base']] / UP
    top_px = m['top']                      # верх буквы в координатах строки
    def P(p):  # пиксели картинки → единицы шрифта (y вверх, 0 — базовая линия)
        return (p.x * s, (m['base'] - top_px) * row_scale[m['base']] - p.y * s)
    rec = RecordingPen()
    for curve in paths:
        rec.moveTo(P(curve.start_point))
        for seg in curve:
            if seg.is_corner:
                rec.lineTo(P(seg.c)); rec.lineTo(P(seg.end_point))
            else:
                rec.curveTo(P(seg.c1), P(seg.c2), P(seg.end_point))
        rec.closePath()
    return rec

glyf, hmtx = f['glyf'], f['hmtx']
for ch in meta:
    rec = trace(ch)
    bp = BoundsPen(gs); rec.replay(bp)
    x0, y0, x1, y1 = bp.bounds
    pen = TTGlyphPen(gs)
    from fontTools.pens.transformPen import TransformPen
    # potrace даёт контуры против часовой для внешних — TrueType ждёт по часовой: разворачиваем
    from fontTools.pens.reverseContourPen import ReverseContourPen
    rec.replay(TransformPen(ReverseContourPen(Cu2QuPen(pen, 1.0, reverse_direction=False)), (1, 0, 0, 1, SB - x0, 0)))
    name = f'uni{ord(ch):04X}'
    glyf[name] = pen.glyph()
    hmtx[name] = (int(round(x1 - x0 + 2 * SB)), SB)
    if name not in f.getGlyphOrder(): f.setGlyphOrder(f.getGlyphOrder() + [name])
    for t in f['cmap'].tables:
        if t.isUnicode(): t.cmap[ord(ch)] = name
f.save(OUT)
print('saved', OUT, len(meta), 'glyphs; row scales', {k: round(v, 2) for k, v in row_scale.items()})
