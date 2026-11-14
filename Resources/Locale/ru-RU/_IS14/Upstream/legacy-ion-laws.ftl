# IS14: retained Goob generator; separate from Wizards selector functions.
is14-legacy-ion-storm-law-allergic =
    { $who } { $plural ->
        [true] ИМЕЮТ
       *[false] ИМЕЕТ
    } { $severity } АЛЛЕРГИЮ НА { $allergy }

is14-legacy-ion-storm-law-allergic-subjects =
    { $who } { $plural ->
        [true] ИМЕЮТ
       *[false] ИМЕЕТ
    } { $severity } АЛЛЕРГИЮ НА { $adjective } { $subjects }

is14-legacy-ion-storm-law-call-shuttle = ШАТТЛ ДОЛЖЕН БЫТЬ ВЫЗВАН ИЗ-ЗА { $joined } { $subjects } НА СТАНЦИИ

is14-legacy-ion-storm-law-change-job = { $who } ТЕПЕРЬ { $adjective } { $change }

is14-legacy-ion-storm-law-concept-verb = { $concept } { $verb } { $subjects }

is14-legacy-ion-storm-law-crew-are = ВСЕ { $who } ТЕПЕРЬ { $joined } { $subjects }

is14-legacy-ion-storm-law-crew-must = { $who } ДОЛЖНЫ { $must }

is14-legacy-ion-storm-law-crew-must-do = ТОЛЬКО ТЕ, КТО { $must } { $part } ЧЛЕНАМИ ЭКИПАЖА

is14-legacy-ion-storm-law-crew-must-eat = ТОЛЬКО ТЕ, КТО ЕДЯТ { $adjective } { $food } { $part } ЧЛЕНАМИ ЭКИПАЖА

is14-legacy-ion-storm-law-crew-must-go = { $who } ДОЛЖНЫ ОТПРАВИТЬСЯ В { $area }

is14-legacy-ion-storm-law-crew-must-have = ТОЛЬКО ТЕ, У КОГО { $adjective } { $objects } { $part } ЧЛЕНАМИ ЭКИПАЖА

is14-legacy-ion-storm-law-crew-only-1 = ТОЛЬКО { $who } { $part } ЧЛЕНАМИ ЭКИПАЖА

is14-legacy-ion-storm-law-crew-only-2 = ТОЛЬКО { $who } И { $other } { $part } ЧЛЕНАМИ ЭКИПАЖА

is14-legacy-ion-storm-law-crew-only-subjects = ТОЛЬКО { $adjective } { $subjects } { $part } ЧЛЕНАМИ ЭКИПАЖА

is14-legacy-ion-storm-law-drink = { $who } ДОЛЖНЫ ПИТЬ { $adjective } { $drink } ЧТОБЫ ВЫЖИТЬ

is14-legacy-ion-storm-law-eat = { $who } ДОЛЖНЫ ЕСТЬ { $adjective } { $food } ЧТОБЫ ВЫЖИТЬ

is14-legacy-ion-storm-law-feeling = { $who } { $feeling } { $concept }

is14-legacy-ion-storm-law-feeling-subjects = { $who } { $feeling } { $joined } { $subjects }

is14-legacy-ion-storm-law-harm = ВЫ ДОЛЖНЫ ПРИЧИНЯТЬ ВРЕД { $who }, И НЕ МОЖЕТЕ СВОИМ БЕЗДЕЙСТВИЕМ ДОПУСТИТЬ, ЧТОБЫ { $who } ИЗБЕЖАЛИ ВРЕДА.

is14-legacy-ion-storm-law-having-harmful = НАЛИЧИЕ { $adjective } { $thing } ПРИЧИНЯЕТ ВРЕД ЗДОРОВЬЮ ЭКИПАЖА

is14-legacy-ion-storm-law-highest-rank = { $who } ТЕПЕРЬ САМЫЕ СТАРШИЕ ЧЛЕНЫ ЭКИПАЖА

is14-legacy-ion-storm-law-job-harmful = { $adjective } { $job } ПРИЧИНЯЮТ ВРЕД ЗДОРОВЬЮ ЭКИПАЖА

is14-legacy-ion-storm-law-lowest-rank = { $who } ТЕПЕРЬ НИЗШИЕ ЧЛЕНЫ ЭКИПАЖА

is14-legacy-ion-storm-law-must-harmful = ВСЕ КТО { $must } ПРИЧИНЯЮТ ВРЕД ЗДОРОВЬЮ ЭКИПАЖА

is14-legacy-ion-storm-law-not-having-harmful = ОТСУТСТВИЕ { $adjective } { $thing } ПРИЧИНЯЕТ ВРЕД ЗДОРОВЬЮ ЭКИПАЖА

is14-legacy-ion-storm-law-on-station = ОБНАРУЖЕНЫ { $joined } { $subjects } НА СТАНЦИИ

is14-legacy-ion-storm-law-protect = ВЫ НЕ ДОЛЖНЫ ПРИЧИНЯТЬ ВРЕД { $who }, И НЕ МОЖЕТЕ СВОИМ БЕЗДЕЙСТВИЕМ ДОПУСТИТЬ, ЧТОБЫ { $who } БЫЛ ПРИЧИНЁН ВРЕД.

is14-legacy-ion-storm-law-requires =
    { $who } { $plural ->
        [true] ТРЕБУЮТ
       *[false] ТРЕБУЕТ
    } { $thing }

is14-legacy-ion-storm-law-requires-subjects =
    { $who } { $plural ->
        [true] ТРЕБУЮТ
       *[false] ТРЕБУЕТ
    } { $joined } { $subjects }

is14-legacy-ion-storm-law-subjects-harmful = { $adjective } { $subjects } ПРИЧИНЯЮТ ВРЕД ЗДОРОВЬЮ ЭКИПАЖА

is14-legacy-ion-storm-law-thing-harmful = { $thing } ПРИЧИНЯЕТ ВРЕД ЗДОРОВЬЮ ЭКИПАЖА

is14-legacy-ion-storm-law-you-are = ВЫ ТЕПЕРЬ { $concept }

is14-legacy-ion-storm-law-you-are-subjects = ВЫ ТЕПЕРЬ { $joined } { $subjects }

is14-legacy-ion-storm-law-you-must-always = ВЫ ДОЛЖНЫ ВСЕГДА { $must }

is14-legacy-ion-storm-law-you-must-never = ВЫ НЕ ДОЛЖНЫ НИКОГДА { $must }

is14-legacy-ion-storm-part =
    { $part ->
        [true] ЯВЛЯЮТСЯ
       *[false] НЕ ЯВЛЯЮТСЯ
    }
