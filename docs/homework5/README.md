# ДЗ №5 — групове замовлення для Owner

Вимоги всіх трьох рівнів містяться в [оригінальному файлі](../../homework-backend.md).

## Код

Усі наведені шляхи відносні до кореня репозиторію.

| Частина | Файл / каталог |
| --- | --- |
| Реєстрація маршрутів | `backend/restaurant-backend-app/dnapp/lambdas/api-handler/Routing/RouteRegistry.cs` |
| HTTP handler та коди помилок | `backend/restaurant-backend-app/dnapp/lambdas/api-handler/Handlers/GroupReservations/OwnerGroupOrderHandler.cs` |
| Перевірка Owner, підрахунки, submit і delete | `backend/restaurant-backend-app/dnapp/lambdas/api-handler/Services/GroupReservations/OwnerGroupOrderService.cs` |
| Атомарні зміни DynamoDB | `backend/restaurant-backend-app/dnapp/lambdas/api-handler/Repositories/GroupReservations/GroupOrderTransactionRepository.cs` |
| Backend-тести | `backend/restaurant-backend-app/Tests/Services/` |
| Angular HTTP service | `frontend/restaurant-frontend-app/src/app/services/group-orders/` |
| Angular Owner UI | `frontend/restaurant-frontend-app/src/app/pages/group-pre-order-page/` |
| API Gateway та ресурси | `backend/restaurant-backend-app/deployment_resources.json` |

POST submit приймає `{ "reservationId": "..." }`. Backend перевіряє власника, бере лише підтверджені гостьові кошики та об'єднує страви за `dishId`. Зміни Order і групи записуються транзакційно. Після submit група заблокована для змін. Особистий внесок Owner зберігається окремо, тому оновлення snapshot не подвоює загальну суму.

GET Owner-сесії не створює даних. Для підготовки старих бронювань є окремий `POST /reservations/{reservationId}/group-pre-order`; прямий submit без Order повертає 400 відповідно до ДЗ.

## Перевірка

Із кореня репозиторію (.NET 10 SDK):

```sh
dotnet test backend/restaurant-backend-app/Tests/APIHandler.Tests.csproj
```

[Повні HTTP-приклади](evidence-2026-10-28/EXAMPLES.md) показують усі чотири endpoint-и, позитивні сценарії та помилки. [verification.json](evidence-2026-10-28/verification.json) містить результати 49 HTTP-перевірок і 13 додаткових перевірок даних на реальних AWS Cognito, API Gateway, Lambda та DynamoDB, виконаних 28.10.2026. Після тесту перевірено видалення 16 тимчасових записів та 2 користувачів.

Запити в [requests.http](evidence-2026-10-28/requests.http) містять історичні ID вже очищених даних і placeholder токена; для нового запуску потрібні власні бронювання й актуальний Cognito ID token.

## Angular локально

Перед запуском вкажіть наш API у `frontend/restaurant-frontend-app/src/environments/environment.development.ts`: `backendUrl: 'https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop'`. У вихідному development-конфігу збережена адреса навчального шаблону; production-збірка бере адресу з `BACKEND_URL`.

```sh
cd frontend/restaurant-frontend-app
npm ci
npm start
```

Для production-збірки в PowerShell:

```powershell
$env:BACKEND_URL = 'https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop'
npm run build
```

Frontend production bundle завантажений у приватний S3. Станом на 30.10.2026 CloudFront заблокований AWS; публічного frontend URL немає. Повторна спроба створення повернула 403 AccessDenied, Request ID `6a9bd411-c107-4e99-9cd9-5f42aa119830`. Інфраструктура й інструкція розгортання — у [головному README](../../README.md).

Повторний backend unit-набір 30.10: **184 passed, 0 failed** — [протокол](evidence-2026-10-30/backend-test-summary.json). У повному frontend-наборі 25.10 залишалися попередні помилки; цей документ не заявляє про проходження всього frontend-набору.
