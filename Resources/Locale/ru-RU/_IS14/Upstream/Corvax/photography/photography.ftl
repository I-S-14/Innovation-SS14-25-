# Translations from space-syndicate/space-station-14, commit 6041191c48f5f75a6284b65bcabd913e186bd442.
# Existing IS14 translations take precedence.

photograph-name-text = Это фотография { PROPER($entity) ->
    *[false] { INDEFINITE($entity) } { $entity }
    [true] { $entity }
}.

photograph-name-text-empty = Это фотография.

photograph-name-text-photograph = Это фотография другой фотографии.
