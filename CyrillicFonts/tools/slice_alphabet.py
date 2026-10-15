# Режем растровый алфавит (5 строк, порядок известен) на отдельные буквы.
# usage: python slice_alphabet.py <картинка> <папка для букв>
# Для каждой буквы — PNG (белое на чёрном → чёрное на белом) и общий json с базовой линией строки.
import sys, os, json
import numpy as np
from PIL import Image
from scipy import ndimage

ROWS = [
    'АБВГДЕЁЖЗИЙКЛМН',
    'ОПРСТУФХЦЧШЩЪЫЬЭЮЯ',
    'абвгдеёжзийклмн',
    'опрстуфхцчшщъыьэюя',
]

src, out = sys.argv[1], sys.argv[2]
os.makedirs(out, exist_ok=True)
img = np.asarray(Image.open(src).convert('L'), dtype=np.float32)
ink = img > 128                     # светлые буквы на тёмном фоне
ink = ndimage.binary_opening(ink, iterations=1)

# строки: горизонтальные полосы с чернилами
rows_profile = ink.sum(axis=1) > 3
bands, y = [], 0
while y < len(rows_profile):
    if rows_profile[y]:
        y0 = y
        while y < len(rows_profile) and rows_profile[y]: y += 1
        if y - y0 > 20: bands.append((y0, y))
    y += 1
print('bands', bands)

# склеиваем полосы точек (Ё, Й) с их строкой: полоса ниже 40px — часть следующей
merged = []
for b in bands:
    if merged and b[0] - merged[-1][1] < 12: merged[-1] = (merged[-1][0], b[1])
    else: merged.append(b)
print('rows', merged)

meta = {}
for (y0, y1), letters in zip(merged, ROWS):
    band = ink[max(0, y0 - 4):y1 + 4]
    lab, n = ndimage.label(band)
    boxes = ndimage.find_objects(lab)
    comps = []
    for i, sl in enumerate(boxes):
        area = (lab[sl] == i + 1).sum()
        if area < 60: continue                       # пылинки
        comps.append([sl[1].start, sl[1].stop, sl[0].start, sl[0].stop, i + 1])
    comps.sort()
    # компоненты с перекрытием по x — одна буква (точки над Ё, бреве Й, части Ы)
    groups = []
    for c in comps:
        if groups and c[0] < groups[-1]['x1'] - 6:
            g = groups[-1]; g['x1'] = max(g['x1'], c[1]); g['ids'].append(c[4])
        else:
            groups.append({'x0': c[0], 'x1': c[1], 'ids': [c[4]]})
    # Ы — две отдельные части (Ь и палочка): узкую палочку приклеиваем к левой соседке
    while len(groups) > len(letters):
        i = min(range(1, len(groups)), key=lambda k: groups[k]['x1'] - groups[k]['x0'])
        g = groups.pop(i); groups[i - 1]['x1'] = g['x1']; groups[i - 1]['ids'] += g['ids']
    if len(groups) != len(letters):
        print(f'row {letters[:3]}…: {len(groups)} groups, expected {len(letters)}')
        print('  widths', [g['x1'] - g['x0'] for g in groups])
        continue
    # базовая линия строки: медиана низа букв без выносных элементов
    bottoms = []
    for g, ch in zip(groups, letters):
        mask = np.isin(lab, g['ids'])
        ys = np.where(mask.any(axis=1))[0]
        bottoms.append(ys[-1])
    base = float(np.median(bottoms))
    tops = []
    for g, ch in zip(groups, letters):
        mask = np.isin(lab, g['ids'])[:, g['x0']:g['x1']]
        ys = np.where(mask.any(axis=1))[0]
        crop = mask[ys[0]:ys[-1] + 1]
        Image.fromarray(np.where(crop, 0, 255).astype(np.uint8)).save(os.path.join(out, f'u{ord(ch):04X}.png'))
        meta[ch] = {'top': int(ys[0]), 'bottom': int(ys[-1]), 'base': base, 'w': int(g['x1'] - g['x0'])}
        tops.append(ys[0])
    print(f'row {letters[:3]}…: ok, base {base}, median top {np.median(tops)}')
json.dump(meta, open(os.path.join(out, 'meta.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print('letters', len(meta))
