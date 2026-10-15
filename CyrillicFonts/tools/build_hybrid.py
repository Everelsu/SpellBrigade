# Гибрид: наш Irish Grover Cyrillic + обведённые буквы (trace_alphabet.py).
# Буквы, совпадающие с латиницей, остаются от Irish Grover (так русский и английский текст
# выглядят одинаково), из обводки берём только TAKE. Обведённые буквы чуть шире — сжимаем по X.
# usage: python build_hybrid.py <наш.ttf> <обводка.ttf> <выход.ttf> [TAKE]
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.reverseContourPen import ReverseContourPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.boundsPen import BoundsPen
import pathops

OURS, TRACED, OUT = sys.argv[1:4]
TAKE = sys.argv[4] if len(sys.argv) > 4 else 'БГДЖЗИЙЛПФЦЧШЩЪЫЬЭЮЯбвдеёжзийклмнптфцчшщъыьэюя'  # г своя: обведённая читается как латинская r; У своя, закруглённая
SQUEEZE = 0.92   # обводка шире Irish Grover примерно на 8–10 %
SB = 30

f, t = TTFont(OURS), TTFont(TRACED)
fc, tc = f.getBestCmap(), t.getBestCmap()
tgs = t.getGlyphSet()
glyf, hmtx = f['glyf'], f['hmtx']

def recording(gs, name, m=(1, 0, 0, 1, 0, 0)):
    rec = RecordingPen()
    pen = TransformPen(rec, m)
    gs[name].draw(ReverseContourPen(pen) if m[0] * m[3] - m[1] * m[2] < 0 else pen)
    return rec

def put(ch, rec):
    bp = BoundsPen(None); rec.replay(bp)
    x0, _, x1, _ = bp.bounds
    pen = TTGlyphPen(None)
    rec.replay(TransformPen(pen, (1, 0, 0, 1, SB - x0, 0)))
    name = f'uni{ord(ch):04X}'
    glyf[name] = pen.glyph()
    hmtx[name] = (int(round(x1 - x0 + 2 * SB)), SB)
    if name not in f.getGlyphOrder(): f.setGlyphOrder(f.getGlyphOrder() + [name])
    for tb in f['cmap'].tables:
        if tb.isUnicode(): tb.cmap[ord(ch)] = name

def traced(ch, mirror=False):
    rec = recording(tgs, tc[ord(ch)], (-SQUEEZE if mirror else SQUEEZE, 0, 0, 1, 0, 0))
    return rec

def union(*recs):
    p = pathops.Path()
    for r in recs: r.replay(p.getPen())
    p.simplify(fix_winding=True)
    out = RecordingPen(); p.draw(out)
    return out

for ch in TAKE: put(ch, traced(ch))

# украинские — из обведённых: Є = зеркальная Э, Ґ = Г + хвостик вверх (тот же, что у нашей Ґ)
for src, dst in (('Э', 'Є'), ('э', 'є')):
    if src in TAKE: put(dst, traced(src, mirror=True))
for src, dst in (('Г', 'Ґ'), ('г', 'ґ')):
    if src not in TAKE: continue
    g = traced(src)
    bp = BoundsPen(None); g.replay(bp); x0, y0, x1, y1 = bp.bounds
    tick = RecordingPen()   # вертикальный хвостик над правым краем перекладины
    w, h = 140, (y1 - y0) * 0.22
    tick.moveTo((x1 - w, y1 - 40)); tick.lineTo((x1 - w, y1 + h)); tick.lineTo((x1, y1 + h)); tick.lineTo((x1, y1 - 40)); tick.closePath()
    put(dst, union(g, tick))

f.save(OUT)
print('saved', OUT, 'traced:', TAKE)
