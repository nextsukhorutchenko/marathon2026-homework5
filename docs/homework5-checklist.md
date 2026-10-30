# ДЗ №5: звірка з оригінальним вкладенням, 28.10.2026

Користувач надав `D:/Users/King/Downloads/Telegram Desktop/homework-backend.md`. Його вміст побайтно ідентичний `homework-backend.md` у корені проєкту та файлу, використаному під час реалізації. SHA-256 усіх копій: `8f3c78a165b6e073aabbb203b72c1e110a8e095e74b718ba412d73769dc8915a`.

**Пропущених функціональних вимог із цього файлу не знайдено.** Усі три рівні реалізовані та підключені до Angular. Додаткових змін у бізнес-логіці за цим вкладенням не потрібно.

## Перелік вимог

| Вимога оригінального ДЗ | Реалізація | Доказ |
| --- | --- | --- |
| GET `/guest/groups/{groupReservationId}/sessions` | Маршрут у `RouteRegistry`, `OwnerGroupOrderHandler`, `GetSessionsAsync` | Серверний сценарій `Level 1 GET participant snapshot` |
| `participants`: id, type, name, items, total, isSubmitted | `BuildSnapshotAsync` формує учасників і страви з серверного каталогу | Повна відповідь GET та unit-тест `Snapshot_UsesServerPricesAndIdentifiesOwner` |
| Кількість × ціна для кожного та загальна сума | Підрахунок у backend; Angular показує відповідь сервера | У серверному тесті 180 + 220 + 360 + 180 = 940 |
| GET `/reservations/{reservationId}/guest-session-id` | `GetOwnerSessionAsync` повертає справжню owner-сесію | `Level 2 GET Owner session`, `Lookup_SelectsOnlyOwnersOwnSessionWithoutWriting` |
| POST `/group-reservations/{groupReservationId}/submit`, request `reservationId` | `SubmitGroupOrderRequest`, `SubmitAsync` | `Level 2 POST consolidated order` |
| Брати лише submitted guest sessions | Відбір `IsSubmitted` перед агрегацією | Чернетковий суп виключений із відповіді та збереженого Order |
| Об’єднати однакові страви за `dishId` та додати в Order | Сума кількостей за `DishId`; збереження особистого внеску Owner | Особистий салат × 1 + submitted guest × 2 = салат × 3 |
| Order → `SubmittedByCustomer`; Group → `Submitted` | Обидві зміни через `GroupOrderTransactionRepository.SubmitAsync` | Реальне читання DynamoDB: Order 3, Group 1; транзакційні unit-тести |
| Обидва endpoint-и рівня 2 лише для Owner | Cognito та перевірка `Reservation.CustomerId` | Без входу 401, сторонньому користувачу 403; перевірено обидва маршрути |
| Відсутні reservation або group → 404 | Відповідні винятки й обробка в handler | `GET missing reservation`, `GET reservation without group`, `POST missing group`, `POST group with missing reservation` |
| Немає submitted sessions → 400 | Валідація перед записом | `POST no submitted sessions` |
| У submitted sessions немає страв → 400 | Валідація перед записом | `POST submitted session without dishes` |
| Order відсутній → 400 | Прямий submit нічого автоматично не створює | `POST submitted dishes without order` |
| DELETE `/guest/groups/{groupReservationId}/sessions/{guestSessionId}` | `DeleteSessionAsync`, транзакційне видалення | `Level 3 DELETE guest returns updated snapshot` |
| DELETE лише для Owner | Перевірка власника групового бронювання | Без входу 401, сторонньому користувачу 403 |
| Guest session з іншої групи → 400 | Перевірка `session.GroupReservationId` до видалення | `DELETE guest from different group` |
| Guest session відсутня → 404 | `GuestSessionNotFoundException` | `DELETE missing guest` |
| Після DELETE оновлений список і сума | Backend повертає новий snapshot; Angular замінює дані відповіддю | 4 → 3 учасники; 940 → 760; запис гостя відсутній у DynamoDB |

GET owner-сесії також повертає `groupReservationId` і `status`, а модель страви містить додаткові наявні поля каталогу. Обов’язкові поля прикладів ДЗ збережені. Для старих груп є окремий явний POST підготовки; GET залишається без записів, а прямий submit без Order повертає потрібний 400.

## Перевірки та їхні дати

**28.10.2026:** відновлено локальні залежності .NET, виконано повний backend-набір: **184 passed, 0 failed, 0 skipped**. Звірено SHA-256 основних файлів реалізації та Angular із попереднім перевіреним станом: збіг. Бізнес-код і розгорнутий backend у цій роботі не змінювалися.

Додатково виконано **5/5 свіжих публічних HTTP-перевірок**: `/locations` повертає 200; усі чотири маршрути ДЗ без токена повертають 401. Це підтверджує доступність API та вимогу авторизації, але не замінює повний сценарій із Owner.

Збірка має наявні попередження NuGet щодо залежностей та попередження аналізаторів тестів; вони збережені в журналі. Оновлення залежностей не входило до звірки ДЗ.

**27.10.2026:** повний запуск на AWS — **49/49 HTTP-перевірок і 13/13 перевірок даних**, Lambda `api-handler:dev`, версія 5, реальні Cognito та DynamoDB. Після завершення перевірено відсутність 16 тестових записів і 2 користувачів. [Повні запити й відповіді](homework5/evidence-2026-10-27/EXAMPLES.md).

**Повторний повний серверний запуск 28.10 завершено успішно:** **49/49 HTTP-перевірок і 13/13 додаткових перевірок**, Lambda `api-handler:dev`, версія 5. Поновлення root-входу явно дозволив користувач; STS підтвердив акаунт `009160045555`. Тимчасові 16 записів і 2 користувачі Cognito прибрані, їхню відсутність перевірено. [Нові реальні запити й відповіді](homework5/evidence-2026-10-28/EXAMPLES.md), [протокол](homework5/evidence-2026-10-28/verification.json).

Початкова спроба цього дня зупинилась до створення даних через завершену AWS-сесію та перевірку дозволів root-входу. Після явного підтвердження користувача й поновлення входу цей блок усунуто. Нових постійних прав або ключів не створювали. У `review-verification.json` початкову перешкоду збережено як історію, а поточний authenticated live run позначено завершеним.

Докази поточної звірки: [каталог результатів від 28.10](homework5/evidence-2026-10-28/), [Підсумок backend-тестів](homework5/evidence-2026-10-28/backend-test-summary.json), [підсумковий протокол](homework5/evidence-2026-10-28/verification.json).

## Що завершено в цій звірці

- Збережено наданий користувачем оригінал і підтверджено його ідентичність наявному ДЗ.
- Усунуто попередню невизначеність у походженні детальних вимог в архіві лекції.
- Зіставлено всі поля, дії та умови помилок із реалізацією й реальними відповідями.
- Повторно перевірено всі backend-тести; сценарії перевірки AWS тепер приймають дату запуску, щоб зберігати нові докази окремо від старих.

Функціональність ДЗ №5 готова. Публічна публікація фронтенду та Git-посилання для фінальної здачі є окремими раніше зафіксованими пунктами; їхній стан у цій локальній звірці не змінювали.

## Публікація 30.10.2026

Повторний backend unit-набір: **184 passed, 0 failed**. Код endpoint-ів і Angular не змінювався. AWS Support відмовив у доступі до CloudFront; повторна спроба створення 30.10 знову повернула 403. Код та приклади зібрані для здачі через GitHub.
