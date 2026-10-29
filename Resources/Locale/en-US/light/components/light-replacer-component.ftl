# SPDX-FileCopyrightText: 2021 Alex Evgrashin <aevgrashin@yandex.ru>
# SPDX-FileCopyrightText: 2021 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
# SPDX-FileCopyrightText: 2021 Galactic Chimp <63882831+GalacticChimp@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 Nemanja <98561806+EmoGarbage404@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later


### Interaction Messages

# Shown when player tries to replace light, but there are no lights left
comp-light-replacer-missing-light = No {MAKEPLURAL($light-name)} left in {THE($light-replacer)}.

# Shown when player tries to insert a broken light bulb into the light replacer.
comp-light-replacer-insert-broken-light = You can't insert broken lights!

# Shown when a player attempts to replace a light with the same color & type as the active light.
comp-light-replacer-same-light = This fixture already holds {INDEFINITE($light)} {$light}!

# Radial Menu messages
comp-light-replacer-open-empty = {CAPITALIZE(THE($light-replacer))} is completely empty!

# Label
comp-light-replacer-label = Tube: {$tube}
                            Bulb: {$bulb}

### Status Control

# Bulbs
comp-light-bulb-incandescent = incandescent
comp-light-bulb-dim = dim
comp-light-bulb-warm = warm
comp-light-bulb-service = service

# Tubes
comp-light-bulb-fluorescent = fluorescent
comp-light-bulb-exterior = exterior
comp-light-bulb-sodium = sodium

# Both
comp-light-bulb-old = old
comp-light-bulb-led = led
comp-light-bulb-cyan = cyan
comp-light-bulb-blue = blue
comp-light-bulb-yellow = yellow
comp-light-bulb-pink = pink
comp-light-bulb-orange = orange
comp-light-bulb-black = black
comp-light-bulb-red = red
comp-light-bulb-green = green
