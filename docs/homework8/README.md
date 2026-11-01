# Test Automation №8 — усі три рівні Напряму 1

Об'єкт: власний інтегрований груповий pre-order з ДЗ №5/6. Реалізація: [AQA framework і запуск](../../aqa/restaurant-aqa-framework/README.md), [CI workflow](../../.github/workflows/homework8-e2e.yml). Специфікація: [офіційний допис](https://t.me/EPAM_IT_Marathon_2026/40), вкладення `homework-aqa.md`. Обрано .NET/NUnit + Playwright, API clients + Page Objects, GitHub Actions; без ReportPortal.

## Покриття

| Вимога | API tests | UI tests |
|---|---|---|
| L1 учасники, страви, кількість, subtotal, total | `Owner_sees_all_participants_items_quantities_and_totals` | `Owner_UI_shows_submitted_draft_empty_carts_and_updates_quantities` |
| L1 зміна кошика перераховує суму | `Total_changes_when_guest_adds_more_of_the_same_dish` | Той самий UI case, реальна зміна guest API та Refresh |
| L1 draft/empty guest і справді порожня група | Основний snapshot case, `Raw_empty_group_returns_empty_participants_and_zero_total` | Empty/draft cards у snapshot case |
| L1 лише Owner / missing group | `Only_owner_can_read_group_sessions` ×4, `Missing_group_returns_404` | Доступ перевіряється через API |
| Мінімальний UI на інтегрованому застосунку | Реальні register/signin fixtures | `Authenticated_owner_can_open_the_integrated_menu` |
| L2 Owner session | `Owner_session_lookup_belongs_to_the_reservation` | Owner page використовує lookup |
| L2 merge, тільки submitted guest carts, обидва statuses | `Submit_merges_submitted_items_excludes_draft_and_persists_both_statuses` | `Owner_UI_submits_merged_submitted_carts_and_locks_order` |
| L2 особистий внесок Owner без дублювання | `Owner_personal_contribution_is_included_once` | Окремий API contract case |
| L2 ресторан бачить order | `Assigned_waiter_reads_the_exact_order_submitted_by_owner` | Owner→Waiter E2E |
| L2 немає submitted / empty submitted / missing order | Три окремі negative cases з exact message | `Owner_UI_disables_submit_without_submitted_dishes` |
| L2 missing reservation/group, repeat submit | Три окремі negative cases, repeat 409 з перевіркою persisted order | UI блокується після submit |
| L2 не Owner | `Only_owner_can_submit` ×4 | API authorization cases |
| L3 draft/submitted guest removal, total | `Removing_draft_or_submitted_guest_recalculates_remaining_total` ×2 | `Owner_UI_removes_guest_and_recalculates_consolidated_total` ×2 |
| L3 останній guest | `Removing_last_guest_returns_empty_list_and_zero_total` | `Owner_UI_removes_last_guest_and_returns_to_empty_order` |
| L3 чужа група / missing guest / owner session | Три negative cases; обидві групи зберігаються | API cases |
| L3 не Owner | `Only_owner_can_remove_a_guest` ×4 | API authorization cases |
| L3 review→remove→submit→waiter | Підготовка через реальні guest endpoints | `Owner_removes_submits_and_waiter_sees_only_remaining_dishes` |

Разом: **32 API cases** (L1: 8, L2: 14, L3: 10), **8 UI cases** (L1: 2, L2: 2, L3: 4), **22 Infrastructure cases**. Усі 40 live-сценаріїв мають підтверджений останній результат Passed; походження кожного — у [машинному протоколі](verification-2026-11-01.json).

## Зафіксовані контракти та неоднозначності

| Умова | Очікування |
|---|---|
| Anonymous або guest session token на Owner endpoint | 401 `Unauthorized` від Cognito authorizer |
| Авторизований інший Customer / Waiter | 403 `Only the reservation owner can perform this action.` |
| Missing group | 404 `Group reservation was not found` |
| Missing reservation при submit | 404 `Reservation not found.` |
| Missing order після submitted guest cart | 400 `Order not found. Prepare the group pre-order first.` |
| Немає submitted sessions | 400 `There are no submitted sessions.` |
| Submitted sessions порожні | 400 `Submitted sessions contain no dishes.` |
| Повторний submit | 409 `This group reservation is no longer open for changes.` |
| Guest іншої групи | 400 `Guest session does not belong to this group reservation.` |
| Missing guest | 404 `Guest session not found.` |
| Видалення Owner session | 400 `The owner session cannot be removed.` |

Ці exact expectations взято з наявного product contract; live прогін має їх перевірити, не підлаштовуючи довільний набір статусів під відповідь deployment.

API snapshot total враховує всі кошики, включно з draft. UI **Grand Total** показує лише склад майбутнього consolidated order; draft лишається видимим у власній картці. Тому ці два totals можуть відрізнятися. Prepared group має Owner session; справді порожня група тестується без prepare. Після видалення останнього guest у prepared group лишається Owner, а у raw group — `participants=[]`.

Наявний backend трактує особистий order Owner як його внесок у момент загального Submit і включає його один раз. Це семантика ДЗ №5/6, а не нова вимога «лише submitted». Основний позитивний тест окремо підтверджує exclusion draft **guest**. За іншого трактування вимоги Owner-внесок потребує уточнення; тест зберігає поточний описаний контракт.

Успішний submit: group `Submitted` (1), order `SubmittedByCustomer` (3). Повторні й unauthorized actions не повинні змінювати дані. Для generated guest ID cleanup перевіряє stored relationship з конкретною fixture group, для seeded rows — ownership marker/підтверджене створення.

## Перевірено 01.11.2026

- `dotnet restore --locked-mode`, build: успішно.
- Infrastructure: **22 passed, 0 failed, 0 skipped**.
- Python report checks: **4 passed**; Node SPA server check: **1 passed**.
- Test discovery: **62 cases**, включно з 40 live cases.
- Після поновлення CLI-сесії live smoke: **1 passed**, cleanup підтверджений.
- API run 1: **28 passed, 4 failed, 0 skipped**. Усі чотири failures — відсутня Cognito-група `Waiter` під час підготовки ролі. 32/32 fixtures прибрано.
- UI run 1 без Waiter E2E: **6 passed, 1 failed** через окремий default assertion timeout 5 секунд. Trace показав понад 5 секунд послідовних live reads; ліміт assertions узгоджено з browser actions (30 секунд).
- UI run 2: **7 passed, 0 failed, 0 skipped**, 7/7 fixtures прибрано.
- За окремим дозволом створено тимчасову `Waiter` без IAM-role. Повторно виконано чотири Waiter API cases та Owner→Waiter UI E2E: **5 passed, 0 failed, 0 skipped**; 5/5 fixtures прибрано. Після перевірки ownership marker і порожнього membership групу видалено, відсутність підтверджено.
- Підсумок останніх результатів: **32 API + 8 UI = 40/40 Passed**. Це зведення трьох реальних прогонів, а не один одночасний запуск 40 cases. Початкові помилки збережено; [JSON з per-case provenance та SHA-256 TRX](verification-2026-11-01.json) містить джерела. У цих трьох прогонах cleanup підтверджено для **44/44 fixtures**, pending joins: 0.
- GitHub Actions: YAML перевірено actionlint 1.7.12; фактичного cloud run і публікації ще немає.
- Production frontend build і перевірка configuration placeholders: успішно; Chromium виконав справжній login, Owner сценарії та окремий Waiter BrowserContext проти AWS.
- Незалежне рев'ю: три важливі findings виправлено з RED→GREEN regression checks — pending join cleanup, order/menu readiness, persisted-state invariance. Нові product behavior changes не вносилися.

Залишилося опублікувати зміни й виконати GitHub Actions з repository variables/secrets. Перед повторним повним запуском потрібна роль `Waiter` у тестовому User Pool: тимчасову роль навмисно прибрано після перевірки. Старі успішні unit/HTTP перевірки з ДЗ №5/6 не підміняють наведені live результати ДЗ №8.

Здача: публічний git repository з `aqa/restaurant-aqa-framework`, workflow та README. [Форма](https://forms.cloud.microsoft/e/6Y8eBy6EyJ?origin=lprLink). Форма автоматично не надсилається.
