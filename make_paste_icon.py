# CtrlV 存图 (CtrlV Paste Image)
# Copyright (C) 2026 xqmake7
# SPDX-License-Identifier: GPL-3.0-or-later
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.
"""生成 paste.ico：均匀线条的「开门放图片进去」符号图标（黑白配色）。
黑线 + 照片白色填充，深浅背景都能看清。每档尺寸单独绘制，再手工组装 ICO。
运行：python make_paste_icon.py
"""
import io
import struct
from PIL import Image, ImageDraw

BLACK = (0, 0, 0, 255)
WHITE = (255, 255, 255, 255)


def draw(size):
    f = 8
    S = size * f
    img = Image.new('RGBA', (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    stroke = max(2.0, size * 0.078) * f
    halo = stroke * 0.30

    def rline(pts, width, color, close=False):
        pts = [(p[0] * S, p[1] * S) for p in pts]
        if close:
            pts = pts + [pts[0]]
        flat = []
        for p in pts:
            flat += [p[0], p[1]]
        w = int(round(width))
        if len(pts) >= 3:
            d.line(flat, fill=color, width=w, joint='curve')
        else:
            d.line(flat, fill=color, width=w)
        for p in (pts[0], pts[-1]):
            d.ellipse([p[0] - width / 2, p[1] - width / 2, p[0] + width / 2, p[1] + width / 2], fill=color)

    def dot(cx, cy, r, color):
        d.ellipse([(cx - r) * S, (cy - r) * S, (cx + r) * S, (cy + r) * S], fill=color)

    if size <= 20:
        door = [(0.30, 0.14), (0.13, 0.14), (0.13, 0.86), (0.30, 0.86)]
        photo = [(0.60, 0.31), (0.87, 0.31), (0.87, 0.69), (0.60, 0.69)]
        mtn = None
    else:
        door = [(0.37, 0.15), (0.19, 0.15), (0.19, 0.85), (0.37, 0.85)]
        photo = [(0.33, 0.30), (0.85, 0.30), (0.85, 0.70), (0.33, 0.70)]
        mtn = [(0.42, 0.645), (0.56, 0.50), (0.70, 0.645)]
    sun = (0.45, 0.40, 0.032) if size >= 40 else None

    d.polygon([(p[0] * S, p[1] * S) for p in photo], fill=WHITE)
    for color, width in ((WHITE, stroke + 2 * halo), (BLACK, stroke)):
        rline(door, width, color)
        rline(photo, width, color, close=True)
        if mtn:
            rline(mtn, width, color)
        if sun:
            dot(sun[0], sun[1], sun[2], color)
    return img.resize((size, size), Image.LANCZOS)


def write_ico(path, images):
    pngs = []
    for im in images:
        b = io.BytesIO()
        im.save(b, 'PNG')
        pngs.append(b.getvalue())
    n = len(images)
    header = struct.pack('<HHH', 0, 1, n)
    entries = b''
    offset = 6 + 16 * n
    for im, png in zip(images, pngs):
        bw, bh = im.size
        entries += struct.pack('<BBBBHHII', 0 if bw >= 256 else bw, 0 if bh >= 256 else bh,
                               0, 0, 1, 32, len(png), offset)
        offset += len(png)
    with open(path, 'wb') as fp:
        fp.write(header)
        fp.write(entries)
        for png in pngs:
            fp.write(png)


sizes = [16, 20, 24, 32, 48, 64]
write_ico('paste.ico', [draw(s) for s in sizes])

big = draw(256)
strip = Image.new('RGB', (2 * 256 + 30, 256 + 20), (255, 255, 255))
for i, bg in enumerate([(245, 246, 248), (32, 34, 38)]):
    panel = Image.new('RGB', (256, 256), bg)
    panel.paste(big, (0, 0), big)
    strip.paste(panel, (i * (256 + 10) + 5, 10))
strip.save('paste_preview.png')

small = Image.new('RGB', (4 * 128 + 30, 148), (245, 246, 248))
for i, s in enumerate([16, 24, 32, 64]):
    im = draw(s).resize((128, 128), Image.NEAREST)
    small.paste(im, (i * 128 + 5, 10), im)
small.save('paste_preview_small.png')
print('written: paste.ico / paste_preview.png (light|dark) / paste_preview_small.png')
