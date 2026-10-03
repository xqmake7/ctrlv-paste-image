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
"""生成使用教程图 how-to-use.png（三步上手）。"""
from PIL import Image, ImageDraw, ImageFont

F = 2
W, H = 1600 * F, 640 * F
BG = (255, 255, 255)
CARD = (238, 243, 250)
BORDER = (206, 220, 240)
BLUE = (43, 108, 255)
BLUE_D = (30, 80, 200)
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
    if fill:
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


def dashed_rect(box, color, width, dash=26, gap=18):
    x0, y0, x1, y1 = box
    for x in range(x0, x1, dash + gap):
        d.line([x, y0, min(x + dash, x1), y0], fill=color, width=width)
        d.line([x, y1, min(x + dash, x1), y1], fill=color, width=width)
    for y in range(y0, y1, dash + gap):
        d.line([x0, y, x0, min(y + dash, y1)], fill=color, width=width)
        d.line([x1, y, x1, min(y + dash, y1)], fill=color, width=width)


def arrow(cx, cy, size, color):
    d.line([cx - size, cy, cx + size, cy], fill=color, width=16)
    d.polygon([(cx + size + 22, cy), (cx + size - 6, cy - 26), (cx + size - 6, cy + 26)], fill=color)


# title
ctext(W / 2, 96, 'CtrlV 存图 · 三步搞定', font(FB, 92), DARK)
ctext(W / 2, 220, '在资源管理器窗口或桌面按 Ctrl+V，剪贴板内容自动存到当前文件夹', font(FR, 42), GRAY)

cards = [
    (150, 1050, '1', '复制图片', 'Win+Shift+S 截图，\n或复制任意图片'),
    (1150, 2050, '2', '打开文件夹 / 桌面', '在资源管理器窗口，\n或回到桌面'),
    (2150, 3050, '3', '按 Ctrl+V', '图片 / 文字自动存到\n当前文件夹'),
]

for x0, x1, num, title, sub in cards:
    rrect([x0, 380, x1, 1160], 40, fill=CARD, outline=BORDER, width=4)
    cx = (x0 + x1) // 2
    # badge
    d.ellipse([x0 + 70, 450, x0 + 170, 550], fill=BLUE)
    nw, nh = d.textsize(num, font=font(FB, 60))
    d.text((x0 + 120 - nw / 2, 450 + 50 - nh / 2 - 8), num, font=font(FB, 60), fill=WHITE)
    # icon
    icy = 720
    if num == '1':
        dashed_rect([cx - 160, icy - 120, cx + 160, icy + 120], (150, 175, 210), 8)
        rrect([cx - 110, icy - 80, cx + 110, icy + 80], 20, outline=BLUE, width=16)
        d.ellipse([cx - 70, icy - 55, cx - 30, icy - 15], fill=BLUE)
        d.line([cx - 90, icy + 55, cx - 10, icy - 35, cx + 70, icy + 55], fill=BLUE, width=16, joint='curve')
    elif num == '2':
        d.line([cx - 150, icy - 70, cx - 90, icy - 70, cx - 60, icy - 40, cx + 150, icy - 40],
               fill=BLUE, width=16, joint='curve')
        rrect([cx - 150, icy - 70, cx + 150, icy + 90], 24, outline=BLUE, width=16)
    else:
        rrect([cx - 190, icy - 70, cx + 190, icy + 70], 26, fill=WHITE, outline=BLUE, width=14)
        ctext(cx, icy - 40, 'Ctrl + V', font(FB, 60), BLUE)
    # texts
    ctext(cx, 900, title, font(FB, 56), DARK)
    for i, line in enumerate(sub.split('\n')):
        ctext(cx, 990 + i * 56, line, font(FR, 38), GRAY)

arrow(1100, 770, 26, (170, 190, 220))
arrow(2100, 770, 26, (170, 190, 220))

img.resize((1600, 640), Image.LANCZOS).save('how-to-use.png')
print('how-to-use.png written')
