# SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
# SPDX-FileCopyrightText: 2023 metalgearsloth <metalgearsloth@gmail.com>
# SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 IProduceWidgets <107586145+IProduceWidgets@users.noreply.github.com>
# SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
# SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
# SPDX-FileCopyrightText: 2025 Aiden <aiden@djkraz.com>
#
# SPDX-License-Identifier: AGPL-3.0-or-later

cmd-weatherremove-desc = Remove specific weather from map.
cmd-weatherset-desc = Removes all weather except the specified one. If the specified weather does not exist on the map, it adds it.
cmd-weatheradd-desc = Add specific weather to map.

cmd-weatherremove-help = weatherremove <mapId> <prototype>
cmd-weatherset-help = weatherset <mapId> <prototype / null>
cmd-weatheradd-help = weatheradd <mapId> <prototype / null>

cmd-weather-error-no-arguments = Not enough arguments!
cmd-weather-error-unknown-proto = Unknown Weather prototype!
cmd-weather-error-wrong-time = Time is in the wrong format!
cmd-weather-error-wrong-map = Map with MapId {$id} doesn't exist!
cmd-weather-error-no-weather = This weather does not exist on the selected map!

cmd-weather-hint-map-id = Map Id
cmd-weather-hint-prototype = Weather entity prototype
cmd-weather-hint-time = Duration in seconds (leave empty for infinite duration)
