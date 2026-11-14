# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

entity-condition-guidebook-total-satiation =
    { $max ->
        [2147483648] цель имеет по крайней мере { NATURALFIXED($min, 2) } общего { $type }
        *[other] { $min ->
                [0] цель имеет не более { NATURALFIXED($max, 2) } общего { $type }
                *[other] цель имеет между { NATURALFIXED($min, 2) } и { NATURALFIXED($max, 2) } общего { $type }
            }
    }

entity-condition-guidebook-mouth-uncovered-condition =
    у существа не прикрыт рот

entity-condition-guidebook-bloodlevel =
    { $max ->
        [2147483648] уровень крови у цели как минимум {NATURALFIXED($min, 2)}%
        *[other] { $min ->
                    [0] уровень крови у цели в сумме не более {NATURALFIXED($max, 2)}%
                    *[other] уровень крови у цели в диапазоне {NATURALFIXED($min, 2)}% и {NATURALFIXED($max, 2)}%
                 }
    }
