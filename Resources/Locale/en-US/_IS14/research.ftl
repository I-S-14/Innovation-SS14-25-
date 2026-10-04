# ─── Research data types ────────────────────────────────────────────────────
is14-research-point-science = scientific data
is14-research-point-science-short = SCI
is14-research-point-science-desc = Fundamental phenomena: artifacts, anomalies, unknown substances. The only currency the NIC earns on its own.
is14-research-donor-science = the NIC itself

is14-research-point-industrial = industrial data
is14-research-point-industrial-short = IND
is14-research-point-industrial-desc = Machinery pushed past its limits, new materials, field-testing prototypes.
is14-research-donor-industrial = engineering and cargo

is14-research-point-military = military data
is14-research-point-military-short = MIL
is14-research-point-military-desc = Measured violence: blast profiles, ballistics, dismantled captured hardware.
is14-research-donor-military = security

is14-research-point-biological = biological data
is14-research-point-biological-short = BIO
is14-research-point-biological-desc = Living and dead material: causes of death, strains, mutations.
is14-research-donor-biological = medical

is14-research-point-social = sociological data
is14-research-point-social-short = SOC
is14-research-point-social-desc = What the living crew answers and does. Cannot be scripted — only talked out of people.
is14-research-donor-social = service

# ─── Novelty of a result ────────────────────────────────────────────────────
is14-research-novelty-new = new data
is14-research-novelty-refined = refinement
is14-research-novelty-repeat = repeat
is14-research-novelty-confirmed = result confirmed

# ─── Measurements ───────────────────────────────────────────────────────────
is14-research-measured = Recorded: { $amount } × { $type } ({ $novelty })
is14-research-measured-failed = The experiment failed. Negative result: { $amount } × { $type }. The repeat will be safer.
is14-research-bench-busy = The instrument is still working on the last sample.
is14-research-bench-verb = Analyse
is14-research-sample-useless = There is nothing to measure on this sample.
is14-research-no-station = The instrument is not tied to any station.
is14-research-scanner-needs-dead = This instrument only works on a dead body.
is14-research-scanner-needs-alive = This instrument only reads a living organism.
is14-research-scanner-start = You start taking readings...
is14-research-doppler-too-close = A blast of intensity { $intensity } was recorded inside the station. Not counted — tests are conducted at a distance.

# ─── Surveys ────────────────────────────────────────────────────────────────
is14-research-printer-busy = The terminal is still printing the last form.
is14-research-printer-printed = A survey form is printed.
is14-survey-filling = You fill in the survey...
is14-survey-filled = The survey is filled in. Hand it to the NIC.
is14-survey-already-filled = This survey is already filled in.
is14-survey-not-filled = The form is blank — somebody has to answer it first.
is14-survey-examine-blank = The form is blank.
is14-survey-examine-filled = Filled in by { $name } ({ $title }).

# ─── NIC console ────────────────────────────────────────────────────────────
is14-research-console-title = NIC console
is14-research-console-server = Server: { $name }
is14-research-console-no-server = No link to a research server
is14-research-console-income-hint = earned in the last { $minutes } min
is14-research-console-prereq = The preceding topics have to be researched first.
is14-research-console-cannot-afford = Not enough data for this topic.
is14-research-console-unlocked-broadcast = Technology researched: { $technology }. Spent { $cost }. Approved by: { $approver }
is14-research-counter-tooltip = Earned in the last { $minutes } min: { $income }
is14-research-counter-donor = Donor: { $donor }

is14-research-card-button = Research
is14-research-card-tier = tier { $tier }
is14-research-card-researched = Researched
is14-research-card-ready = Enough data
is14-research-card-needs-data = Not enough data — see the red figures above
is14-research-card-needs-prereq = Requires: { $list }
is14-research-card-needs-prereq-short = Preceding topics required
is14-research-card-no-server = No server link

# ─── Station modifiers ──────────────────────────────────────────────────────
is14-modifier-research-payout = Research yield
is14-modifier-research-payout-science = Scientific data yield
is14-modifier-research-payout-industrial = Industrial data yield
is14-modifier-research-payout-military = Military data yield
is14-modifier-research-payout-biological = Biological data yield
is14-modifier-research-payout-social = Sociological data yield
is14-modifier-novelty-retention = Value of repeated measurements
is14-modifier-lathe-speed = Lathe print time
is14-modifier-lathe-materials = Lathe material use

# ─── Technology effects ─────────────────────────────────────────────────────
is14-effect-payout-all = All measurements yield 10% more data
is14-effect-payout-science = Scientific measurements yield 20% more
is14-effect-payout-industrial = Industrial measurements yield 20-25% more
is14-effect-payout-military = Military measurements yield 20% more
is14-effect-payout-biological = Biological measurements yield 25% more
is14-effect-payout-social = Surveys yield 25% more
is14-effect-novelty = Repeated measurements pay noticeably more
is14-effect-lathe-materials = Lathes use 10% less material
is14-effect-lathe-speed = Lathes print 15-20% faster
is14-effect-grant-scanner = Hands out a prototype pathology scanner
is14-effect-grant-doppler = Hands out a doppler array
is14-effect-grant-surveys = Hands out three survey forms
is14-effect-grant-science = Grants 40 scientific data once

# ─── Research map ───────────────────────────────────────────────────────────
is14-research-map-select-hint = Pick a topic on the map
is14-research-map-effects = Effects:
is14-research-map-unlocks = Unlocks production:
is14-research-map-prereqs = Requires:
is14-research-map-cost = Cost:
is14-research-map-legend-researched = researched
is14-research-map-legend-available = available
is14-research-map-legend-locked = locked

# ─── Destructive analyzer ───────────────────────────────────────────────────
is14-analyzer-started = The analyzer takes the sample in. It is not coming back out.
is14-analyzer-nothing-to-take-apart = Nothing to take apart — the sample has no known composition.
is14-analyzer-reverse-engineered = Reverse engineering: "{ $technology }" is { $percent }% cheaper.
is14-research-map-discount = Reverse engineering: -{ $percent }%
is14-research-map-no-prereqs = No preceding topics required.
is14-research-map-prereq-done = Already researched.
is14-research-map-prereq-missing = Not researched yet.
is14-research-map-open-hint = Click to open this topic on the map.

# ─── Randomised upgrade effects ─────────────────────────────────────────────
is14-effect-grant-calibration = Grants 10 scientific data once
is14-effect-payout-all-30 = All measurements yield 30% more data
is14-effect-payout-all-15 = All measurements yield 15% more data
is14-effect-payout-all-worse-10 = All measurements yield 10% less data
is14-effect-novelty-15 = Repeated measurements pay noticeably more
is14-effect-novelty-30 = Repeated measurements barely lose value
is14-effect-lathe-speed-10 = Lathes print 10% faster
is14-effect-lathe-speed-20 = Lathes print 20% faster
is14-effect-lathe-speed-25 = Lathes print 25% faster
is14-effect-lathe-speed-worse-5 = Lathes print 5% slower
is14-effect-lathe-materials-10 = Lathes use 10% less material
is14-effect-lathe-materials-20 = Lathes use 20% less material
is14-effect-lathe-materials-worse-10 = Lathes use 10% more material
is14-effect-lathe-materials-worse-15 = Lathes use 15% more material
is14-effect-payout-science-40 = Scientific measurements yield 40% more
is14-effect-payout-science-worse-15 = Scientific measurements yield 15% less
is14-effect-payout-biological-40 = Biological measurements yield 40% more
is14-effect-payout-biological-worse-15 = Biological measurements yield 15% less
is14-effect-payout-military-40 = Military measurements yield 40% more
is14-effect-payout-military-worse-15 = Military measurements yield 15% less
is14-effect-payout-social-40 = Surveys yield 40% more
is14-effect-payout-social-worse-15 = Surveys yield 15% less
is14-research-map-drag-hint = Drag the map with the mouse

# ─── Tachyon-doppler array ──────────────────────────────────────────────────
is14-doppler-readout-header = Disturbance recorded near { $location }.
is14-doppler-readout-radii = Epicentre radius { $epicenter }, outer { $outer }, shockwave { $shockwave }. Total intensity { $intensity }.
is14-doppler-readout-too-close = Blast inside the station. Not counted — tests are conducted at a distance.

is14-doppler-facing-south = south
is14-doppler-facing-north = north
is14-doppler-facing-east = east
is14-doppler-facing-west = west
is14-doppler-facing-southeast = south-east
is14-doppler-facing-southwest = south-west
is14-doppler-facing-northeast = north-east
is14-doppler-facing-northwest = north-west
is14-doppler-facing-invalid = an unknown direction

is14-doppler-window-title = Tachyon-doppler array
is14-doppler-window-facing = Sensor faces { $facing }
is14-doppler-window-best = Station record: intensity { $intensity }
is14-doppler-window-no-records = No record yet: the first reading will set it.
is14-doppler-window-hint = The array only sees what is inside the cone in front of it, between 8 and 120 m away. Turn it with a wrench or from the menu.
is14-doppler-window-log = Reading log
is14-doppler-window-empty = The log is empty.
is14-doppler-window-print = Protocol
is14-doppler-window-record-title = Reading #{ $number } · { $timestamp }
is14-doppler-window-record-body = { $location } (grid { $coordinates }), distance { $distance } m.[color=#9a9a9a] Intensity { $intensity }; radii: epicentre { $epicenter }, outer { $outer }, shockwave { $shockwave }.[/color]
is14-doppler-window-verdict-record = record, +{ $amount }
is14-doppler-window-verdict-repeat = repeat, +{ $amount }
is14-doppler-window-verdict-too-close = not counted
is14-doppler-protocol-printed = The reading protocol is printed.
is14-doppler-protocol-body =
    ORDNANCE READING PROTOCOL #{ $number }
    Time: { $timestamp }
    Location: { $location }
    Epicentre: grid ({ $coordinates }), { $distance } m from the array

    Total intensity: { $intensity }
    Falloff per tile: { $slope }
    Per-tile cap: { $peak }

    Epicentre radius: { $epicenter }
    Outer radius: { $outer }
    Shockwave radius: { $shockwave }

    Military data credited: { $payout }

    Signed off by: ____________________
is14-doppler-location-unknown = an unidentified sector

# ─── Ordnance launch chamber ────────────────────────────────────────────────
is14-transfer-valve-slot-a = Receiving tank
is14-transfer-valve-slot-b = Donor tank
is14-transfer-valve-slot-trigger = Trigger
is14-transfer-valve-wrong-tank = Only an oxygen tank and a plasma tank fit in here.

is14-launch-console-title = Launch control
is14-launch-console-ready = Ready to launch
is14-launch-console-counting = Launch in { $seconds } s
is14-launch-console-countdown = Delay, s
is14-launch-console-launch = LAUNCH
is14-launch-console-abort = Abort
is14-launch-console-links = Linked devices — before: { $pre }, on launch: { $launch }, after: { $post }. Lead time { $lead } s.
is14-launch-console-not-linked = Nothing is wired to the launch output. Link the console with a network configurator.

# ─── Launch console ports ───────────────────────────────────────────────────
is14-signal-port-name-launch-pre = Before launch
is14-signal-port-description-launch-pre = Fires one lead time before the shot. Wire shutters here to open them.
is14-signal-port-name-launch = Launch
is14-signal-port-description-launch = Fires exactly when the countdown runs out. Wire the mass driver here.
is14-signal-port-name-launch-post = After launch
is14-signal-port-description-launch-post = Fires one lead time after the shot. Wire shutters here to close them.

# ─── Breakthroughs, forks and priorities ───────────────────────────────────
is14-research-console-needs-sample = Data will not buy this one — it needs a sample in the destructive analyzer.
is14-research-console-excluded = Another road was taken: the alternative is already researched.
is14-research-card-needs-sample = Needs a breakthrough sample
is14-research-card-excluded = Closed off: the alternative is researched
is14-research-map-breakthrough = Breakthrough — sample needed:
is14-research-map-breakthrough-done = Sample taken apart, the topic is open.
is14-research-map-breakthrough-missing = The sample has not been taken apart yet.
is14-research-map-exclusive = Rules out:
is14-research-map-exclusive-taken = That alternative is already researched — this road is closed.
is14-research-map-exclusive-open = Researching this one closes the alternative.
is14-research-map-priority = Gosplan priority: −{ $percent }%
is14-analyzer-breakthrough-broadcast = Breakthrough: the sample is gone and "{ $technology }" is open.
is14-modifier-lathe-speed-science = Science lathe print time
is14-modifier-lathe-materials-science = Science lathe material use
is14-modifier-lathe-speed-medical = Medical lathe print time
is14-modifier-lathe-materials-medical = Medical lathe material use
is14-modifier-lathe-speed-arms = Armoury lathe print time
is14-modifier-lathe-materials-arms = Armoury lathe material use
is14-modifier-lathe-speed-industrial = Industrial lathe print time
is14-modifier-lathe-materials-industrial = Industrial lathe material use
is14-modifier-lathe-speed-service = Service lathe print time
is14-modifier-lathe-materials-service = Service lathe material use

# ─── Telemetry and gas samples ─────────────────────────────────────────────
is14-gas-sample-empty = Nothing to measure — the container is all but empty.

# ─── Volunteers and consent ────────────────────────────────────────────────
is14-consent-signing = You sign the consent form...
is14-consent-signed = Consent signed.
is14-consent-already-signed = The form is already signed.
is14-consent-examine-blank = The form is unsigned.
is14-consent-examine-signed = Signed by: { $name } ({ $title }).
is14-volunteer-scan-start = You take the readings...
is14-volunteer-no-consent = You are being examined without your consent!
is14-volunteer-paid = You have been paid { $amount } cr for taking part in the research.
is14-volunteer-no-funds = Science has no funds to pay the volunteer.
is14-volunteer-payment-description = Research participation payment

# ─── Department work orders ────────────────────────────────────────────────
is14-workorder-window-title = Order terminal
is14-workorder-current = Current order:
is14-workorder-none = no order
is14-workorder-available = Available to commission:
is14-workorder-order = Commission
is14-workorder-cancel = Withdraw order
is14-workorder-terms = On delivery: { $bonus } × { $type } and { $payment } cr from the department budget
is14-workorder-budget = Department budget: { $balance } cr
is14-workorder-no-access = Only the head of the department may sign an order.
is14-workorder-placed-broadcast = Order from { $department }: "{ $technology }" requested. Signed: { $signer }
is14-workorder-delivered-broadcast = Order from { $department } delivered: "{ $technology }". The NIC receives { $bonus } data and { $payment } cr.
is14-workorder-department-engineering = Engineering
is14-workorder-department-medical = Medical
is14-workorder-department-security = Security
is14-workorder-department-cargo = Logistics
is14-workorder-department-service = Service

# ─── Publications ──────────────────────────────────────────────────────────
is14-publication-printed = The paper is printed. It needs the research director's and the captain's stamps.
is14-publication-nothing-to-report = Nothing to write about yet — the station has researched nothing.
is14-publication-already-filed = That result has already been published.
is14-publication-not-a-paper = This is not a scientific paper.
is14-publication-needs-stamp = Missing stamp: { $stamp }.
is14-publication-body = NIC scientific paper. Topic: { $technology } (tier { $tier }). Result obtained, method attached, signatures below.
is14-publication-accepted-broadcast = The Academy of Sciences accepted the paper on "{ $technology }": { $amount } scientific data and { $payment } cr to the science account.

# ─── Reverse engineering ───────────────────────────────────────────────────
is14-reverse-started = The bench takes the sample in. It will not come back out.
is14-reverse-nothing-to-learn = There is nothing to learn from this — it is not enemy hardware.
is14-reverse-revealed = Reverse engineering complete: the classified topic "{ $technology }" is open.
is14-research-map-zoom = Zoom
is14-research-map-center = Recentre
