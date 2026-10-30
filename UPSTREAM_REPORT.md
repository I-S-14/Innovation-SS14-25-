# Отчёт: апстрим 2026-10-29 (Goob → space-wizards)

Ветка: `2026-29-10-upstream`. Ничего не пушилось.

Приоритеты при разрешении конфликтов (по твоему ТЗ):
1. Фичи IS14 (`_IS14`, `IS14-change`/`IS14 Edit`) — не переписываются.
2. Контент Goob (медицина/Shitmed, антаги, геймрежимы и т.д.) — сохраняется.
3. Визарды — берём всё остальное: респрайты, фиксы, оптимизации, новый контент.

---

## Этап 1. Последний мерж Goob (`space-syndicate/Goob-Station` master `30d4c598db`)

Объём: 691 коммит у Goob против 286 наших с общей базы (2026-07-22). 19 файлов с конфликтами.

### Движок
Goob поднял RobustToolbox с v248 до **v293.0.0** (официальный коммит визардов `0a83625b9b`,
проверил — он является предком их v294.0.0, т.е. это не форк движка). Принял как есть.

### Решения по конфликтам

| Файл | Что было | Решение и почему |
|---|---|---|
| `OptionsMenu.xaml(.cs)` | И мы, и Goob добавили вкладку настроек на индекс 7 (наша IS14, у Goob «Кастомизация») | Оставил обе: Goob — индекс 7, IS14 — 8. Ничего не теряем. |
| `SlotControl.cs` | Мы сделали класс `partial` (для `_IS14/UserInterface/SlotControl.IS14.cs`), Goob убрал `[Virtual]` (на abstract не нужен) | `public abstract partial class` без `[Virtual]`. |
| `PaperSystem.cs` | У нас штамп не ставится сразу, а открывает документ для прицеливания (StampPlacementSystem). Goob добавил в старую ветку «штамп сразу» вызов события `PaperStampedEvent` (для ачивок) | Оставил нашу механику, а событие Goob перенёс в `Content.Shared/_IS14/Paper/StampPlacementSystem.cs` — туда, где штамп реально ставится. Ачивка Goob за штампы будет работать. |
| `ActivatableUISystem.cs` | Goob сменил сигнатуру `InteractUI(user, (uid, comp))`, мы добавили `Priority = component.VerbPriority` | Взял новую сигнатуру + оставил наш `Priority`. |
| `role_loadouts.yml` (апстрим и `_Goobstation`) | У нас почти все roleLoadout закомментированы и перенесены в `_IS14/Loadouts`. Goob правил оригиналы | Оставил наш вариант. Из изменений Goob вручную перенёс в наши копии: `StationAiLaws` для ИИ (выбор набора законов в лодауте) и `canCustomizeName` для священника. **Не** переносил новые `*JobTrinkets`/пояс атмосферника — в наших лодаутах эти группы намеренно вырезаны. |
| `job-names.ftl` | Мы переименовали Blueshield в «Оперативник ГСО»; Goob перевёл прототип профессии на ключ `job-name-bso` | Оставил наши названия и добавил ключ `job-name-bso` с нашим названием (en и ru), иначе бы вылезло «Blueshield Officer». |
| `replays.ftl` | Goob удалил строку для удалённой команды `replay_toggleui`, мы добавили строки своей команды скриншот-режима | Наши строки оставил, мёртвую строку удалил. |
| `cat_parts.yml` | Goob сменил состояние хвоста на `tail_cat_wag` (спонсорская механика Corvax) | Оставил наше `tail_cat`. |
| `migration.yml` | Обе стороны дописали миграции в конец | Оставил обе. |
| Карты (`core`, `avrite`, `marathon`, `maus`, `atlas`) | У нас свой состав профессий (милиция вместо СБ, убраны часть медролей). Goob менял слоты СБ и добавил новые профессии (RadioHost, ShaftMiner, Magistrate, Paramedic на Atlas, Virologist/Surgeon) | Оставил наш состав. Из изменений Goob принял только число боргов на Maus (`[1, 2]`). **На твоё решение:** нужны ли RadioHost/ShaftMiner/Magistrate на наших картах. |
| `.github/workflows/*` | Goob менял CI (таймауты, способ коммита кредитов), удалил `reuse-updater.yml` | CI — наш, оставил полностью наш вариант, `reuse-updater.yml` сохранил. |

### Поломки сборки после мержа Goob (исправлены)
- `DamageModifyEvent`, `DamageChangedEvent`, `DamageableSystem` переехали в `Content.Shared.Damage.Systems`
  (Goob взял апстрим-рефакторинг DamageableSystem). Добавил using в наш код модсьютов, вендоматов и тест.
- Движок v293: `Color.TryFromHex` теперь `bool TryFromHex(span, out Color)`. Поправил `ModularChassisWindow`.

После этого `dotnet build -c DebugOpt` — 0 ошибок.

После мержа Goob YAML-линтер: **0 ошибок**.

---

## Этап 2. Мерж space-wizards (`space-wizards/space-station-14` master `bd217732d1`, 2026-10-29)

### Проблема №1: у git не было нормальной общей базы
Goob никогда не делал настоящих merge-коммитов с визардами — он переносил их коммиты rebase/cherry-pick'ом
(заголовки те же, хеши другие). Поэтому общий предок нашей ветки и визардов — **май 2025**, и наивный мерж
давал >6000 конфликтов из уже давно перенесённых изменений.

Я сопоставил коммиты визардов с историей Goob по заголовкам (номерам PR): до **2026-01-16** Goob содержит ~90%
коммитов визардов, после — почти ничего. Точка синхронизации — `d2ac15c76f` ("Fix flatpacker exploit…", 16.01.2026).
Сразу после неё у визардов идёт **Nubody** — видимо, на нём Goob и остановился.

Решение: коммит `28a48c2fa6` — `git merge -s ours d2ac15c76f` (файлы не меняет, только записывает в историю,
что визарды до 16.01.2026 уже влиты). После этого обычный `git merge wizden/master` получил правильную базу.
Будущие апстримы от визардов будут обычными мержами.

Цена: ~470 коммитов визардов до 16.01.2026, которые Goob в своё время пропустил (часто намеренно — они
конфликтовали с медициной Goob), так и остаются невлитыми. Список — в приложении A.

### Движок
RobustToolbox → **v294.0.0** (ровно та версия, на которой сейчас визарды).

### Главное архитектурное решение: стек «тело/медицина» остаётся от Goob
За 9 месяцев визарды переписали тело и всё вокруг него:
- **Nubody** (#42419) — новая система тела, удалены `SharedBodySystem.Parts/Organs/Body`, `BodyPartComponent` и т.д.;
- **Visual Nubody** (#42476) — внешность гуманоидов перенесена на органы, удалён `HumanoidAppearanceComponent`, переписаны маркировки;
- **Metabolism stages** (#42172) — группы метаболизма (Medicine/Poison/Food…) заменены «стадиями» (Bloodstream/Digestion…);
- **Damage split** (InjurableComponent, DamageDealtEvent) — новый поток урона.

Вся медицина Goob (Shitmed: хирургия, раны, таргетинг частей тела, Woundmed-урон с капом целостности частей)
построена на старой системе. Принять это = переписать медицину Goob — недели работы и прямо против приоритета
«медицину Goob сохранить». Плюс ты планируешь дальше черри-пикать у Goob, а Goob на старом теле.

**Поэтому следующие домены целиком оставлены нашими (Goob), а изменения визардов в них отклонены:**

| Домен | Почему |
|---|---|
| `Body`, `Humanoid`, `MagicMirror`, прототипы `Body/`, `Species/`, мобы-виды, маркировки, текстуры видов | Nubody / Visual Nubody |
| `Metabolism` (новый каталог визардов удалён), `ReagentPrototype` + метаболизм реагентов | стадии метаболизма |
| `Damage` (Shared/Server/Client) | поток урона Goob/Woundmed |
| `Temperature` | на ней завязан Shitmed (scaleByTemperature, криокапсула), 20 файлов форков |
| `Forensics` | визарды переписали (Forensics V2), у Goob своё (touch DNA, DNA в растворах) |
| `Medical/Cryogenics`, `HealthAnalyzer`, `BiomassReclaimer`, `MedicalScanner` | медицина Goob/Shitmed |
| `Changeling` (апстримный генокрад визардов), `Gibbing`, моргание глаз (`Eye/Blinking`) | построены на Nubody |
| `Mech` + YAML мехов | визарды перевели мехов на Vehicle, у Goob оборудование/оружие мехов |
| `VendingMachines` | у Goob/Corvax возврат товаров в автомат, UI Funkystation |
| `ToggleableClothing` | у Goob многочастные прикреплённые вещи |
| Правило предателя (`TraitorRuleSystem`) | у Goob свой аплинк/кодовые слова; оставлено серверным |

Код визардов за пределами этих доменов, который использует их новые API тела/метаболизма, откатывался к
нашей версии файла (или удалялся, если это новая фича визардов только под Nubody).

### Что принято от визардов (крупное)
- Движок v294, «Move GameTicker to Shared», правила режимов в Shared (кроме предателя).
- DeviceNetwork refactor (типизированные payload'ы) — фичи Corvax на робототехнике портированы на новый API.
- Унификация иконок статуса (`factionIcon`/`securityIcon`/… → `statusIcon`) — YAML и C# форков переведены.
- Decals (новые чанки), Disposal (переписанный мусоропровод), StationSpawning в Shared, Hunger/Thirst → Satiation (в процессе портирования потребителей Goob).
- Все текстуры/респрайты визардов («The Respritening», шлюзы, стены, окна…), кроме тех, что трогал IS14.
- Карты визардов, которые IS14 не трогал.

### Механика разрешения ~6200 конфликтов (скрипты лежат в scratchpad сессии)
1. SPDX-заголовки Goob (`// SPDX-License-Identifier`) конфликтовали почти с каждым файлом — авторешение: заголовок + код визардов, если в остальном наша сторона = база.
2. BOM: визарды удалили BOM из файлов — нормализация при сравнении.
3. `using`-блоки — объединение; `[Dependency]` — объединение с дедупликацией (визарды убрали `readonly` под IoC source-gen).
4. YAML-прототипы — структурный трёхсторонний мерж по `id` → полям → компонентам; при истинном конфликте:
   `parent` — объединение, спрайтовые компоненты — визарды (респрайты), остальное — наше.
   Прототипы, которые визарды перенесли в другие файлы (реорганизация джампсьютов/животных/снарядов), — наши правки пересажены на новое место (379 шт).
5. ftl — мерж по ключам, при конфликте текста — наш.
6. Файлы, переведённые визардами на file-scoped namespace (переиндентация) — перемерж после нормализации отступов.
7. Оставшиеся содержательные хунки кода: **наш**, если в хунке маркер форка (`// Goob`, `IS14`, `Shitmed`, `DeltaV`, `Corvax`, `WD`…) или файл трогал IS14; иначе — **визардов** (обычно это тот же апстримный рефакторинг, сделанный по-разному).

### Совместимость (shim'ы), добавленные, чтобы код визардов работал поверх стека Goob
Все лежат в `_IS14` или помечены `IS14-change`/`IS14-merge`:
- `Content.Shared/_IS14/Damage/DamageableSystem.WizdenCompat.cs` — API урона визардов (`GetTotalDamage`, `GetAllDamage`, `GetDamagePerGroup`, `GetDamages`) поверх нашего урона; `DamageDealtEvent` поднимается из нашего `OnEntityDamageChanged`. `InjurableComponent` визардов в коде заменён на наш `DamageableComponent` (все его поля у нас есть).
- `Content.Shared/_IS14/Damage/DamageExamineSystem.WizdenCompat.cs` — перегрузка осмотра урона со стаминой.
- `SolutionContainerChangedEvent` (Goob/Corvax) поднимается рядом с новым `SolutionChangedEvent`; код Goob переведён с устаревшего `SolutionContainerManagerComponent` на `SolutionManagerComponent` (визарды сами конвертируют старый компонент на MapInit и удаляют его — без перевода код Goob молча перестал бы видеть растворы).
- `Content.Shared/_IS14/Nutrition/HungerThirstCompat.cs` — фасады `HungerSystem`/`ThirstSystem` поверх `SatiationSystem` визардов; YAML `!type:SatiateHunger/Thirst` → `!type:Satiate` + `satiationType`.
- `HumanoidProfileComponent` (визарды) в их коде → наш `HumanoidAppearanceComponent`.
- Иконки `factionIcon/securityIcon/healthIcon/contrabandIcon/curseStatusIcon/...` → единый `statusIcon` (C# и YAML).
- `StationSpawningSystem` визардов в Shared, но вместо их VisualBody вызывает наш `LoadProfile`; Corvax-выбор законов ИИ перенесён в Shared partial.

### Русская локализация акцентов Corvax
Визарды перенесли акценты в Shared (`RelayAccentSystem`). Русские замены Corvax перенесены в новые версии:
ящеры, мотыльки, шепелявость, лай, обезьяна, OwO, невнятная речь, заикание.

### Состояние сборки (обновляется)
- [x] Content.Shared
- [ ] Content.Goobstation.Shared — в процессе
- [ ] Content.Server / Content.Goobstation.Server
- [ ] Content.Client / Content.Goobstation.Client
- [ ] Тесты, YAML-линтер, локализация новых строк
