# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

battery-status-charge = Заряд: [color=#5E7C16]{ $percent }[/color] %

battery-status-switchable-state = { $state ->
    [on] [color=green]Вкл[/color]
    [off] [color=red]Выкл[/color]
    *[other] Неизвестно
}

battery-status-state = Статус: { $state }

charge-status-count = Заряды: [color=fuchsia]{ $current }/{ $max }[/color]

charge-status-recharge = Перезарядка: [color=yellow]{ $seconds }с[/color]

tank-pressure-status = Давл.: [color=orange]{ $pressure } кПа[/color]

tank-status-switchable-state = { $state ->
    [open] [color=red]Открыт[/color]
    [closed] [color=green]Закрыт[/color]
    *[other] Неизвестно
}

tank-status-state = Клапан: { $state }

magazine-status-rounds = Патроны: [color=yellow]{ $current }/{ $max }[/color]

guardian-status-used = [color=red]Использован[/color]

guardian-status-ready = [color=green]Готов[/color]

anomaly-status-infinite = [color=gold]Бесконечные заряды[/color]

anomaly-status-charges = [color=orange]{ $charges } зарядов[/color]

timer-trigger-status-delay = Задержка: [color=white]{ $delay } сек[/color]

timer-trigger-status-delay-unknown = Установить задержку: [color=gray]???[/color]
