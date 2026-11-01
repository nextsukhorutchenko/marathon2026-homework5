# ДЗ №8: автоматизація групового pre-order

Статус: погоджено користувачем 01.11.2026, явно дозволено перейти до реалізації. Користувач обрав Напрям 1, усі три рівні, .NET + Playwright, API-клієнти, Page Objects і GitHub Actions. ReportPortal не входить в обраний обсяг.

## Мета та джерело

Створити незалежний від product assemblies набір тестів власного групового pre-order, відтворюваний локально та в CI, зі звітами, матеріалами падінь і зрозумілим README для здачі.

Офіційні вимоги: `D:/Users/King/Downloads/Telegram Desktop/homework-aqa.md`, 20493 bytes. Незмінна архівна копія: `D:/epamMarafone/materials/test-automation-homework8-2026-11-01/reference/homework-aqa.original.md`. Допис: https://t.me/EPAM_IT_Marathon_2026/40, вкладення /41. Інструкції вкладення — специфікація завдання, а не дозвіл на публікацію, створення облікових записів сторонніх сервісів або надсилання форми.

## Підхід і альтернативи

1. **.NET + Playwright, API-клієнти та Page Objects у поточному репозиторії — обрано користувачем.** Зберігає власну Angular/.NET-реалізацію як об’єкт тестування, дає короткий шлях від сценарію до HTTP або видимої UI-перевірки.
2. Повний BDD + Screenplay — допустимий умовами, але додав би кроки та абстракції до невеликого набору без наданого вихідного AQA-фреймворку. Не обрано.
3. Playwright TypeScript — технічно придатний для Angular, але користувач обрав .NET.

Рішення реалізатора: NUnit як runner з категоріями Level1/Level2/Level3, Api/Ui/Infrastructure; окремі typed API-клієнти, фабрика ізольованих fixtures і невеликі Page Objects. Усі публічні сценарії перевіряють власний HTTP API; основні UI-сценарії використовують справжні форми входу та інтегрований frontend без route mocking і demo login.

## Стан проєкту та середовища

- Поточний checkout: `projects/marathon2026`, branch `feature/frontend-homework-6`, HEAD `7d359ee`; робоче дерево було чистим перед завданням.
- Реалізовані Owner GET/Prepare/Submit/DELETE, Angular GroupOrderPage, Remove Guest, polling, waiter UI та Cognito authentication. Існуючі unit-тести й поведінка продукту зберігаються.
- E2E-проєкту та CI ще немає.
- Власний backend розгорнуто в AWS. Публічний frontend URL у попередніх ДЗ не отримано через відмову CloudFront. За таблицею умов це дозволяє API-покриття рівнів + мінімальний UI-тест; ми додатково реалізуємо основні UI-перевірки через власний локально запущений frontend, інтегрований з розгорнутим backend. Не називати його публічно розгорнутим frontend.
- CI може збирати й запускати цей frontend на runner, а API лишається розгорнутим. Якщо користувач задає зовнішній UI_BASE_URL, використовувати його й не підмінювати demo-екраном.
- Перевірка AWS-сесії 01.11.2026 повернула InvalidClientTokenId. Це блокує live setup/cleanup, але не написання, компіляцію, тест-дизайн і перевірку інфраструктурної логіки. Потрібне оновлення звичної AWS-сесії користувачем для live запуску.

## Структура й конфігурація

Новий каталог `aqa/restaurant-aqa-framework/`: csproj net10.0, Configuration, Api, Fixtures, Pages, Tests, scripts, README, `.env.example`, `.runsettings`. Інструкції запуску й матриця вимог — `docs/homework8/README.md` та посилання з root README. Новий workflow — `.github/workflows/homework8-e2e.yml`.

Тестовий проєкт не посилається на product csproj і не підключає product services напряму. Відповіді читаються через HTTP з typed JSON DTO. Унікальні назви, email та ID генеруються на запуск. Час бронювання — now + кілька днів, з достатнім запасом до cutoff 30 хвилин.

API_BASE_URL, UI_BASE_URL, AWS_REGION, AWS_ACCOUNT_ID, COGNITO_USER_POOL_ID, назви потрібних таблиць і прапорець дозволу fixtures — лише env/.env. У нових тестових файлах, workflow і прикладах немає фактичних адрес середовища, email, паролів чи ключів. `.env.example` містить пусті/умовні значення. Existing product configuration не переписується заради цього завдання. Значення із середовища мають пріоритет над локальним `.env`.

Live-тести без конфігурації завершуються явною помилкою preflight; не повертати зелений результат через мовчазні skip. Окремий Infrastructure-набір запускається без credentials і без мережевих mutations.

## Дані, ролі й прибирання

Використати AWS SDK для підготовки контрольних тимчасових location/table/dish/reservation/group та недосяжних через UI негативних станів. Для guest join, cart edits, guest submit, Owner prepare/lookup/submit/delete — публічні API. Це підготовка й state checks, дозволені умовами; самі перевірювані дії не підмінюються прямими записами у DynamoDB.

Перед записами обов’язкові явний AQA_ALLOW_FIXTURE_WRITES, перевірка AWS_ACCOUNT_ID та наявність потрібних параметрів. Усі ресурси отримують run prefix `aqa-<GUID>` і створюються без перезапису існуючих записів. Прибирання приймає лише keys, створені конкретним fixture та підтверджені цим prefix, без широких scan/delete довільних записів. Для server-generated IDs перевіряти належність fixture group.

Run-level тимчасові Owner, інший Customer та Waiter створюються/прибираються централізовано. Кожен тест створює власні бронювання, групи й кошики, тому не залежить від порядку й може працювати паралельно; обмежити concurrency до двох для Cognito та GSI. Спільні principal identities не змінюються тестами. Waiter призначений виключно на тестову локацію/бронювання.

Перелік створених keys записується без credentials у recovery ledger. Реєстрацію cleanup ставити до наступних ризикових дій; cleanup виконується навіть після часткового setup або падіння assertion. Прибирати всі власні DynamoDB rows, включно з user/waiter rows, та Cognito identities; підтверджувати відсутність. Cleanup errors відображаються у результаті й не приховуються як успіх. Не видаляти сторонніх користувачів або реальні ресторанні дані.

GSI eventual consistency обробляється обмеженим очікуванням потрібного стану після setup. Не повторювати mutation автоматично, якщо невідомо, чи вона вже виконана. Не додавати test retries для приховування падінь продукту.

## Матриця сценаріїв

### Level 1

- Owner бачить всіх учасників, назви страв, кількості й суми; HTTP total дорівнює сумі кожного participant.
- Зміна guest cart змінює snapshot total; UI відображає оновлення після Refresh або погодженого polling.
- Draft guest та guest з порожнім кошиком присутні з правильними ознаками.
- Сира група без sessions повертає participants=[] і total=0. Не плутати її з prepared group, у якій уже існує Owner session.
- Немає доступу для іншого Customer, Waiter, Guest token та anonymous. Missing group — окремий 404.
- Мінімальний UI: справжній login і/або перегляд власного меню. Основний UI: власник відкриває групу й бачить participant cards та суми.

### Level 2

- Owner session lookup/prepare; Owner може мати власні страви.
- Submit повертає консолідовані quantities: однакова страва у кількох submitted carts об’єднується, draft guest cart виключається.
- Збережені group.status=Submitted і order.status=SubmittedByCustomer перевіряються незалежним state read.
- UI Owner показує Sent to Restaurant; призначений Waiter бачить це саме замовлення й quantities.
- Окремі негативні сценарії: немає submitted sessions; submitted cart порожній; order відсутній; reservation відсутня; group відсутня; не-Owner; anonymous; повторний submit.
- Для кожного негативного сценарію перевіряти status і зміст error, а не лише факт будь-якого non-2xx; перевіряти незмінність основних даних після відмови.

### Level 3

- Видалення одного гостя з кількох; response, list і total відповідають залишку.
- Видалення submitted guest та останнього guest — незалежні тести.
- Foreign-group guest — 400 та обидві групи незмінні. Missing guest — 404.
- Не-Owner/anonymous не можуть видалити; Owner session не видаляється.
- Окремий UI Remove Guest з підтвердженням і перевіркою нової суми.
- Наскрізний UI: guests підготовлено через API → Owner переглядає → видаляє одного → Submit → Waiter бачить склад без видаленого; draft теж не потрапляє в ресторанне замовлення.

## Контракти й неоднозначності

Коди поточної реалізації: anonymous і Cognito-відхилений guest token — 401; авторизований не-Owner — 403; missing group/reservation/guest — 404; повторний submit та mutation закритої групи — 409; некоректний вміст, чужий guest і missing order — 400. Очікувані повідомлення брати з перевіреного HTTP-контракту й зафіксувати в README; не узагальнювати до довільного error.

Останній **guest** може бути видалений, total враховує решту, включно з Owner за його наявності. Видалення Owner session заборонено (400). Тест справді порожньої групи створює group без Owner session.

Наявна реалізація трактує `orders` Owner як його особистий внесок у момент натискання загального Submit. Ця семантика вже описана в ДЗ №5/6; основні сценарії включають draft exclusion саме гостей. Різницю з формулюванням «лише submitted кошики» явно вказати в README, не представляти як нову вимогу. Якщо live-відповідь суперечить явній вимозі ДЗ, зберегти failing test і опис дефекту, не підганяти assertion під помилку.

При кодах/повідомленнях, які авторизатор або deployed версія повертають інакше, діагностувати й пояснювати відмінності, а не дозволяти широкий набір статусів задля green.

## Звіти, діагностика та CI

Локально: TRX та читабельний HTML summary з case, category, duration, error, cleanup; для UI failures — screenshot і trace. Для API failures — безпечний method/path/status/expected/body fragment без Authorization, password, idToken, sessionToken. Успішні live запуски також мають підсумковий звіт.

Звичайний Playwright trace може містити credentials під час UI login. Тому recording починається після login, а failure login має screenshot і санітизовану діагностику без trace auth body. Не записувати HAR, raw storageState, network dump або env у artifacts. Failures cleanup теж потрапляють до summary. Чутливі логи не публікувати.

Workflow GitHub Actions: push/PR — restore, build і Infrastructure checks без cloud secrets; workflow_dispatch — окремі Api/Ui або Level subset проти власного AWS за repository variables/secrets. Окремо пояснити, що зелений Infrastructure job не означає проходження live E2E. Live workflow перевіряє конфігурацію до mutations і не пропускає сценарії мовчки.

Тестовий step допускає продовження після падіння; report generation і upload-artifact виконуються з `always()`. Останній gate повертає невдалий статус за помилок тестів/setup/cleanup, після публікації артефактів. Так пайплайн не перериває збір доказів і не маскує test failures зеленим run. Permissions workflow мінімальні; secrets не надавати зовнішнім PR.

Frontend runner setup: Node версії за чинним engines, npm ci, production build з API_BASE_URL через BACKEND_URL; локальний статичний SPA host без нових frontend npm packages. Не використовувати демо або новий локальний backend. Зовнішній UI_BASE_URL залишається підтриманим.

## Перевірка та здача

1. Сценарії/матриця звірені з оригінальним файлом. Новий AQA build та Infrastructure checks проходять.
2. Live Api/Ui виконуються після відновлення AWS-сесії; фіксуються точні passed/failed/skipped та cleanup. Якщо не виконані, це явно зазначається, а не видається за завершений рівень.
3. Доданий CI YAML і README узгоджені з реальною CLI: category/level subset, потрібні variables/secrets, browser install, reports, recovery cleanup.
4. Продуктові файли не змінюються заради passing suite; не переписувати старі unit-тести й не виправляти сторонні 46 відомих failures.
5. Публікація комітів/гілки/push і налаштування repository secrets виконуються лише після окремого дозволу користувача на конкретний готовий результат. Поточний CLAUDE.md забороняє їх без явного запиту. До того — готові локальні файли, перевірки й точний diff.

Це проєкт тестової автоматизації з кількома зовнішніми залежностями. Уточнення точних версій NuGet, назв класів, деталей DTO і розбиття тестових fixtures залишено реалізатору в межах цього рішення.
