# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

nukeops-disk-location-title = Конечное местоположение диска:

nukeops-disk-carried-by = { " " }у [color=White]{ $name }[/color], [color=orange]{ $job }[/color], { $location } { $user ->
    [unknown] { "" }
    *[other] ([color=gray]{ $user }[/color])
}

storage-hierarchy-list = { $items-left ->
    [0] { $existing-text } { $item },
    *[other] { $existing-text } { $item }, в
}
