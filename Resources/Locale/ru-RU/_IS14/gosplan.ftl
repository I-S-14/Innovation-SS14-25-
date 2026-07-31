# ─── Announcements ──────────────────────────────────────────────────────────
is14-gosplan-announcer = Госплан
is14-gosplan-plan-issued = Станции спущен План на период №{ $period }. Отчётный период — { $minutes } мин. Финансирование отделов производится по проценту выполнения. Товарищи, к работе!
is14-gosplan-report-header = Итоги периода №{ $period }:
is14-gosplan-report-line = { $fund }: { $percent }%, ассигновано { $payout } кр.
is14-gosplan-report-line-failed = ⤷ сорвано: { $quotas }
is14-gosplan-report-banner = Переходящее Красное знамя присуждается: { $fund }. Отделу выделен премиальный фонд.
is14-gosplan-report-sanction = Второй провал плана подряд: { $fund }. Взыскано в доход Госплана { $amount } кр. Ожидайте комиссию.

# ─── Monitor log ────────────────────────────────────────────────────────────
economy-transaction-gosplan-funding = Ассигнование Госплана — { $quota }
economy-transaction-gosplan-sanction = Взыскание Госплана за срыв плана

# ─── Quotas ─────────────────────────────────────────────────────────────────
is14-quota-engineering-power = Бесперебойное энергоснабжение
is14-quota-engineering-power-desc = Доля отсеков под напряжением. Считается непрерывно: обесточка бьёт по фонду, даже если её потом устранили.

is14-quota-engineering-atmos = Атмосфера в норме
is14-quota-engineering-atmos-desc = Доля воздушных сигнализаций в штатном режиме. Усредняется за период: пробоина стоит денег всё время, пока горит тревога.

is14-quota-medical-crew = Сохранность личного состава
is14-quota-medical-crew-desc = Доля живых членов экипажа. Усредняется за период — час в мешке для трупов не списывается воскрешением.

is14-quota-security-peace = Общественный порядок
is14-quota-security-peace-desc = Доля времени на зелёном коде. Каждая минута повышенной тревоги стоит отделу денег.

is14-quota-security-fines = План по взысканиям
is14-quota-security-fines-desc = Штрафов выписано за период. Аннулированные не в счёт — приписки Госплан не оплачивает.

is14-quota-research-tech = Внедрение технологий
is14-quota-research-tech-desc = Технологий исследовано за отчётный период.

is14-quota-cargo-revenue = Плановая выручка снабжения
is14-quota-cargo-revenue-desc = Кредитов поступило в фонд отдела за период.

is14-quota-service-revenue = Обслуживание товарищей
is14-quota-service-revenue-desc = Кредитов поступило в фонд сервиса за период — бар, кухня, автоматы.

# ─── Soc-competition board ──────────────────────────────────────────────────
is14-gosplan-window-title = Доска соцсоревнования
is14-gosplan-tab-plan = План
is14-gosplan-tab-history = Пятилетки
is14-gosplan-motto = Пятилетку — в четыре смены!

# ── Шапка с таймером ────────────────────────────────────────────────────────
is14-gosplan-header-caption = ОТЧЁТНЫЙ ПЕРИОД
is14-gosplan-period = Период №{ $period }
is14-gosplan-timer-caption = до подведения итогов
is14-gosplan-timer-scoring = идёт подведение итогов
is14-gosplan-elapsed = Период пройден на { $percent }%
is14-gosplan-quota-count = Отделов: { $departments }, позиций плана: { $count }
is14-gosplan-no-plan = План не спущен
is14-gosplan-no-plan-hint = Госплан не довёл до станции плановых заданий.

# ── Переходящее Красное знамя ───────────────────────────────────────────────
is14-gosplan-banner-title = ПЕРЕХОДЯЩЕЕ КРАСНОЕ ЗНАМЯ
is14-gosplan-banner-since = Присуждено по итогам периода №{ $period }
is14-gosplan-banner-bonus = премиальный фонд +{ $amount } кр.
is14-gosplan-banner-none = Знамя не присуждено
is14-gosplan-banner-none-hint = Перевыполните план — и оно будет вашим, товарищи.

# ── Отделы ──────────────────────────────────────────────────────────────────
is14-gosplan-departments = ОТДЕЛЫ
is14-gosplan-department-banner-mark = ★ { $fund }
is14-gosplan-department-banner = держит знамя
is14-gosplan-department-metrics = Позиций плана: { $count }
is14-gosplan-department-streak = ⚠ Провалов подряд: { $streak }
is14-gosplan-department-caption = Среднее по всем позициям плана ({ $count }) — по нему и спрос

# ── Строка плана ────────────────────────────────────────────────────────────
is14-gosplan-percent = { $percent }%
is14-gosplan-progress = { $current } из { $target }
is14-gosplan-unit-percent = { $value }%
is14-gosplan-unit-credits = { $value } кр.
is14-gosplan-mark-fail = срыв < { $percent }%
is14-gosplan-mark-target = план { $percent }%
is14-gosplan-mark-cap = потолок { $percent }%
is14-gosplan-payout-line = Ассигнование: { $current } из { $full } кр.
is14-gosplan-status-over = перевыполнен
is14-gosplan-status-ontrack = в графике
is14-gosplan-status-failing = срыв плана
is14-gosplan-note-warning = ⚠ План провален. Провалов подряд: { $streak }. Ещё один — взыскание.
is14-gosplan-note-last = В прошлом периоде: { $percent }%

# ── Пятилетки ───────────────────────────────────────────────────────────────
is14-gosplan-history-empty = Отчётных периодов пока не было.
is14-gosplan-history-badge = ПЕРИОД №{ $period }
is14-gosplan-history-latest = только что подведён
is14-gosplan-history-average = Среднее выполнение по станции
is14-gosplan-history-payout = Ассигновано: { $payout } кр.
is14-gosplan-history-leader = ★ Знамя: { $fund }
is14-gosplan-history-failed = Провалено: { $funds }
