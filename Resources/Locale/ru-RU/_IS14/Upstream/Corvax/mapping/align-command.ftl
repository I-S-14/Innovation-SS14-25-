# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

cmd-align-desc = Автоматически выравнивает закреплённые шлюзы, двери, пожарные шлюзы и другие объекты по соседним конструкциям.
    Параметр [dry run] запускает проверку без поворота объектов.

cmd-align-help = Запуск — { $command } [MapID] [dry run?]

cmd-align-no-release = Команда недоступна в конфигурации RELEASE.

cmd-align-hint-id = MapID

cmd-align-hint-dry = dry run?

cmd-align-feedback-none =
    { $dry ->
        [true] Проверка завершена. Объекты, поддерживаемые AlignerSystem, отсутствуют.
       *[false] Объекты, поддерживаемые AlignerSystem, отсутствуют.
    }

cmd-align-feedback-good =
    { $dry ->
        [true] Проверка завершена. Объекты с неверным поворотом отсутствуют.
       *[false] Объекты с неверным поворотом отсутствуют.
    }

cmd-align-feedback =
    { $dry ->
        [true] Найдено объектов с неверным поворотом
       *[false] Найдено и исправлено объектов с неверным поворотом
    } — { $fixed }.
