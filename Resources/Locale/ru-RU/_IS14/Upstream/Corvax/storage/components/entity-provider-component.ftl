# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

comp-entity-provider-cannot-receive = { CAPITALIZE($refillTarget) } не может быть пополнен!

comp-entity-provider-cannot-transfer = { CAPITALIZE($provider) } не может быть использован для пополнения!

comp-entity-provider-full = { CAPITALIZE(THE($provider)) } уже полон!

comp-entity-provider-no-ejected = Здесь нечего извлекать!

comp-entity-provider-select-entity = Выбрать { $entity }.

comp-entity-provider-eject-all-specified-entities = Извлечь все { $entity }.

comp-entity-provider-select-new-active = Выбрать содержащийся предмет.

comp-entity-provider-no-stored-entities = Пустой.

comp-entity-provider-has-entities = Содержит:

comp-entity-provider-entity-listing = { $amount ->
    [one] [color=yellow]{ $amount }[/color] [color=gray]{ $name }[/color]
    *[other] [color=yellow]{ $amount }[/color] [color=gray]{ $name }[/color]
}
