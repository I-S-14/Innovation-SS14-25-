bank-salary-notification = Зарплата +{ $salary } кр. Баланс: { $balance } кр.
bank-salary-delayed = Задержка зарплаты! Фонд отдела пуст. Задолженность: { $owed } кр. Обратитесь к главе отдела.

# Economy transaction descriptions (used in monitor logs)
economy-transaction-salary = Зарплата
economy-transaction-salary-fund = Выплата зарплаты сотруднику
economy-transaction-vending-purchase = Покупка: { $item } ({ $machine })
economy-transaction-vending-revenue = Выручка: { $item } ({ $machine })
economy-transaction-vending-tax = Налог с продажи ({ $machine })
economy-transaction-atm-withdraw = Снятие наличных
economy-transaction-atm-deposit = Внесение наличных
economy-transaction-atm-transfer-out = Перевод на счёт #{ $target }
economy-transaction-atm-transfer-in = Перевод со счёта #{ $source }

# Cash
is14-stack-credit = кредиты

# ATM
is14-atm-window-title = Банкомат
is14-atm-card-slot-name = Слот для ID-карты
is14-atm-card-header = { $owner } | Счёт #{ $account }
is14-atm-no-card = Вставьте ID-карту
is14-atm-no-account = Карта не привязана к счёту.
is14-atm-set-pin-prompt = Придумайте PIN-код (4 цифры)
is14-atm-enter-pin-prompt = Введите PIN-код
is14-atm-change-pin-prompt = Новый PIN-код (4 цифры)
is14-atm-attempts-left = Осталось попыток: { $attempts }
is14-atm-locked = Счёт заблокирован. Попробуйте через { $seconds } сек.
is14-atm-balance-label = Баланс:
is14-atm-withdraw-label = Снять наличные
is14-atm-withdraw = Снять
is14-atm-transfer-label = Перевод на другой счёт
is14-atm-transfer = Перевести
is14-atm-transfer-account-placeholder = Номер счёта
is14-atm-amount-placeholder = Сумма
is14-atm-deposit-hint = [color=#666E80]Чтобы внести наличные, приложите купюры к банкомату.[/color]
is14-atm-change-pin = Сменить PIN
is14-atm-eject-card = Извлечь карту
is14-atm-cancel = Отмена
is14-atm-status-wrong-pin = Неверный PIN.
is14-atm-status-locked-now = Слишком много попыток. Счёт заблокирован.
is14-atm-status-pin-set = PIN установлен.
is14-atm-status-pin-changed = PIN изменён.
is14-atm-status-insufficient = Недостаточно средств.
is14-atm-status-bad-target = Счёт получателя не найден.
is14-atm-status-own-account = Это ваш счёт.
is14-atm-status-transfer-done = Перевод выполнен.
is14-atm-status-withdraw-done = Возьмите наличные.
is14-atm-status-deposited = Зачислено { $amount } кр.
is14-atm-popup-need-auth = Сначала вставьте карту и войдите в систему.

# Payment terminal
is14-payterm-window-title = Платёжный терминал
is14-payterm-recipient = Получатель: { $name }
is14-payterm-no-recipient = Получатель не привязан
is14-payterm-charge-label = Выставить счёт
is14-payterm-amount-placeholder = Сумма
is14-payterm-desc-placeholder = Комментарий (необязательно)
is14-payterm-set-charge = Выставить
is14-payterm-pending = К оплате: { $amount } кр.
is14-payterm-pay = Оплатить картой
is14-payterm-cancel = Отменить
is14-payterm-bind = Привязать мою карту
is14-payterm-status-bound = Карта привязана.
is14-payterm-status-paid = Оплачено: { $amount } кр.
is14-payterm-status-denied = Оплата отклонена.
is14-payterm-status-no-card = ID-карта со счётом не найдена.
is14-payterm-status-no-recipient = Получатель не привязан.
is14-payterm-status-bad-amount = Некорректная сумма.
is14-payterm-receipt-note = Назначение: { $desc }
is14-payterm-receipt-content = [head=2]ЧЕК[/head]
    Получатель: { $recipient }
    Сумма: [bold]{ $amount } кр.[/bold]
    { $note }
    Спасибо за покупку!
economy-transaction-terminal-payment = Оплата через терминал — { $recipient }
economy-transaction-terminal-payment-desc = Оплата: { $desc } — { $recipient }
economy-transaction-terminal-revenue = Выручка терминала
economy-transaction-terminal-tax = Налог с выручки терминала

# Treasury console UI
is14-treasury-window-title = Консоль казначейства
is14-treasury-balance-label = Казна станции:
is14-treasury-funds-label = Фонды отделов
is14-treasury-transfer-label = Ассигнование из казны
is14-treasury-transfer = Перевести
is14-treasury-amount-placeholder = Сумма
is14-treasury-credits = { $amount } кр.
is14-treasury-status-bad-amount = Некорректная сумма.
is14-treasury-status-bad-target = Фонд получателя не найден.
is14-treasury-status-insufficient = В казне недостаточно средств.
is14-treasury-status-done = Переведено { $amount } кр. — { $target }.
economy-transaction-allocation-out = Ассигнование: { $target }
economy-transaction-allocation-in = Ассигнование из казны

# Payroll console UI
is14-payroll-window-title = Консоль расчёта зарплат
is14-payroll-tab-payroll = Расчёт
is14-payroll-tab-log = Журнал
is14-payroll-col-name = Сотрудник
is14-payroll-col-job = Должность
is14-payroll-col-salary = Оклад
is14-payroll-salary-label = Оклад за смену
is14-payroll-salary-placeholder = Оклад
is14-payroll-salary-apply = Применить
is14-payroll-oneoff-label = Разовая выплата или взыскание
is14-payroll-amount-placeholder = Сумма
is14-payroll-limits = Премиальный фонд { $pool } кр. · доступно к выплате { $bonus } кр. · штраф до { $fine } кр.
is14-payroll-bonus = Премировать
is14-payroll-fine = Оштрафовать
is14-payroll-credits = { $amount } кр.
is14-payroll-owed = долг { $amount }
is14-payroll-no-selection = Сотрудник не выбран
is14-payroll-selection = { $name } — { $job }
is14-payroll-selection-hint = Базовый оклад { $base } кр. · допустимо { $min }–{ $max } кр.
is14-payroll-row-tooltip = Базовый оклад: { $base } кр. Допустимый диапазон: { $min }–{ $max } кр.
is14-payroll-unknown-job = Должность неизвестна
is14-payroll-status-bad-salary = Оклад должен быть в пределах { $min }–{ $max } кр.
is14-payroll-status-bad-bonus = В премиальном фонде только { $max } кр. Он пополняется с поступлений отдела.
is14-payroll-status-bad-fine = Штраф не может превышать { $max } кр.
is14-payroll-status-not-subordinate = Этот сотрудник не на довольствии вашего отдела.
is14-payroll-status-no-self = Нельзя проводить начисления самому себе.
is14-payroll-status-no-fund = Бюджет отдела недоступен.
is14-payroll-status-fund-insufficient = В бюджете отдела недостаточно средств.
is14-payroll-status-no-account = Счёт сотрудника не найден.
is14-payroll-status-employee-broke = На счету сотрудника пусто — взыскивать нечего.
is14-payroll-status-salary-set = Оклад { $name } установлен: { $salary } кр.
is14-payroll-status-bonus-paid = { $name } премирован на { $amount } кр.
is14-payroll-status-fine-collected = С { $name } удержано { $amount } кр.

is14-payroll-log-empty = Действий пока не было.
is14-payroll-log-raise = Оклад повышен: { $name } — { $old } → { $new } кр.
is14-payroll-log-cut = Оклад понижен: { $name } — { $old } → { $new } кр.
is14-payroll-log-bonus = Премия: { $name } — { $amount } кр.
is14-payroll-log-fine = Штраф: { $name } — { $amount } кр.

is14-payroll-notify-raise = Ваш оклад повышен до { $salary } кр.
is14-payroll-notify-cut = Ваш оклад понижен до { $salary } кр.
is14-payroll-notify-bonus = Начислена премия: { $amount } кр.
is14-payroll-notify-fine = Удержан штраф: { $amount } кр.

economy-transaction-bonus = Премия
economy-transaction-bonus-fund = Премия сотруднику — { $name }
economy-transaction-fine = Штраф
economy-transaction-fine-fund = Взыскание с сотрудника — { $name }
economy-transaction-salary-changed = Оклад изменён: { $name } — { $old } → { $new } кр.

# Economy monitor console UI
economy-monitor-window-title = Монитор экономики
economy-monitor-no-records = Транзакций пока нет.
economy-monitor-col-time = Время
economy-monitor-col-account = Счёт
economy-monitor-col-amount = Сумма
economy-monitor-col-description = Описание
economy-monitor-col-balance = Баланс
economy-monitor-search-account = Счёт:
economy-monitor-search-placeholder = Номер счёта...
economy-monitor-unknown-vendor = Неизвестное устройство
economy-monitor-delete = Удалить
economy-monitor-print = Печать
economy-monitor-report-name = отчёт по транзакциям
economy-monitor-report-header = [head=2]Отчёт по транзакциям[/head]
economy-monitor-report-line = { $time } | счёт #{ $account } | { $amount } кр. | { $description } | баланс: { $balance }
bank-holder-examine-balance = Баланс счёта: [color=green]{ $balance } кр.[/color]

is14-vending-balance-label = Баланс:
is14-vending-balance-value = { $balance } кр.
is14-vending-price = { $price } кр.
is14-vending-stock = x{ $stock }
is14-vending-out-of-stock = Нет
is14-vending-no-access-tooltip = Требуется доступ на ID-карте
is14-vending-tab-contraband = ☠ Контрабанда
is14-vending-search-placeholder = Поиск...
is14-vending-unknown = Неизвестно

is14-vending-ad-engine-1 = Точные инструменты. Ненадёжные люди. Не наша проблема.
is14-vending-ad-engine-2 = Хороший ключ никогда никого не предавал.

is14-vending-ad-medical-1 = Первая помощь лучше, чем вторая.
is14-vending-ad-medical-2 = Ваше здоровье — наш бизнес. Буквально.
