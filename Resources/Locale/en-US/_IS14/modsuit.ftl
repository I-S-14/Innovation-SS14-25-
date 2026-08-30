# Modular chassis - shared layer, used by MOD suits and later by mechs

chassis-panel-closed = The panel is closed.
chassis-complexity-exceeded = Not enough room for modules.
chassis-module-conflict = Incompatible with { $module }.
chassis-module-installed = { $module } installed.
chassis-module-removed = { $module } removed.
chassis-module-not-removable = This module is built in.
chassis-module-slots-in-use = Take it off first — it is only being carried there because of this module.

chassis-module-no-power = Not enough charge.
chassis-module-cooldown = Module is recharging.
chassis-module-missing-parts = Required parts are not sealed.
chassis-module-chassis-inactive = The suit is not active.
chassis-module-not-worn = The suit is not worn.
chassis-module-incapacitated = You cannot do that right now.
chassis-module-malfunctioning = The module is malfunctioning.
chassis-module-unavailable = Module unavailable.

# Quick module ring

chassis-radial-active = { $module } — on
chassis-radial-blocked = { $module } — { $reason }

# MOD suit

modsuit-parts-still-deployed = Retract the suit parts first.
modsuit-power-depleted = Charge depleted, the suit unseals!
modsuit-busy-sealing = The suit is busy sealing.
modsuit-slot-occupied = That body part is already covered.
modsuit-not-worn = The suit is not worn.
modsuit-no-core = No core installed.
modsuit-nothing-to-seal = Nothing to seal.
modsuit-nothing-to-unseal = Nothing to unseal.
modsuit-sealed-cannot-retract = Break the seal first.
modsuit-seal-confirm = Press again to seal up.
modsuit-unseal-confirm = Press again to break the seal.
modsuit-no-switchable-modules = Nothing installed that can be switched.

# Part popups

modsuit-seal-helmet = The helmet seals shut.
modsuit-unseal-helmet = The helmet unseals.
modsuit-seal-chestplate = The chestplate pressurizes.
modsuit-unseal-chestplate = The chestplate depressurizes.
modsuit-seal-gauntlets = The gauntlets click shut.
modsuit-unseal-gauntlets = The gauntlets unclasp.
modsuit-seal-boots = The boots click shut.
modsuit-unseal-boots = The boots unclasp.

# Chassis UI

chassis-ui-title = MODsuit
chassis-ui-charge = Charge
chassis-ui-charge-value = { $current } / { $max } J · { $draw } W
chassis-ui-no-core = No core
chassis-ui-complexity = Complexity
chassis-ui-complexity-value = { $used } / { $max }
chassis-ui-draw = Drawing { $draw } W
chassis-ui-panel-open = Panel open
chassis-ui-malfunctioning = Malfunctioning
chassis-ui-activate = Seal
chassis-ui-deactivate = Unseal
chassis-ui-parts = Parts
chassis-ui-modules = Modules
chassis-ui-no-modules = No modules installed.
chassis-ui-deploy = Deploy
chassis-ui-retract = Retract
chassis-ui-seal = Seal
chassis-ui-unseal = Unseal
chassis-ui-part-stowed = stowed
chassis-ui-part-deployed = deployed
chassis-ui-part-sealed = sealed
chassis-ui-part-broken = beaten in
chassis-ui-part-ruptured = split open
chassis-ui-kind-passive = passive
chassis-ui-kind-toggleable = toggleable
chassis-ui-kind-usable = usable
chassis-ui-kind-active = active
chassis-ui-module-idle = { $value } W
chassis-ui-module-active = { $value } W
chassis-ui-module-use = { $value } J
chassis-ui-module-idle-tooltip = Drawn while installed
chassis-ui-module-active-tooltip = Drawn while switched on
chassis-ui-module-use-tooltip = Spent per use
chassis-ui-module-ready = Working
chassis-ui-module-on = Switch on
chassis-ui-module-off = Switch off
chassis-ui-module-use-button = Use
chassis-ui-module-select = Select
chassis-ui-module-deselect = Stow

# Interaction

chassis-panel-opened = The panel swings open.
chassis-panel-closed-now = The panel closes.
chassis-active-cannot-open = The suit is active.
chassis-nothing-to-pry = There is nothing in the cradle to lever out.

mod-core-slot = MOD core

# UI: new strings

chassis-ui-hardware = Hardware
chassis-ui-state-stowed = stowed
chassis-ui-state-deployed = deployed
chassis-ui-state-sealed = sealed
chassis-ui-unworn = Not worn
chassis-ui-deploy-all = Deploy all
chassis-ui-retract-all = Retract all
chassis-ui-no-parts = No parts.
chassis-ui-electrified = Electrified
chassis-ui-interface-broken = Interface damaged
chassis-ui-panel-shut = closed
chassis-ui-hw-core = Core
chassis-ui-hw-cell = Cell
chassis-ui-hw-panel = Panel
chassis-ui-hw-lock = ID lock
chassis-ui-hw-draw = Draw
chassis-ui-faults = Faults
chassis-ui-faults-none = None detected.
chassis-ui-fault-power-cut = Power severed: core out of circuit.
chassis-ui-fault-overloaded = Power circuit overloaded.
chassis-ui-fault-malfunctioning = Controller malfunction.
chassis-ui-fault-link-deploy = Actuator link severed.
chassis-ui-fault-link-seal = Pressure link severed.
chassis-ui-fault-interface = Interface damaged.
chassis-ui-fault-electrified = Shell is live.
chassis-ui-overloaded = Overloaded
chassis-ui-power-cut = No power
chassis-ui-lock-engaged = engaged
chassis-ui-lock-open = open
chassis-ui-lock-wiped = access wiped

# ID lock and sabotage

modsuit-breach-fold-instead = Fold it away from the suit panel.
modsuit-breach-not-subdued = They are still on their feet — restrain them first.
modsuit-breach-released = The suit unseals and folds away.
modsuit-breach-cutting = You cut into the { $part }.
modsuit-breach-already-cut = The { $part } is already cut through.
modsuit-breach-nothing-there = Nothing covers what you are aiming at.
modsuit-breach-struggling = They are squirming — this will take far longer.
modsuit-lock-override = The suit acknowledges the card and lets go.
modsuit-lock-denied = Access denied.
modsuit-lock-engaged = The lock engages.
modsuit-lock-released = The lock releases.

# Wires

wire-name-mod-power = PWR
wire-name-mod-deploy = DPL
wire-name-mod-seal = SEAL
wire-name-mod-shock = SHK
wire-name-mod-interface = IFC
modsuit-wires-board = MOD control unit

# Dashboard

chassis-ui-shell = Shell
chassis-ui-power = Power plant
chassis-ui-slot-head = Helmet
chassis-ui-slot-chest = Torso
chassis-ui-slot-hands = Gauntlets
chassis-ui-slot-feet = Boots
chassis-ui-slot-other = Other
chassis-ui-module-count = { $count } installed
chassis-device-reeled-in = The suit reels { $device } back in.

# Module controls

chassis-ui-eject = Eject
chassis-ui-eject-tooltip = Pull this module out of the chassis.
chassis-ui-eject-needs-panel = The maintenance panel has to be open first.
chassis-ui-module-open = Open

# Plating condition

chassis-ui-integrity = Integrity
chassis-ui-integrity-value = { $current } / { $max }
chassis-ui-status-seal = Pressure seal
chassis-ui-status-modules = Hardpoints
chassis-ui-fault-structural = Bent plating — weld it.
chassis-ui-fault-electrical = Burnt wiring — run new cable.
modsuit-part-broken = The { $part } buckles — its hardpoints go dead.
modsuit-part-ruptured = The { $part } splits open and blows its seal.
modsuit-part-cannot-seal = The { $part } is too far gone to close.
modsuit-no-storage = No compartments on this suit.

# Servicing

modsuit-repair-done = You work the { $part } back into shape.
modsuit-repair-needs-plasteel = This piece has bent plating. It wants plasteel.
modsuit-repair-needs-cable = This piece has burnt wiring. It wants cable.
modsuit-repair-no-cable = Not enough cable left.
modsuit-repair-no-plasteel = Not enough plasteel left.

chassis-device-no-power = The suit has nothing left to run that on.
chassis-device-no-hands = You need both hands free for the { $device }.
chassis-ui-module-details = Show what this does.

modsuit-core-full = The core is already full.
modsuit-core-refuelled = You feed { $count } of the { $fuel } into the core.

# Tank

chassis-ui-tank = Bottle
chassis-ui-tank-value = { $kpa } kPa
chassis-ui-tank-idle = compressor idle
chassis-ui-tank-seal = Airtight seal
chassis-ui-tank-internals = Breathe from bottle
chassis-ui-tank-internals-off = Stop breathing from bottle
chassis-ui-tank-internals-tooltip = Feeds the wearer from the suit's own bottle instead of the air around them.
chassis-ui-tank-internals-needs-seal = Every piece has to be sealed and the valve shut.
chassis-ui-tank-unsealed = The compressor needs a sealed chestplate.
chassis-ui-tank-temperature = { $celsius } °C
chassis-ui-tank-empty = empty
chassis-ui-tank-share = { $percent }%
chassis-ui-tank-rest = trace { $percent }%
chassis-ui-tank-valve = Open valve
chassis-ui-tank-valve-close = Close valve
chassis-ui-tank-valve-tooltip = Vents the bottle into the room. Internals cannot run through an open valve.
chassis-ui-tank-pump-tooltip = Runs the compressor while the suit is closed up.
chassis-ui-no-tank = no tank

chassis-jetpack-no-gas = The thrusters have nothing to push against.
chassis-ui-eject-cell = Eject cell
chassis-ui-eject-cell-tooltip = Puts the core's power cell into your hand.
chassis-ui-insert-cell = Insert cell
chassis-ui-insert-cell-tooltip = Puts the cell you are holding into the core.
chassis-ui-no-cell = no cell
chassis-ui-hopper = Fuel hopper
chassis-ui-hopper-tooltip = Load the core with fuel. It burns what it needs as it needs it.
chassis-config-open-storage = Open
chassis-config-pump = Compressor
chassis-config-light-radius = Beam radius
chassis-config-regulator-temperature = Body temperature, °C
chassis-config-gas-oxygen = Oxygen
chassis-config-gas-nitrogen = Nitrogen
chassis-config-gas-carbondioxide = CO2
chassis-config-gas-plasma = Plasma

alerts-is14-modsuit-charge-name = MOD charge
alerts-is14-modsuit-charge-desc = Core charge of the suit you are wearing. When it runs out the suit unseals itself.
alerts-is14-modsuit-nocharge-name = MOD without power
alerts-is14-modsuit-nocharge-desc = The suit has no core, or the core has nothing left in it. Until that is fixed it is armour and nothing more.

research-technology-is14-modsuit = MOD Technology
research-technology-is14-modsuit-specialization = MOD Specialization
research-technology-is14-modsuit-advanced = Advanced MOD Systems

chassis-ui-module-dna-lock = Lock
modsuit-dna-lock-set = The suit has memorised you.
modsuit-dna-lock-cleared = The suit has forgotten you.
modsuit-dna-lock-denied = The suit does not recognise you.
modsuit-dna-lock-broken = The lock is burned out.
modsuit-dna-lock-no-dna = The suit has nothing to memorise.
modsuit-shock-voice-armed = Controller defence armed.
modsuit-shock-voice-nopower = Defence failed: insufficient charge.
modsuit-shock-voice-discharge = Unauthorised access. Discharging.

# Panel armour

is14-modsuit-panel-rods = Steel rods are wedged across the wiring. Use a [color=cyan]Crowbar[/color] to lever them out.
is14-modsuit-panel-rods-welded = Steel rods have been welded across the wiring. Use a [color=cyan]Welder[/color] to free them.
is14-modsuit-panel-steel = A steel plate covers the wiring. Use a [color=cyan]Crowbar[/color] to remove it.
is14-modsuit-panel-steel-welded = A steel plate has been welded over the wiring. Use a [color=cyan]Welder[/color] to free it.
is14-modsuit-panel-plasteel = A plasteel plate covers the wiring. Use a [color=cyan]Crowbar[/color] to remove it.
is14-modsuit-panel-plasteel-welded = A plasteel plate has been welded over the wiring. Use a [color=cyan]Welder[/color] to free it.
modsuit-link-deploy-cut = The command never reaches the plating — the actuator line is cut.
modsuit-link-seal-cut = The command never reaches the plating — the pressure line is cut.
wire-status-mod-overload = OVLD
modsuit-interface-broken = The suit's interface does not answer.
modsuit-holster-slot = Holster
chassis-ui-hw-dna = DNA lock
chassis-ui-dna-imprinted = imprinted
chassis-ui-dna-blank = blank
chassis-ui-dna-broken = burnt out
chassis-ui-fault-dna-broken = DNA lock burnt out — the module needs replacing.
modsuit-hat-slot = Headwear
