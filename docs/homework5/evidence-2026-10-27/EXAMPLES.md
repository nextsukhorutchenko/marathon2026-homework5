# ДЗ №5: реальні запити й відповіді AWS, 27.10.2026

Перевірка завершена: 2026-10-27T02:16:38.331628+00:00 (UTC). API: https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop. Lambda `api-handler:dev`, версія 5.

**Успішно: 49/49 HTTP-перевірок та 13/13 перевірок даних і конфігурації.** Усі чотири endpoint-и з `homework-backend.md` викликано на сервері з реальними Cognito й DynamoDB.

Локація, стіл, страви, бронювання та некоректні стани для негативних сценаріїв були тимчасово підготовлені безпосередньо в DynamoDB. Кошики основної групи створені й підтверджені через публічні гостьові API; усі endpoint-и ДЗ перевірені HTTP-запитами. Моки не використовувалися.

**Тестові дані вже видалені.** ID нижче є історичними прикладами цього запуску; повторення цих URL після очищення не відновить сценарій. У запитах токен замінено на `<OWNER_ID_TOKEN>`; фактичні токени й паролі не збережені.

Послідовність сценарію: GET учасників → GET Owner → DELETE одного гостя до підтвердження → POST submit → перевірка DynamoDB і повторного читання. Приклади відповідей нижче наведені повністю, без зміни отриманих полів. DELETE після submit вже повертає 409.

## Підсумки кошиків

| Учасник | Страва | Кількість | Ціна | Сума | Підтверджено |
| --- | --- | --- | --- | --- | --- |
| Owner | Test Salad | 1 | 180 | 180 | Особистий кошик Owner |
| Andrii | Test Salad | 2 | 180 | 360 | Так |
| Olena | Test Soup | 1 | 220 | 220 | Ні |
| Remove Test | Test Salad | 1 | 180 | 180 | Ні |

Початкова сума всіх кошиків — **940**. DELETE прибрав гостя Remove Test: сума стала **760**, залишилося 3 учасники. До ресторану передано **Test Salad × 3** на **540**: особистий внесок Owner × 1 та підтверджений кошик Andrii × 2. Чернетка Olena на 220 залишилась у snapshot, але не потрапила в консолідоване замовлення.

## Рівень 1: учасники, страви й суми

Запит:

```http
GET https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop/guest/groups/hw5-20261027-e615e626-group/sessions
Authorization: Bearer <OWNER_ID_TOKEN>
```

Відповідь: **HTTP 200**. AWS Request ID: `39af5894-ad16-440b-b429-9afa6d9175f2`.

```json
{
  "participants": [
    {
      "id": "owner-hw5-20261027-e615e626-group",
      "type": "Owner",
      "name": "Owner",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-salad",
            "name": "Test Salad",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 180,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 1
        }
      ],
      "total": 180,
      "isSubmitted": false
    },
    {
      "id": "46fda4f1-04c3-4c36-9082-0a592dd1fef9",
      "type": "Guest",
      "name": "Olena",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-soup",
            "name": "Test Soup",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 220,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 1
        }
      ],
      "total": 220,
      "isSubmitted": false
    },
    {
      "id": "8d7807a3-0cb8-4cbe-b2ed-0eba78c70cce",
      "type": "Guest",
      "name": "Andrii",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-salad",
            "name": "Test Salad",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 180,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 2
        }
      ],
      "total": 360,
      "isSubmitted": true
    },
    {
      "id": "d01af9ce-3b7e-4cb3-b256-2fd6e264c977",
      "type": "Guest",
      "name": "Remove Test",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-salad",
            "name": "Test Salad",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 180,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 1
        }
      ],
      "total": 180,
      "isSubmitted": false
    }
  ],
  "total": 940
}
```

## Рівень 2: owner-сесія

Запит:

```http
GET https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop/reservations/hw5-20261027-e615e626-reservation/guest-session-id
Authorization: Bearer <OWNER_ID_TOKEN>
```

Відповідь: **HTTP 200**. AWS Request ID: `45c44469-4b87-4a95-8fcd-6f941909f2c0`.

```json
{
  "guestSessionId": "owner-hw5-20261027-e615e626-group",
  "groupReservationId": "hw5-20261027-e615e626-group",
  "status": "Open"
}
```

## Рівень 3: видалення гостя

Запит:

```http
DELETE https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop/guest/groups/hw5-20261027-e615e626-group/sessions/d01af9ce-3b7e-4cb3-b256-2fd6e264c977
Authorization: Bearer <OWNER_ID_TOKEN>
```

Відповідь: **HTTP 200**. AWS Request ID: `489d1e77-1903-4fa9-9bde-ab6f91961259`.

```json
{
  "participants": [
    {
      "id": "owner-hw5-20261027-e615e626-group",
      "type": "Owner",
      "name": "Owner",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-salad",
            "name": "Test Salad",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 180,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 1
        }
      ],
      "total": 180,
      "isSubmitted": false
    },
    {
      "id": "46fda4f1-04c3-4c36-9082-0a592dd1fef9",
      "type": "Guest",
      "name": "Olena",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-soup",
            "name": "Test Soup",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 220,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 1
        }
      ],
      "total": 220,
      "isSubmitted": false
    },
    {
      "id": "8d7807a3-0cb8-4cbe-b2ed-0eba78c70cce",
      "type": "Guest",
      "name": "Andrii",
      "items": [
        {
          "dish": {
            "id": "hw5-20261027-e615e626-salad",
            "name": "Test Salad",
            "description": "Disposable homework verification dish",
            "dishType": "Appetizer",
            "price": 180,
            "weight": "200 g",
            "calories": "100",
            "proteins": "1",
            "fats": "1",
            "carbohydrates": "1",
            "vitamins": "A",
            "imageUrl": null,
            "state": "Available"
          },
          "quantity": 2
        }
      ],
      "total": 360,
      "isSubmitted": true
    }
  ],
  "total": 760
}
```

## Рівень 2: підтвердження консолідованого pre-order

Запит:

```http
POST https://njnyvufs51.execute-api.us-west-2.amazonaws.com/develop/group-reservations/hw5-20261027-e615e626-group/submit
Authorization: Bearer <OWNER_ID_TOKEN>
Content-Type: application/json

{
  "reservationId": "hw5-20261027-e615e626-reservation"
}
```

Відповідь: **HTTP 200**. AWS Request ID: `7207ca6f-557c-4ea5-98a3-35db351b81dc`.

```json
[
  {
    "dishId": "hw5-20261027-e615e626-salad",
    "quantity": 3
  }
]
```

## Обов’язкові умови й помилки

| Перевірка | Очікувано | Отримано | Результат |
| --- | --- | --- | --- |

| Unauthenticated GET /guest/groups/hw5-20261027-e615e626-group/sessions | 401 | 401 | OK |

| Non-owner forbidden GET /guest/groups/hw5-20261027-e615e626-group/sessions | 403 | 403 | OK |

| Unauthenticated GET /reservations/hw5-20261027-e615e626-reservation/guest-session-id | 401 | 401 | OK |

| Non-owner forbidden GET /reservations/hw5-20261027-e615e626-reservation/guest-session-id | 403 | 403 | OK |

| Unauthenticated POST /group-reservations/hw5-20261027-e615e626-group/submit | 401 | 401 | OK |

| Non-owner forbidden POST /group-reservations/hw5-20261027-e615e626-group/submit | 403 | 403 | OK |

| Unauthenticated DELETE /guest/groups/hw5-20261027-e615e626-group/sessions/hw5-20261027-e615e626-missing-guest | 401 | 401 | OK |

| Non-owner forbidden DELETE /guest/groups/hw5-20261027-e615e626-group/sessions/hw5-20261027-e615e626-missing-guest | 403 | 403 | OK |

| GET missing group | 404 | 404 | OK |

| GET missing reservation | 404 | 404 | OK |

| GET reservation without group | 404 | 404 | OK |

| POST missing group | 404 | 404 | OK |

| POST group with missing reservation | 404 | 404 | OK |

| POST no submitted sessions | 400 | 400 | OK |

| POST submitted session without dishes | 400 | 400 | OK |

| POST submitted dishes without order | 400 | 400 | OK |

| DELETE guest from different group | 400 | 400 | OK |

| DELETE missing guest | 404 | 404 | OK |

| POST mismatched reservation | 400 | 400 | OK |

| POST missing reservationId body | 400 | 400 | OK |

| GET legacy Owner session absent | 404 | 404 | OK |

| DELETE Owner session prohibited | 400 | 400 | OK |

| GET snapshot after submission | 200 | 200 | OK |

| POST repeat submission conflict | 409 | 409 | OK |

| DELETE after submission conflict | 409 | 409 | OK |

| CORS GET /guest/groups/hw5-20261027-e615e626-group/sessions | 200 | 200 | OK |

| CORS GET /reservations/hw5-20261027-e615e626-reservation/guest-session-id | 200 | 200 | OK |

| CORS POST /group-reservations/hw5-20261027-e615e626-group/submit | 200 | 200 | OK |

| CORS DELETE /guest/groups/hw5-20261027-e615e626-group/sessions/hw5-20261027-e615e626-missing-guest | 200 | 200 | OK |

## Приклади реальних помилок

**GET missing group — HTTP 404**

```json
{
  "error": "Group reservation was not found"
}
```

**POST no submitted sessions — HTTP 400**

```json
{
  "message": "There are no submitted sessions.",
  "errorCode": "Unknown"
}
```

**POST submitted session without dishes — HTTP 400**

```json
{
  "message": "Submitted sessions contain no dishes.",
  "errorCode": "Unknown"
}
```

**POST submitted dishes without order — HTTP 400**

```json
{
  "message": "Order not found. Prepare the group pre-order first.",
  "errorCode": "Unknown"
}
```

**DELETE guest from different group — HTTP 400**

```json
{
  "message": "Guest session does not belong to this group reservation.",
  "errorCode": "Unknown"
}
```

**DELETE missing guest — HTTP 404**

```json
{
  "error": "Guest session not found."
}
```

**POST repeat submission conflict — HTTP 409**

```json
{
  "error": "This group reservation is no longer open for changes."
}
```

## Збереження й очищення даних

Прямим читанням DynamoDB підтверджено Order `SubmittedByCustomer` (3), Group `Submitted` (1), Salad × 3, відсутність Soup у переданому замовленні та окреме збереження внеску Owner × 1. Повторний GET не подвоїв суму.

Після тесту перевірено відсутність **16 тимчасових записів** і **2 користувачів Cognito**. Помилки очищення: 0.

Початкова спроба підготовки відхилилася на реєстрації через надто довгий згенерований тестовий пароль. Тестові дані виправлено відповідно до чинного обмеження 8–16 символів; це не зміна backend. Початковий результат і підтвердження очищення збережені в `setup-attempt-1.json`.

## Файли й повторна перевірка

- [Усі запити, відповіді, Request ID, перевірки та очищення](verification.json).
- [Чотири запити у форматі HTTP](requests.http).
- [Повні окремі HTTP-приклади](http/).
- [Сценарій повторного запуску](../../tools/verify-homework5-live.py). Він створює нові ізольовані дані й прибирає їх у блоці завершення; потребує наявного доступу до цього AWS.
- [Контрольні суми](checksums.sha256).

Перевірялися серверні API. Публікацію фронтенду, браузерний інтерфейс і повні локальні набори unit-тестів у цьому запуску не перевіряли.
