# ─── PDA program ────────────────────────────────────────────────────────────
is14-fine-program-name = Протокол

# ─── Articles ───────────────────────────────────────────────────────────────
is14-fine-article-disorderly = Мелкое хулиганство
is14-fine-article-dress-code = Нарушение формы одежды
is14-fine-article-idleness = Тунеядство
is14-fine-article-trespassing = Проникновение в служебное помещение
is14-fine-article-insubordination = Неповиновение сотруднику СБ
is14-fine-article-property-damage = Порча казённого имущества
is14-fine-article-contraband = Хранение запрещённых предметов
is14-fine-article-speculation = Спекуляция

# ─── Criminal record history ────────────────────────────────────────────────
is14-fine-history-issued = Выписан штраф: { $article } — { $amount } кр.
is14-fine-history-voided = Штраф отменён: { $article }
is14-fine-history-paid = Штраф оплачен: { $article } — { $amount } кр.
is14-fine-wanted-reason = Неоплаченный штраф: { $article } ({ $amount } кр.)

# ─── Notifications to the offender ──────────────────────────────────────────
is14-fine-notify-issued = Вам выписан штраф: { $article }. К оплате { $amount } кр. Погасите в банкомате.
is14-fine-notify-voided = Штраф отменён: { $article }.

# ─── Officer statuses ───────────────────────────────────────────────────────
is14-fine-status-issued = Штраф выписан: { $name } — { $amount } кр.
is14-fine-status-voided = Штраф отменён.
is14-fine-status-not-found = Штраф не найден или уже закрыт.
is14-fine-status-insufficient = Недостаточно средств на счету.
is14-fine-status-paid = Штраф погашен: { $amount } кр.
is14-fine-status-no-station = Нет связи со станцией.
is14-fine-status-bad-article = Статья не найдена.
is14-fine-status-bad-amount = Некорректная сумма.
is14-fine-status-rejected = Сумма вне допустимых пределов. Максимум: { $max } кр.

# ─── Monitor log ────────────────────────────────────────────────────────────
economy-transaction-fine-paid = Оплата штрафа — { $article }
economy-transaction-fine-collected = Штраф с { $name }

# ─── Cartridge UI ───────────────────────────────────────────────────────────
is14-fine-label-offender = Нарушитель
is14-fine-search-placeholder = Поиск по фамилии...
is14-fine-label-article = Статья
is14-fine-label-ledger = Выписанные штрафы
is14-fine-issue = Выписать
is14-fine-void = Отменить
is14-fine-target = { $name } ({ $job })
is14-fine-target-debt = { $name } ({ $job }) — долг { $debt } кр.
is14-fine-article = { $name } — { $amount } кр.
is14-fine-amount = { $amount } кр.
is14-fine-ledger-empty = Штрафов пока не выписано.
is14-fine-row-details = { $article } · { $officer } · { $status }
is14-fine-state-unpaid = не оплачен
is14-fine-state-paid = оплачен
is14-fine-state-voided = отменён

# ─── ATM ────────────────────────────────────────────────────────────────────
is14-atm-fines-header = Неоплаченные штрафы: { $count } на { $total } кр.
is14-atm-pay-fine = Оплатить { $amount } кр.
