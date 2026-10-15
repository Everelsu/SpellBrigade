# Кириллица для Irish Grover (Apache 2.0) из его же глифов: заимствование похожих латинских,
# зеркала/перевороты и сборка из частей. Результат — IrishGroverCyrillic-Classic.ttf.
# Запуск из этой папки: python build_irish_cyrillic.py [выход.ttf] [исходный IrishGrover.ttf]
# (нужен fontTools: pip install fonttools)
import sys
from fontTools.ttLib import TTFont
from fontTools.pens.recordingPen import RecordingPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.reverseContourPen import ReverseContourPen
import pathops  # pip install skia-pathops — вырезать кусок глифа (основание Д)

OUT = sys.argv[1] if len(sys.argv) > 1 else '../Fonts/IrishGroverCyrillic-Classic.ttf'
SRC = sys.argv[2] if len(sys.argv) > 2 else 'IrishGrover-Regular.ttf'  # оригинал из игры (Apache 2.0)
f = TTFont(SRC)
gs = f.getGlyphSet()
cmap = f.getBestCmap()
glyf, hmtx = f['glyf'], f['hmtx']
CAP = f['OS/2'].sCapHeight      # 731
XH = f['OS/2'].sxHeight         # 573
SB = 30                         # боковые отступы новых букв

def g(ch): return cmap[ord(ch)]

def bounds(rec):
    bp = BoundsPen(gs); rec.replay(bp); return bp.bounds

def part(ch, mirror=False, flip=False, rotate=0):
    """Глиф буквы как набор контуров, с зеркалом/переворотом/поворотом вокруг своей рамки."""
    rec = RecordingPen(); gs[g(ch)].draw(rec)
    x0, y0, x1, y1 = bounds(rec)
    m = (1, 0, 0, 1, 0, 0)
    if rotate == 90:   m = (0, 1, -1, 0, y1 + y0, 0)       # против часовой
    if rotate == 180:  m = (-1, 0, 0, -1, x1 + x0, y1 + y0)
    if mirror:         m = (-1, 0, 0, 1, x1 + x0, 0)
    if flip:           m = (1, 0, 0, -1, 0, y1 + y0)
    out = RecordingPen()
    pen = TransformPen(out, m)
    # зеркало переворачивает направление контуров — возвращаем как было, иначе при наложении
    # на обычные части (общие штрихи) они взаимно «вычитаются» и получаются дыры
    if m[0] * m[3] - m[1] * m[2] < 0: pen = ReverseContourPen(pen)
    rec.replay(pen); return out

def place(rec, box):
    """Вписать контуры в рамку (x0, y0, x1, y1) — растяжением по обеим осям."""
    x0, y0, x1, y1 = bounds(rec)
    sx = (box[2] - box[0]) / (x1 - x0); sy = (box[3] - box[1]) / (y1 - y0)
    out = RecordingPen(); rec.replay(TransformPen(out, (sx, 0, 0, sy, box[0] - x0 * sx, box[1] - y0 * sy))); return out

def fit(rec, height, bottom=0, squeeze=1.0):
    """Пропорционально к высоте height, низ на bottom; squeeze < 1 — уже."""
    x0, y0, x1, y1 = bounds(rec)
    s = height / (y1 - y0)
    w = (x1 - x0) * s * squeeze
    return place(rec, (0, bottom, w, bottom + height))

def merge(*recs):
    out = RecordingPen()
    for r in recs: r.replay(out)
    return out

def bar(x0, y0, x1, y1):
    """Прямоугольная перемычка (направление контура — как у обычных глифов: по часовой)."""
    r = RecordingPen(); r.moveTo((x0, y0)); r.lineTo((x0, y1)); r.lineTo((x1, y1)); r.lineTo((x1, y0)); r.closePath(); return r

def add(code, rec, name=None):
    x0, y0, x1, y1 = bounds(rec)
    rec = place(rec, (SB, y0, SB + (x1 - x0), y1))
    pen = TTGlyphPen(gs); rec.replay(pen)
    name = name or f'uni{code:04X}'
    glyf[name] = pen.glyph()
    hmtx[name] = (int(round(x1 - x0 + 2 * SB)), SB)
    if name not in f.getGlyphOrder(): f.setGlyphOrder(f.getGlyphOrder() + [name])
    for t in f['cmap'].tables:
        if t.isUnicode(): t.cmap[code] = name

def alias(code, ch):
    for t in f['cmap'].tables:
        if t.isUnicode(): t.cmap[code] = g(ch)

def width(rec): b = bounds(rec); return b[2] - b[0]

STEM = 150  # толщина штриха Irish Grover примерно

# ---------- ЗАГЛАВНЫЕ ----------
for cyr, lat in zip('АВЕКМНОРСТХЁІЇ', 'ABEKMHOPCTXËIÏ'): alias(ord(cyr), lat)

def flipped(rec):
    """Переворот по вертикали в своей рамке (с исправлением направления контуров)."""
    x0, y0, x1, y1 = bounds(rec)
    out = RecordingPen(); rec.replay(TransformPen(ReverseContourPen(out), (1, 0, 0, -1, 0, y0 + y1))); return out

def clip(rec, x0, y0, x1, y1):
    """Кусок контуров внутри прямоугольника (пересечение)."""
    a = pathops.Path(); rec.replay(a.getPen())
    b = pathops.Path(); bar(x0, y0, x1, y1).replay(b.getPen())
    out = RecordingPen(); pathops.op(a, b, pathops.PathOp.INTERSECTION).draw(out); return out

def union(rec):
    """Слить наложенные части в один чистый контур."""
    p = pathops.Path(); rec.replay(p.getPen())
    p.simplify(fix_winding=True)
    out = RecordingPen(); p.draw(out); return out

caps = {}
caps['И'] = part('N', flip=True)   # переворот по вертикали: диагональ как у И, засечки ровнее, чем у зеркала
caps['Я'] = part('R', mirror=True)
caps['Г'] = part('L', flip=True)
caps['Л'] = part('V', flip=True)
caps['Ь'] = part('P', flip=True)
caps['Ч'] = fit(part('h', rotate=180), CAP)
caps['З'] = fit(part('3'), CAP)
# Ш и Ц с плоским дном: L (штрих + нога с загнутым носком) + L + I. Шаг 406 — носок
# предыдущей L (x 482–527) прячется в штрих следующей (x 76–197 от её начала).
def shifted(ch, dx):
    r = RecordingPen(); gs[g(ch)].draw(r)
    out = RecordingPen(); r.replay(TransformPen(out, (1, 0, 0, 1, dx, 0))); return out
STEP = 406
caps['Ш'] = place(merge(shifted('L', 0), shifted('L', STEP), shifted('I', 2 * STEP - 80)), (0, 0, 1000, CAP))
caps['Ц'] = merge(shifted('L', 0), shifted('I', STEP - 80))
# П — та же пара L + I, перевёрнутая: плоский верх, засечки внизу как у шрифта
caps['П'] = flipped(merge(shifted('L', 0), shifted('I', STEP - 80)))
# У — строчная y того же шрифта (по форме это и есть кириллическая У) на высоту заглавных
caps['У'] = fit(part('y'), CAP)
# Ж = зеркальная K и K спинами: штрихи накладываются точно (у K он x 74–231, у зеркальной —
# 554–711 при ширине 785), поэтому общий штрих один; чуть ужимаем по ширине
km = part('K', mirror=True)
caps['Ж'] = place(merge(km, shifted('K', 480)), (0, 0, 1060, CAP))
o = fit(part('O'), CAP * 0.86, bottom=CAP * 0.07)
caps['Ф'] = merge(o, bar((width(o) - STEM) / 2, -60, (width(o) + STEM) / 2, CAP + 40))
c = part('C', mirror=True)
caps['Э'] = merge(c, bar(width(c) * 0.28, CAP * 0.43, width(c) - 40, CAP * 0.43 + STEM * 0.8))
# Украинские: Є — та же C с перекладиной, только не зеркальная; Ґ — Г с носиком вверх
c2 = part('C')
caps['Є'] = merge(c2, bar(40, CAP * 0.43, width(c2) * 0.72, CAP * 0.43 + STEM * 0.8))
gg = caps['Г']; gx0, _, gx1, gy1 = bounds(gg)
caps['Ґ'] = union(merge(gg, bar(gx1 - STEM * 0.85, gy1 - 40, gx1 - 15, gy1 + 150)))
# Й = И + бреве над ним
br = fit(part('˘'), 120)
i_ = caps['И']
caps['Й'] = merge(i_, place(br, (width(i_) * 0.25, CAP + 40, width(i_) * 0.75, CAP + 160)))
# Ь + перемычки: Ъ (слева сверху), Б (вправо сверху), Ы (с палкой справа)
b = caps['Ь']; bw = width(b)
caps['Ъ'] = merge(bar(0, CAP - STEM, STEM * 1.3, CAP), place(b, (STEM * 1.0, 0, STEM * 1.0 + bw, CAP)))
caps['Б'] = merge(b, bar(0, CAP - STEM * 0.9, bw * 0.95, CAP))
caps['Ы'] = merge(b, place(fit(part('I'), CAP), (bw + 40, 0, bw + 40 + width(fit(part('I'), CAP)), CAP)))
# Ю = I + перемычка + O
ii = fit(part('I'), CAP); oo = fit(part('O'), CAP, squeeze=0.85)
caps['Ю'] = merge(ii, bar(width(ii) - 20, CAP * 0.42, width(ii) + 90, CAP * 0.42 + STEM * 0.8),
                  place(oo, (width(ii) + 70, 0, width(ii) + 70 + width(oo), CAP)))
# Хвостик (Ц, Щ) — сплошной блок внизу справа, заходит в основание буквы
def with_tail(rec):
    w = width(rec)
    return merge(rec, bar(w - STEM * 0.85, -190, w + 25, STEM * 0.7))
caps['Щ'] = with_tail(caps['Ш'])
caps['Ц'] = with_tail(caps['Ц'])
# Д: «крыша» Λ (перевёрнутая V) на перекладине, ножки вниз. Расширенные концы ног V
# оставляли щели над перекладиной — закрываем их и сливаем всё в один контур
lam =fit(part('V', flip=True), CAP * 0.88, bottom=CAP * 0.12); lw = width(lam)
roof = place(lam, (60, CAP * 0.12, 60 + lw, CAP))
rx0, _, rx1, _ = bounds(roof)
caps['Д'] = union(merge(roof, bar(0, 0, lw + 120, STEM * 0.9),
                        bar(0, -150, STEM * 0.8, STEM * 0.5), bar(lw + 120 - STEM * 0.8, -150, lw + 120, STEM * 0.5),
                        bar(rx0 + 10, STEM * 0.5, rx0 + lw * 0.32, CAP * 0.2), bar(rx1 - lw * 0.32, STEM * 0.5, rx1 - 10, CAP * 0.2)))

for ch, rec in caps.items(): add(ord(ch), rec)

# ---------- СТРОЧНЫЕ ----------
for cyr, lat in zip('аеорсухёпії', 'aeopcyxënii'): alias(ord(cyr), lat)
alias(ord('ї'), 'ï')
add(ord('б'), fit(part('6'), CAP))
SMALL = XH / CAP
def small(rec): b = bounds(rec); return place(rec, (0, b[1] * SMALL, (b[2] - b[0]) * SMALL * 1.08, b[3] * SMALL))
smallcaps = 'вгджзийклмнтфцчшщъыьэюяєґ'
for low in smallcaps:
    up = low.upper()
    src = caps.get(up)
    if src is None:
        src = RecordingPen(); gs[g({'В': 'B', 'К': 'K', 'М': 'M', 'Н': 'H', 'Т': 'T'}[up])].draw(src)
    add(ord(low), small(src))

# имя семейства: модифицированная версия
for rec in f['name'].names:
    if rec.nameID in (1, 4, 16):
        rec.string = 'Irish Grover Cyrillic'
    elif rec.nameID == 6:
        rec.string = 'IrishGroverCyrillic-Regular'
    elif rec.nameID == 3:
        rec.string = 'Irish Grover Cyrillic (Cyrillic Fonts mod)'
f.save(OUT)
print('saved', OUT)
