# ДЗ №8 — Direction 1, рівні 1–3

Автоматизація власного групового pre-order: **.NET 10 + NUnit + Playwright**, typed HTTP client, Page Objects, ізольовані AWS fixtures та GitHub Actions. ReportPortal не входить в обраний обсяг.

**Стан на 01.11.2026:** підтверджено всі **32 API + 8 UI cases** проти AWS; 22/22 Infrastructure, 4/4 перевірки генератора звітів, 1/1 перевірка SPA-сервера. Це останні результати окремих прогонів: 28 API + 7 UI + 4 повторно перевірені Waiter API + Owner→Waiter E2E. Початкові setup/timeout failures збережено в протоколі. Cleanup усіх fixtures і видалення тимчасової групи перевірено. GitHub Actions cloud run ще не виконувався. [Матриця й протокол](../../docs/homework8/README.md).

## Структура

| Каталог | Призначення |
|---|---|
| `Api` | HTTP transport, DTO, очікування GSI стану, безпечна діагностика |
| `Fixtures` | Облікові записи, дані ресторану, журнал і cleanup |
| `Pages` | Справжні форми входу, Owner group order, Waiter reservations/pre-order |
| `Diagnostics` | Скріншот падіння та санітизація Playwright trace |
| `Tests` | Infrastructure та незалежні API/UI сценарії |
| `scripts` | Локальний runner, HTML report, статичний production SPA server |

Тести не посилаються на product assemblies, не використовують demo mode, підміну HTTP routes чи локальний backend. API працює в AWS; UI можна запускати локально з production bundle, підключеним до цього API. Кожен тест має власний `aqa-<GUID>` prefix і власні principals. Два NUnit workers обмежують навантаження; тести одного класу виконуються послідовно, класи можуть виконуватися паралельно.

## Підготовка

Потрібні .NET SDK 10, PowerShell 7, Python 3.11+ (лише stdlib), Node за `frontend/restaurant-frontend-app/package.json` та доступ до власного AWS deployment. Виконуй команди з цього каталогу.

```powershell
Copy-Item .env.example .env
dotnet restore --locked-mode
dotnet build --no-restore
./bin/Debug/net10.0/playwright.ps1 install chromium
```

У Linux CI: `pwsh ./bin/Debug/net10.0/playwright.ps1 install --with-deps chromium`.

Заповни `.env`; файл і результати ігноруються Git. Значення shell environment мають пріоритет. Для іншого файлу задай `AQA_ENV_FILE` або `-EnvFile` runner-а.

| Змінна | Значення |
|---|---|
| `API_BASE_URL` | Коренева HTTPS адреса розгорнутого API зі stage, без кінцевого `/` |
| `UI_BASE_URL` | Корінь інтегрованого SPA; локально `http://127.0.0.1:4200` |
| `AWS_REGION`, `AWS_ACCOUNT_ID` | Регіон і очікуваний AWS account для fixture guard |
| `COGNITO_USER_POOL_ID` | User pool цього deployment |
| `AQA_TABLE_LOCATIONS`, `AQA_TABLE_TABLES`, `AQA_TABLE_DISHES` | Відповідні DynamoDB tables |
| `AQA_TABLE_RESERVATIONS`, `AQA_TABLE_GROUPS`, `AQA_TABLE_SESSIONS` | Reservation/group/guest session tables |
| `AQA_TABLE_ORDERS`, `AQA_TABLE_WAITERS` | Orders і Waiters tables |
| `AQA_ALLOW_FIXTURE_WRITES` | Явно `true` для live запуску |
| `AQA_RESULTS_DIR` | Необов'язковий каталог результатів; runner задає сам |

AWS SDK використовує credentials chain: змінні середовища/локальний credentials profile (`AWS_PROFILE`). Потрібна чинна короткострокова сесія; для session credentials задаються всі три `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`. Паролі тестових користувачів генеруються в пам'яті; вони не є конфігурацією.

Fixture permissions: `sts:GetCallerIdentity`; `dynamodb:GetItem/PutItem/DeleteItem/Query` для зазначених таблиць та їхніх indexes; `cognito-idp:AdminGetUser/AdminDeleteUser/AdminAddUserToGroup/AdminRemoveUserFromGroup` для потрібного user pool. Публічний `/register` створює disposable accounts; групи `Customer` та `Waiter` мають існувати. Дані готуються через SDK, дії, які тестуємо, — через публічний HTTP або UI. У перевіреному deployment група `Waiter` спочатку була відсутня: за окремим дозволом її створено без IAM-role для тестів і видалено після перевірки порожнього membership. Перед повторним повним запуском підготуй цю роль у своєму тестовому User Pool; suite не змінює спільну конфігурацію ролей автоматично.

## Запуск і фільтри

```powershell
./scripts/run-tests.ps1 -Suite Infrastructure
./scripts/run-tests.ps1 -Suite Api
./scripts/run-tests.ps1 -Suite Ui -Level 1
./scripts/run-tests.ps1 -Suite Live -Level 3
./scripts/run-tests.ps1 -Suite Live
```

`Suite`: Infrastructure / Api / Ui / Live. `Level`: All / 1 / 2 / 3. Runner підтримує `-Dotnet`, `-Python`, `-EnvFile`, `-ResultsDirectory`. Без live configuration отримуємо явну помилку, не skip. Infrastructure не потребує AWS або браузера.

Окремий case:

```powershell
dotnet test --no-restore --settings .runsettings --filter 'FullyQualifiedName~Raw_empty_group' --logger 'trx;LogFileName=results.trx' --results-directory test-results/single
python scripts/report.py test-results/single
```

Прямий `dotnet test` запускає всі категорії, включно з live. Для cloud-free перевірки завжди задавай `TestCategory=Infrastructure`.

Перевірки інструментів:

```powershell
python -m unittest discover -s scripts -p test_report.py
node --test scripts/serve-ui.test.mjs
```

## Локальний інтегрований frontend

В окремому терміналі, у `frontend/restaurant-frontend-app`:

```powershell
$env:BACKEND_URL = $env:API_BASE_URL # попередньо задай той самий API, що у .env тестів
npm ci
npm run build
```

Повернись до цього AQA каталогу й запусти:

```powershell
node scripts/serve-ui.mjs
```

Сервер слухає лише loopback, port 4200 (`AQA_UI_PORT` для іншого). Deep links віддають SPA index; відсутні assets повертають 404. Зовнішній готовий frontend теж підтримано через `UI_BASE_URL`; тоді локальний сервер не потрібний. Обидва варіанти мають звертатися до того самого API deployment. Public frontend hosting не є умовою локального прогону.

## Fixtures і cleanup

Перед мутаціями перевіряються конфігурація, явний дозвіл і AWS account. Унікальні location/table/dishes/reservation/group створюються conditional puts без перезапису. Час бронювання — UTC today + 2 days, 14:00–16:00; він фіксується один раз на fixture. Guest join/cart/submit та Owner prepare/submit/remove використовують реальні endpoints.

Cleanup реєструється до ризикової дії, виконується у зворотному порядку після кожного тесту й частково невдалого setup. Server-generated guest IDs видаляються лише після перевірки їхнього фактичного `groupReservationId`. Перед guest join у durable journal записується pending attempt, який закривається лише після реєстрації поверненого session ID для cleanup. Якщо відповідь втрачено, виконується best-effort recovery query лише по власній групі, а cleanup позначається невдалим: навіть порожній eventual GSI не доводить відсутності невідомого session ID. Pending attempt зберігається для ручної перевірки. Власні seeded rows перевіряються за `aqaRunId`; для product updates, що прибирають marker, потрібне підтверджене створення цим fixture. Cognito identities також видаляються. Кожне відоме видалення перевіряється strong read; cleanup errors роблять тест невдалим.

`test-results/<aqa-prefix>/cleanup-ledger.json` містить keys, ownership relationships, emails disposable users та стан, без passwords/tokens. `cleaned-and-verified` означає завершений cleanup зареєстрованих ресурсів. Якщо процес примусово зупинено або credentials спливли, автоматичний teardown може не завершитися: збережи ledger, понови AWS доступ і перевір **лише перелічені keys** у тому самому deployment. Для server-generated rows звір relationship; для seed rows звір marker/доказ створення; перевір guest sessions index **тільки точного власного group ID**, потім видали власні Cognito emails. Не застосовуй scan/delete до всього `aqa-*` або чужого запуску. Після ручного прибирання перевір відсутність усіх перелічених keys/users і зафіксуй це у протоколі. У журналі навмисно немає credentials, тому вони поновлюються окремо.

GSI стан очікується read polling (до 25 секунд): після prepare — точний order, після guest join — доступність обох seeded dishes. Browser actions та locator assertions мають окремо налаштований ліміт 30 секунд; стандартні 5 секунд недостатні для кількох послідовних live reads під час завантаження сторінки. Мутації й тести автоматично не повторюються. Negative cases перевіряють snapshot та fingerprints усіх зареєстрованих DynamoDB rows до/після дії; значення session token hashes до звітів не потрапляють. Authorization/missing-resource cases використовують populated prepared baseline, крім станів, які навмисно моделюють відсутній order або sessions.

## Звіти та діагностика

Runner створює окремий каталог запуску з TRX, NUnit XML categories та `index.html`. HTML показує case, категорії, duration, outcome, errors і cleanup state. Немає TRX, є failed/skipped case або незавершений cleanup — report повертає non-zero. Runner повертає failure після створення звіту.

UI failure додає `owner/failure.png` (або `waiter/`) і `trace.zip`. Запис trace починається після справжнього UI login. Перед публікацією текстові entries ZIP, включно з network headers і response resources, очищаються від відомих паролів/токенів та Bearer/credential fields. Invitation credentials прибираються також із `/guest/invites/...` request paths. Raw ZIP тимчасовий і видаляється. Login failure має screenshot та санітизовану помилку без auth trace. HAR/storageState/env dumps не створюються.

Trace можна відкрити після розпакування artifact:

```powershell
./bin/Debug/net10.0/playwright.ps1 show-trace test-results/<aqa-prefix>/owner/trace.zip
```

API failures містять method/path/expected/actual HTTP status та обмежений санітизований фрагмент response. Перевірки очищення, які впали, видно і в результаті тесту, і в HTML.

## GitHub Actions

Workflow: [homework8-e2e.yml](../../.github/workflows/homework8-e2e.yml).

- Push/PR: restore/build, Infrastructure, перевірки report/server; cloud secrets не використовуються. Цей зелений job не означає passing E2E.
- `workflow_dispatch`: Live / Api / Ui та All / 1 / 2 / 3. Live UI job збирає production frontend і запускає його на runner проти AWS. AWS credentials передаються лише кроку запуску live тестів; frontend install/build та report/upload їх не отримують.
- Report та upload виконуються через `always()`; кінцевий gate зберігає failure тестів/setup/cleanup. Artifacts зберігаються 14 днів.

Repository variables: `AQA_API_BASE_URL`, `AQA_AWS_REGION`, `AQA_AWS_ACCOUNT_ID`, `AQA_COGNITO_USER_POOL_ID`, `AQA_ALLOW_FIXTURE_WRITES=true` та всі `AQA_TABLE_*` із таблиці вище. Secrets: `AQA_AWS_ACCESS_KEY_ID`, `AQA_AWS_SECRET_ACCESS_KEY`, `AQA_AWS_SESSION_TOKEN` для тимчасової сесії. Потрібен достатній час дії сесії на весь run. Публікація workflow не налаштовує ці значення автоматично.

Actions → Homework 8 AQA → Run workflow → suite/level → artifacts `hw8-live-*` → `index.html`. Workflow dispatch доступний у UI після появи workflow у default branch. Для коротких AWS sessions запускай окремі Level subsets з credentials, чинними протягом конкретного run. Поточні live перевірки підтверджені локально; публікація та фактичний GitHub Actions run — окремий наступний крок.

Офіційна документація: [Playwright .NET CI](https://playwright.dev/dotnet/docs/ci), [NUnit adapter settings](https://docs.nunit.org/articles/vs-test-adapter/Tips-And-Tricks.html), [GitHub artifacts](https://github.com/actions/upload-artifact).
