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
chassis-ui-kind-passive = пассивный
chassis-ui-kind-toggleable = включаемый
chassis-ui-kind-usable = разовый
chassis-ui-kind-active = активный
chassis-ui-module-complexity = Сложность { $value }
chassis-ui-module-idle = { $value } Вт покоя
chassis-ui-module-active = { $value } Вт работы
chassis-ui-module-use = { $value } Дж за раз
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
chassis-ui-hw-panel = Панель
chassis-ui-hw-lock = ID-замок
chassis-ui-hw-draw = Расход
chassis-ui-lock-engaged = заперт
chassis-ui-lock-open = открыт
chassis-ui-lock-wiped = доступ стёрт
chassis-ui-hw-hint = Отвёртка открывает панель, лом извлекает модули, ID-карта переключает замок.

# ID-замок и саботаж

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
chassis-ui-integrity-broken = Разбита слишком сильно, модули не держит. Герметичность сохраняется.
modsuit-part-broken = { $part } проминается — крепления обесточиваются.
