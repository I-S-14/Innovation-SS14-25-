bank-salary-notification = beeps: "Salary +{ $salary } cr. Balance: { $balance } cr."
bank-salary-delayed = buzzes: "Salary delayed! Department fund is empty. Owed: { $owed } cr. Contact your head of staff."
economy-transaction-salary-fund = Employee salary payout
economy-transaction-vending-revenue = Revenue: { $item } ({ $machine })
economy-transaction-vending-revenue-cash = Cash sale: { $item } ({ $machine })
economy-transaction-vending-tax = Sales tax ({ $machine })
economy-transaction-allocation-out = Allocation: { $target }
economy-transaction-allocation-in = Allocation from the treasury

# Payment terminal
is14-payterm-window-title = Payment terminal
is14-payterm-recipient = Recipient: { $name }
is14-payterm-no-recipient = No recipient bound
is14-payterm-charge-label = New charge
is14-payterm-amount-placeholder = Amount
is14-payterm-desc-placeholder = Note (optional)
is14-payterm-set-charge = Charge
is14-payterm-pending = To pay: { $amount } cr.
is14-payterm-pay = Pay with ID card
is14-payterm-cancel = Cancel
is14-payterm-bind = Bind my card
is14-payterm-status-bound = Card bound.
is14-payterm-status-paid = Paid: { $amount } cr.
is14-payterm-status-denied = Payment declined.
is14-payterm-status-no-card = No ID card with an account found.
is14-payterm-status-no-recipient = No recipient bound.
is14-payterm-status-bad-amount = Invalid amount.
is14-payterm-popup-nothing-to-pay = The terminal has no standing charge.
is14-payterm-tap-hint = Or just tap your card on the terminal.
is14-payterm-receipt-note = Note: { $desc }
is14-payterm-receipt-content = [head=2]RECEIPT[/head]
    Recipient: { $recipient }
    Amount: [bold]{ $amount } cr.[/bold]
    { $note }
    Thank you for your purchase!
economy-transaction-terminal-payment = Terminal payment — { $recipient }
economy-transaction-terminal-payment-desc = Payment: { $desc } — { $recipient }
economy-transaction-terminal-revenue = Terminal revenue
economy-transaction-terminal-tax = Terminal revenue tax

# Economy monitor
economy-monitor-window-title = Economy monitor
economy-monitor-no-records = No transactions yet.
economy-monitor-col-time = Time
economy-monitor-col-account = Account
economy-monitor-col-amount = Amount
economy-monitor-col-description = Description
economy-monitor-col-balance = Balance
economy-monitor-search-account = Account:
economy-monitor-search-placeholder = Account number...
economy-monitor-unknown-vendor = Unknown device
economy-monitor-delete = Delete
economy-monitor-print = Print
economy-monitor-report-name = transaction report
economy-monitor-report-header = [head=2]Transaction report[/head]
economy-monitor-report-line = { $time } | account #{ $account } | { $amount } cr. | { $description } | balance: { $balance }

# Treasury console UI
is14-treasury-window-title = Treasury console
is14-treasury-balance-label = Station treasury:
is14-treasury-funds-label = Department funds
is14-treasury-transfer-label = Allocation from the treasury
is14-treasury-transfer = Transfer
is14-treasury-amount-placeholder = Amount
is14-treasury-credits = { $amount } cr.
is14-treasury-status-bad-amount = Invalid amount.
is14-treasury-status-bad-target = Target fund not found.
is14-treasury-status-insufficient = Insufficient treasury funds.
is14-treasury-status-done = Transferred { $amount } cr. — { $target }.

# Payroll console UI
is14-payroll-window-title = Payroll console
is14-payroll-tab-payroll = Payroll
is14-payroll-tab-log = Log
is14-payroll-col-name = Employee
is14-payroll-col-job = Position
is14-payroll-col-salary = Salary
is14-payroll-salary-label = Shift salary
is14-payroll-salary-placeholder = Salary
is14-payroll-salary-apply = Apply
is14-payroll-oneoff-label = One-off bonus or fine
is14-payroll-amount-placeholder = Amount
is14-payroll-limits = Bonus pool { $pool } cr. · payable now { $bonus } cr. · fine up to { $fine } cr.
is14-payroll-bonus = Bonus
is14-payroll-fine = Fine
is14-payroll-credits = { $amount } cr.
is14-payroll-owed = owed { $amount }
is14-payroll-no-selection = No employee selected
is14-payroll-selection = { $name } — { $job }
is14-payroll-selection-hint = Base salary { $base } cr. · allowed { $min }–{ $max } cr.
is14-payroll-row-tooltip = Base salary: { $base } cr. Allowed range: { $min }–{ $max } cr.
is14-payroll-unknown-job = Unknown position
is14-payroll-status-bad-salary = Salary must be within { $min }–{ $max } cr.
is14-payroll-status-bad-bonus = The bonus pool only holds { $max } cr. It grows with the department's income.
is14-payroll-status-bad-fine = A fine can't exceed { $max } cr.
is14-payroll-status-not-subordinate = This employee is not on your department's payroll.
is14-payroll-status-no-self = You can't run payroll operations on yourself.
is14-payroll-status-no-fund = Department fund unavailable.
is14-payroll-status-fund-insufficient = Insufficient department funds.
is14-payroll-status-no-account = Employee account not found.
is14-payroll-status-employee-broke = The employee's account is empty — nothing to collect.
is14-payroll-status-salary-set = { $name }'s salary set to { $salary } cr.
is14-payroll-status-bonus-paid = { $name } awarded a { $amount } cr. bonus.
is14-payroll-status-fine-collected = Collected { $amount } cr. from { $name }.

is14-payroll-log-empty = No actions yet.
is14-payroll-log-raise = Salary raised: { $name } — { $old } → { $new } cr.
is14-payroll-log-cut = Salary cut: { $name } — { $old } → { $new } cr.
is14-payroll-log-bonus = Bonus: { $name } — { $amount } cr.
is14-payroll-log-fine = Fine: { $name } — { $amount } cr.

is14-payroll-notify-raise = Your salary has been raised to { $salary } cr.
is14-payroll-notify-cut = Your salary has been cut to { $salary } cr.
is14-payroll-notify-bonus = Bonus paid: { $amount } cr.
is14-payroll-notify-fine = Fine deducted: { $amount } cr.

economy-transaction-bonus = Bonus
economy-transaction-bonus-fund = Employee bonus — { $name }
economy-transaction-fine = Fine
economy-transaction-fine-fund = Employee fine — { $name }
economy-transaction-salary-changed = Salary changed: { $name } — { $old } → { $new } cr.

is14-stack-credit = credits
bank-holder-examine-balance = Account balance: [color=green]{ $balance } cr.[/color]

is14-vending-balance-label = Balance:
is14-vending-cash-label = Cash:
is14-vending-eject-cash = Take cash back
is14-vending-eject-cash-tooltip = Returns the cash left in the machine.
is14-vending-cash-inserted = Inserted. In the machine: { $amount } cr.
is14-vending-balance-value = { $balance } cr.
is14-vending-price = { $price } cr.
is14-vending-stock = x{ $stock }
is14-vending-out-of-stock = Out
is14-vending-tab-contraband = ☠ Contraband
is14-vending-search-placeholder = Search...
is14-vending-unknown = Unknown

is14-vending-ad-engine-1 = Precision tools. Unreliable humans. Not our problem.
is14-vending-ad-engine-2 = A good wrench has never betrayed anyone.

is14-vending-ad-medical-1 = First aid is better than second aid.
is14-vending-ad-medical-2 = Your health is our business. Literally.
