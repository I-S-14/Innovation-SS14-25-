# ─── Типы научных данных ────────────────────────────────────────────────────
is14-research-point-science = Научные данные
is14-research-point-science-short = НАУ
is14-research-point-science-desc = Фундаментальные явления: артефакты, аномалии, неизвестные вещества. Единственная валюта, которую НИЦ добывает сам.
is14-research-donor-science = НИЦ

is14-research-point-industrial = Промышленные данные
is14-research-point-industrial-short = ПРО
is14-research-point-industrial-desc = Экстремальные режимы техники, новые материалы, обкатка изделий.
is14-research-donor-industrial = инженерия и снабжение

is14-research-point-military = Военные данные
is14-research-point-military-short = ВОЕН
is14-research-point-military-desc = Измеренное насилие: профили взрывов, баллистика, разбор трофейных образцов.
is14-research-donor-military = служба безопасности

is14-research-point-biological = Биологические данные
is14-research-point-biological-short = БИО
is14-research-point-biological-desc = Живой и мёртвый материал: причины смерти, штаммы, мутации.
is14-research-donor-biological = медицинский отсек

is14-research-point-social = Социологические данные
is14-research-point-social-short = СОЦ
is14-research-point-social-desc = Ответы и поведение живого экипажа. Заскриптовать нельзя — только разговаривать с людьми.
is14-research-donor-social = служба снабжения быта

# ─── Новизна результата ─────────────────────────────────────────────────────
is14-research-novelty-new = новые данные
is14-research-novelty-refined = уточнение
is14-research-novelty-repeat = повтор
is14-research-novelty-confirmed = подтверждение результата

# ─── Измерения ──────────────────────────────────────────────────────────────
is14-research-measured = Записано: { $amount } × { $type } ({ $novelty })
is14-research-measured-failed = Эксперимент сорвался. Отрицательный результат: { $amount } × { $type }. Повтор будет безопаснее.
is14-research-bench-busy = Прибор ещё обрабатывает предыдущий образец.
is14-research-bench-verb = Анализировать
is14-research-sample-useless = С этого образца снять нечего.
is14-research-no-station = Прибор не привязан ни к одной станции.
is14-research-scanner-needs-dead = Прибор работает только с мёртвым телом.
is14-research-scanner-needs-alive = Прибор снимает данные только с живого организма.
is14-research-scanner-start = Вы начинаете снимать показания...
is14-research-doppler-too-close = Взрыв мощностью { $intensity } зафиксирован в пределах станции. Данные не зачтены — испытания проводятся на удалении.

# ─── Анкетирование ──────────────────────────────────────────────────────────
is14-research-printer-busy = Терминал ещё печатает предыдущий бланк.
is14-research-printer-printed = Бланк анкеты распечатан.
is14-survey-filling = Вы заполняете анкету...
is14-survey-filled = Анкета заполнена. Сдайте её в НИЦ.
is14-survey-already-filled = Анкета уже заполнена.
is14-survey-not-filled = Анкета пустая — сначала пусть кто-нибудь ответит на вопросы.
is14-survey-examine-blank = Бланк не заполнен.
is14-survey-examine-filled = Заполнил: { $name } ({ $title }).

# ─── Консоль НИЦ ────────────────────────────────────────────────────────────
is14-research-console-title = Консоль НИЦ
is14-research-console-server = Сервер: { $name }
is14-research-console-no-server = Нет связи с сервером НИЦ
is14-research-console-income-hint = приход за последние { $minutes } мин
is14-research-console-prereq = Сначала нужно изучить предшествующие темы.
is14-research-console-cannot-afford = Недостаточно данных для этой темы.
is14-research-console-unlocked-broadcast = Изучена технология: { $technology }. Списано { $cost }. Утвердил: { $approver }
is14-research-counter-tooltip = За последние { $minutes } мин поступило: { $income }
is14-research-counter-donor = Поставщик: { $donor }

is14-research-card-button = Изучить
is14-research-card-tier = тир { $tier }
is14-research-card-researched = Изучено
is14-research-card-ready = Данных достаточно
is14-research-card-needs-data = Не хватает данных — красные графы выше
is14-research-card-needs-prereq = Требуется: { $list }
is14-research-card-needs-prereq-short = Требуются предшествующие темы
is14-research-card-no-server = Нет связи с сервером

# ─── Модификаторы станции ───────────────────────────────────────────────────
is14-modifier-research-payout = Отдача всех исследований
is14-modifier-research-payout-science = Отдача научных данных
is14-modifier-research-payout-industrial = Отдача промышленных данных
is14-modifier-research-payout-military = Отдача военных данных
is14-modifier-research-payout-biological = Отдача биологических данных
is14-modifier-research-payout-social = Отдача социологических данных
is14-modifier-novelty-retention = Ценность повторных измерений
is14-modifier-lathe-speed = Время печати на латах
is14-modifier-lathe-materials = Расход материалов на латах

# ─── Карта исследований ─────────────────────────────────────────────────────
is14-research-map-select-hint = Выберите тему на карте
is14-research-map-effects = Что даёт:
is14-research-map-unlocks = Открывает производство:
is14-research-map-prereqs = Требует:
is14-research-map-cost = Стоимость:
is14-research-map-legend-researched = изучено
is14-research-map-legend-available = доступно
is14-research-map-legend-locked = закрыто

# ─── Деструктивный анализатор ───────────────────────────────────────────────
is14-analyzer-started = Анализатор захватывает образец. Обратно он не выйдет.
is14-analyzer-nothing-to-take-apart = Разбирать тут нечего — состав образца неизвестен.
is14-analyzer-reverse-engineered = Обратная разработка: тема «{ $technology }» дешевле на { $percent }%.
is14-research-map-discount = Обратная разработка: −{ $percent }%
is14-research-map-no-prereqs = Предшествующих тем не требуется.
is14-research-map-prereq-done = Тема изучена.
is14-research-map-prereq-missing = Тема ещё не изучена.
is14-research-map-open-hint = Нажмите, чтобы открыть эту тему на карте.

is14-research-map-drag-hint = Карта перетаскивается мышью

# ─── Тахионно-доплеровский массив ───────────────────────────────────────────
is14-doppler-readout-header = Зафиксировано возмущение вблизи { $location }.
is14-doppler-readout-radii = Радиус эпицентра { $epicenter }, внешний { $outer }, ударная волна { $shockwave }. Суммарная интенсивность { $intensity }.
is14-doppler-readout-too-close = Взрыв в пределах станции. Данные не зачтены — испытания проводятся на удалении.

is14-doppler-facing-south = на юг
is14-doppler-facing-north = на север
is14-doppler-facing-east = на восток
is14-doppler-facing-west = на запад
is14-doppler-facing-southeast = на юго-восток
is14-doppler-facing-southwest = на юго-запад
is14-doppler-facing-northeast = на северо-восток
is14-doppler-facing-northwest = на северо-запад
is14-doppler-facing-invalid = в неизвестном направлении

is14-doppler-window-title = Тахионно-доплеровский массив
is14-doppler-window-facing = Сенсор развёрнут { $facing }
is14-doppler-window-best = Рекорд станции: интенсивность { $intensity }
is14-doppler-window-no-records = Рекорда пока нет: первый же замер его и поставит.
is14-doppler-window-hint = Массив видит только то, что находится в конусе перед ним, на удалении от 8 до 120 м. Разворачивается гаечным ключом или через меню.
is14-doppler-window-log = Журнал замеров
is14-doppler-window-empty = Журнал пуст.
is14-doppler-window-print = Протокол
is14-doppler-window-record-title = Замер №{ $number } · { $timestamp }
is14-doppler-window-record-body = { $location } (сетка { $coordinates }), дистанция { $distance } м.[color=#9a9a9a] Интенсивность { $intensity }; радиусы: эпицентр { $epicenter }, внешний { $outer }, волна { $shockwave }.[/color]
is14-doppler-window-verdict-record = рекорд, +{ $amount }
is14-doppler-window-verdict-repeat = повтор, +{ $amount }
is14-doppler-window-verdict-too-close = не зачтено
is14-doppler-protocol-printed = Протокол замера распечатан.
is14-doppler-protocol-body =
    ПРОТОКОЛ ОГНЕВОГО ЗАМЕРА №{ $number }
    Время: { $timestamp }
    Место: { $location }
    Эпицентр: сетка ({ $coordinates }), дистанция до массива { $distance } м

    Суммарная интенсивность: { $intensity }
    Спад на плитку: { $slope }
    Предел на плитку: { $peak }

    Радиус эпицентра: { $epicenter }
    Внешний радиус: { $outer }
    Радиус ударной волны: { $shockwave }

    Начислено военных данных: { $payout }

    Подпись ответственного: ____________________
is14-doppler-location-unknown = неопознанный сектор

# ─── Пусковая камера ордананса ──────────────────────────────────────────────
is14-transfer-valve-slot-a = Приёмный баллон
is14-transfer-valve-slot-b = Расходный баллон
is14-transfer-valve-slot-trigger = Спусковое устройство
is14-transfer-valve-wrong-tank = Сюда встанут только кислородный и плазменный баллоны.

is14-launch-console-title = Пульт запуска
is14-launch-console-ready = Готов к запуску
is14-launch-console-counting = До запуска: { $seconds } с
is14-launch-console-countdown = Задержка, с
is14-launch-console-launch = ЗАПУСК
is14-launch-console-abort = Отмена
is14-launch-console-links = Связано устройств — до пуска: { $pre }, на пуск: { $launch }, после: { $post }. Упреждение { $lead } с.
is14-launch-console-not-linked = К выходу пуска ничего не подключено. Свяжите пульт сетевым конфигуратором.

# ─── Порты пульта запуска ───────────────────────────────────────────────────
is14-signal-port-name-launch-pre = Перед пуском
is14-signal-port-description-launch-pre = Срабатывает за время упреждения до пуска. Сюда вешают открытие шлюзов.
is14-signal-port-name-launch = Пуск
is14-signal-port-description-launch = Срабатывает ровно в момент пуска, когда истекла выставленная задержка. Сюда вешают масс-драйвер.
is14-signal-port-name-launch-post = После пуска
is14-signal-port-description-launch-post = Срабатывает через время упреждения после пуска. Сюда вешают закрытие шлюзов.

# ─── Прорывы, развилки и приоритеты ────────────────────────────────────────
is14-research-console-needs-sample = Эту тему не купить за данные — нужен образец в деструктивном анализаторе.
is14-research-console-excluded = Выбран другой путь: альтернативная тема уже изучена.
is14-research-card-needs-sample = Требуется образец для прорыва
is14-research-card-excluded = Путь закрыт: изучена альтернатива
is14-research-map-breakthrough = Прорыв — нужен образец:
is14-research-map-breakthrough-done = Образец разобран, тема открыта.
is14-research-map-breakthrough-missing = Образец ещё не разобран в деструктивном анализаторе.
is14-research-map-exclusive = Исключает темы:
is14-research-map-exclusive-taken = Эта альтернатива уже изучена — путь закрыт.
is14-research-map-exclusive-open = Изучив эту тему, вы закроете альтернативу.
is14-research-map-priority = Приоритет Госплана: −{ $percent }%
is14-analyzer-breakthrough-broadcast = Прорыв: образец разобран, открыто направление «{ $technology }».
is14-modifier-lathe-speed-science = Время печати на научных латах
is14-modifier-lathe-materials-science = Расход материалов на научных латах
is14-modifier-lathe-speed-medical = Время печати на медицинских латах
is14-modifier-lathe-materials-medical = Расход материалов на медицинских латах
is14-modifier-lathe-speed-arms = Время печати на оружейных латах
is14-modifier-lathe-materials-arms = Расход материалов на оружейных латах
is14-modifier-lathe-speed-industrial = Время печати на промышленных латах
is14-modifier-lathe-materials-industrial = Расход материалов на промышленных латах
is14-modifier-lathe-speed-service = Время печати на сервисных латах
is14-modifier-lathe-materials-service = Расход материалов на сервисных латах

# ─── Телеметрия и атмосферные пробы ────────────────────────────────────────
is14-gas-sample-empty = В ёмкости нечего мерить — газа почти нет.

# ─── Подопытные и согласия ─────────────────────────────────────────────────
is14-consent-signing = Вы подписываете бланк согласия...
is14-consent-signed = Согласие подписано.
is14-consent-already-signed = Бланк уже подписан.
is14-consent-examine-blank = Бланк не подписан.
is14-consent-examine-signed = Подписал: { $name } ({ $title }).
is14-volunteer-scan-start = Вы снимаете показатели...
is14-volunteer-no-consent = Вас исследуют без вашего согласия!
is14-volunteer-paid = Вам начислено { $amount } кр. за участие в исследовании.
is14-volunteer-no-funds = У науки нет средств на выплату добровольцу.
is14-volunteer-payment-description = Оплата участия в исследовании НИЦ

# ─── Техзадания отделов ────────────────────────────────────────────────────
is14-workorder-window-title = Терминал заявок
is14-workorder-current = Текущая заявка:
is14-workorder-none = заявки нет
is14-workorder-available = Что можно заказать:
is14-workorder-order = Заказать
is14-workorder-cancel = Отозвать заявку
is14-workorder-terms = За выполнение: { $bonus } × { $type } и { $payment } кр. из бюджета отдела
is14-workorder-budget = Бюджет отдела: { $balance } кр.
is14-workorder-no-access = Заявку подписывает глава отдела.
is14-workorder-placed-broadcast = Заявка от отдела «{ $department }»: требуется «{ $technology }». Подписал: { $signer }
is14-workorder-delivered-broadcast = Заявка отдела «{ $department }» выполнена: «{ $technology }». НИЦ получает { $bonus } данных и { $payment } кр.
is14-workorder-department-engineering = Инженерия
is14-workorder-department-medical = Медотсек
is14-workorder-department-security = Служба безопасности
is14-workorder-department-cargo = Снабжение
is14-workorder-department-service = Сервисная служба

# ─── Публикации ────────────────────────────────────────────────────────────
is14-publication-printed = Научная работа напечатана. Нужны печати научного руководителя и капитана.
is14-publication-nothing-to-report = Писать пока не о чем — станция ещё ничего не изучила.
is14-publication-already-filed = Этот результат уже опубликован.
is14-publication-not-a-paper = Это не научная работа.
is14-publication-needs-stamp = Не хватает печати: { $stamp }.
is14-publication-body = Научная работа НИЦ. Тема: { $technology } (тир { $tier }). Результат получен, методика приложена, подписи ниже.
is14-publication-accepted-broadcast = Академия наук приняла работу по теме «{ $technology }»: { $amount } научных данных и { $payment } кр. на счёт науки.

# ─── Обратная разработка трофеев ───────────────────────────────────────────
is14-reverse-started = Стенд захватывает образец. Обратно он не выйдет.
is14-reverse-nothing-to-learn = Из этого образца нечего извлечь — он не чужой разработки.
is14-reverse-revealed = Обратная разработка завершена: открыто закрытое направление «{ $technology }».
is14-research-map-zoom = Масштаб
is14-research-map-center = По центру
