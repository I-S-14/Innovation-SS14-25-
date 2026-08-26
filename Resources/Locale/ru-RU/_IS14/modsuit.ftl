# Модульное шасси — общий слой, используется МОД-костюмами и в будущем мехами

chassis-panel-closed = Панель закрыта.
chassis-complexity-exceeded = Не хватает места под модули.
chassis-module-conflict = Несовместим с модулем «{ $module }».
chassis-module-installed = Модуль «{ $module }» установлен.
chassis-module-removed = Модуль «{ $module }» извлечён.
chassis-module-not-removable = Этот модуль встроен и не извлекается.

chassis-module-no-power = Недостаточно заряда.
chassis-module-cooldown = Модуль перезаряжается.
chassis-module-missing-parts = Нужные части не загерметизированы.
chassis-module-chassis-inactive = Костюм не активен.
chassis-module-not-worn = Костюм не надет.
chassis-module-incapacitated = Вы не можете этого сделать.
chassis-module-malfunctioning = Модуль неисправен.
chassis-module-unavailable = Модуль недоступен.

# МОД-костюм

modsuit-parts-still-deployed = Сначала сложите части костюма.
modsuit-power-depleted = Заряд иссяк, костюм расшивается!
modsuit-busy-sealing = Костюм сейчас герметизируется.
modsuit-slot-occupied = Эта часть тела уже чем-то занята.
modsuit-not-worn = Костюм не надет.
modsuit-no-core = Нет ядра.
modsuit-nothing-to-seal = Герметизировать нечего.
modsuit-nothing-to-unseal = Расшивать нечего.

# Попапы частей

modsuit-seal-helmet = Шлем герметизируется.
modsuit-unseal-helmet = Шлем расшивается.
modsuit-seal-chestplate = Нагрудник герметизируется.
modsuit-unseal-chestplate = Нагрудник расшивается.
modsuit-seal-gauntlets = Перчатки защёлкиваются.
modsuit-unseal-gauntlets = Перчатки расстёгиваются.
modsuit-seal-boots = Ботинки защёлкиваются.
modsuit-unseal-boots = Ботинки расстёгиваются.

# Интерфейс шасси

chassis-ui-title = МОД-костюм
chassis-ui-charge = Заряд
chassis-ui-charge-value = { $current } / { $max } Дж · { $draw } Вт
chassis-ui-no-core = Нет ядра
chassis-ui-complexity = Сложность
chassis-ui-complexity-value = { $used } / { $max }
chassis-ui-draw = Расход { $draw } Вт
chassis-ui-panel-open = Панель открыта
chassis-ui-malfunctioning = Неисправен
chassis-ui-activate = Загерметизировать
chassis-ui-deactivate = Расшить
chassis-ui-parts = Части
chassis-ui-modules = Модули
chassis-ui-no-modules = Модулей нет.
chassis-ui-deploy = Развернуть
chassis-ui-retract = Сложить
chassis-ui-seal = Запечатать
chassis-ui-unseal = Расшить
chassis-ui-part-stowed = сложена
chassis-ui-part-deployed = надета
chassis-ui-part-sealed = запечатана
chassis-ui-part-broken = разбита
chassis-ui-part-ruptured = вскрыта
chassis-ui-kind-passive = пассивный
chassis-ui-kind-toggleable = включаемый
chassis-ui-kind-usable = разовый
chassis-ui-kind-active = активный
chassis-ui-module-idle = { $value } Вт
chassis-ui-module-active = { $value } Вт
chassis-ui-module-use = { $value } Дж
chassis-ui-module-idle-tooltip = Потребление в установленном виде
chassis-ui-module-active-tooltip = Потребление во включённом виде
chassis-ui-module-use-tooltip = Расход за одно применение
chassis-ui-module-on = Включить
chassis-ui-module-off = Выключить
chassis-ui-module-use-button = Применить
chassis-ui-module-select = Выбрать
chassis-ui-module-deselect = Убрать

# Взаимодействия

chassis-panel-opened = Панель открыта.
chassis-panel-closed-now = Панель закрыта.
chassis-active-cannot-open = Костюм активен, панель не открыть.
chassis-nothing-to-pry = В гнезде нечего поддевать.

mod-core-slot = МОД-ядро

# Интерфейс: новые строки

chassis-ui-hardware = Железо
chassis-ui-state-stowed = сложен
chassis-ui-state-deployed = развёрнут
chassis-ui-state-sealed = запечатан
chassis-ui-unworn = Не надет
chassis-ui-deploy-all = Развернуть всё
chassis-ui-retract-all = Сложить всё
chassis-ui-no-parts = Частей нет.
chassis-ui-electrified = Под напряжением
chassis-ui-interface-broken = Интерфейс повреждён
chassis-ui-panel-shut = закрыта
chassis-ui-hw-core = Ядро
chassis-ui-hw-cell = Батарея
chassis-ui-hw-panel = Панель
chassis-ui-hw-lock = ID-замок
chassis-ui-hw-draw = Расход
chassis-ui-lock-engaged = заперт
chassis-ui-lock-open = открыт
chassis-ui-lock-wiped = доступ стёрт
chassis-ui-hw-hint = Отвёртка открывает панель, лом извлекает модули, ID-карта переключает замок.

# ID-замок и саботаж

modsuit-breach-fold-instead = Сложите её через панель костюма.
modsuit-breach-not-subdued = Он ещё на ногах — сначала обездвижьте.
modsuit-breach-not-yours = Панель слушается только того, кто в костюме.
modsuit-breach-released = Костюм расшивается и складывается.
modsuit-breach-cutting = Вы врезаетесь в { $part }.
modsuit-breach-already-cut = { $part } уже прорезана насквозь.
modsuit-breach-nothing-there = То, куда вы целитесь, ничем не закрыто.
modsuit-lock-override = Костюм принимает карту и отпускает.
wire-name-mod-release = СБР
modsuit-lock-denied = Отказано в доступе.
modsuit-lock-engaged = Замок заперт.
modsuit-lock-released = Замок открыт.

# Провода

wire-name-mod-lock = ЗМК
wire-name-mod-malfunction = СБОЙ
wire-name-mod-shock = ТОК
wire-name-mod-interface = ИНТФ
modsuit-wires-board = Блок управления МОД-костюма

# Панель управления

chassis-ui-shell = Обшивка
chassis-ui-power = Энергоблок
chassis-ui-slot-head = Шлем
chassis-ui-slot-chest = Корпус
chassis-ui-slot-hands = Перчатки
chassis-ui-slot-feet = Ботинки
chassis-ui-slot-other = Прочее
chassis-ui-module-count = установлено: { $count }
chassis-ui-part-hint-deploy = Сначала разверните часть, потом её можно герметизировать.
chassis-device-reeled-in = Костюм втягивает { $device } обратно.

# Управление модулями

chassis-ui-eject = Извлечь
chassis-ui-eject-tooltip = Вынуть модуль из шасси.
chassis-ui-eject-needs-panel = Сначала нужно открыть сервисную панель.
chassis-ui-module-open = Открыть

# Целостность обшивки

chassis-ui-integrity = Целостность
chassis-ui-integrity-value = { $current } / { $max }
chassis-ui-status-seal = Герметичность
chassis-ui-status-modules = Модули
chassis-ui-fault-structural = Погнутая обшивка — заварить.
chassis-ui-fault-electrical = Выгоревшая проводка — протянуть кабель.
modsuit-part-broken = { $part } проминается — крепления обесточиваются.
modsuit-part-ruptured = { $part } вскрывается и срывает герметизацию.
modsuit-part-cannot-seal = { $part } разбита слишком сильно, чтобы закрыться.
modsuit-no-storage = На этом костюме нет отсеков.

# Обслуживание

modsuit-repair-done = Вы выправляете { $part }.
modsuit-repair-needs-welder = Здесь погнута обшивка. Нужна сварка.
modsuit-repair-needs-cable = Здесь выгорела проводка. Нужен кабель.
modsuit-repair-no-cable = Кабеля не хватает.

chassis-device-no-power = Костюму нечем это запитать.
chassis-device-no-hands = Для { $device } нужны обе руки.
chassis-ui-module-details = Показать, что он делает.

modsuit-core-full = Ядро и так полное.
modsuit-core-refuelled = Вы скармливаете ядру { $count } ед. { $fuel }.

# Баллон

chassis-ui-tank = Баллон
chassis-ui-tank-value = { $kpa } кПа
chassis-ui-tank-idle = компрессор стоит
chassis-ui-tank-seal = Герметичность среды
chassis-ui-tank-internals = Дышать из баллона
chassis-ui-tank-internals-off = Отключить дыхание
chassis-ui-tank-internals-tooltip = Носитель дышит из баллона костюма, а не из того, что вокруг.
chassis-ui-tank-internals-needs-seal = Нужны запечатанные все части и закрытый клапан.
chassis-ui-tank-unsealed = Компрессору нужен запечатанный нагрудник.
chassis-ui-tank-temperature = { $celsius } °C
chassis-ui-tank-empty = пусто
chassis-ui-tank-share = { $percent }%
chassis-ui-tank-rest = прочее { $percent }%
chassis-ui-tank-valve = Открыть клапан
chassis-ui-tank-valve-close = Закрыть клапан
chassis-ui-tank-valve-tooltip = Стравливает баллон в помещение. При открытом клапане внутреннее дыхание не работает.
chassis-ui-tank-pump-tooltip = Гоняет компрессор, пока костюм закрыт.
chassis-ui-no-tank = баллона нет

chassis-jetpack-no-gas = Двигателям нечем отталкиваться.
chassis-ui-eject-cell = Извлечь батарею
chassis-ui-eject-cell-tooltip = Кладёт ячейку питания из ядра вам в руку.
chassis-ui-insert-cell = Вставить батарею
chassis-ui-insert-cell-tooltip = Ставит батарею из руки в ядро.
chassis-ui-no-cell = нет батареи
chassis-ui-hopper = Бункер топлива
chassis-ui-hopper-tooltip = Загрузите ядро топливом. Оно сжигает столько, сколько нужно, и тогда, когда нужно.
chassis-config-open-storage = Открыть
chassis-config-pump = Компрессор
chassis-config-gas-oxygen = Кислород
chassis-config-gas-nitrogen = Азот
chassis-config-gas-carbondioxide = CO2
chassis-config-gas-plasma = Плазма
