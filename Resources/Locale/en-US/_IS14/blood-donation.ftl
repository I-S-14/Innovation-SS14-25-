signal-port-name-is14-blood-donation-sender = Blood donation console
signal-port-description-is14-blood-donation-sender = Links to the smart bed the console reads from.
signal-port-name-is14-blood-donation-receiver = Smart bed
signal-port-description-is14-blood-donation-receiver = Reports the patient on the bed to a blood donation console.

is14-blood-donation-console-title = Blood donation console

is14-blood-donation-console-no-donor = Bed empty
is14-blood-donation-console-unlinked = No bed linked.
is14-blood-donation-console-empty = Nobody is on the bed.

# Status pill in the patient bar
is14-blood-donation-console-status-unlinked = NO LINK
is14-blood-donation-console-status-empty = STANDBY
is14-blood-donation-console-status-waiting = NO NEEDLE
is14-blood-donation-console-status-drawing = DRAWING
is14-blood-donation-console-status-stalled = HALTED

# Section headers
is14-blood-donation-console-section-donor = DONOR VITALS
is14-blood-donation-console-section-draw = BLOOD DRAW
is14-blood-donation-console-section-payment = SETTLEMENT

is14-blood-donation-console-blood-level = Blood level
is14-blood-donation-console-pack = Bag fill
is14-blood-donation-console-drawn = Taken this sitting
is14-blood-donation-console-quota = Quota remaining
is14-blood-donation-console-purity = Blood purity
is14-blood-donation-console-payout = Owed

is14-blood-donation-console-purity-clean = fasting
is14-blood-donation-console-purity-tainted = contaminated

is14-blood-donation-console-percent = { $percent }%
is14-blood-donation-console-units = { $volume }u
is14-blood-donation-console-units-of = { $volume }u / { $max }u
is14-blood-donation-console-credits = { $credits } cr.

is14-blood-donation-console-stop = Withdraw needle
is14-blood-donation-console-pay = Dispense cash

is14-blood-donation-console-block-paid = This sitting has already been paid out.
is14-blood-donation-console-block-nothing = No blood has been given yet.
is14-blood-donation-console-block-quota = The donor's shift quota is used up.
is14-blood-donation-console-block-tainted = The station does not buy contaminated blood.
is14-blood-donation-console-no-funds = The department budget is empty.

is14-blood-donation-console-rate = Rate per unit
is14-blood-donation-console-autostop = Withdraw needle automatically
is14-blood-donation-console-autostop-on = The needle comes out on its own at { $percent }% blood.
is14-blood-donation-console-autostop-off = The draw has to be stopped by hand.
is14-blood-donation-console-auto-stopped = The donation console withdraws the needle: safe limit reached.

is14-blood-donation-receipt-content = [head=2]DONOR RECEIPT[/head]
    Donor: { $donor }
    Given: [bold]{ $volume }u[/bold]
    Rate: { $rate } cr. per unit
    Total: [bold]{ $total } cr.[/bold]
    Thank you for donating!
