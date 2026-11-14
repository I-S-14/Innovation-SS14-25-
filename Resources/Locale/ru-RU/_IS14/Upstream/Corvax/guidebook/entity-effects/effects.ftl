# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

entity-effect-guidebook-satiate =
    { $chance ->
        [1] Утоляет
        *[other] утолить
    } { $relative ->
        [1] {$type}
        *[other] {$type} с { NATURALFIXED($relative, 3) }х эффективностью
    }

entity-effect-guidebook-plant-change-trait =
    { $chance ->
        [1] { $change ->
            [Add] Добавляет {$trait} в растение
            [Remove] Удаляет {$trait} из растения
            [Toggle] Переключает {$trait} в растении
            *[other] Изменяет {$trait} в растении
        }
        *[other] { $change ->
            [Add] добавить {$trait} в растение
            [Remove] удалить {$trait} из растения
            [Toggle] переключить {$trait} в растении
            *[other] изменить {$trait} в растении
        }
    }

entity-effect-guidebook-plant-mutate-exude-gasses =
    { $chance ->
        [1] Мутирует
        *[other] мутировать
    } выделение растением газов от {$minValue} до {$maxValue} молей

entity-effect-guidebook-plant-mutate-consume-gasses =
    { $chance ->
        [1] Мутирует
        *[other] мутировать
    } потребление растением газов от {$minValue} до {$maxValue} молей

entity-effect-disarm =
    { $chance ->
        [1] Обезоруживает
        *[other] обезоружить
    } цель

entity-effect-guidebook-make-trap-in-container =
    { $chance ->
        [1] Заключает
        *[other] заключить
    } цель в { $entityname }
