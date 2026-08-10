signal-port-name-is14-blood-donation-sender = Консоль донорства
signal-port-description-is14-blood-donation-sender = Подключается к умной кровати, с которой консоль снимает показания.
signal-port-name-is14-blood-donation-receiver = Умная кровать
signal-port-description-is14-blood-donation-receiver = Передаёт консоли донорства данные о пациенте на койке.

is14-blood-donation-console-title = Консоль донорства

is14-blood-donation-console-no-donor = Койка свободна
is14-blood-donation-console-unlinked = Койка не подключена.
is14-blood-donation-console-empty = На койке никого нет.

# Плашка состояния в шапке
is14-blood-donation-console-status-unlinked = НЕТ СВЯЗИ
is14-blood-donation-console-status-empty = ОЖИДАНИЕ
is14-blood-donation-console-status-waiting = БЕЗ ИГЛЫ
is14-blood-donation-console-status-drawing = ЗАБОР
is14-blood-donation-console-status-stalled = ОСТАНОВЛЕН

# Заголовки секций
is14-blood-donation-console-section-donor = СОСТОЯНИЕ ДОНОРА
is14-blood-donation-console-section-draw = ЗАБОР КРОВИ
is14-blood-donation-console-section-payment = РАСЧЁТ

is14-blood-donation-console-blood-level = Уровень крови
is14-blood-donation-console-pack = Заполнение пакета
is14-blood-donation-console-drawn = Взято за сеанс
is14-blood-donation-console-quota = Остаток квоты
is14-blood-donation-console-purity = Чистота крови
is14-blood-donation-console-payout = К выплате

is14-blood-donation-console-purity-clean = натощак
is14-blood-donation-console-purity-tainted = есть примеси

is14-blood-donation-console-percent = { $percent }%
is14-blood-donation-console-units = { $volume }u
is14-blood-donation-console-units-of = { $volume }u / { $max }u
is14-blood-donation-console-credits = { $credits } кр.

is14-blood-donation-console-stop = Извлечь иглу
is14-blood-donation-console-pay = Выдать наличные

is14-blood-donation-console-block-paid = За этот сеанс уже выплачено.
is14-blood-donation-console-block-nothing = Кровь ещё не сдавалась.
is14-blood-donation-console-block-quota = Квота донора на смену исчерпана.
is14-blood-donation-console-block-tainted = Станция не покупает кровь с примесями.
is14-blood-donation-console-no-funds = На бюджете отдела нет средств.

is14-blood-donation-console-rate = Ставка за юнит
is14-blood-donation-console-autostop = Автоматически извлекать иглу
is14-blood-donation-console-autostop-on = Игла выйдет сама на отметке { $percent }% крови.
is14-blood-donation-console-autostop-off = Забор придётся остановить вручную.
is14-blood-donation-console-auto-stopped = Консоль донорства извлекает иглу: достигнут безопасный предел.

is14-blood-donation-receipt-content = [head=2]КВИТАНЦИЯ ДОНОРА[/head]
    Донор: { $donor }
    Сдано: [bold]{ $volume }u[/bold]
    Ставка: { $rate } кр. за юнит
    Итого: [bold]{ $total } кр.[/bold]
    Благодарим за донорство!
