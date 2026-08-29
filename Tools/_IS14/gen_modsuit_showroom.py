#!/usr/bin/env python3
# SPDX-License-Identifier: AGPL-3.0-or-later
"""
Генератор отладочной карты /Maps/_IS14/modsuit_showroom.yml — стенда для тестов МОД-костюмов.

Карту правят здесь и перегенерируют, а не редактируют руками: набор тем, модулей и ядер
меняется чаще, чем хочется двигать сущности в мапном редакторе.

    python Tools/_IS14/gen_modsuit_showroom.py

План (главный корпус слева, полигон справа):

    +-----------------------------------------------+  +--------------------+
    |  A  КОСТЮМЫ      12 контроллеров тем          |  | тёмный  | ВАКУУМ   |
    |  B  ЯДРА         ядра, ячейки, топливо        |  | коридор +----------+
    |  C  МОДУЛИ       все 37 модулей               |  | (света  | ЖАР/ХОЛОД|
    |  D  ИНСТРУМЕНТЫ  панель, замок, ремонт, урон  |  |  нет)   +----------+
    |  E  ПРОИЗВОДСТВО фабрикатор, наука, ящики     |  |         | РАДИАЦИЯ |
    |  F  ПИТАНИЕ      RTG -> подстанция -> ЛКП     |  |         +----------+
    +-----------------------------------------------+  |         | ПОРОДА   |
                                                        +--------------------+

Отдельной сеткой на севере висит платформа без гравитации — магботы и джетпак.
"""

import base64
import datetime
import io
import os
import struct

# ------------------------------------------------------------------ геометрия

# Главный корпус: рамка стен, внутренность 1..43 x 1..34.
MAIN = (0, 0, 44, 35)
# Полигон: пристройка справа, общая стена по x=44.
WING = (44, 0, 62, 35)
# Внутренняя стена полигона: коридор 45..47, камеры 49..61.
WING_INNER_X = 48
# Стены-перегородки между камерами.
WING_SPLITS = (26, 18, 10)

# Платформа невесомости — отдельная сетка, её мировые координаты.
PLATFORM_ORIGIN = (18, 43)
PLATFORM_SIZE = 7

# ------------------------------------------------------------------- контент

SUITS = [
    "IS14ModsuitStandard",
    "IS14ModsuitCivilian",
    "IS14ModsuitEngineering",
    "IS14ModsuitAtmospheric",
    "IS14ModsuitMedical",
    "IS14ModsuitMining",
    "IS14ModsuitResearch",
    "IS14ModsuitSecurity",
    "IS14ModsuitLoader",
    "IS14ModsuitMagnate",
    "IS14ModsuitSyndicate",
    "IS14ModsuitDebug",
]

CORES = [
    "IS14ModCoreStandard",
    "IS14ModCorePlasma",
    "IS14ModCoreInfinite",
]

# Порядок — по смыслу, а не по алфавиту: так на полу читается, чем модули отличаются.
MODULES = [
    # свет и обзор
    "IS14ModuleFlashlight",
    "IS14ModuleVisorMedical",
    "IS14ModuleVisorSecurity",
    "IS14ModuleVisorNight",
    "IS14ModuleVisorThermal",
    "IS14ModuleVisorDiagnostic",
    "IS14ModuleGps",
    "IS14ModuleTrayScanner",
    "IS14ModuleCrewMonitor",
    "IS14ModuleMegaphone",
    # хранение
    "IS14ModuleStorage",
    "IS14ModuleStorageLarge",
    "IS14ModuleOreBag",
    "IS14ModuleOrganizer",
    "IS14ModuleHolster",
    "IS14ModuleHatHolder",
    "IS14ModuleCompression",
    "IS14ModuleMouthhole",
    # защита
    "IS14ModuleArmorBooster",
    "IS14ModuleEmpShield",
    "IS14ModuleRadProtection",
    "IS14ModuleThermalRegulator",
    "IS14ModuleWeldingProtection",
    "IS14ModuleDnaLock",
    # передвижение
    "IS14ModuleMagboots",
    "IS14ModuleNoSlip",
    "IS14ModuleJetpack",
    "IS14ModuleKineticCharge",
    "IS14ModuleAtmosphericsSmall",
    "IS14ModuleAtmosphericsLarge",
    # инструменты
    "IS14ModuleDrill",
    "IS14ModuleConstructor",
    "IS14ModuleHealthAnalyzer",
    "IS14ModuleDefibrillator",
    "IS14ModuleInjector",
    "IS14ModuleCleaner",
    "IS14ModulePepper",
]

CRATES = [
    "IS14CrateModsuitStandard",
    "IS14CrateModsuitEngineering",
    "IS14CrateModsuitAtmospheric",
    "IS14CrateModsuitMedical",
    "IS14CrateModsuitSecurity",
    "IS14CrateModsuitMining",
    "IS14CrateModsuitResearch",
    "IS14CrateModsuitCores",
    "IS14CrateModsuitModules",
]

HEADER_COLOR = "#63C7FFFF"

# --------------------------------------------------------------------- каркас

class Grid:
    """Одна сетка: тайлы, сущности, декали, воздух."""

    def __init__(self, name, pos=None, gravity=True):
        self.name = name
        self.pos = pos
        self.gravity = gravity
        self.tiles = {}
        self.ents = []
        self.decals = []
        self.air = set()
        self.uid = None


class Builder:
    def __init__(self):
        self.tilemap = {}
        self.next_tile_id = 0
        self.grids = []
        self._next_uid = 10

    # ---- тайлы

    def tile_id(self, name):
        if name not in self.tilemap:
            self.tilemap[name] = self.next_tile_id
            self.next_tile_id += 1
        return self.tilemap[name]

    def uid(self):
        self._next_uid += 1
        return self._next_uid


B = Builder()
B.tile_id("Space")  # обязан быть нулевым: пустая клетка чанка пишется как 0

main = Grid("MOD showroom")
platform = Grid("MOD zero-g platform", pos=PLATFORM_ORIGIN, gravity=False)
B.grids = [main, platform]


def fill(grid, x0, y0, x1, y1, tile):
    """Заливка прямоугольника включительно."""
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            grid.tiles[(x, y)] = tile


def add(grid, proto, x, y, rot=None, comps=None, label=None,
        label_size=10, label_offset=(0, 30), label_color=None, label_sticky=False):
    """Сущность на клетке (x, y). label — подпись MapText над ней."""
    grid.ents.append({
        "proto": proto,
        "pos": (x + 0.5, y + 0.5),
        "rot": rot,
        "comps": comps or [],
        "label": label,
        "label_size": label_size,
        "label_offset": label_offset,
        "label_color": label_color,
        "label_sticky": label_sticky,
    })


def item(grid, proto, x, y):
    """Экспонат: лежит на полу, подписан своим id, подпись пропадает при взятии в руку."""
    add(grid, proto, x, y, label=proto)


def header(grid, text, x, y):
    """Заголовок зоны на стойке — чтобы в мешанине предметов было видно границы."""
    add(grid, "Rack", x, y, label=text, label_size=18,
        label_offset=(0, 46), label_color=HEADER_COLOR, label_sticky=True)


def sign(grid, proto, x, y, text, size=14, offset=(0, 40)):
    """Вывеска. Если на клетке уже что-то стоит — подписываем это, а не плодим дубль."""
    for e in grid.ents:
        if e["pos"] == (x + 0.5, y + 0.5):
            e["label"] = text
            e["label_size"] = size
            e["label_offset"] = offset
            e["label_color"] = HEADER_COLOR
            e["label_sticky"] = True
            return
    add(grid, proto, x, y, label=text, label_size=size,
        label_offset=offset, label_color=HEADER_COLOR, label_sticky=True)


def wall(grid, x, y, proto="WallSolid", tile="Plating"):
    """Одна стена. Корпуса стыкуются по общим стенам, поэтому клетка берётся один раз."""
    grid.tiles[(x, y)] = tile
    if any(e["pos"] == (x + 0.5, y + 0.5) and e["proto"].startswith("Wall")
           for e in grid.ents):
        return
    add(grid, proto, x, y)


def walls(grid, x0, y0, x1, y1, proto="WallSolid", tile="Plating"):
    """Рамка стен по периметру прямоугольника."""
    for x in range(x0, x1 + 1):
        for y in (y0, y1):
            wall(grid, x, y, proto, tile)
    for y in range(y0 + 1, y1):
        for x in (x0, x1):
            wall(grid, x, y, proto, tile)


def wall_line(grid, x0, y0, x1, y1, proto="WallSolid", tile="Plating"):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            wall(grid, x, y, proto, tile)


def door(grid, x, y, proto="AirlockGlass", tile="Plating"):
    """Дверь вместо стены: снимает стену с клетки и ставит шлюз."""
    grid.ents = [e for e in grid.ents
                 if not (e["pos"] == (x + 0.5, y + 0.5) and e["proto"].startswith("Wall"))]
    grid.tiles[(x, y)] = tile
    add(grid, proto, x, y)


def air(grid, x0, y0, x1, y1):
    for x in range(x0, x1 + 1):
        for y in range(y0, y1 + 1):
            grid.air.add((x, y))


def decal(grid, decal_id, x, y, color="#FFFFFFFF"):
    grid.decals.append((decal_id, x, y, color))


# ============================================================ ГЛАВНЫЙ КОРПУС

fill(main, 1, 1, 43, 34, "FloorSteel")
walls(main, *MAIN)

# Полы зон — чтобы границы читались без подписей.
fill(main, 1, 27, 43, 34, "FloorWhite")     # A костюмы
fill(main, 1, 23, 43, 26, "FloorShowroom")  # B ядра
fill(main, 1, 13, 43, 22, "FloorSteel")     # C модули
fill(main, 1, 7, 43, 12, "FloorTechMaint")  # D инструменты
fill(main, 1, 1, 43, 6, "FloorDark")        # E производство
fill(main, 36, 7, 43, 12, "FloorReinforced")  # F питание

air(main, 1, 1, 43, 34)

# ------------------------------------------------- A. Костюмы (12 тем)
header(main, "A  КОСТЮМЫ", 1, 33)
for i, proto in enumerate(SUITS):
    col, row = i % 6, i // 6
    item(main, proto, 4 + col * 6, 32 - row * 3)

# ------------------------------------------------- B. Ядра, ячейки, топливо
header(main, "B  ЯДРА И ЭНЕРГИЯ", 1, 26)
for i, proto in enumerate(CORES):
    item(main, proto, 4 + i * 5, 25)
for i, proto in enumerate(["PowerCellSmall", "PowerCellMedium", "PowerCellHigh"]):
    item(main, proto, 21 + i * 4, 25)
# Топливо плазменного ядра: листы и руда — ядро ест и то, и другое.
for i, proto in enumerate(["SheetPlasma", "PlasmaOre"]):
    item(main, proto, 34 + i * 4, 25)
add(main, "PowerCellRecharger", 42, 25, label="PowerCellRecharger",
    label_size=10, label_offset=(0, 34), label_sticky=True)

# ------------------------------------------------- C. Модули (все 37)
header(main, "C  МОДУЛИ", 1, 22)
for i, proto in enumerate(MODULES):
    col, row = i % 10, i // 10
    # Подписи длинные: чётные колонки поднимаем выше, нечётные опускаем — не наезжают.
    add(main, proto, 3 + col * 4, 21 - row * 2, label=proto,
        label_offset=(0, 34 if col % 2 == 0 else 18))

# ------------------------------------------------- D. Инструменты и обслуживание
header(main, "D  ИНСТРУМЕНТЫ", 1, 12)
# Панель проводов (отвёртка -> кусачки/мультитул), броня панели (лом), взлом.
tools_top = [
    "ToolDebug", "Screwdriver", "Wirecutter", "Multitool", "Crowbar",
    "NetworkConfigurator", "ToolboxElectricalFilled", "ClothingBeltUtilityFilled",
]
for i, proto in enumerate(tools_top):
    item(main, proto, 3 + i * 4, 11)
# Ремонт — пласталь и катушки; вскрытие — сварка; урон — отладочное оружие.
tools_bottom = [
    "SheetPlasteel", "CableApcStack", "CableMVStack", "CableHVStack", "PartRodMetal",
    "SheetSteel", "WelderExperimental", "MeleeDebug100", "MeleeDebug200",
    "WeaponPistolDebug", "EmpGrenade",
]
for i, proto in enumerate(tools_bottom):
    item(main, proto, 3 + i * 3, 8)
add(main, "WeldingFuelTankFull", 37, 8, label="WeldingFuelTankFull",
    label_size=10, label_offset=(0, 34), label_sticky=True)
# Замок костюма читает доступ — карта нужна, чтобы проверить и запирание, и отказ.
item(main, "CaptainIDCard", 40, 8)
# Расходники под модули, которым нужен предмет: шляпа — стабилизатору, еда и питьё —
# ротовому отверстию, ёмкости — инъектору, кобуре — ствол из ряда выше.
consumables = ["ClothingHeadHatWelding", "ClothingHeadHatBeret", "FoodBurgerBig",
               "DrinkColaCan", "Beaker", "ChemBag"]
for i, proto in enumerate(consumables):
    item(main, proto, 3 + i * 3, 9)

# Живые мишени: анализатор, дефибриллятор, инъектор.
add(main, "MobHuman", 34, 11, label="MobHuman", label_size=10,
    label_offset=(0, 40), label_sticky=True)
add(main, "MobHuman", 36, 11, label="MobHuman", label_size=10,
    label_offset=(0, 40), label_sticky=True)
# Грязь под модуль уборщика.
for i in range(10):
    decal(main, "DirtHeavy", 22 + i, 9)
    decal(main, "DirtMedium", 22 + i, 10)

# ------------------------------------------------- E. Производство и снабжение
header(main, "E  ПРОИЗВОДСТВО", 1, 6)
add(main, "ExosuitFabricator", 3, 5, label="ExosuitFabricator",
    label_size=10, label_offset=(0, 40), label_sticky=True)
add(main, "ResearchAndDevelopmentServer", 6, 5, label="R&D server",
    label_size=10, label_offset=(0, 40), label_sticky=True)
add(main, "ComputerResearchAndDevelopment", 9, 5, label="R&D console",
    label_size=10, label_offset=(0, 40), label_sticky=True)
mats = ["SheetSteel", "SheetGlass", "SheetPlastic", "SheetPlasma", "SheetPlasteel",
        "IngotGold", "IngotSilver", "SheetUranium", "MaterialDiamond"]
for i, proto in enumerate(mats):
    item(main, proto, 13 + i * 3, 5)
for i, proto in enumerate(CRATES):
    add(main, proto, 3 + i * 4, 2, label=proto, label_size=9,
        label_offset=(0, 40 if i % 2 == 0 else 24), label_sticky=True)

# ------------------------------------------------- F. Питание
# RTG -> ВВ -> подстанция -> СВ -> ЛКП. Кабели под полом: заодно мишень для t-ray.
sign(main, "GeneratorRTG", 38, 11, "F  ПИТАНИЕ", size=16, offset=(0, 46))
add(main, "SubstationBasic", 40, 11)
add(main, "APCBasic", 42, 11)

for x in range(38, 41):
    add(main, "CableHV", x, 11)
for x in range(40, 43):
    add(main, "CableMV", x, 11)

# Магистраль ЛКП: вдоль y=10 через весь корпус и вниз к фабрикатору.
lv = set()
for x in range(3, 43):
    lv.add((x, 10))
for y in range(5, 11):
    lv.add((3, y))
for x in range(3, 11):
    lv.add((x, 5))
for y in range(10, 12):
    lv.add((42, y))
for y in range(11, 26):
    lv.add((42, y))
for (x, y) in sorted(lv):
    add(main, "CableApcExtension", x, y)

# ------------------------------------------------- свет
# Лампы «всегда под напряжением»: свет не должен зависеть от того, цела ли проводка.
for x in range(4, 42, 6):
    add(main, "AlwaysPoweredWallLight", x, 34)               # у северной стены
    add(main, "AlwaysPoweredWallLight", x, 1, rot=3.141592653589793)
for y in range(4, 34, 6):
    # x=1 занят стойками-заголовками, поэтому западный ряд ламп стоит на клетку правее.
    add(main, "AlwaysPoweredWallLight", 2, y, rot=1.5707963267948966)
    add(main, "AlwaysPoweredWallLight", 43, y, rot=-1.5707963267948966)
# Середина зала: до неё стены не достают.
for x in (12, 22, 32):
    add(main, "AlwaysPoweredWallLight", x, 17)

# ------------------------------------------------- северный тамбур в космос
walls(main, 19, 35, 23, 39)
fill(main, 20, 36, 22, 38, "Plating")
air(main, 20, 36, 22, 38)
door(main, 21, 35, "AirlockExternalGlass")
air(main, 21, 35, 21, 35)
door(main, 21, 39, "AirlockExternalGlass")
add(main, "AlwaysPoweredWallLight", 21, 38)
sign(main, "WallSolid", 19, 37, "ВЫХОД К ПЛАТФОРМЕ", size=12, offset=(0, 40))

# ============================================================ ПОЛИГОН

walls(main, *WING)
fill(main, 45, 1, 61, 34, "FloorTechMaint")
wall_line(main, WING_INNER_X, 1, WING_INNER_X, 34)
for y in WING_SPLITS:
    wall_line(main, WING_INNER_X, y, 61, y)

door(main, 44, 18, "AirlockGlass")
air(main, 44, 18, 44, 18)
air(main, 45, 1, 47, 34)
sign(main, "WallSolid", 44, 20, "ПОЛИГОН  (света нет)", size=14, offset=(0, 40))

# Тёмный коридор — фонарь, ночной и тепловой визор. Грязь для уборщика заодно.
for i in range(10):
    decal(main, "DirtLight", 46, 4 + i * 3)

# --- камера 1: вакуум. Решётка — это isSpace: ходить можно, воздуха нет.
fill(main, 49, 27, 61, 34, "Lattice")
door(main, WING_INNER_X, 30, "AirlockExternalGlass")
sign(main, "WallSolid", WING_INNER_X, 32, "1  ВАКУУМ", size=14, offset=(40, 0))
add(main, "AtmosFixBlockerMarker", 55, 31)

# --- камера 2: жар и холод.
fill(main, 49, 19, 61, 25, "FloorReinforced")
fill(main, 56, 19, 61, 25, "FloorFreezer")
door(main, WING_INNER_X, 22, "AirlockGlass")
air(main, WING_INNER_X, 22, WING_INNER_X, 22)
air(main, 49, 19, 61, 25)
sign(main, "WallSolid", WING_INNER_X, 24, "2  ЖАР / ХОЛОД", size=14, offset=(40, 0))
add(main, "AtmosFixInstantPlasmaFireMarker", 51, 22)
add(main, "AtmosFixFreezerMarker", 59, 22)
item(main, "PlasmaCanister", 50, 20)
item(main, "OxygenCanister", 52, 20)
item(main, "AirCanister", 54, 20)

# --- камера 3: радиация. Битый RTG светит постоянно и никуда не девается.
fill(main, 49, 11, 61, 17, "FloorTechMaint")
door(main, WING_INNER_X, 14, "AirlockGlass")
air(main, WING_INNER_X, 14, WING_INNER_X, 14)
air(main, 49, 11, 61, 17)
sign(main, "WallSolid", WING_INNER_X, 16, "3  РАДИАЦИЯ", size=14, offset=(40, 0))
add(main, "GeneratorRTGDamaged", 55, 14, label="GeneratorRTGDamaged",
    label_size=10, label_offset=(0, 40), label_sticky=True)

# --- камера 4: порода под дрель и руда под плазменное ядро.
fill(main, 49, 1, 61, 9, "FloorMining")
door(main, WING_INNER_X, 5, "AirlockGlass")
air(main, WING_INNER_X, 5, WING_INNER_X, 5)
air(main, 49, 1, 61, 9)
sign(main, "WallSolid", WING_INNER_X, 7, "4  ПОРОДА", size=14, offset=(40, 0))
for x in range(53, 61):
    for y in range(2, 9):
        main.tiles[(x, y)] = "FloorAsteroidSand"
        add(main, "AsteroidRock", x, y)
item(main, "PlasmaOre", 50, 3)
item(main, "PlasmaOre1", 51, 3)

# ============================================================ ПЛАТФОРМА (0g)

fill(platform, 0, 0, PLATFORM_SIZE - 1, PLATFORM_SIZE - 1, "FloorSteel")
add(platform, "AlwaysPoweredWallLight", 3, PLATFORM_SIZE - 1)
add(platform, "AlwaysPoweredWallLight", 3, 0, rot=3.141592653589793)
sign(platform, "Rack", 3, 3, "НЕВЕСОМОСТЬ: магботы / джетпак", size=12, offset=(0, 46))

# ================================================================ сериализация

TILE_STRUCT = struct.Struct("<iBBB")


def chunk_tiles(grid, cx, cy):
    buf = bytearray()
    filled = False
    for ly in range(16):
        for lx in range(16):
            name = grid.tiles.get((cx * 16 + lx, cy * 16 + ly))
            if name is None:
                buf += TILE_STRUCT.pack(0, 0, 0, 0)
            else:
                filled = True
                buf += TILE_STRUCT.pack(B.tile_id(name), 0, 0, 0)
    return (base64.b64encode(bytes(buf)).decode("ascii") if filled else None)


def atmos_chunks(grid):
    """Воздух пишем прямо в карту: без этого зал грузится вакуумом до fixgridatmos."""
    chunks = {}
    for (x, y) in grid.air:
        if (x, y) not in grid.tiles:
            continue
        cx, cy = x // 4, y // 4
        bit = (x - cx * 4) + (y - cy * 4) * 4
        chunks[(cx, cy)] = chunks.get((cx, cy), 0) | (1 << bit)
    return chunks


def fmt_num(v):
    return ("%g" % v)


def emit_grid(grid, out, parent_uid):
    """Сущность-сетка со всеми чанками, атмосферой и декалями."""
    out.append("  - uid: %d" % grid.uid)
    out.append("    components:")
    out.append("    - type: MetaData")
    out.append("      name: %s" % grid.name)
    out.append("    - type: Transform")
    if grid.pos:
        out.append("      pos: %s,%s" % (fmt_num(grid.pos[0]), fmt_num(grid.pos[1])))
    out.append("      parent: %d" % parent_uid)
    out.append("    - type: MapGrid")
    out.append("      chunks:")
    cxs = sorted({x // 16 for (x, _) in grid.tiles})
    cys = sorted({y // 16 for (_, y) in grid.tiles})
    for cx in cxs:
        for cy in cys:
            data = chunk_tiles(grid, cx, cy)
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
    if grid.gravity:
        # Гравитация «своя»: генератор не нужен, и пропажа питания её не роняет.
        out.append("    - type: Gravity")
        out.append("      enabled: True")
        out.append("      inherent: True")
        out.append("      gravityShakeSound: !type:SoundPathSpecifier")
        out.append("        path: /Audio/Effects/alert.ogg")
    out.append("    - type: DecalGrid")
    out.append("      chunkCollection:")
    out.append("        version: 2")
    if not grid.decals:
        out.append("        nodes: []")
    else:
        out.append("        nodes:")
        by_id = {}
        for (did, x, y, color) in grid.decals:
            by_id.setdefault((did, color), []).append((x, y))
        n = 0
        for (did, color), coords in by_id.items():
            out.append("        - node:")
            out.append("            color: '%s'" % color)
            out.append("            id: %s" % did)
            out.append("          decals:")
            for (x, y) in coords:
                out.append("            %d: %d,%d" % (n, x, y))
                n += 1
    chunks = atmos_chunks(grid)
    if chunks:
        out.append("    - type: GridAtmosphere")
        out.append("      version: 2")
        out.append("      data:")
        out.append("        tiles:")
        for (cx, cy) in sorted(chunks):
            out.append("          %d,%d:" % (cx, cy))
            out.append("            0: %d" % chunks[(cx, cy)])
        out.append("        uniqueMixes:")
        out.append("        - volume: 2500")
        out.append("          temperature: 293.15")
        out.append("          moles:")
        for mole in [21.824879, 82.10312] + [0] * 10:
            out.append("          - %s" % fmt_num(mole))
        out.append("        chunkSize: 4")
        out.append("    - type: GasTileOverlay")
    out.append("    - type: GridPathfinding")
    out.append("    - type: RadiationGridResistance")
    out.append("    - type: SpreaderGrid")


def emit_entities(grid, out):
    """Сущности группами по прототипу — так же, как их пишет сам движок."""
    groups = {}
    order = []
    for e in grid.ents:
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
            if e["rot"] is not None:
                out.append("      rot: %s rad" % repr(e["rot"]))
            out.append("      pos: %s,%s" % (fmt_num(e["pos"][0]), fmt_num(e["pos"][1])))
            out.append("      parent: %d" % grid.uid)
            for line in e["comps"]:
                out.append(line)
            if e["label"]:
                out.append("    - type: MapText")
                # Кавычки обязательны: в заголовках есть двоеточия, и без них YAML рвётся.
                out.append("      text: '%s'" % e["label"].replace("'", "''"))
                out.append("      fontSize: %d" % e["label_size"])
                if e["label_color"]:
                    out.append("      color: '%s'" % e["label_color"])
                out.append("      offset: %d,%d" % e["label_offset"])
                if not e["label_sticky"]:
                    # Подпись мешает, как только предмет взяли в руку — пусть исчезает.
                    out.append("    - type: TriggerOnGotEquippedHand")
                    out.append("    - type: RemoveComponentsOnTrigger")
                    out.append("      triggerOnce: true")
                    out.append("      components:")
                    out.append("      - type: MapText")


def build():
    map_uid = 1
    main.uid = 2
    platform.uid = 3
    for grid in B.grids:
        for e in grid.ents:
            e["uid"] = B.uid()

    # Номера тайлов раздаём до записи tilemap: чанки сериализуются позже, а секция
    # tilemap идёт в файле раньше — ленивая нумерация оставила бы её пустой.
    for grid in B.grids:
        for name in sorted(set(grid.tiles.values())):
            B.tile_id(name)

    count = 2 + len(B.grids) - 1 + sum(len(g.ents) for g in B.grids)

    out = []
    out.append("# SPDX-License-Identifier: AGPL-3.0-or-later")
    out.append("#")
    out.append("# Стенд для тестов МОД-костюмов: темы, ядра, все модули, производство и полигон.")
    out.append("# Генерируется, руками не правится — Tools/_IS14/gen_modsuit_showroom.py")
    out.append("#")
    out.append("# Загрузка (mapinit обязателен: без него костюмы не разложат части, а карта стоит на паузе):")
    out.append("#   loadmap 100 /Maps/_IS14/modsuit_showroom.yml")
    out.append("#   mapinit 100")
    out.append("#   tp 22 18 100")
    out.append("#")
    out.append("# Зоны главного корпуса, снизу вверх по подписям на стойках у западной стены:")
    out.append("#   A костюмы (12 тем)   B ядра и ячейки   C все 37 модулей")
    out.append("#   D инструменты, ремонт, урон, мишени   E фабрикатор, наука, ящики   F питание")
    out.append("# Полигон справа за шлюзом: тёмный коридор, вакуум (решётка), жар/холод,")
    out.append("# радиация (битый RTG), порода под дрель. Платформа без гравитации — на севере,")
    out.append("# через тамбур: туда только на джетпаке, стоять на ней — только в магботах.")
    out.append("# Атмосферу можно пересобрать командой fixgridatmos по маркерам в камерах.")
    out.append("")
    out.append("meta:")
    out.append("  format: 7")
    out.append("  category: Map")
    out.append("  engineVersion: 270.1.0")
    out.append('  forkId: ""')
    out.append('  forkVersion: ""')
    out.append("  time: %s" % datetime.datetime.now().strftime("%m/%d/%Y %H:%M:%S"))
    out.append("  entityCount: %d" % count)
    out.append("maps:")
    out.append("- %d" % map_uid)
    out.append("grids:")
    for grid in B.grids:
        out.append("- %d" % grid.uid)
    out.append("orphans: []")
    out.append("nullspace: []")
    out.append("tilemap:")
    for name, tid in sorted(B.tilemap.items(), key=lambda kv: kv[1]):
        out.append("  %d: %s" % (tid, name))
    out.append("entities:")
    out.append('- proto: ""')
    out.append("  entities:")
    for grid in B.grids:
        emit_grid(grid, out, map_uid)
    out.append("  - uid: %d" % map_uid)
    out.append("    components:")
    out.append("    - type: MetaData")
    out.append("      name: IS14 modsuit showroom")
    out.append("    - type: Transform")
    out.append("    - type: Map")
    out.append("      mapPaused: False")
    out.append("    - type: GridTree")
    out.append("    - type: Broadphase")
    out.append("    - type: OccluderTree")
    for grid in B.grids:
        emit_entities(grid, out)

    return "\n".join(out) + "\n"


if __name__ == "__main__":
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    target = os.path.join(root, "Resources", "Maps", "_IS14", "modsuit_showroom.yml")
    text = build()
    with io.open(target, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote %s (%d lines)" % (target, text.count("\n")))
