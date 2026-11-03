# Рівень 3: розгортання та додаткова функціональність

**Сайт:** [Green & Tasty](https://green-and-tasty-level3.vercel.app) · **Приклад пошуку:** [salad, від дешевших](https://green-and-tasty-level3.vercel.app/menu?q=salad&sort=PriceAscending).

Запит користувача від 02.11.2026: розгорнути застосунок із `marathon2026-main.zip` і реалізувати додаткову функціональність. Користувач обрав пошук страв і доручив деталі реалізації. Збережено попередні завершені ДЗ; стартовий архів не перезаписував робочий проєкт.

## Додаткова функціональність

У Menu додано пошук за назвою через справжній `GET /dishes?name=...`. API шукає підрядок без урахування регістру. Форма доступна з клавіатури: введіть текст і натисніть Enter або Search. Пробіли по краях прибираються, порожній запит знімає фільтр. Clear search зберігає категорію та сортування.

Параметр `q` зберігається в URL; посилання можна передати іншому користувачу. Reload і Back відновлюють запит. Пошук комбінується з категорією та сортуванням і не видаляє параметри бронювання/pre-order. Новий пошук скасовує попередній HTTP-запит; при виході зі сторінки підписки завершуються. Додано loading, кількість результатів, empty/no matches, error та Try again.

Основний код: `frontend/restaurant-frontend-app/src/app/pages/menu-page/`; HTTP-контракт — `src/app/services/dishes/`. Нових npm-залежностей немає. Версія frontend: **1.1.4**.

## Де розгорнуто

- Frontend: Vercel Hobby, окремий `green-and-tasty-level3` у підтвердженому користувачем акаунті. Завантажено лише готові статичні файли production-збірки, без `.env`, AWS/Vercel credentials та backend-артефактів.
- Backend: наявні Lambda/API Gateway/DynamoDB у AWS `us-west-2`; URL — `https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop`.
- Deep links підтримує Vercel Build Output API v3: спочатку файл, потім fallback на `/index.html` для Angular Router.
- AWS CloudFront не створено: план містив лише distribution і policy приватного frontend S3, але AWS повернув `403 AccessDenied`, Request ID `9d9124da-b587-4007-b376-7017667631db`. S3 Block Public Access залишився ввімкненим. Використано HTTPS-хостинг Vercel.
- Команда CLI була без `--prod`; Vercel автоматично призначив перше розгортання нового проєкту production. Захист preview не вимикався. Канонічний URL перевірено у браузері без входу.

Це ручне розгортання готової збірки; автоматичне оновлення сайту після git push не налаштоване. Вихідний код пошуку, відповідний розгорнутій збірці, міститься в цьому репозиторії; історичні результати ДЗ №8 не замінюють перевірок цього доповнення.

## Демонстраційні дані

На початку перевірки реальний каталог був порожній. `scripts/seed-level3-menu.py` додав 6 страв, одну явно вигадану локацію та два столи. Це постійний демонстраційний каталог для роботи сайту, а не залишені після тестів `aqa-*` fixtures. Харчові значення позначені як приклади в описі страв.

Скрипт перевіряє цільовий AWS-акаунт, читає кожен стабільний ID, відмовляється перезаписувати відмінний запис і створює відсутні записи однією умовною транзакцією. Після запису перевіряє всі 9 записів повторним читанням. Повторний запуск з тими самими даними нічого не змінює. Для іншого акаунта спочатку адаптуйте його константу ACCOUNT свідомо.

## Запуск і перевірка

```powershell
cd frontend/restaurant-frontend-app
npm ci
npm start
# http://localhost:4200/menu?q=salad&sort=PriceAscending

npx tsc -p tsconfig.app.json --noEmit
npx tsc -p tsconfig.spec.json --noEmit
npm test -- --watch=false --browsers=ChromeHeadless --include src/app/pages/menu-page --include src/app/services/dishes
npm run test:hw -- --browsers=ChromeHeadless

$env:BACKEND_URL = 'https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop'
npm run build
```

Для повторної публікації підготуйте окремий каталог поза git-репозиторієм, скопіюйте `dist/restaurant-frontend-app/browser/*` у `.vercel/output/static/`, а у `.vercel/output/config.json` збережіть:

```json
{"version":3,"routes":[{"handle":"filesystem"},{"src":"/.*","dest":"/index.html"}]}
```

У цьому каталозі виконайте `vercel link --project green-and-tasty-level3 --scope <ваша-команда>`, перевірте `vercel project inspect --non-interactive`, потім `vercel deploy --prebuilt`. Для production-оновлення використовуйте погоджений release-процес і `--prod`; звичайні наступні deploy створюють preview. Не копіюйте `.env` або файли автентифікації в output/static.

Перевірки від 02.11.2026:

- **13/13** цільових тестів пошуку: форма, URL, API, clear, порожні результати, помилки/retry, скасування й teardown; окремо перевірено коректне кодування Unicode та `& + ?`.
- **98/98** пошук + офіційні тести групового pre-order. TypeScript app/spec і production build успішні.
- Повний frontend-набір: **298 passed / 45 failed**. Усі 45 помилок уже були у [попередньому переліку](../homework6/known-test-failures.md); нових немає. Старий несправний creation-only тест Menu замінено змістовними перевірками, тому попередніх помилок стало на одну менше. [Точні назви поточних помилок](known-test-failures.txt).
- **14/14** перевірок публічного Vercel сайту в браузері: реальні дані AWS, trim/Enter, нечутливість до регістру, ціни, категорія, reload, Clear, Back, відсутній/порожній запит, деталі страви, 375px без горизонтального scroll, мережевий збій і retry. Тільки перевірка збою навмисно перериває HTTP; решта використовує справжній API.

[Машинний протокол](verification.json) · [Desktop](screenshots/search-desktop.png) · [375px](screenshots/search-mobile.png).

[Фінальне незалежне рев’ю](code-review.md): блокувальних зауважень немає; два дрібні зауваження збережені для наступного доповнення.
