# ─── Announcements ──────────────────────────────────────────────────────────
is14-gosplan-announcer = Gosplan
is14-gosplan-plan-issued = The station has been issued the Plan for period #{ $period }. Reporting period — { $minutes } min. Departments are funded by their fulfillment percentage. Comrades, to work!
is14-gosplan-report-header = Period #{ $period } results:
is14-gosplan-report-line = { $fund }: { $percent }%, allocated { $payout } cr.
is14-gosplan-report-line-failed = ⤷ failed: { $quotas }
is14-gosplan-report-banner = The Red Banner is awarded to: { $fund }. A bonus pool has been granted to the department.
is14-gosplan-report-sanction = Second consecutive failure: { $fund }. { $amount } cr. confiscated to Gosplan. Expect an inspection.

# ─── Monitor log ────────────────────────────────────────────────────────────
economy-transaction-gosplan-funding = Gosplan allocation — { $quota }
economy-transaction-gosplan-sanction = Gosplan penalty for failing the plan

# ─── Quotas ─────────────────────────────────────────────────────────────────
is14-quota-engineering-power = Uninterrupted power supply
is14-quota-engineering-power-desc = Share of areas under power. Sampled continuously: a blackout costs the fund even once it's fixed.

is14-quota-engineering-atmos = Atmosphere within norms
is14-quota-engineering-atmos-desc = Share of air alarms reading normal. Averaged over the period: a breach costs money for as long as the alarm stays lit.

is14-quota-medical-crew = Preservation of personnel
is14-quota-medical-crew-desc = Share of the crew still alive. Averaged over the period — an hour in a body bag isn't undone by a revival.

is14-quota-security-peace = Public order
is14-quota-security-peace-desc = Share of the period on green alert. Every minute of a raised code costs the department money.

is14-quota-security-fines = Enforcement quota
is14-quota-security-fines-desc = Fines written during the period. Voided ones don't count — Gosplan does not pay for padded paperwork.

is14-quota-research-tech = Technology rollout
is14-quota-research-tech-desc = Technologies researched during the reporting period.

is14-quota-cargo-revenue = Supply revenue target
is14-quota-cargo-revenue-desc = Credits paid into the department fund during the period.

is14-quota-service-revenue = Serving the comrades
is14-quota-service-revenue-desc = Credits paid into the service fund during the period — bar, kitchen, vendors.

# ─── Soc-competition board ──────────────────────────────────────────────────
is14-gosplan-window-title = Soc-competition board
is14-gosplan-tab-plan = Plan
is14-gosplan-tab-history = Periods
is14-gosplan-motto = The five-year plan in four shifts!

# ── Timer header ────────────────────────────────────────────────────────────
is14-gosplan-header-caption = REPORTING PERIOD
is14-gosplan-period = Period #{ $period }
is14-gosplan-timer-caption = until results
is14-gosplan-timer-scoring = scoring the period
is14-gosplan-elapsed = Period { $percent }% gone
is14-gosplan-quota-count = Departments: { $departments }, plan items: { $count }
is14-gosplan-no-plan = No plan issued
is14-gosplan-no-plan-hint = Gosplan has not issued this station any quotas.

# ── Red banner ──────────────────────────────────────────────────────────────
is14-gosplan-banner-title = THE RED BANNER
is14-gosplan-banner-since = Awarded for period #{ $period }
is14-gosplan-banner-bonus = bonus pool +{ $amount } cr.
is14-gosplan-banner-none = The banner is unclaimed
is14-gosplan-banner-none-hint = Overfulfil the plan and it is yours, comrades.

# ── Departments ─────────────────────────────────────────────────────────────
is14-gosplan-departments = DEPARTMENTS
is14-gosplan-department-banner-mark = ★ { $fund }
is14-gosplan-department-banner = holds the banner
is14-gosplan-department-metrics = Plan items: { $count }
is14-gosplan-department-streak = ⚠ Failures in a row: { $streak }
is14-gosplan-department-caption = Mean across all { $count } plan items — this is what you answer for

# ── Plan row ────────────────────────────────────────────────────────────────
is14-gosplan-percent = { $percent }%
is14-gosplan-progress = { $current } of { $target }
is14-gosplan-unit-percent = { $value }%
is14-gosplan-unit-credits = { $value } cr.
is14-gosplan-mark-fail = fail < { $percent }%
is14-gosplan-mark-target = plan { $percent }%
is14-gosplan-mark-cap = cap { $percent }%
is14-gosplan-payout-line = Allocation: { $current } of { $full } cr.
is14-gosplan-status-over = overfulfilled
is14-gosplan-status-ontrack = on track
is14-gosplan-status-failing = failing
is14-gosplan-note-warning = ⚠ Plan failed. Failures in a row: { $streak }. One more means a penalty.
is14-gosplan-note-last = Last period: { $percent }%

# ── Periods ─────────────────────────────────────────────────────────────────
is14-gosplan-history-empty = No reporting periods yet.
is14-gosplan-history-badge = PERIOD #{ $period }
is14-gosplan-history-latest = just scored
is14-gosplan-history-average = Station average fulfillment
is14-gosplan-history-payout = Allocated: { $payout } cr.
is14-gosplan-history-leader = ★ Banner: { $fund }
is14-gosplan-history-failed = Failed: { $funds }
