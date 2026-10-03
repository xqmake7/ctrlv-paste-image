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
# -*- coding: utf-8 -*-
"""生成功能特性图 features.png。"""
from PIL import Image, ImageDraw, ImageFont

F = 2
W, H = 1600 * F, 700 * F
BG = (255, 255, 255)
CARD = (246, 249, 253)
BORDER = (222, 232, 246)
BLUE = (43, 108, 255)
DARK = (31, 41, 55)
GRAY = (110, 118, 130)
WHITE = (255, 255, 255)
FB = r'C:\Windows\Fonts\msyhbd.ttc'
FR = r'C:\Windows\Fonts\msyh.ttc'


def font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.truetype(r'C:\Windows\Fonts\simhei.ttf', size)


img = Image.new('RGB', (W, H), BG)
d = ImageDraw.Draw(img)


def rrect(box, r, fill=None, outline=None, width=1):
    x0, y0, x1, y1 = box
    d.rectangle([x0 + r, y0, x1 - r, y1], fill=fill)
    d.rectangle([x0, y0 + r, x1, y1 - r], fill=fill)
    d.pieslice([x0, y0, x0 + 2 * r, y0 + 2 * r], 180, 270, fill=fill)
    d.pieslice([x1 - 2 * r, y0, x1, y0 + 2 * r], 270, 360, fill=fill)
    d.pieslice([x0, y1 - 2 * r, x0 + 2 * r, y1], 90, 180, fill=fill)
    d.pieslice([x1 - 2 * r, y1 - 2 * r, x1, y1], 0, 90, fill=fill)
    if outline:
        d.arc([x0, y0, x0 + 2 * r, y0 + 2 * r], 180, 270, fill=outline, width=width)
        d.arc([x1 - 2 * r, y0, x1, y0 + 2 * r], 270, 360, fill=outline, width=width)
        d.arc([x0, y1 - 2 * r, x0 + 2 * r, y1], 90, 180, fill=outline, width=width)
        d.arc([x1 - 2 * r, y1 - 2 * r, x1, y1], 0, 90, fill=outline, width=width)
        d.line([x0 + r, y0, x1 - r, y0], fill=outline, width=width)
        d.line([x0 + r, y1, x1 - r, y1], fill=outline, width=width)
        d.line([x0, y0 + r, x0, y1 - r], fill=outline, width=width)
        d.line([x1, y0 + r, x1, y1 - r], fill=outline, width=width)


def ctext(cx, y, text, fnt, color):
    w, h = d.textsize(text, font=fnt)
    d.text((int(cx - w / 2), int(y)), text, font=fnt, fill=color)


def check(cx, cy, r):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=BLUE)
    d.line([cx - r * 0.42, cy + r * 0.02, cx - r * 0.08, cy + r * 0.38], fill=WHITE, width=12)
    d.line([cx - r * 0.08, cy + r * 0.38, cx + r * 0.48, cy - r * 0.34], fill=WHITE, width=12)


ctext(W / 2, 70, '功能特性', font(FB, 84), DARK)
ctext(W / 2, 186, '小 · 快 · 准，单文件免安装', font(FR, 40), GRAY)

items = [
    ('只认资源管理器 / 桌面', '在别的程序里按 Ctrl+V 完全不受影响'),
    ('原分辨率 · 不变糊', '优先保存剪贴板原始 PNG，声明 DPI 感知'),
    ('秒存 · 几乎零延迟', '复制图片时后台预读，Ctrl+V 约 30ms 落盘'),
    ('图片 / 文字都能存', '图片存 PNG，文字按内容存成 TXT'),
    ('矢量图形识别', 'Adobe Illustrator 的图形自动存为图片'),
    ('开机自启 · 零依赖', '托盘一键开关；exe 仅约 25 KB'),
]

margin, gap, cw = 120, 60, 1450
top, ch, vgap = 360, 250, 40
for i, (t, s) in enumerate(items):
    col = i % 2
    row = i // 2
    x0 = margin + col * (cw + gap)
    y0 = top + row * (ch + vgap)
    rrect([x0, y0, x0 + cw, y0 + ch], 32, fill=CARD, outline=BORDER, width=4)
    cy = y0 + ch // 2
    check(x0 + 110, cy, 44)
    d.text((x0 + 190, cy - 52), t, font=font(FB, 50), fill=DARK)
    d.text((x0 + 192, cy + 14), s, font=font(FR, 36), fill=GRAY)

img.resize((1600, 700), Image.LANCZOS).save('features.png')
print('features.png written')
