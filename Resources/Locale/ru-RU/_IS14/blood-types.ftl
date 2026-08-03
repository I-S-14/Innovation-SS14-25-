# ─── Антигены ───────────────────────────────────────────────────────────────
is14-blood-antigen-a = антиген A
is14-blood-antigen-a-short = A
is14-blood-antigen-b = антиген B
is14-blood-antigen-b-short = B
is14-blood-antigen-rhd = резус-фактор D
is14-blood-antigen-rhd-short = D

# ─── Группы крови ───────────────────────────────────────────────────────────
is14-blood-type-o-pos = первая положительная, O(I) Rh+
is14-blood-type-o-pos-short = O+
is14-blood-type-a-pos = вторая положительная, A(II) Rh+
is14-blood-type-a-pos-short = A+
is14-blood-type-b-pos = третья положительная, B(III) Rh+
is14-blood-type-b-pos-short = B+
is14-blood-type-ab-pos = четвёртая положительная, AB(IV) Rh+
is14-blood-type-ab-pos-short = AB+
is14-blood-type-o-neg = первая отрицательная, O(I) Rh−
is14-blood-type-o-neg-short = O−
is14-blood-type-a-neg = вторая отрицательная, A(II) Rh−
is14-blood-type-a-neg-short = A−
is14-blood-type-b-neg = третья отрицательная, B(III) Rh−
is14-blood-type-b-neg-short = B−
is14-blood-type-ab-neg = четвёртая отрицательная, AB(IV) Rh−
is14-blood-type-ab-neg-short = AB−
is14-blood-type-synthetic = синтетический заменитель крови
is14-blood-type-synthetic-short = СИНТ

is14-blood-type-insect = гемолимфа
is14-blood-type-insect-short = ГЕМ
is14-blood-type-copper = гемоцианин
is14-blood-type-copper-short = ГЦН
is14-blood-type-slime = слизь
is14-blood-type-slime-short = СЛЗ
is14-blood-type-ammonia = аммиачная
is14-blood-type-ammonia-short = АММ
is14-blood-type-sap = древесный сок
is14-blood-type-sap-short = СОК

# ─── Этикетка ───────────────────────────────────────────────────────────────
is14-blood-label-examine = Этикетка: [color={ $color }]{ $type }[/color]
is14-blood-label-title = Надпись на пакете
is14-blood-label-current = Сейчас написано: { $type }
is14-blood-label-current-none = Пакет не подписан
is14-blood-label-hint = Пакет не знает, что в нём. Написанное — ваше слово.
is14-blood-label-erase = Стереть надпись
is14-blood-label-need-pen = Нечем писать.

# ─── Гемоанализатор ─────────────────────────────────────────────────────────
is14-blood-test-title = Экспресс-анализ крови
is14-blood-test-sample = Образец: { $sample }
is14-blood-test-working = идёт реакция…
is14-blood-test-no-blood = Крови в образце нет.
is14-blood-test-unreadable = Кровь неизвестного типа. Реактивы не подходят.
is14-blood-test-result-unknown = ???
is14-blood-test-well-title = анти-{ $antigen }
is14-blood-test-well-positive = +
is14-blood-test-well-negative = −

# ─── Переливание ────────────────────────────────────────────────────────────
is14-blood-transfusion-rejected = Кровь сворачивается прямо в вене { $target }!
is14-blood-transfusion-reaction-1 = Вас бросает в жар, перед глазами плывёт.
is14-blood-transfusion-reaction-2 = Внутри всё горит, дышать нечем.

# ─── Реагент ────────────────────────────────────────────────────────────────
reagent-name-is14-hemolysed-blood = свернувшаяся кровь
reagent-desc-is14-hemolysed-blood = Слипшиеся комья разрушенных эритроцитов. Кислород они не носят, а вот отравить успевают.

# ─── Тест-полоска ───────────────────────────────────────────────────────────
is14-blood-strip-examine-blank = Полоска чистая — на ней три пятна реагента.
is14-blood-strip-examine-spoiled = Пятна пропитаны чем-то, кроме крови. Полоска испорчена.
is14-blood-strip-examine-developing = Кровь расходится по бумаге, реакция ещё идёт.
is14-blood-strip-examine-nothing = Реагенты не сработали: кровь им незнакома.
is14-blood-strip-examine-result = Проступило: { $wells }
is14-blood-strip-well-positive = анти-{ $antigen } [color=#B23A3A]свернулась[/color]
is14-blood-strip-well-negative = анти-{ $antigen } [color=#6E7A90]чисто[/color]
is14-blood-strip-title = Тест-полоска
is14-blood-strip-header = ЭКСПРЕСС-ТЕСТ ГРУППЫ КРОВИ
is14-blood-strip-patient = Пациент:
is14-blood-strip-patient-placeholder = впишите имя
is14-blood-strip-need-pen = Нечем писать.
is14-blood-strip-already-signed = Полоска уже подписана — переписать нельзя.
is14-blood-strip-no-wells = На эту кровь у станции нет реактивов.
is14-blood-strip-examine-patient = Подписана: [color=#B8A87A]{ $patient }[/color]
is14-blood-strip-footer = НТ Медтех · одноразовая · повторно не применять
