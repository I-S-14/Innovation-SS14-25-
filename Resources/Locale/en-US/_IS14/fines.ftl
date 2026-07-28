# ─── PDA program ────────────────────────────────────────────────────────────
is14-fine-program-name = Protocol

# ─── Articles ───────────────────────────────────────────────────────────────
is14-fine-article-disorderly = Disorderly conduct
is14-fine-article-dress-code = Dress code violation
is14-fine-article-idleness = Idleness
is14-fine-article-trespassing = Trespassing in a restricted area
is14-fine-article-insubordination = Insubordination to a Security officer
is14-fine-article-property-damage = Damage to state property
is14-fine-article-contraband = Possession of prohibited items
is14-fine-article-speculation = Speculation

# ─── Criminal record history ────────────────────────────────────────────────
is14-fine-history-issued = Fine issued: { $article } — { $amount } cr.
is14-fine-history-voided = Fine voided: { $article }
is14-fine-history-paid = Fine paid: { $article } — { $amount } cr.
is14-fine-wanted-reason = Unpaid fine: { $article } ({ $amount } cr.)

# ─── Notifications to the offender ──────────────────────────────────────────
is14-fine-notify-issued = You have been fined: { $article }. { $amount } cr. due. Settle it at an ATM.
is14-fine-notify-voided = Fine voided: { $article }.

# ─── Officer statuses ───────────────────────────────────────────────────────
is14-fine-status-issued = Fine issued: { $name } — { $amount } cr.
is14-fine-status-voided = Fine voided.
is14-fine-status-not-found = Fine not found or already closed.
is14-fine-status-insufficient = Insufficient funds.
is14-fine-status-paid = Fine settled: { $amount } cr.
is14-fine-status-no-station = No connection to the station.
is14-fine-status-bad-article = Article not found.
is14-fine-status-bad-amount = Invalid amount.
is14-fine-status-rejected = Amount out of bounds. Maximum: { $max } cr.

# ─── Monitor log ────────────────────────────────────────────────────────────
economy-transaction-fine-paid = Fine payment — { $article }
economy-transaction-fine-collected = Fine from { $name }

# ─── Cartridge UI ───────────────────────────────────────────────────────────
is14-fine-label-offender = Offender
is14-fine-search-placeholder = Search by name...
is14-fine-label-article = Article
is14-fine-label-ledger = Issued fines
is14-fine-issue = Issue
is14-fine-void = Void
is14-fine-target = { $name } ({ $job })
is14-fine-target-debt = { $name } ({ $job }) — owes { $debt } cr.
is14-fine-article = { $name } — { $amount } cr.
is14-fine-amount = { $amount } cr.
is14-fine-ledger-empty = No fines issued yet.
is14-fine-row-details = { $article } · { $officer } · { $status }
is14-fine-state-unpaid = unpaid
is14-fine-state-paid = paid
is14-fine-state-voided = voided

# ─── ATM ────────────────────────────────────────────────────────────────────
is14-atm-fines-header = Unpaid fines: { $count } totalling { $total } cr.
is14-atm-pay-fine = Pay { $amount } cr.
