"""Generates the IS14 research tree: technology prototypes, their IS14 data and the locale.

The tree is written here as a table rather than by hand in YAML for three reasons: positions on
the console map are derived from the shape of each branch (a centre with rays going out), each
node inherits the recipe unlocks of the upstream technologies it stands in for, and the design
rules are checked while generating instead of being hoped for.

Shape of a branch: one central topic, and from it several named rays in different directions —
anomalistics one way, xenoarchaeology another — each of which can fork sideways into lanes. The
generator turns (ray direction, depth, lane) into map coordinates, so adding a topic never means
hand-editing a coordinate table.

Rules enforced here, loudly, because a quiet violation is a balance bug:
  * tier 3 and above costs at least two currencies — no branch can be finished alone;
  * nothing in the ordinary tree depends on a hidden (randomised) variant;
  * no two nodes of a branch land on the same square;
  * every prerequisite, upstream source and recipe actually exists.

Run from the repo root:
    python Tools/_IS14/generate_research_tree.py
"""
import collections
import glob
import io
import os
import re

SCI, IND, MIL, BIO, SOC = 'Science', 'Industrial', 'Military', 'Biological', 'Social'

# Branch id -> (loc name, short ui tag, colour, discipline icon state)
BRANCHES = collections.OrderedDict([
    ('IS14Fundamental', ('Фундаментальные и прикладные науки', 'ФУН', '#9a6ef0', 'experimental')),
    ('IS14Materials', ('Материаловедение и производство', 'МАТ', '#eeac34', 'industrial')),
    ('IS14Energetics', ('Энергетика и атмосферные процессы', 'ЭНР', '#f07f3c', 'industrial')),
    ('IS14Medicine', ('Медицина и биохимия', 'МЕД', '#4bb8a5', 'biochemical')),
    ('IS14Cybernetics', ('Кибернетика и вычислительная техника', 'КИБ', '#5a9ad6', 'industrial')),
    ('IS14Armament', ('Вооружение и защита', 'ВОО', '#dc373b', 'arsenal')),
    ('IS14Society', ('Общество и быт', 'ОБЩ', '#7ecd48', 'civilianservices')),
])

# Ray direction -> (step, lane offset). Lanes run across the ray, so a fork sits beside its
# parent rather than on top of the next topic along.
DIRECTIONS = {
    'N': ((0, -1), (1, 0)),
    'NE': ((1, -1), (1, 1)),
    'E': ((1, 0), (0, 1)),
    'SE': ((1, 1), (1, -1)),
    'S': ((0, 1), (1, 0)),
    'SW': ((-1, 1), (-1, -1)),
    'W': ((-1, 0), (0, 1)),
    'NW': ((-1, -1), (-1, 1)),
}

# A node of the tree. `ray`/`depth`/`lane` place it on the map; `sources` are the upstream
# technologies whose recipes (and icon) it takes over; `recipes` are recipes named directly,
# which is how one upstream technology gets split across several of ours.
Node = collections.namedtuple(
    'Node',
    'id name depth lane prereqs costs sources icon_from recipes effects summary breakthrough '
    'exclusive hidden contraband tier',
)

Ray = collections.namedtuple('Ray', 'id name direction nodes')

Branch = collections.namedtuple('Branch', 'center rays')

# A breakthrough: the object that has to be destroyed in the analyzer before the topic opens.
# The same tuple describes contraband, which uncovers a hidden topic instead of unlocking a
# visible one — see IS14TechDataPrototype.Contraband.
Breakthrough = collections.namedtuple('Breakthrough', 'name samples icon hint point_type payout')


def node(id_, name, depth, costs, lane=0, prereqs=None, sources=(), icon_from=None, recipes=(),
         effects=(), summary='', breakthrough=None, exclusive=None, hidden=False, contraband=None,
         tier=None):
    return Node(id_, name, depth, lane, prereqs, costs, list(sources), icon_from, list(recipes),
                list(effects), summary, breakthrough, exclusive, hidden, contraband, tier)


def grant_points(point_type, amount, text):
    return ('IS14GrantPointsEffect', {'pointType': point_type, 'amount': amount}, text)


def grant_entity(prototype, text, count=1):
    fields = {'prototype': prototype}
    if count != 1:
        fields['count'] = count
    return ('IS14GrantEntityEffect', fields, text)


def modifier(name, delta, text):
    return ('IS14ModifierEffect', {'modifier': name, 'delta': delta}, text)


TREE = collections.OrderedDict()


# ─── Фундаментальные исследования ──────────────────────────────────────────
# Метрология в центре; приборы наверх, аномалистика наискось, ксеноархеология вправо,
# блюспейс вниз-вправо, радиология вниз. Слева — слоты наладки и методологии.
TREE['IS14Fundamental'] = Branch(
    center=node('IS14Metrology', 'Метрология и стандартизация', 0, {SCI: 15},
                summary='Единые эталоны измерений — точка, с которой начинается любая наука станции.',
                effects=[grant_points(SCI, 10, 'Разово начисляет 10 научных данных')]),
    rays=[
        Ray('instruments', 'Приборостроение', 'N', [
            node('IS14SpectralAnalysis', 'Спектральный анализ', 1, {SCI: 25},
                 summary='Разложение излучения по спектру — основа всей приборной науки.'),
            node('IS14LabAutomation', 'Лабораторная автоматика', 2, {SCI: 45, IND: 15},
                 summary='Стенды, которые ведут измерение сами и не врут от усталости.'),
            node('IS14PrecisionSensors', 'Прецизионная сенсорика', 3, {SCI: 70, IND: 30},
                 summary='Датчики, различающие то, что раньше считалось шумом.'),
            node('IS14ExperimentalPhysics', 'Экспериментальная физика', 4, {SCI: 130, IND: 45},
                 sources=['ExperimentalScience'],
                 summary='Установки, собранные под один единственный вопрос.',
                 effects=[grant_points(SCI, 30, 'Разово начисляет 30 научных данных')]),
            node('IS14StatisticalMethods', 'Статистическая обработка', 2, {SCI: 40}, lane=1,
                 prereqs=['IS14SpectralAnalysis'],
                 summary='Как отличить результат от совпадения.'),
            node('IS14ScientificRecords', 'Научная документация', 3, {SCI: 50, SOC: 20}, lane=1,
                 prereqs=['IS14StatisticalMethods'], sources=['AlternativeResearch'],
                 summary='Архив, протоколы и диски технологий: результат, который можно передать.'),
        ]),
        Ray('anomalies', 'Аномалистика', 'NE', [
            node('IS14FieldTheory', 'Теория поля', 1, {SCI: 35, IND: 10}, sources=['MagnetsTech'],
                 summary='Математика полей: без неё нет ни гравитации, ни эмиттеров.'),
            node('IS14AnomalousPhysics', 'Аномальная физика', 2, {SCI: 60, IND: 25},
                 sources=['BasicAnomalousResearch'],
                 summary='Работа с объектами, нарушающими известные законы.'),
            node('IS14AnomalyContainment', 'Удержание аномалий', 3, {SCI: 90, IND: 45},
                 sources=['AnomalyCoreHarnessing'],
                 summary='Сосуды, синхронизаторы и регламент: аномалия работает, а не убивает.'),
            node('IS14AnomalyCoreTheory', 'Теория аномального ядра', 4, {SCI: 60, IND: 30},
                 summary='Что остаётся от аномалии и как это читать. Нужно ядро — любое.',
                 breakthrough=Breakthrough(
                     name='ядро аномалии (любого типа)',
                     samples=['BaseAnomalyCore'],
                     icon='AnomalyCoreGravity',
                     hint='Остаётся от погашенной аномалии.',
                     point_type=SCI, payout=50)),
            node('IS14AppliedAnomalistics', 'Прикладная аномалистика', 5, {SCI: 140, IND: 60, MIL: 30},
                 sources=['AdvancedAnomalyResearch'],
                 summary='Аномалия как инструмент и как оружие. Дальше только осторожность.'),
            node('IS14Gravimetry', 'Гравиметрия', 3, {SCI: 65, IND: 25}, lane=1,
                 prereqs=['IS14AnomalousPhysics'],
                 recipes=['WeaponForceGun', 'WeaponTetherGun'],
                 summary='Поле тяжести как инструмент: силовая пушка и гравитационный захват.'),
        ]),
        Ray('xenoarch', 'Ксеноархеология', 'E', [
            node('IS14ArtifactStudies', 'Артефактология', 1, {SCI: 30},
                 icon_from='BasicXenoArcheology',
                 recipes=['NodeScanner', 'AnalysisComputerCircuitboard',
                          'ArtifactAnalyzerMachineCircuitboard'],
                 summary='Систематика чужих артефактов. Даёт аналитическую консоль и анализатор артефактов.'),
            node('IS14ArtifactStructure', 'Структура артефактов', 2, {SCI: 55, IND: 20},
                 icon_from='AbnormalArtifactManipulation',
                 recipes=['ArtifactCrusherMachineCircuitboard'],
                 summary='Узлы, связи и предел прочности. Даёт дробитель артефактов.'),
            node('IS14NodeCartography', 'Узловая картография', 3, {SCI: 75, IND: 25},
                 sources=['Pinpointers'],
                 summary='Карта узлов артефакта и пеленгаторы к ней: искать стало осмысленно.'),
            node('IS14AncientTechnology', 'Древние технологии', 4, {SCI: 70, IND: 35},
                 summary='Чужая инженерия в разобранном виде. Нужна целая реликвия.',
                 breakthrough=Breakthrough(
                     name='реликвия ксеноархеологии',
                     samples=['BaseXenoArtifactItem', 'ArtifactFragment'],
                     icon='ArtifactFragment1',
                     hint='Привозят с экспедиций и находят в руинах.',
                     point_type=SCI, payout=60)),
            node('IS14ArtifactReconstruction', 'Реконструкция устройств', 5, {SCI: 150, IND: 70},
                 summary='Повторить то, что собрали не люди. Иногда даже работает.',
                 effects=[grant_points(SCI, 50, 'Разово начисляет 50 научных данных')]),
            node('IS14FieldArchaeology', 'Полевая археология', 2, {SCI: 45, IND: 15}, lane=1,
                 prereqs=['IS14ArtifactStudies'],
                 summary='Как достать находку, не превратив её в осколки.'),
        ]),
        Ray('bluespace', 'Блюспейс-технологии', 'SE', [
            node('IS14SubspaceMath', 'Подпространственная математика', 1, {SCI: 50},
                 summary='Описание того, чего не видно. Пока только на бумаге.'),
            node('IS14BluespaceTheory', 'Теория блюспейса', 2, {SCI: 90, IND: 30},
                 sources=['BluespaceTheory'],
                 summary='Фундамент всей блюспейс-техники. Без кристалла — только теория.',
                 breakthrough=Breakthrough(
                     name='блюспейс-кристалл',
                     samples=['MaterialBSCrystal'],
                     icon='MaterialBSCrystal1',
                     hint='Снабжение возит под заказ, шахтёры находят в астероидах.',
                     point_type=SCI, payout=60)),
            node('IS14BluespaceNavigation', 'Блюспейс-навигация', 3, {SCI: 110, IND: 50},
                 sources=['BluespaceMining'],
                 summary='Прокладка курса там, где нет расстояний.'),
            node('IS14Desynchronisation', 'Десинхронизация', 4, {SCI: 140, IND: 60}, lane=-1,
                 prereqs=['IS14BluespaceNavigation'], sources=['BluespaceTimeManipulation'],
                 summary='Сдвиг объекта во времени на доли секунды. Выглядит как исчезновение.'),
            node('IS14QuantumTransport', 'Квантовый транспорт', 4, {SCI: 180, IND: 90, MIL: 30},
                 sources=['QuantumLeaping'],
                 summary='Перемещение материи без прохождения пути между точками.',
                 effects=[grant_points(SCI, 40, 'Разово начисляет 40 научных данных')]),
            node('IS14BluespaceComms', 'Блюспейс-связь', 3, {SCI: 85, SOC: 35}, lane=1,
                 prereqs=['IS14BluespaceTheory'],
                 summary='Канал, которому всё равно, где находится собеседник.'),
        ]),
        # Первый отдел: эти темы не существуют на карте, пока СБ не сдаст трофей.
        # Ни одна обычная тема от них не зависит — иначе дерево зависело бы от того,
        # поймали ли в этом раунде предателя.
        Ray('xenobio', 'Ксенобиология', 'S', [
            node('IS14Xenobiology', 'Ксенобиология', 1, {BIO: 30, SCI: 15},
                 sources=['Xenobiology'],
                 summary='Чужая биология: слаймы, выводки, поведение.'),
            node('IS14XenoCompatibility', 'Ксеносовместимость', 2, {BIO: 70, SCI: 30},
                 sources=['XenoCompatibility'],
                 summary='Можно ли совместить чужую ткань с человеческой. Нужен образец ткани.',
                 breakthrough=Breakthrough(
                     name='образец ткани ксеноморфа',
                     samples=['FoodMeatXeno', 'FoodMeatRouny'],
                     icon='FoodMeatXeno',
                     hint='Срезают с туши ксеноморфа. Живой образец брать не советуем.',
                     point_type=BIO, payout=60)),
            node('IS14Slimeology', 'Слаймология', 3, {BIO: 100, SCI: 35},
                 sources=['XenobagHolding'],
                 summary='Содержание и скрещивание слаймов без потерь среди персонала.'),
            node('IS14Biosynthesis', 'Биосинтез', 4, {BIO: 90, IND: 40},
                 summary='Выращивать материал, а не добывать. Нужна ткань слайма.',
                 breakthrough=Breakthrough(
                     name='ткань слайма',
                     samples=['FoodMeatSlime'],
                     icon='FoodMeatSlime',
                     hint='Остаётся от слайма. Ксенобиолог поделится, если попросить.',
                     point_type=BIO, payout=50)),
            node('IS14ExoticFauna', 'Экзотическая фауна', 3, {BIO: 95, MIL: 30}, lane=1,
                 prereqs=['IS14Xenobiology'],
                 summary='Лаваландская живность: чем опасна и что из неё можно сделать.'),
        ]),
        Ray('robotics', 'Робототехника', 'SW', [
            node('IS14Servomechanics', 'Сервомеханика', 1, {IND: 30},
                 sources=['RipleyAPLU'],
                 summary='Приводы и шасси: от погрузчика до экзоскелета.'),
            node('IS14CargoExosuits', 'Грузовые экзокостюмы', 2, {IND: 55, SCI: 15},
                 sources=['Ripley2'],
                 summary='Техника, которая носит вместо грузчика.'),
            node('IS14CritterMechs', 'Малые шасси', 2, {IND: 45, SOC: 15}, lane=-1,
                 prereqs=['IS14Servomechanics'], sources=['CritterMechs'],
                 summary='Лёгкие мехи под одного оператора — от погрузки до уборки.'),
            node('IS14ExosuitEngineering', 'Экзоскелетостроение', 3, {IND: 110, SCI: 45},
                 sources=['Clarke'],
                 summary='Тяжёлые несущие каркасы с внешним питанием.'),
            node('IS14KineticModules', 'Кинетические модули', 3, {IND: 90, MIL: 30}, lane=-1,
                 prereqs=['IS14ExosuitEngineering'], sources=['KineticModifications'],
                 summary='Навесное горное и ударное оснащение для экзоскелетов.'),
            node('IS14CombatExosuits', 'Боевые экзоскелеты', 4, {IND: 150, MIL: 90},
                 sources=['Gygax'],
                 summary='Экзоскелет, собранный под бой, а не под склад.'),
            node('IS14HeavyExosuits', 'Тяжёлые экзоскелеты', 5, {IND: 190, MIL: 120, SCI: 50},
                 sources=['Durand'],
                 summary='Предел того, что станция способна собрать и прокормить энергией.'),

            node('IS14OnboardSystems', 'Бортовые системы', 2, {IND: 30, SCI: 10}, lane=1,
                 prereqs=['IS14Servomechanics'],
                 summary='Питание, связь и телеметрия машины, которая ходит сама.'),
            node('IS14BorgChassis', 'Шасси кибернетических организмов', 3, {IND: 60, SCI: 25}, lane=1,
                 summary='Корпус, в который можно поставить мозг — чей угодно.'),
            node('IS14BorgModules', 'Модульное оснащение', 4, {IND: 95, SCI: 35}, lane=1,
                 recipes=['BorgModuleAdvancedTool', 'BorgModuleAdvancedMining',
                          'BorgModuleLollypop'],
                 summary='Один корпус, десять профессий — вопрос модуля.'),
        ]),
        Ray('firstdepartment', 'Первый отдел', 'W', [
            node('IS14SecretEnergetics', 'Трофейная энергетика', 1, {MIL: 120, SCI: 60},
                 prereqs=['IS14Metrology'], hidden=True, tier=3,
                 summary='Разбор чужого энергетического клинка: как они держат плазму в руке.',
                 effects=[modifier('ResearchPayoutMilitary', 0.2,
                                   'Военные измерения приносят на 20% больше'),
                          grant_points(MIL, 40, 'Разово начисляет 40 военных данных')],
                 contraband=Breakthrough(
                     name='энергетический клинок',
                     samples=['EnergySword'],
                     icon='EnergySword',
                     hint='Изымается у предателей. Сдаёт обычно СБ — если захочет.',
                     point_type=MIL, payout=80)),
            node('IS14SecretInfiltration', 'Схемы проникновения', 2, {SCI: 120, IND: 50},
                 prereqs=['IS14Metrology'], hidden=True, tier=3,
                 summary='Что именно делает с платой криптографический секвенсор.',
                 effects=[modifier('LatheSpeedScience', -0.15,
                                   'Научные латы печатают на 15% быстрее'),
                          modifier('ResearchPayoutScience', 0.1,
                                   'Научные измерения приносят на 10% больше')],
                 contraband=Breakthrough(
                     name='криптографический секвенсор',
                     samples=['Emag'],
                     icon='Emag',
                     hint='Та самая карточка. Изымают при обыске.',
                     point_type=SCI, payout=80)),
            node('IS14SecretCamouflage', 'Маскировочные поля', 3, {MIL: 110, SCI: 70},
                 prereqs=['IS14Metrology'], hidden=True, tier=3,
                 summary='Проектор чужого облика: поле, которое врёт глазам и камерам.',
                 effects=[modifier('ResearchPayoutMilitary', 0.15,
                                   'Военные измерения приносят на 15% больше')],
                 contraband=Breakthrough(
                     name='хамелеон-проектор',
                     samples=['ChameleonProjector'],
                     icon='ChameleonProjector',
                     hint='Снимают с агентов вместе с остальным набором.',
                     point_type=MIL, payout=70)),
            node('IS14SecretHardware', 'Трофейная оснастка', 4, {IND: 130, MIL: 50},
                 prereqs=['IS14Metrology'], hidden=True, tier=3,
                 summary='Синдикатский инструмент разобран до винта — и кое-что из этого повторимо.',
                 effects=[modifier('LatheSpeedIndustrial', -0.15,
                                   'Промышленные латы печатают на 15% быстрее'),
                          modifier('LatheMaterialIndustrial', -0.1,
                                   'Промышленные латы расходуют на 10% меньше материалов')],
                 contraband=Breakthrough(
                     name='синдикатские челюсти жизни',
                     samples=['SyndicateJawsOfLife'],
                     icon='SyndicateJawsOfLife',
                     hint='Из набора диверсанта. Тяжёлые, красные, очень быстрые.',
                     point_type=IND, payout=80)),
        ]),
        Ray('funding', 'Финансирование', 'NW', [
            node('IS14GosplanGrant', 'Грант Госплана', 1, {SCI: 30},
                 exclusive='funding',
                 summary='Разовая дотация под отчёт: данные сейчас, а не когда-нибудь.',
                 effects=[grant_points(SCI, 60, 'Разово начисляет 60 научных данных'),
                          grant_points(IND, 30, 'Разово начисляет 30 промышленных данных')]),
            node('IS14PlannedFunding', 'Плановое финансирование', 2, {SCI: 30},
                 prereqs=['IS14Metrology'], exclusive='funding',
                 summary='Скучный годовой план вместо аврала: все приборы отдают больше.',
                 effects=[modifier('ResearchPayout', 0.2,
                                   'Все измерения приносят на 20% больше данных')]),
        ]),
    ],
)


# ─── Материаловедение и производство ───────────────────────────────────────
TREE['IS14Materials'] = Branch(
    center=node('IS14Metallurgy', 'Металлургия', 0, {IND: 15},
                summary='Сплавы, режимы плавки и контроль качества — начало любого производства.'),
    rays=[
        Ray('alloys', 'Материаловедение', 'N', [
            node('IS14MetalScience', 'Металловедение', 1, {IND: 25},
                 summary='Структура металла под микроскопом: почему сплав держит и когда он лопнет.'),
            node('IS14Composites', 'Композитные материалы', 2, {IND: 55, SCI: 20},
                 sources=['MechanicalCompression'],
                 summary='Слоистые и армированные материалы под нагрузку.'),
            node('IS14HighStrengthAlloys', 'Высокопрочные сплавы', 3, {IND: 100, SCI: 40},
                 summary='Сплавы, которые держат то, что не держит сталь.'),
            node('IS14Metamaterials', 'Метаматериалы', 4, {IND: 150, SCI: 80},
                 summary='Материал со свойствами, которых нет ни у одного его компонента.'),
            node('IS14SurfaceTreatment', 'Поверхностная обработка', 2, {IND: 45}, lane=1,
                 prereqs=['IS14MetalScience'],
                 summary='Закалка, напыление и покрытия: деталь живёт дольше смены.'),
        ]),
        Ray('mining', 'Горное дело', 'NE', [
            node('IS14Geology', 'Геология и разведка', 1, {IND: 20, SCI: 10},
                 sources=['SpaceScanning'],
                 summary='Разведка породы: снабжение возит меньше пустого груза.'),
            node('IS14OreProcessing', 'Обогащение руды', 2, {IND: 50, SCI: 15},
                 sources=['SalvageEquipment'],
                 summary='Больше металла из той же породы. И инструмент шахтёра.'),
            node('IS14DeepExcavation', 'Глубокая выемка', 3, {IND: 95, SCI: 30},
                 sources=['MassExcavation'],
                 recipes=['MiningDrillDiamond', 'MechEquipmentDrillDiamond',
                          'AdvancedMineralScannerEmpty', 'OreBagOfHolding'],
                 summary='Алмазный бур, глубинный сканер и сумка, в которую влезает жила.'),
            node('IS14MiningLogistics', 'Шахтная логистика', 3, {IND: 80, SOC: 25}, lane=1,
                 prereqs=['IS14OreProcessing'],
                 summary='Поток руды без пробок на складе.'),
        ]),
        Ray('shop', 'Цех и автоматизация', 'E', [
            node('IS14PrecisionMachining', 'Точная механообработка', 1, {IND: 25},
                 sources=['AdvancedToolsTechnology'],
                 summary='Допуски, оснастка и метрология цеха.'),
            node('IS14IndustrialAutomation', 'Промышленная автоматизация', 2, {IND: 60, SCI: 25},
                 sources=['IndustrialEngineering'],
                 summary='Линии без человека: меньше брака и ручных операций.'),
            node('IS14AdditiveFabrication', 'Аддитивное производство', 3, {IND: 130, SCI: 45},
                 sources=['OptimizedMicrogalvanism'],
                 summary='Послойный синтез изделий вместо обработки заготовки.'),
            node('IS14FlexibleLines', 'Гибкие производственные линии', 4, {IND: 170, SCI: 70},
                 summary='Цех, который переналаживается под заказ, а не под план.'),
            node('IS14QualityControl', 'Технический контроль', 2, {IND: 45, SOC: 15}, lane=1,
                 prereqs=['IS14PrecisionMachining'],
                 summary='Военпред в цеху: брак не уходит на станцию.'),
        ]),
        Ray('logistics', 'Трубы и склад', 'SE', [
            node('IS14Hydraulics', 'Гидравлика и трубопроводы', 1, {IND: 25},
                 sources=['Plumbing'],
                 summary='Насосы, арматура и всё, что гоняет жидкость по станции.'),
            node('IS14WarehouseAutomation', 'Складская автоматика', 2, {IND: 55, SOC: 20},
                 sources=['BluespaceCargoTransport'],
                 summary='Склад, который сам знает, где что лежит.'),
            node('IS14BluespaceContainment', 'Блюспейс-контейнеры', 3, {IND: 110, SCI: 70},
                 prereqs=['IS14WarehouseAutomation', 'IS14BluespaceTheory'],
                 sources=['BluespaceStorage', 'BluespaceConstructionStorage'],
                 summary='Объём больше, чем занимаемое место. Требует теории блюспейса.'),
        ]),
        Ray('tools', 'Инструмент и оснастка', 'S', [
            node('IS14PowerTools', 'Силовой инструмент', 1, {IND: 30},
                 recipes=['PowerDrill', 'JawsOfLife', 'WelderExperimental'],
                 summary='Гидравлические ножницы, перфоратор и опытный сварочник.'),
            node('IS14CuttingWelding', 'Сварка и резка', 2, {IND: 50, SCI: 15},
                 summary='Соединить и разделить металл быстрее, чем руками.'),
            node('IS14StructuralEngineering', 'Строительные конструкции', 3, {IND: 90, SCI: 25},
                 sources=['FauxAstroTiles'],
                 summary='Плиты, каркасы и отделка: станция растёт по проекту.'),
        ]),
        Ray('shoptuning', 'Наладка промышленного цеха', 'NW', [
            node('IS14ShopSpeed', 'Цех: форсированный режим', 1, {IND: 35},
                 exclusive='industrial_shop',
                 summary='Автолаты и экзофабрика гонят быстрее. Материал считать не будем.',
                 effects=[modifier('LatheSpeedIndustrial', -0.25,
                                   'Промышленные латы печатают на 25% быстрее')]),
            node('IS14ShopThrift', 'Цех: экономный режим', 1, {IND: 35}, lane=1,
                 prereqs=['IS14Metallurgy'], exclusive='industrial_shop',
                 summary='Те же станки, но расход материала по норме, а не по факту.',
                 effects=[modifier('LatheMaterialIndustrial', -0.25,
                                   'Промышленные латы расходуют на 25% меньше материалов')]),
        ]),
    ],
)


# ─── Энергетика и атмосферные процессы ─────────────────────────────────────
TREE['IS14Energetics'] = Branch(
    center=node('IS14Thermodynamics', 'Техническая термодинамика', 0, {IND: 20},
                summary='Расчёт тепловых процессов: основа и энергетики, и атмосферной техники.'),
    rays=[
        Ray('power', 'Электроэнергетика', 'N', [
            node('IS14PowerEngineering', 'Генерация энергии', 1, {IND: 25},
                 sources=['PowerGeneration'],
                 summary='Генераторы, солнечные панели и эмиттеры.'),
            node('IS14EnergyStorage', 'Накопление энергии', 2, {IND: 50, SCI: 15},
                 sources=['AdvancedPowercells'],
                 summary='Ёмкие ячейки: энергия есть и после того, как генератор встал.'),
            node('IS14GridEngineering', 'Электрические сети', 3, {IND: 85, SCI: 30},
                 summary='Распределение и защита: одна авария не гасит станцию целиком.'),
            node('IS14FusionPower', 'Термоядерная энергетика', 4, {IND: 140, SCI: 60},
                 sources=['AdvancedPowerGeneration'],
                 summary='Энергия синтеза: дорого, опасно, окупается.'),
        ]),
        Ray('supermatter', 'Физика суперматерии', 'NE', [
            node('IS14CrystalPhysics', 'Кристаллофизика', 1, {SCI: 35, IND: 15},
                 summary='Решётка, дефекты и то, почему кристалл вообще держится.'),
            node('IS14SupermatterPhysics', 'Физика суперматерии', 2, {SCI: 80, IND: 40},
                 summary='Что происходит внутри кристалла. Нужен настоящий осколок.',
                 breakthrough=Breakthrough(
                     name='осколок суперматерии',
                     samples=['SupermatterSliver'],
                     icon='SupermatterSliver',
                     hint='Откалывают от станционного кристалла. Радиоактивен — не носить в кармане.',
                     point_type=SCI, payout=70)),
            node('IS14SupermatterEngineering', 'Суперматериальная энергетика', 3,
                 {IND: 150, SCI: 90, MIL: 30},
                 summary='Энергия на пределе того, что станция способна удержать.'),
        ]),
        Ray('atmos', 'Атмосферные процессы', 'E', [
            node('IS14AtmosphericTech', 'Атмосферная техника', 1, {IND: 25},
                 sources=['AtmosphericTech'],
                 summary='Теплообменники, рециркуляция и контроль состава.'),
            node('IS14AdvancedAtmospherics', 'Продвинутый атмос', 2, {IND: 60, SCI: 25},
                 sources=['AdvancedAtmospherics'],
                 summary='Морозильники на пределе, скрубберы и голофаны.'),
            node('IS14GasMixtures', 'Газовые смеси', 3, {IND: 90, MIL: 30},
                 summary='Смеси под задачу: от дыхательной до той, которой рвут полигон.'),
            node('IS14TritiumProduction', 'Производство трития', 4, {IND: 130, SCI: 60, MIL: 40},
                 summary='Премиальное топливо орднанса и термояда, выращенное в атмосе.'),
        ]),
        Ray('cryo', 'Криогеника', 'SE', [
            node('IS14Cryogenics', 'Криогеника', 1, {IND: 30, BIO: 15},
                 sources=['BiochemicalStasis'],
                 summary='Глубокий холод: стазис, консервация, криокапсулы.'),
            node('IS14Superconductivity', 'Сверхпроводимость', 2, {IND: 65, SCI: 30},
                 summary='Ток без потерь, пока хватает холода.'),
        ]),
        Ray('plasma', 'Физика плазмы', 'S', [
            node('IS14PlasmaPhysics', 'Физика плазмы', 1, {IND: 35, SCI: 20},
                 summary='Удержание и диагностика высокотемпературной плазмы.'),
            node('IS14PlasmaContainment', 'Магнитное удержание', 2, {IND: 70, SCI: 35},
                 prereqs=['IS14PlasmaPhysics', 'IS14FieldTheory'],
                 summary='Поле вместо стенки. Требует теории поля.'),
            node('IS14GravityControl', 'Гравитационные системы', 3, {SCI: 100, IND: 80},
                 sources=['GravityManipulation'],
                 summary='Управляемое искривление поля тяжести.'),
        ]),
        Ray('thrusters', 'Движители', 'SW', [
            node('IS14Propulsion', 'Реактивные движители', 1, {IND: 35, SCI: 15},
                 sources=['Shuttlecraft'],
                 summary='Двигатели, гироскопы и консоли: шаттл слушается руля.'),
            node('IS14InertialSystems', 'Инерционные системы', 2, {IND: 70, SCI: 30},
                 summary='Стабилизация корпуса и компенсация перегрузок.'),
        ]),
        Ray('radiology', 'Радиология', 'W', [
            node('IS14Dosimetry', 'Дозиметрия', 1, {SCI: 25, BIO: 10},
                 summary='Считать излучение раньше, чем его посчитает врач.'),
            node('IS14RadiationShielding', 'Радиационная защита', 2, {SCI: 50, IND: 25},
                 summary='Экранирование, регламент и то, что остаётся от нарушителя регламента.'),
            node('IS14IsotopeSources', 'Изотопные источники', 3, {SCI: 80, IND: 40},
                 summary='Излучение как источник энергии и как инструмент измерения.'),
        ]),
    ],
)


# ─── Медицина и биохимия ───────────────────────────────────────────────────
TREE['IS14Medicine'] = Branch(
    center=node('IS14Pathology', 'Патологическая анатомия', 0, {BIO: 15},
                summary='Систематика причин смерти. Выдаёт первый патологоанатомический сканер.',
                effects=[grant_entity('IS14PathologyScanner',
                                      'Выдаёт опытный патологоанатомический сканер')]),
    rays=[
        Ray('pharma', 'Фармакология', 'N', [
            node('IS14Pharmacology', 'Фармакология', 1, {BIO: 25},
                 sources=['MedipenFilling'],
                 summary='Синтез и дозирование лекарственных средств.'),
            node('IS14AdvancedTreatment', 'Продвинутая терапия', 2, {BIO: 55, SCI: 20},
                 sources=['AdvancedTreatment'],
                 summary='Препараты и приборы, вытаскивающие с того света.'),
            node('IS14CombatPharmacology', 'Боевая фармакология', 3, {BIO: 100, MIL: 50},
                 summary='Стимуляторы под нагрузкой. Абонемент выдаётся вместе с печенью.'),
            node('IS14BluespaceChemistry', 'Блюспейс-химия', 4, {BIO: 140, SCI: 80},
                 prereqs=['IS14CombatPharmacology', 'IS14BluespaceTheory'],
                 sources=['BluespaceChemistry', 'ServiceBluespaceChemistry'],
                 summary='Реакции, которым не нужен объём. Требует теории блюспейса.'),
        ]),
        Ray('immuno', 'Иммунология', 'NE', [
            node('IS14Immunology', 'Иммунология', 1, {BIO: 30},
                 sources=['MedicalDefense'],
                 summary='Защита организма: вакцины, антидоты, барьерная медицина.'),
            node('IS14Epidemiology', 'Эпидемиология', 2, {BIO: 60, SOC: 20},
                 sources=['RescueTechnology'],
                 summary='Болезнь как процесс на станции, а не в одном пациенте.'),
            node('IS14VaccineProduction', 'Производство вакцин', 3, {BIO: 110, IND: 40},
                 summary='От штамма до ампулы на весь экипаж.'),
        ]),
        Ray('surgery', 'Хирургия', 'E', [
            node('IS14SurgicalTechnology', 'Хирургические технологии', 1, {BIO: 30, IND: 10},
                 sources=['Autodoc'],
                 summary='Инструмент и автоматика операционной.'),
            node('IS14MechanizedTreatment', 'Механизированная помощь', 2, {BIO: 55, IND: 30},
                 sources=['MechanizedTreatment'],
                 summary='Модули для боргов и автоинъекторы: помощь приходит сама.'),
            node('IS14HighEndSurgery', 'Высшая хирургия', 3, {BIO: 105, IND: 45},
                 sources=['HighEndSurgery'],
                 recipes=['MedicalScannerMachineCircuitboardRecipe'],
                 summary='Операции, которые раньше считались вскрытием.'),
            node('IS14TissueEngineering', 'Тканевая инженерия', 4, {BIO: 160, IND: 80, SCI: 50},
                 summary='Выращивание органов и тканей под пациента.'),
        ]),
        Ray('genetics', 'Генетика', 'SE', [
            node('IS14Genetics', 'Генетика', 1, {BIO: 35, SCI: 15},
                 summary='Чтение генома и первые осмысленные правки.'),
            node('IS14Cloning', 'Клонирование', 2, {BIO: 80, IND: 35},
                 sources=['Cloning'],
                 recipes=['CloningPodMachineCircuitboardRecipe',
                          'CloningConsoleComputerCircuitboardRecipe'],
                 summary='Воссоздание организма по записи. Этику обсудим потом.'),
            node('IS14Mutagenesis', 'Мутагенез', 3, {BIO: 120, SCI: 50},
                 summary='Направленная мутация: иногда получается именно то, что хотели.'),
        ]),
        Ray('medtuning', 'Наладка медицинского цеха', 'NW', [
            node('IS14MedShopSpeed', 'Медцех: поток', 1, {BIO: 35},
                 exclusive='medical_shop',
                 summary='Медлаты печатают быстрее — реанимация не ждёт норматива.',
                 effects=[modifier('LatheSpeedMedical', -0.25,
                                   'Медицинские латы печатают на 25% быстрее')]),
            node('IS14MedShopThrift', 'Медцех: нормирование', 1, {BIO: 35}, lane=1,
                 prereqs=['IS14Pathology'], exclusive='medical_shop',
                 summary='Каждый грамм биомассы под отчёт. Медленнее, зато хватает на смену.',
                 effects=[modifier('LatheMaterialMedical', -0.25,
                                   'Медицинские латы расходуют на 25% меньше материалов')]),
        ]),
    ],
)


# ─── Кибернетика и робототехника ───────────────────────────────────────────
TREE['IS14Cybernetics'] = Branch(
    center=node('IS14ControlSystems', 'Системы автоматического управления', 0, {IND: 20},
                sources=['BasicRobotics'],
                summary='Регуляторы и обратные связи — азбука роботостроения.'),
    rays=[
        Ray('cyber', 'Кибернетика живого', 'N', [
            node('IS14Cybernetics', 'Кибернетические имплантаты', 1, {IND: 25, BIO: 20},
                 prereqs=['IS14ControlSystems', 'IS14SurgicalTechnology'],
                 sources=['BasicCybernetics'],
                 summary='Сопряжение техники с живой тканью. Требует хирургии.'),
            node('IS14Augmentation', 'Протезирование', 2, {IND: 45, BIO: 35}, lane=-1,
                 prereqs=['IS14Cybernetics'], sources=['BasicAugmentation'],
                 summary='Конечности и органы, которые можно поставить вместо утраченных.'),
            node('IS14ImplantedTools', 'Встроенный инструмент', 2, {IND: 50, BIO: 25}, lane=-2,
                 prereqs=['IS14Cybernetics'], sources=['ImplantedTools'],
                 summary='Инструмент, который всегда с собой, потому что он в руке.'),
            node('IS14CombatAugmentation', 'Боевая аугментация', 2, {IND: 60, MIL: 45},
                 sources=['CombatAugmentation'],
                 summary='Имплантат, который ставят не для здоровья.'),
            node('IS14NeuralInterfaces', 'Нейроинтерфейсы', 3, {SCI: 100, BIO: 60, IND: 45},
                 sources=['AdvancedCybernetics'],
                 summary='Прямая связь нервной системы с машиной.'),
        ]),
        Ray('computing', 'Вычислительная техника', 'E', [
            node('IS14ComputingSystems', 'Вычислительная техника', 1, {SCI: 30, SOC: 10},
                 sources=['AudioVisualCommunication'],
                 summary='Машинная обработка данных, камеры, серверы и голопады.'),
            node('IS14NetworkSystems', 'Сети и серверы', 2, {SCI: 55, IND: 25},
                 summary='Станция, в которой машины разговаривают друг с другом.'),
            node('IS14PositronicTheory', 'Теория позитронного мозга', 3, {SCI: 90, IND: 40},
                 summary='Как устроено мышление машины. Нужен готовый позитронный мозг.',
                 breakthrough=Breakthrough(
                     name='позитронный мозг',
                     samples=['PositronicBrain'],
                     icon='PositronicBrain',
                     hint='Печатает робототехник. Разбирать придётся вместе с личностью.',
                     point_type=SCI, payout=70)),
            node('IS14PositronicIntelligence', 'Позитронный интеллект', 4,
                 {SCI: 170, IND: 85, SOC: 45},
                 sources=['AdvancedRobotics'],
                 summary='Машина, обучающаяся на поведении экипажа. Нужны социологические данные.'),
        ]),
        Ray('sensors', 'Сенсорика', 'S', [
            node('IS14VisionSystems', 'Системы наблюдения', 1, {SCI: 30, MIL: 15},
                 sources=['NightVisionTech'],
                 summary='Видеть в темноте — и видеть то, что скрыто.'),
            node('IS14ThermalImaging', 'Термография', 2, {SCI: 55, MIL: 30},
                 sources=['ThermalVisionTech'],
                 summary='Тепловая картина: живое и работающее не спрячешь.'),
        ]),
    ],
)


# ─── Вооружение и защита ───────────────────────────────────────────────────
TREE['IS14Armament'] = Branch(
    center=node('IS14Ballistics', 'Баллистика', 0, {MIL: 20},
                sources=['SalvageWeapons'],
                summary='Расчёт полёта снаряда и отдачи.'),
    rays=[
        Ray('ammo', 'Боеприпасы', 'N', [
            node('IS14NonlethalAmmo', 'Нелетальные боеприпасы', 1, {MIL: 25},
                 sources=['NonlethalAmmunition'],
                 summary='Остановить, не убивая. СБ оценит, антаг — нет.'),
            node('IS14HeavyMunitions', 'Тяжёлые боеприпасы', 2, {MIL: 55, IND: 25},
                 sources=['DraconicMunitions'],
                 summary='Крупный калибр и всё, что к нему положено.'),
            node('IS14UraniumMunitions', 'Урановые боеприпасы', 3, {MIL: 100, IND: 40},
                 sources=['UraniumMunitions'],
                 summary='Плотность вместо скорости. И немного радиации на сдачу.'),
            node('IS14SmartWeaponry', 'Умное оружие', 4, {MIL: 140, SCI: 70},
                 sources=['SmartWeaponry'],
                 summary='Оружие, которое знает, в кого стрелять не надо.'),
        ]),
        Ray('optics', 'Оптика и лазеры', 'NE', [
            node('IS14OpticalSystems', 'Оптические системы', 1, {MIL: 25},
                 sources=['WeaponizedLaserManipulation'],
                 summary='Линзы, резонаторы и наведение — от прицела до лазера.'),
            node('IS14ConcentratedLasers', 'Концентрированные лазеры', 2, {MIL: 55, SCI: 25},
                 sources=['ConcentratedLaserWeaponry'],
                 summary='Пучок, который не рассеивается на полпути.'),
            node('IS14EnergyEmitters', 'Энергетические эмиттеры', 3, {MIL: 95, SCI: 45},
                 sources=['WaveParticleHarnessing'],
                 summary='Направленный пучок как оружие.'),
            node('IS14BoltWeaponry', 'Болтовые излучатели', 3, {MIL: 85, SCI: 35}, lane=-1,
                 prereqs=['IS14ConcentratedLasers'], sources=['EnergyBoltBasedWeaponry'],
                 summary='Сгусток вместо луча: медленнее, зато бьёт тяжелее.'),
            node('IS14MicrofusionWeaponry', 'Микрофузионное оружие', 4, {MIL: 140, IND: 65, SCI: 45},
                 sources=['PortableMicrofusionWeaponry'],
                 summary='Носимые системы на энергии синтеза.'),
            node('IS14CellWeaponry', 'Батарейное оружие', 2, {MIL: 60, IND: 20}, lane=1,
                 prereqs=['IS14OpticalSystems'],
                 sources=['WeaponLaserCellRevolverTech', 'WeaponLaserCellSMGTech'],
                 summary='Сменные ячейки вместо магазинов: револьвер и пистолет-пулемёт.'),
            node('IS14HeavyCellWeaponry', 'Тяжёлое батарейное оружие', 3, {MIL: 95, IND: 35}, lane=1,
                 prereqs=['IS14CellWeaponry'],
                 sources=['WeaponLaserCellSniperTech', 'WeaponLaserCellLMGTech'],
                 summary='Та же ячейка в снайперском и пулемётном калибре.'),
        ]),
        Ray('ordnance', 'Детоника', 'E', [
            node('IS14Detonics', 'Детонационные процессы', 1, {MIL: 30, IND: 15},
                 sources=['ExplosiveTechnology'],
                 recipes=['IS14OrdnanceTimer'],
                 summary='Управляемый взрыв. Даёт детонационный таймер и доплеровский массив.',
                 effects=[grant_entity('IS14DopplerArray', 'Выдаёт доплеровский массив')]),
            node('IS14BlastMetrology', 'Полигонные измерения', 2, {MIL: 60, SCI: 25},
                 summary='Метрология взрыва: рекорд считается только если он измерен.'),
            node('IS14ShuttleArmament', 'Корабельное вооружение', 3, {MIL: 110, IND: 60},
                 sources=['BasicShuttleArmament'],
                 summary='Орудийные комплексы для шаттлов.'),
            node('IS14AdvancedShuttleArmament', 'Тяжёлое корабельное вооружение', 4,
                 {MIL: 150, IND: 80},
                 sources=['AdvancedShuttleWeapon'],
                 summary='То, после чего от цели остаётся обломок с номером.'),
            node('IS14BluespaceMunitions', 'Блюспейс-боеприпасы', 5, {MIL: 170, SCI: 90, IND: 50},
                 prereqs=['IS14AdvancedShuttleArmament', 'IS14BluespaceTheory'],
                 sources=['BlueSpaceMunitions'],
                 summary='Снаряд, приходящий не оттуда, откуда выстрелили.'),
        ]),
        Ray('armour', 'Броня и сдерживание', 'SE', [
            node('IS14ArmorTechnology', 'Технологии бронирования', 1, {MIL: 25, IND: 15},
                 sources=['AdvancedRiotControl'],
                 summary='Пассивная защита: щиты, шлемы, бронепластины.'),
            node('IS14RestraintTechnology', 'Средства ограничения', 2, {MIL: 50, IND: 20},
                 sources=['RestraintTechnology'],
                 summary='Задержать и доставить, а не добить.'),
            node('IS14CaptureDevices', 'Средства захвата', 3, {MIL: 75, IND: 30}, lane=-1,
                 prereqs=['IS14RestraintTechnology'], sources=['SecurityCaptureDevice'],
                 summary='Техника, которая берёт живым то, что не хочет быть взятым.'),
            node('IS14DeterrenceSystems', 'Системы сдерживания', 3, {MIL: 90, SCI: 40},
                 sources=['DeterrenceTechnologies'],
                 summary='Техника, которая решает конфликт до выстрела.'),
            node('IS14SpecialMeans', 'Спецсредства', 4, {MIL: 110, SOC: 30},
                 sources=['SpecialMeans'],
                 summary='То, что выдают под роспись и считают по штукам.'),
        ]),
        Ray('mechguns', 'Мехвооружение', 'S', [
            node('IS14MechWeapons', 'Вооружение экзоскелетов', 1, {MIL: 35, IND: 20},
                 sources=['DualWieldingTechnology'],
                 summary='Как повесить на экзоскелет то, что держат двумя руками.'),
            node('IS14MechMissiles', 'Тяжёлое вооружение мехов', 2, {MIL: 70, IND: 35},
                 sources=['ExplosiveMechAmmunition'],
                 recipes=['WeaponMechCombatShotgun', 'WeaponMechCombatShotgunIncendiary',
                          'WeaponMechCombatUltraRifle'],
                 summary='Залп с подвеса. Полигон после этого выглядит иначе.'),
        ]),
    ],
)


# ─── Общество и быт ────────────────────────────────────────────────────────
TREE['IS14Society'] = Branch(
    center=node('IS14Sociometry', 'Социометрия', 0, {SOC: 15},
                summary='Измеримая оценка коллектива: анкеты дают больше данных.',
                effects=[grant_entity('IS14SurveyForm', 'Выдаёт три бланка анкет', count=3)]),
    rays=[
        Ray('comms', 'Системы связи', 'N', [
            node('IS14Communications', 'Системы связи', 1, {SOC: 25, IND: 10},
                 sources=['ExtendedCommunications'],
                 summary='Каналы и ретрансляторы: станция слышит сама себя.'),
            node('IS14Broadcasting', 'Радиовещание', 2, {SOC: 45, IND: 20}, lane=-1,
                 prereqs=['IS14Communications'], sources=['RadioMusicCommunications'],
                 summary='Музыка и эфир по всей станции. Вкус диджея не исследуется.'),
            node('IS14Linguistics', 'Прикладная лингвистика', 2, {SOC: 50, SCI: 20},
                 sources=['BasicTranslation'],
                 summary='Машинный перевод и работа с чужими языками.'),
            node('IS14AdvancedTranslation', 'Синхронный перевод', 3, {SOC: 95, SCI: 40},
                 sources=['AdvancedTranslation'],
                 summary='Понимать всех на станции, включая тех, кого понимать не хочется.'),
            node('IS14Cryptography', 'Служебная криптография', 3, {SOC: 80, MIL: 35}, lane=1,
                 prereqs=['IS14Linguistics'],
                 summary='Канал, который читает только тот, кому положено.'),
        ]),
        Ray('press', 'Пресса и досуг', 'NE', [
            node('IS14Press', 'Печать и пресса', 1, {SOC: 25},
                 recipes=['MassMediaCircuitboard', 'CrayonRainbowLarge'],
                 summary='Редакционный сервер и наборный карандаш: газету надо на чём-то делать.'),
            node('IS14Entertainment', 'Индустрия развлечений', 2, {SOC: 60, IND: 25},
                 sources=['AdvancedEntertainment', 'PushHorn'],
                 recipes=['JukeboxCircuitBoard', 'DawInstrumentMachineCircuitboard',
                          'SynthesizerInstrument'],
                 summary='Досуг экипажа как отрасль: музыкальный автомат, студия и синтезатор.'),
            node('IS14ClownTechnology', 'Клоунская техника', 3, {SOC: 90, IND: 40},
                 sources=['HONKMech'],
                 summary='Да, это тоже наука. Ответственность за последствия — на заказчике.'),
            node('IS14ClownArmament', 'Клоунское вооружение', 4, {SOC: 110, MIL: 40},
                 sources=['HONKWeapons'],
                 summary='Банановый мортир существует. Это не шутка, это прототип.'),
        ]),
        Ray('living', 'Быт и сервис', 'E', [
            node('IS14Ergonomics', 'Эргономика', 1, {SOC: 25, IND: 15},
                 sources=['AccessibilityTech'],
                 summary='Рабочая среда: от поручней до доступной консоли.'),
            node('IS14WorkwearRigs', 'Рабочая оснастка', 2, {SOC: 45, IND: 25}, lane=-1,
                 prereqs=['IS14Ergonomics'], sources=['WokrBelts'],
                 summary='Пояса и подвесы под каждую профессию: инструмент под рукой.'),
            node('IS14Sanitation', 'Санитария', 2, {SOC: 50, IND: 20},
                 sources=['AdvancedCleaning'],
                 summary='Чистота как техпроцесс, а не как подвиг уборщика.'),
            node('IS14SprayTechnology', 'Распылительная техника', 3, {SOC: 70, IND: 30}, lane=-1,
                 prereqs=['IS14Sanitation'], sources=['AdvancedSpray'],
                 summary='Напор вместо тряпки. И немного вместо аргумента.'),
            node('IS14DomesticAutomation', 'Бытовая автоматика', 3, {SOC: 85, IND: 40},
                 recipes=['ComputerTelevisionCircuitboard'],
                 summary='Техника, которая делает скучное сама. И телевизор в комнате отдыха.'),
        ]),
        Ray('food', 'Пищевые технологии', 'SE', [
            node('IS14FoodTechnology', 'Пищевые технологии', 1, {SOC: 25, BIO: 10},
                 sources=['MeatManipulation'],
                 summary='Промышленная переработка: биомасса, жир, фабрикаторы.'),
            node('IS14Agronomy', 'Агрономия', 2, {SOC: 50, BIO: 25},
                 sources=['Hydroponics'],
                 summary='Гидропоника и семена: ботаника выходит на поток.'),
            node('IS14FoodIndustry', 'Пищевая промышленность', 3, {SOC: 90, IND: 35},
                 summary='Кухня масштаба станции. Шеф всё ещё главный.'),
        ]),
        Ray('psych', 'Психология', 'S', [
            node('IS14Psychometrics', 'Психологическая экспертиза', 1, {SOC: 30, BIO: 10},
                 summary='Интервью и стресс-данные: экипаж как объект наблюдения.'),
            node('IS14SocialPsychology', 'Социальная психология', 2, {SOC: 65, BIO: 25},
                 summary='Поведение коллектива под нагрузкой — и как его выправлять.'),
            node('IS14CollectiveDynamics', 'Коллективная динамика', 3, {SOC: 110, SCI: 45},
                 summary='Почему смена развалилась — в цифрах, а не в рапорте.'),
        ]),
    ],
)


# ─── Случайные слоты ───────────────────────────────────────────────────────
# Слот — одно место на карте и несколько взаимоисключающих вариантов; станция в начале смены
# получает ровно один. Обычные темы никогда не зависят от варианта, иначе дерево ломалось бы
# от раунда к раунду. Это противовес детерминированному дереву: структура известна заранее,
# а что именно даст слот — нет.
Upgrade = collections.namedtuple('Upgrade', 'id name tier costs effects summary')
Group = collections.namedtuple('Group', 'id name branch pos prereqs variants')


def upgrade(id_, name, tier, costs, effects, summary):
    return Upgrade(id_, name, tier, costs, list(effects), summary)


UPGRADE_GROUPS = [
    Group('IS14UpgradeMethodology', 'Методология исследований', 'IS14Fundamental', (-3, -3),
          ['IS14Metrology'], [
        upgrade('IS14MethodMassSeries', 'Методология: массовые серии', 1, {SCI: 30}, [
            modifier('ResearchPayout', 0.3, 'Все измерения приносят на 30% больше данных'),
            modifier('LatheMaterialEfficiency', 0.1, 'Латы расходуют на 10% больше материалов'),
        ], 'Поток однотипных замеров: данных заметно больше, но цех работает на износ.'),
        upgrade('IS14MethodDoubleCheck', 'Методология: двойной контроль', 1, {SCI: 30}, [
            modifier('ResearchPayout', 0.15, 'Все измерения приносят на 15% больше данных'),
            modifier('ExperimentNoveltyRetention', 0.15, 'Повторные измерения приносят ощутимо больше'),
        ], 'Каждый результат проверяется дважды: отдача выше, повторы перестают быть мусором.'),
        upgrade('IS14MethodArchive', 'Методология: архивная сверка', 1, {SCI: 30}, [
            modifier('ExperimentNoveltyRetention', 0.3, 'Повторные измерения почти не теряют в цене'),
        ], 'Сверка с архивом станции: повторные измерения почти не теряют в цене.'),
        upgrade('IS14MethodRushPublication', 'Методология: авральная публикация', 1, {SCI: 30}, [
            grant_points(SCI, 80, 'Разово начисляет 80 научных данных'),
            modifier('ResearchPayout', -0.1, 'Все измерения приносят на 10% меньше данных'),
        ], 'Сдать отчёт вперёд плана: данные сразу и много, дальше работать тяжелее.'),
    ]),
    Group('IS14UpgradeProfile', 'Профиль института', 'IS14Fundamental', (-4, -4),
          ['IS14Metrology'], [
        upgrade('IS14ProfilePhysics', 'Профиль института: физический', 2, {SCI: 40}, [
            modifier('ResearchPayoutScience', 0.4, 'Научные измерения приносят на 40% больше'),
            modifier('ResearchPayoutBiological', -0.15, 'Биологические измерения приносят на 15% меньше'),
        ], 'Госплан утвердил физический профиль: приборы точнее, виварий подождёт.'),
        upgrade('IS14ProfileBiology', 'Профиль института: биологический', 2, {SCI: 40}, [
            modifier('ResearchPayoutBiological', 0.4, 'Биологические измерения приносят на 40% больше'),
            modifier('ResearchPayoutScience', -0.15, 'Научные измерения приносят на 15% меньше'),
        ], 'Профиль института биологический: вскрытия и штаммы в приоритете.'),
        upgrade('IS14ProfileDefence', 'Профиль института: оборонный', 2, {SCI: 40}, [
            modifier('ResearchPayoutMilitary', 0.4, 'Военные измерения приносят на 40% больше'),
            modifier('ResearchPayoutSocial', -0.15, 'Анкетирование приносит на 15% меньше'),
        ], 'Оборонная тематика под грифом и щедро финансируется. Анкеты никого не волнуют.'),
        upgrade('IS14ProfileCivil', 'Профиль института: гражданский', 2, {SCI: 40}, [
            modifier('ResearchPayoutSocial', 0.4, 'Анкетирование приносит на 40% больше'),
            modifier('ResearchPayoutMilitary', -0.15, 'Военные измерения приносят на 15% меньше'),
        ], 'Гражданский профиль: соцопросы в почёте, оружейная тематика урезана.'),
    ]),
    Group('IS14UpgradeLatheTuning', 'Наладка станочного парка', 'IS14Materials', (-3, -2),
          ['IS14Metallurgy'], [
        upgrade('IS14TuningForced', 'Наладка: форсированные приводы', 1, {IND: 30}, [
            modifier('LatheSpeed', -0.2, 'Латы печатают на 20% быстрее'),
            modifier('LatheMaterialEfficiency', 0.15, 'Латы расходуют на 15% больше материалов'),
        ], 'Станки гонят на пределе: быстро, но стружки и брака больше.'),
        upgrade('IS14TuningCycles', 'Наладка: оптимизация циклов', 1, {IND: 30}, [
            modifier('LatheSpeed', -0.1, 'Латы печатают на 10% быстрее'),
        ], 'Аккуратная перестройка программ обработки. Без побочных эффектов.'),
        upgrade('IS14TuningThrift', 'Наладка: экономная подача', 1, {IND: 30}, [
            modifier('LatheMaterialEfficiency', -0.2, 'Латы расходуют на 20% меньше материалов'),
            modifier('LatheSpeed', 0.05, 'Латы печатают на 5% медленнее'),
        ], 'Материал считают по граммам: дешевле, но чуть медленнее.'),
    ]),
]


def iter_nodes():
    """Every ordinary node of the tree, with the branch and the ray it belongs to."""
    for branch, data in TREE.items():
        yield branch, None, 0, data.center

        for ray in data.rays:
            for index, entry in enumerate(ray.nodes):
                yield branch, ray, index, entry


def prereqs_of(branch, ray, index, entry):
    """Declared prerequisites, or the previous topic along the ray — the line on the map."""
    if entry.prereqs is not None:
        return list(entry.prereqs)

    if ray is None:
        return []

    if index == 0:
        return [TREE[branch].center.id]

    return [ray.nodes[index - 1].id]


def tier_of(entry):
    # Depth along a ray is the tier, unless the topic says otherwise: the First Department's
    # four sit at different depths only so they do not overlap on the map, and they are all
    # equally late-game.
    if entry.tier is not None:
        return entry.tier

    return 1 if entry.depth == 0 else min(4, entry.depth)


def parse_upstream():
    """Upstream technology id -> (icon block lines, recipe ids)."""
    files = sorted(glob.glob('Resources/Prototypes/Research/*.yml')
                   + glob.glob('Resources/Prototypes/_*/Research/*.yml'))
    result = {}

    for path in files:
        text = io.open(path, encoding='utf-8').read()
        for block in re.split(r'(?m)^- type:\s*', text)[1:]:
            if block.split('\n', 1)[0].split('#')[0].strip() != 'technology':
                continue

            match = re.search(r'(?m)^  id:\s*([^\n#]+)', block)
            if not match:
                continue

            icon = []
            icon_match = re.search(r'(?m)^  icon:\n((?:    .+\n)+)', block)
            if icon_match:
                icon = [line.rstrip() for line in icon_match.group(1).splitlines()]

            recipes = []
            recipe_match = re.search(r'(?m)^  recipeUnlocks:\n((?:  - .+\n|  #.*\n)+)', block)
            if recipe_match:
                for line in recipe_match.group(1).splitlines():
                    line = line.strip()
                    if line.startswith('- '):
                        recipe = line[2:].split('#')[0].strip()
                        if recipe:
                            recipes.append(recipe)

            result[match.group(1).strip()] = (icon, recipes)

    return result


def parse_ids(pattern, type_name):
    """Prototype ids of one type across the whole resource tree."""
    found = set()

    for path in glob.glob(pattern, recursive=True):
        text = io.open(path, encoding='utf-8').read()

        for block in re.split(r'(?m)^- type:\s*', text)[1:]:
            if block.split('\n', 1)[0].split('#')[0].strip() != type_name:
                continue

            match = re.search(r'(?m)^  id:\s*([^\n#]+)', block)
            if match:
                found.add(match.group(1).strip())

    return found


def layout():
    """Branch -> {node id: (column, row)}, shifted so the smallest coordinate is zero."""
    result = {}

    for branch, data in TREE.items():
        placed = {data.center.id: (0, 0)}
        taken = {(0, 0): data.center.id}

        for ray in data.rays:
            step, lane = DIRECTIONS[ray.direction]

            for entry in ray.nodes:
                position = (step[0] * entry.depth + lane[0] * entry.lane,
                            step[1] * entry.depth + lane[1] * entry.lane)

                if position in taken:
                    raise SystemExit('%s: %s and %s both land on %s'
                                     % (branch, taken[position], entry.id, position))

                placed[entry.id] = position
                taken[position] = entry.id

        for group in UPGRADE_GROUPS:
            if group.branch != branch:
                continue

            if group.pos in taken and taken[group.pos] != group.id:
                raise SystemExit('%s: slot %s sits on %s' % (branch, group.id, taken[group.pos]))

            taken[group.pos] = group.id

            # Variants of one slot share a square on purpose: only one of them exists per round.
            for variant in group.variants:
                placed[variant.id] = group.pos

        min_x = min(x for x, _ in placed.values())
        min_y = min(y for _, y in placed.values())
        result[branch] = {key: (x - min_x, y - min_y) for key, (x, y) in placed.items()}

    return result


def validate(upstream, recipes, entities, modifiers):
    """
    Checks the design rules while generating. A violation is a balance bug that would be
    invisible in the YAML, so this is deliberately fatal rather than a warning.
    """
    problems = []

    node_ids = {entry.id for _, _, _, entry in iter_nodes()}
    hidden_ids = {entry.id for _, _, _, entry in iter_nodes() if entry.hidden}
    variant_ids = {variant.id for group in UPGRADE_GROUPS for variant in group.variants}
    seen = set()
    exclusive = collections.Counter()

    for _, _, _, entry in iter_nodes():
        if entry.id in seen:
            problems.append('duplicate node id %s' % entry.id)
        seen.add(entry.id)

    for group in UPGRADE_GROUPS:
        for variant in group.variants:
            if variant.id in seen:
                problems.append('duplicate node id %s' % variant.id)
            seen.add(variant.id)

    for branch, ray, index, entry in iter_nodes():
        where = '%s/%s' % (branch, entry.id)

        for prereq in prereqs_of(branch, ray, index, entry):
            if prereq in variant_ids:
                problems.append('%s depends on randomised variant %s' % (where, prereq))
            elif prereq not in node_ids:
                problems.append('%s has unknown prerequisite %s' % (where, prereq))
            elif prereq in hidden_ids and not entry.hidden:
                problems.append('%s depends on hidden %s' % (where, prereq))

        # Everything from tier 3 up costs at least two currencies: no branch is finished alone.
        if tier_of(entry) >= 3 and len(entry.costs) < 2:
            problems.append('%s is tier %d and costs one currency' % (where, tier_of(entry)))

        if not entry.costs:
            problems.append('%s has no price' % where)

        for source in entry.sources:
            if source not in upstream:
                problems.append('%s inherits unknown upstream technology %s' % (where, source))

        if entry.icon_from is not None and entry.icon_from not in upstream:
            problems.append('%s takes its icon from unknown %s' % (where, entry.icon_from))

        for recipe in entry.recipes:
            if recipe not in recipes:
                problems.append('%s unlocks unknown recipe %s' % (where, recipe))

        if entry.breakthrough is not None:
            for sample in entry.breakthrough.samples:
                if sample not in entities:
                    problems.append('%s wants unknown sample %s' % (where, sample))

            if entry.breakthrough.icon is not None and entry.breakthrough.icon not in entities:
                problems.append('%s draws unknown sample icon %s' % (where, entry.breakthrough.icon))

        if entry.contraband is not None:
            if not entry.hidden:
                problems.append('%s is uncovered by contraband but is not hidden' % where)

            for sample in entry.contraband.samples:
                if sample not in entities:
                    problems.append('%s wants unknown contraband %s' % (where, sample))

            if entry.contraband.icon is not None and entry.contraband.icon not in entities:
                problems.append('%s draws unknown contraband icon %s' % (where, entry.contraband.icon))

        if entry.hidden and entry.contraband is None:
            problems.append('%s is hidden with nothing to uncover it' % where)

        if entry.exclusive is not None:
            exclusive[entry.exclusive] += 1

        problems.extend(check_effects(where, entry.effects, entities, modifiers))

    for group in UPGRADE_GROUPS:
        if group.branch not in TREE:
            problems.append('slot %s sits in unknown branch %s' % (group.id, group.branch))

        for prereq in group.prereqs:
            if prereq not in node_ids:
                problems.append('slot %s has unknown prerequisite %s' % (group.id, prereq))

        if len(group.variants) < 2:
            problems.append('slot %s has fewer than two variants' % group.id)

        for variant in group.variants:
            problems.extend(check_effects('%s/%s' % (group.id, variant.id), variant.effects,
                                          entities, modifiers))

    for group, count in exclusive.items():
        if count < 2:
            problems.append('exclusive group %s has a single member' % group)

    if problems:
        raise SystemExit('generation refused:\n  ' + '\n  '.join(problems))


def check_effects(where, effects, entities, modifiers):
    problems = []

    for kind, fields, description in effects:
        if not description:
            problems.append('%s has an effect with no description' % where)

        if kind == 'IS14ModifierEffect' and fields['modifier'] not in modifiers:
            problems.append('%s moves unknown modifier %s' % (where, fields['modifier']))

        if kind == 'IS14GrantEntityEffect' and fields['prototype'] not in entities:
            problems.append('%s grants unknown entity %s' % (where, fields['prototype']))

    return problems


HEADER = """# SPDX-FileCopyrightText: 2025 IS14
#
# SPDX-License-Identifier: AGPL-3.0-or-later
"""


def key_of(node_id):
    return node_id[4:].lower() if node_id.startswith('IS14') else node_id.lower()


def technology_block(node_id, branch, tier, cost, position, icon, prereqs, recipes, hidden=False):
    lines = ['', '- type: technology', '  id: %s' % node_id,
             '  name: is14-tech-%s' % key_of(node_id),
             '  discipline: %s' % branch, '  tier: %d' % tier, '  cost: %d' % cost,
             '  position: %d,%d' % position]

    if hidden:
        # Hidden until the station reveals it: which variant of a slot turns up is decided at
        # round start, and the others must not exist on the console at all.
        lines.append('  hidden: true')

    lines.append('  icon:')
    lines.extend(icon)

    if prereqs:
        lines.append('  technologyPrerequisites:')
        lines.extend('  - %s' % prereq for prereq in prereqs)

    if recipes:
        lines.append('  recipeUnlocks:')
        lines.extend('  - %s' % recipe for recipe in recipes)

    return lines


def data_block(node_id, costs, effects, summary, breakthrough=None, exclusive=None,
               contraband=None):
    key = key_of(node_id)
    lines = ['', '- type: is14Technology', '  id: %s' % node_id]

    if summary:
        lines.append('  summary: is14-techsummary-%s' % key)

    lines.append('  costs:')
    for currency in (SCI, IND, MIL, BIO, SOC):
        if currency in costs:
            lines.append('    %s: %d' % (currency, costs[currency]))

    if exclusive is not None:
        lines.append('  exclusiveGroup: %s' % exclusive)

    if breakthrough is not None:
        lines.append('  breakthrough:')
        lines.append('    name: is14-sample-%s' % key)
        lines.append('    samples:')
        lines.extend('    - %s' % sample for sample in breakthrough.samples)

        if breakthrough.icon is not None:
            lines.append('    icon: %s' % breakthrough.icon)

        if breakthrough.hint:
            lines.append('    hint: is14-samplehint-%s' % key)

        lines.append('    pointType: %s' % breakthrough.point_type)
        lines.append('    payout: %d' % breakthrough.payout)

    if contraband is not None:
        lines.append('  contraband:')
        lines.append('    name: is14-contraband-%s' % key)
        lines.append('    samples:')
        lines.extend('    - %s' % sample for sample in contraband.samples)

        if contraband.icon is not None:
            lines.append('    icon: %s' % contraband.icon)

        if contraband.hint:
            lines.append('    hint: is14-contrabandhint-%s' % key)

        lines.append('    pointType: %s' % contraband.point_type)
        lines.append('    payout: %d' % contraband.payout)

    if effects:
        lines.append('  effects:')
        for index, (kind, fields, _) in enumerate(effects):
            lines.append('  - !type:%s' % kind)
            lines.append('    description: is14-effect-%s-%d' % (key, index))
            for field, value in fields.items():
                lines.append('    %s: %s' % (field, value))

    return lines


def locale_lines(node_id, name, summary, effects, breakthrough=None, contraband=None):
    key = key_of(node_id)
    lines = ['is14-tech-%s = %s' % (key, name)]

    if summary:
        lines.append('is14-techsummary-%s = %s' % (key, summary))

    for index, (_, _, description) in enumerate(effects):
        lines.append('is14-effect-%s-%d = %s' % (key, index, description))

    if breakthrough is not None:
        lines.append('is14-sample-%s = %s' % (key, breakthrough.name))

        if breakthrough.hint:
            lines.append('is14-samplehint-%s = %s' % (key, breakthrough.hint))

    if contraband is not None:
        lines.append('is14-contraband-%s = %s' % (key, contraband.name))

        if contraband.hint:
            lines.append('is14-contrabandhint-%s = %s' % (key, contraband.hint))

    return lines


def main():
    apply_theory_effects()

    upstream = parse_upstream()
    recipes = parse_ids('Resources/Prototypes/**/*.yml', 'latheRecipe')
    entities = parse_ids('Resources/Prototypes/**/*.yml', 'entity')
    modifiers = parse_ids('Resources/Prototypes/_IS14/Research/modifiers.yml', 'is14ResearchModifier')

    validate(upstream, recipes, entities, modifiers)

    positions = layout()

    tech_lines = [HEADER + """
# The IS14 research tree. GENERATED by Tools/_IS14/generate_research_tree.py — edit the tables
# in that script, not this file.
#
# Nodes are upstream `technology` prototypes so the research server, lathes and tech disks keep
# working unchanged. Positions drive the console map: every branch starts from a centre and
# spreads along named rays — anomalistics one way, xenoarchaeology another — which fork into
# lanes. Prices, effects, breakthroughs and forks live in tech_data.yml.
"""]
    data_lines = [HEADER + """
# Prices, effects, breakthrough samples and mutually exclusive forks for the IS14 tree.
# GENERATED by Tools/_IS14/generate_research_tree.py.
"""]
    upgrade_lines = [HEADER + """
# Randomised upgrade slots. GENERATED by Tools/_IS14/generate_research_tree.py.
#
# Each slot holds several mutually exclusive variants of the same upgrade; the station reveals
# exactly one per round, so the map offers a different trade-off every shift. Variants are
# hidden technologies and nothing in the standard tree depends on them.
"""]
    disciplines = [HEADER + """
# IS14 research branches. GENERATED by Tools/_IS14/generate_research_tree.py.
# Branches are themes, not currencies: prices mix currencies across every branch.
"""]
    loc_lines = ['# GENERATED by Tools/_IS14/generate_research_tree.py\n']

    counts = collections.OrderedDict()
    breakthroughs = 0
    forks = set()

    for branch, (branch_name, ui_name, colour, icon_state) in BRANCHES.items():
        disciplines.append("""
- type: techDiscipline
  id: %s
  name: is14-discipline-%s
  uiName: %s
  color: "%s"
  icon:
    sprite: Interface/Misc/research_disciplines.rsi
    state: %s
  tierPrerequisites:
    1: 0
    2: 0
    3: 0
    4: 0
""" % (branch, branch.lower(), ui_name, colour, icon_state))

        loc_lines.append('\nis14-discipline-%s = %s' % (branch.lower(), branch_name))

        branch_icon = ['    sprite: Interface/Misc/research_disciplines.rsi',
                       '    state: %s' % icon_state]

        counts[branch] = 0

        for node_branch, ray, index, entry in iter_nodes():
            if node_branch != branch:
                continue

            counts[branch] += 1

            if entry.breakthrough is not None:
                breakthroughs += 1

            if entry.exclusive is not None:
                forks.add(entry.exclusive)

            icon = []
            unlocks = []

            for source in list(entry.sources) + ([entry.icon_from] if entry.icon_from else []):
                source_icon, source_recipes = upstream[source]

                if not icon and source_icon:
                    icon = source_icon

                if source == entry.icon_from:
                    continue

                for recipe in source_recipes:
                    if recipe not in unlocks:
                        unlocks.append(recipe)

            for recipe in entry.recipes:
                if recipe not in unlocks:
                    unlocks.append(recipe)

            tech_lines.extend(technology_block(
                entry.id,
                branch,
                tier_of(entry),
                sum(entry.costs.values()) * 100,
                positions[branch][entry.id],
                icon or branch_icon,
                prereqs_of(branch, ray, index, entry),
                unlocks,
                hidden=entry.hidden))

            data_lines.extend(data_block(entry.id, entry.costs, entry.effects, entry.summary,
                                         entry.breakthrough, entry.exclusive, entry.contraband))

            loc_lines.extend(locale_lines(entry.id, entry.name, entry.summary, entry.effects,
                                          entry.breakthrough, entry.contraband))

    variants = 0

    for group in UPGRADE_GROUPS:
        branch_icon = ['    sprite: Interface/Misc/research_disciplines.rsi',
                       '    state: %s' % BRANCHES[group.branch][3]]

        upgrade_lines.append('\n- type: is14ResearchUpgradeGroup')
        upgrade_lines.append('  id: %s' % group.id)
        upgrade_lines.append('  name: is14-upgradegroup-%s' % key_of(group.id))
        upgrade_lines.append('  variants:')
        upgrade_lines.extend('  - %s' % variant.id for variant in group.variants)

        loc_lines.append('is14-upgradegroup-%s = %s' % (key_of(group.id), group.name))

        for variant in group.variants:
            variants += 1
            counts[group.branch] += 1

            upgrade_lines.extend(technology_block(
                variant.id,
                group.branch,
                variant.tier,
                sum(variant.costs.values()) * 100,
                positions[group.branch][variant.id],
                branch_icon,
                group.prereqs,
                [],
                hidden=True))

            upgrade_lines.extend(data_block(variant.id, variant.costs, variant.effects,
                                            variant.summary))

            loc_lines.extend(locale_lines(variant.id, variant.name, variant.summary,
                                          variant.effects))

    write('Resources/Prototypes/_IS14/Research/disciplines.yml', ''.join(disciplines))
    write('Resources/Prototypes/_IS14/Research/technologies.yml', '\n'.join(tech_lines) + '\n')
    write('Resources/Prototypes/_IS14/Research/tech_data.yml', '\n'.join(data_lines) + '\n')
    write('Resources/Prototypes/_IS14/Research/upgrades.yml', '\n'.join(upgrade_lines) + '\n')
    write('Resources/Locale/ru-RU/_IS14/research_tree.ftl', '\n'.join(loc_lines) + '\n')

    total = sum(counts.values())
    print('branches: %d, nodes: %d (%d in the tree + %d variants in %d slots)'
          % (len(BRANCHES), total, total - variants, variants, len(UPGRADE_GROUPS)))
    print('breakthroughs: %d, exclusive forks: %d' % (breakthroughs, len(forks)))

    for branch, count in counts.items():
        print('  %-18s %d' % (branch, count))

    unused = sorted(set(upstream) - {source
                                     for _, _, _, entry in iter_nodes()
                                     for source in entry.sources}
                    - {entry.icon_from for _, _, _, entry in iter_nodes() if entry.icon_from})
    unused = [tech for tech in unused if not tech.startswith('IS14')]

    if unused:
        print('upstream technologies not placed in our tree (%d): %s'
              % (len(unused), ', '.join(unused)))


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    io.open(path, 'w', encoding='utf-8', newline='\n').write(text)



# ─── Что дают «теоретические» темы ─────────────────────────────────────────
# Крупные сделки с минусами живут в случайных слотах и развилках. Здесь — маленькие,
# безусловные прибавки на темах, которые иначе были бы просто перемычками: приборный луч
# повышает отдачу приборов, цеховой — качество печати своего цеха. Отдельной таблицей, потому
# что это балансный слой: его правят целиком, а не выискивая строки по дереву.
THEORY_EFFECTS = {
    'IS14LabAutomation': [
        modifier('ResearchPayoutScience', 0.1, 'Научные измерения приносят на 10% больше')],
    'IS14PrecisionSensors': [
        modifier('ResearchPayout', 0.05, 'Все измерения приносят на 5% больше данных')],
    'IS14StatisticalMethods': [
        modifier('ExperimentNoveltyRetention', 0.1, 'Повторные измерения приносят больше')],
    'IS14Dosimetry': [
        modifier('ResearchPayoutBiological', 0.05, 'Биологические измерения приносят на 5% больше')],
    'IS14SurfaceTreatment': [
        modifier('LatheMaterialIndustrial', -0.05, 'Промышленные латы расходуют на 5% меньше материалов')],
    'IS14Metamaterials': [
        modifier('LatheMaterialEfficiency', -0.1, 'Латы расходуют на 10% меньше материалов')],
    'IS14FlexibleLines': [
        modifier('LatheSpeed', -0.1, 'Латы печатают на 10% быстрее')],
    'IS14MiningLogistics': [
        modifier('ResearchPayoutIndustrial', 0.1, 'Промышленные измерения приносят на 10% больше')],
    'IS14GridEngineering': [
        modifier('LatheSpeed', -0.05, 'Латы печатают на 5% быстрее')],
    'IS14GasMixtures': [
        modifier('ResearchPayoutMilitary', 0.1, 'Военные измерения приносят на 10% больше')],
    'IS14TritiumProduction': [
        modifier('ResearchPayoutMilitary', 0.2, 'Военные измерения приносят на 20% больше')],
    'IS14SupermatterEngineering': [
        grant_points(IND, 60, 'Разово начисляет 60 промышленных данных')],
    'IS14Genetics': [
        modifier('ResearchPayoutBiological', 0.1, 'Биологические измерения приносят на 10% больше')],
    'IS14Mutagenesis': [
        modifier('ResearchPayoutBiological', 0.15, 'Биологические измерения приносят на 15% больше')],
    'IS14ExoticFauna': [
        modifier('ResearchPayoutBiological', 0.15, 'Биологические измерения приносят на 15% больше')],
    'IS14TissueEngineering': [
        grant_points(BIO, 50, 'Разово начисляет 50 биологических данных')],
    'IS14NetworkSystems': [
        modifier('ResearchPayout', 0.05, 'Все измерения приносят на 5% больше данных')],
    'IS14BlastMetrology': [
        modifier('ResearchPayoutMilitary', 0.15, 'Военные измерения приносят на 15% больше')],
    'IS14Psychometrics': [
        modifier('ResearchPayoutSocial', 0.1, 'Анкетирование приносит на 10% больше')],
    'IS14CollectiveDynamics': [
        modifier('ResearchPayoutSocial', 0.2, 'Анкетирование приносит на 20% больше')],
    # Центры веток — входной билет, и он что-то даёт: иначе первая покупка смены выглядит
    # как налог. По образцу метрологии, патанатомии и социометрии.
    'IS14Metallurgy': [
        grant_points(IND, 10, 'Разово начисляет 10 промышленных данных')],
    'IS14Thermodynamics': [
        grant_points(IND, 10, 'Разово начисляет 10 промышленных данных')],
    'IS14MetalScience': [
        grant_points(IND, 15, 'Разово начисляет 15 промышленных данных')],
}


def apply_theory_effects():
    """Folds the balance table into the tree before anything reads it."""
    for branch, data in TREE.items():
        if data.center.id in THEORY_EFFECTS:
            TREE[branch] = data._replace(
                center=data.center._replace(
                    effects=data.center.effects + THEORY_EFFECTS[data.center.id]))

        for ray in data.rays:
            for index, entry in enumerate(ray.nodes):
                if entry.id in THEORY_EFFECTS:
                    ray.nodes[index] = entry._replace(
                        effects=entry.effects + THEORY_EFFECTS[entry.id])

    unknown = set(THEORY_EFFECTS) - {entry.id for _, _, _, entry in iter_nodes()}

    if unknown:
        raise SystemExit('theory effects for unknown topics: %s' % ', '.join(sorted(unknown)))


if __name__ == '__main__':
    main()
