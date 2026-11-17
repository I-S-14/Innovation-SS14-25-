#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
"""Render a PR contact sheet from the real RSI frames and their current delays.

Requires Pillow. Does not edit textures or simulate gameplay lighting/audio.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

VENDORS = [
    ("engineering_equipment", "Инженерия · снаряжение"),
    ("engineering_clothing", "Инженерия · одежда"),
    ("atmos_clothing", "Атмосферный отдел"),
    ("captain", "Капитан"),
    ("med", "Медицина · снаряжение"),
    ("medical_clothing", "Медицина · одежда"),
    ("chem", "Химический отдел"),
    ("milicia", "Милиция · снаряжение"),
    ("security_clothing", "Милиция · одежда"),
    ("moko", "МОКО"),
    ("science_clothing", "НИЦ"),
    ("robotics", "Робототехника"),
    ("cargo_equipment", "Снабжение · снаряжение"),
    ("cargo_clothing", "Снабжение · одежда"),
    ("ur", "Юридический отдел"),
    ("obshiy", "Общее ателье"),
    ("water", "Вода"),
    ("food", "Закуски"),
    ("kofee", "Кофе"),
    ("cigs", "Табак"),
]
TECHFABS = [("engineering", "Инженерный"), ("medical", "Медицинский"),
            ("security", "Милицейский"), ("science", "НИЦ"), ("cargo", "Снабжение")]


def font(size, explicit=None):
    for path in [explicit, "C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]:
        if path and Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default(size=size)


class Rsi:
    def __init__(self, path):
        self.path = path
        self.meta = json.loads((path / "meta.json").read_text(encoding="utf-8"))
        self.states = {s["name"]: s for s in self.meta["states"]}
        self.size = self.meta["size"]["x"], self.meta["size"]["y"]

    def frames(self, state):
        specification = self.states[state]
        with Image.open(self.path / (state + ".png")) as source:
            source = source.convert("RGBA")
            width, height = self.size
            delays = specification.get("delays", [[1]])[0]
            columns = source.width // width
            frames = [source.crop((i % columns * width, i // columns * height,
                                   i % columns * width + width, i // columns * height + height)) for i in range(len(delays))]
        return frames, delays

    def static(self, state):
        return self.frames(state)[0][0]


def sequence(rsi, state, base):
    frames, delays = rsi.frames(state)
    return [Image.alpha_composite(base, frame).resize((64, 64), Image.Resampling.NEAREST) for frame in frames], delays


def at(sequence, seconds):
    frames, delays = sequence
    cursor = seconds % sum(delays)
    for frame, delay in zip(frames, delays):
        if cursor < delay:
            return frame
        cursor -= delay
    return frames[-1]


def render(output, seconds=16, explicit_font=None):
    root = Path(__file__).resolve().parents[2]
    textures = root / "Resources/Textures/_IS14/Structures/Machines"
    background, text, muted, line = "#252d34", "#e6ecea", "#a4b3b8", "#42505b"
    width, height = 896, 860
    canvas = Image.new("RGB", (width, height), background)
    draw = ImageDraw.Draw(canvas)
    regular, small, heading = font(12, explicit_font), font(11, explicit_font), font(23, explicit_font)
    draw.text((16, 8), "Обновлённые автоматы и техфабы", font=heading, fill=text)
    draw.text((16, 39), f"20 автоматов · 5 техфабов · текущие RSI-тайминги · {seconds:g} секунд", font=regular, fill=muted)
    sequences, palette_frames, covered = [], [], {}
    for index, (name, title) in enumerate(VENDORS):
        rsi = Rsi(textures / "Vends" / (name + ".rsi"))
        x, y = index % 4 * 224, index // 4 * 120 + 66
        draw.rectangle((x + 3, y, x + 220, y + 115), outline=line)
        draw.text((x + 12, y + 5), title, font=regular, fill=text)
        covered[name] = []
        for column, (state, label) in enumerate([("normal-unshaded", "Ожидание"), ("deny-unshaded", "Отказ"), ("eject-unshaded", "Выдача")]):
            position = x + 9 + column * 70, y + 25
            draw.text((position[0], y + 93), label, font=small, fill=muted)
            if state not in rsi.states:
                draw.text((position[0] + 24, y + 45), "—", font=heading, fill=line)
                continue
            animation = sequence(rsi, state, rsi.static("off"))
            sequences.append((position, animation))
            palette_frames.extend(animation[0])
            covered[name].append(state)
        animated = {s["name"] for s in rsi.meta["states"] if "delays" in s}
        assert animated == set(covered[name]), (name, animated - set(covered[name]))
    draw.text((16, 672), "ТЕХФАБЫ", font=regular, fill=text)
    for index, (name, title) in enumerate(TECHFABS):
        rsi = Rsi(textures / "Techfabs" / (name + ".rsi"))
        x, y = index * 179, 695
        draw.rectangle((x + 3, y, x + 174, y + 116), outline=line)
        draw.text((x + 13, y + 5), title, font=regular, fill=text)
        base = Image.alpha_composite(rsi.static("icon"), rsi.static("cover"))
        for column, (state, label) in enumerate([("unlit-building", "Работа"), ("inserting", "Загрузка")]):
            underlay = Image.alpha_composite(base, rsi.static("unlit")) if state == "inserting" else base
            animation = sequence(rsi, state, underlay)
            sequences.append(((x + 14 + column * 78, y + 25), animation))
            palette_frames.extend(animation[0])
            draw.text((x + 14 + column * 78, y + 94), label, font=small, fill=muted)
        assert {s["name"] for s in rsi.meta["states"] if "delays" in s} == {"unlit-building", "inserting"}
    draw.text((16, 821), "— Отдельного RSI-состояния нет. Это превью кадров, не запись игрового процесса.", font=small, fill=muted)
    draw.text((16, 839), "Ореол освещения и эмбиент в GIF не представлены.", font=small, fill=muted)

    # A single palette prevents the GIF from changing colors between frames.
    swatch = Image.new("RGB", (1024, ((len(palette_frames) + 15) // 16 + 1) * 64), background)
    swatch_draw = ImageDraw.Draw(swatch)
    swatch_draw.rectangle((0, 0, 255, 63), fill=text)
    swatch_draw.rectangle((256, 0, 511, 63), fill=muted)
    swatch_draw.rectangle((512, 0, 767, 63), fill=line)
    for index, frame in enumerate(palette_frames):
        swatch.paste(frame, (index % 16 * 64, (index // 16 + 1) * 64), frame)
    palette = swatch.quantize(colors=255)
    rendered = []
    for tick in range(round(seconds * 20)):
        image = canvas.copy()
        for position, animation in sequences:
            frame = at(animation, tick * 0.05)
            image.paste(frame, position, frame)
        rendered.append(image.quantize(palette=palette, dither=Image.Dither.NONE))
    output.parent.mkdir(parents=True, exist_ok=True)
    rendered[0].save(output, save_all=True, append_images=rendered[1:], duration=50, loop=0, optimize=False, disposal=1)
    with Image.open(output) as gif:
        duration = sum(gif.seek(i) or gif.info.get("duration", 0) for i in range(gif.n_frames))
        assert duration == round(seconds * 1000), duration
    print(f"Saved {output}: {output.stat().st_size / 1024 / 1024:.2f} MiB, {len(sequences)} animated states, {duration / 1000:g} seconds")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--seconds", type=float, default=16)
    parser.add_argument("--font", type=Path)
    arguments = parser.parse_args()
    if arguments.seconds <= 0:
        parser.error("--seconds must be positive")
    render(arguments.output, arguments.seconds, arguments.font)
