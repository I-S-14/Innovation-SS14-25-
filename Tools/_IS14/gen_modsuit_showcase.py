#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
"""
Генератор витрины /Maps/_IS14/modsuit_showcase.yml — по одному экземпляру каждого
МОД-контроллера, ядра и модуля, разложенных по клеткам и подписанных своим id.

    python Tools/_IS14/gen_modsuit_showcase.py

Списки не хардкодятся: id вычитываются из самих прототипов, поэтому новый модуль
попадает на витрину сам, без правок здесь. Витрина — это каталог «что вообще есть»;
полигон для проверки механик (вакуум, радиация, невесомость, производство) — это
соседний Tools/_IS14/gen_modsuit_showroom.py.
"""

import base64
import datetime
import io
import os
import re
import struct

# -------------------------------------------------------------------- разметка

COLS = 8            # экспонатов в ряду
DX = 4              # шаг по горизонтали: подписи длинные, теснее нельзя
DY = 3              # шаг по вертикали: 2 мало — подпись лезет на ряд выше
MARGIN = 2          # поля пола вокруг экспонатов
HEADER_X = -4       # колонка стоек с названиями разделов, слева от витрины

FLOOR = "FloorSteel"
HEADER_COLOR = "#63C7FFFF"

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROTO_DIR = os.path.join(ROOT, "Resources", "Prototypes", "_IS14", "Entities", "Modsuits")

# ---------------------------------------------------------------- сбор списков

PROTO_FILES = ["base.yml", "themes.yml", "modules.yml", "devices.yml"]

# id -> {"file", "parents", "text", "abstract"} по всем файлам МОДов сразу:
# наследование ходит между ними, а компонент чаще всего объявлен у предка.
PROTOS = {}
ORDER = []


def load_protos():
    for filename in PROTO_FILES:
        path = os.path.join(PROTO_DIR, filename)
        with io.open(path, encoding="utf-8") as f:
            blocks = f.read().split("- type: entity")[1:]

        for block in blocks:
            match = re.search(r"^\s+id:\s*(\S+)", block, re.M)
            if not match:
                continue
            parent = re.search(r"^\s+parent:\s*(.+)$", block, re.M)
            raw = parent.group(1).strip() if parent else ""
            PROTOS[match.group(1)] = {
                "file": filename,
                "parents": [p.strip() for p in raw.strip("[]").split(",") if p.strip()],
                "text": block,
                "abstract": re.search(r"^\s+abstract:\s*true", block, re.M) is not None,
            }
            ORDER.append(match.group(1))


def has_component(proto_id, component, seen=None):
    """Объявлен ли компонент у прототипа или у кого-то из его предков."""
    seen = seen or set()
    if proto_id in seen or proto_id not in PROTOS:
        return False
    seen.add(proto_id)

    entry = PROTOS[proto_id]
    if re.search(r"^\s+-\s+type:\s+%s\s*$" % component, entry["text"], re.M):
        return True
    return any(has_component(p, component, seen) for p in entry["parents"])


def ids_with(filename, component):
    """Неабстрактные прототипы файла, несущие компонент — свой или унаследованный."""
    return [i for i in ORDER
            if PROTOS[i]["file"] == filename
            and not PROTOS[i]["abstract"]
            and has_component(i, component)]


# Порядок внутри разделов — как в самих файлах: он там осмысленный, по назначению,
# и на полу читается лучше алфавитного.
SECTIONS = [
    ("КОНТРОЛЛЕРЫ", lambda: ids_with("themes.yml", "ModsuitControl")),
    ("ЯДРА", lambda: ids_with("base.yml", "ModCore")),
    ("МОДУЛИ", lambda: ids_with("modules.yml", "ChassisModule")),
]

# ---------------------------------------------------------------------- каркас

TILE_STRUCT = struct.Struct("<iBBB")

tiles = {}
ents = []
tilemap = {}
GRID_UID = 2
MAP_UID = 1


def tile_id(name):
    if name not in tilemap:
        tilemap[name] = len(tilemap)
    return tilemap[name]


def add(proto, x, y, label=None, size=10, offset=(0, 30), color=None, sticky=False):
    ents.append({
        "proto": proto,
        "pos": (x + 0.5, y + 0.5),
        "label": label,
        "size": size,
        "offset": offset,
        "color": color,
        "sticky": sticky,
    })


def layout():
    """Раскладывает разделы рядами и возвращает занятый прямоугольник."""
    y = 0
    xs, ys = [], []

    for title, collect in SECTIONS:
        items = collect()
        if not items:
            raise SystemExit("раздел %r пуст — изменился формат прототипов?" % title)

        # Стойка с названием раздела: подпись липкая, её незачем прятать.
        add("Rack", HEADER_X, y, label="%s (%d)" % (title, len(items)),
            size=18, offset=(0, 46), color=HEADER_COLOR, sticky=True)
        xs.append(HEADER_X)
        ys.append(y)

        for n, proto in enumerate(items):
            x = (n % COLS) * DX
            row = y - (n // COLS) * DY
            add(proto, x, row, label=proto)
            xs.append(x)
            ys.append(row)

        rows = (len(items) + COLS - 1) // COLS
        y -= rows * DY + DY  # пустой ряд между разделами

    return min(xs), min(ys), max(xs), max(ys)


# ------------------------------------------------------------- сериализация

def chunk_tiles(cx, cy):
    buf = bytearray()
    filled = False
    for ly in range(16):
        for lx in range(16):
            name = tiles.get((cx * 16 + lx, cy * 16 + ly))
            if name is None:
                buf += TILE_STRUCT.pack(0, 0, 0, 0)
            else:
                filled = True
                buf += TILE_STRUCT.pack(tile_id(name), 0, 0, 0)
    return base64.b64encode(bytes(buf)).decode("ascii") if filled else None


def atmos_chunks():
    """Воздух пишем в карту: иначе витрина грузится вакуумом до fixgridatmos."""
    chunks = {}
    for (x, y) in tiles:
        cx, cy = x // 4, y // 4
        bit = (x - cx * 4) + (y - cy * 4) * 4
        chunks[(cx, cy)] = chunks.get((cx, cy), 0) | (1 << bit)
    return chunks


def fmt(v):
    return "%g" % v


def emit_grid(out):
    out.append("  - uid: %d" % GRID_UID)
    out.append("    components:")
    out.append("    - type: MetaData")
    out.append("      name: MOD showcase")
    out.append("    - type: Transform")
    out.append("      parent: %d" % MAP_UID)
    out.append("    - type: MapGrid")
    out.append("      chunks:")
    for cx in sorted({x // 16 for (x, _) in tiles}):
        for cy in sorted({y // 16 for (_, y) in tiles}):
            data = chunk_tiles(cx, cy)
            if data is None:
                continue
            out.append("        %d,%d:" % (cx, cy))
            out.append("          ind: %d,%d" % (cx, cy))
            out.append("          tiles: %s" % data)
            out.append("          version: 7")
    out.append("    - type: Broadphase")
    out.append("    - type: Physics")
    out.append("      bodyStatus: InAir")
    out.append("      angularDamping: 0.05")
    out.append("      linearDamping: 0.05")
    out.append("      fixedRotation: False")
    out.append("      bodyType: Static")
    out.append("    - type: Fixtures")
    out.append("      fixtures: {}")
    out.append("    - type: OccluderTree")
    # Гравитация «своя»: генератор не нужен, и пропажа питания её не роняет.
    out.append("    - type: Gravity")
    out.append("      enabled: True")
    out.append("      inherent: True")
    out.append("      gravityShakeSound: !type:SoundPathSpecifier")
    out.append("        path: /Audio/Effects/alert.ogg")
    out.append("    - type: DecalGrid")
    out.append("      chunkCollection:")
    out.append("        version: 2")
    out.append("        nodes: []")
    out.append("    - type: GridAtmosphere")
    out.append("      version: 2")
    out.append("      data:")
    out.append("        tiles:")
    chunks = atmos_chunks()
    for (cx, cy) in sorted(chunks):
        out.append("          %d,%d:" % (cx, cy))
        out.append("            0: %d" % chunks[(cx, cy)])
    out.append("        uniqueMixes:")
    out.append("        - volume: 2500")
    out.append("          temperature: 293.15")
    out.append("          moles:")
    for mole in [21.824879, 82.10312] + [0] * 10:
        out.append("          - %s" % fmt(mole))
    out.append("        chunkSize: 4")
    out.append("    - type: GasTileOverlay")
    out.append("    - type: GridPathfinding")
    out.append("    - type: RadiationGridResistance")
    out.append("    - type: SpreaderGrid")


def emit_entities(out):
    """Сущности группами по прототипу — так же, как их пишет сам движок."""
    groups, order = {}, []
    for e in ents:
        if e["proto"] not in groups:
            groups[e["proto"]] = []
            order.append(e["proto"])
        groups[e["proto"]].append(e)

    for proto in order:
        out.append("- proto: %s" % proto)
        out.append("  entities:")
        for e in groups[proto]:
            out.append("  - uid: %d" % e["uid"])
            out.append("    components:")
            out.append("    - type: Transform")
            out.append("      pos: %s,%s" % (fmt(e["pos"][0]), fmt(e["pos"][1])))
            out.append("      parent: %d" % GRID_UID)
            if not e["label"]:
                continue
            out.append("    - type: MapText")
            # Кавычки обязательны: в заголовках есть скобки и цифры, YAML лучше не дразнить.
            out.append("      text: '%s'" % e["label"].replace("'", "''"))
            out.append("      fontSize: %d" % e["size"])
            if e["color"]:
                out.append("      color: '%s'" % e["color"])
            out.append("      offset: %d,%d" % e["offset"])
            if not e["sticky"]:
                # Подпись мешает, как только предмет взяли в руку — пусть исчезает.
                out.append("    - type: TriggerOnGotEquippedHand")
                out.append("    - type: RemoveComponentsOnTrigger")
                out.append("      triggerOnce: true")
                out.append("      components:")
                out.append("      - type: MapText")


def build():
    load_protos()
    x0, y0, x1, y1 = layout()
    for x in range(x0 - MARGIN, x1 + MARGIN + 1):
        for y in range(y0 - MARGIN, y1 + MARGIN + 1):
            tiles[(x, y)] = FLOOR

    for n, e in enumerate(ents):
        e["uid"] = 10 + n

    # Space обязан быть нулевым: пустая клетка чанка пишется как 0. Номера остальным
    # тайлам раздаём до записи tilemap — секция идёт в файле раньше сериализации чанков.
    tile_id("Space")
    for name in sorted(set(tiles.values())):
        tile_id(name)

    out = []
    out.append("# SPDX-License-Identifier: AGPL-3.0-or-later")
    out.append("#")
    out.append("# Витрина МОД-костюмов: каждый контроллер, ядро и модуль по одному, подписаны id.")
    out.append("# Генерируется, руками не правится — Tools/_IS14/gen_modsuit_showcase.py")
    out.append("#")
    out.append("# Загрузка (mapinit обязателен: без него карта стоит на паузе, а контроллеры")
    out.append("# остаются без ядра — оно выдаётся стартовым предметом на мап-ините):")
    out.append("#   loadmap 100 /Maps/_IS14/modsuit_showcase.yml")
    out.append("#   mapinit 100")
    out.append("#   tp 12 0 100")
    out.append("#")
    out.append("# Подпись предмета пропадает, как только его берут в руку.")
    out.append("")
    out.append("meta:")
    out.append("  format: 7")
    out.append("  category: Map")
    out.append("  engineVersion: 270.1.0")
    out.append('  forkId: ""')
    out.append('  forkVersion: ""')
    out.append("  time: %s" % datetime.datetime.now().strftime("%m/%d/%Y %H:%M:%S"))
    out.append("  entityCount: %d" % (2 + len(ents)))
    out.append("maps:")
    out.append("- %d" % MAP_UID)
    out.append("grids:")
    out.append("- %d" % GRID_UID)
    out.append("orphans: []")
    out.append("nullspace: []")
    out.append("tilemap:")
    for name, tid in sorted(tilemap.items(), key=lambda kv: kv[1]):
        out.append("  %d: %s" % (tid, name))
    out.append("entities:")
    out.append('- proto: ""')
    out.append("  entities:")
    emit_grid(out)
    out.append("  - uid: %d" % MAP_UID)
    out.append("    components:")
    out.append("    - type: MetaData")
    out.append("      name: IS14 modsuit showcase")
    out.append("    - type: Transform")
    out.append("    - type: Map")
    out.append("      mapPaused: False")
    out.append("    - type: GridTree")
    out.append("    - type: Broadphase")
    out.append("    - type: OccluderTree")
    emit_entities(out)

    return "\n".join(out) + "\n"


if __name__ == "__main__":
    target = os.path.join(ROOT, "Resources", "Maps", "_IS14", "modsuit_showcase.yml")
    text = build()
    with io.open(target, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %s (%d lines, %d экспонатов)" % (target, text.count("\n"), len(ents)))
