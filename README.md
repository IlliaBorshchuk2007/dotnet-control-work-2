# Контрольна робота 2 — Hotel Rooms API

Фільтрація даних та інтеграційне тестування Web API на ASP.NET Core (.NET 10).
Продовження [КР №1](https://github.com/IlliaBorshchuk2007/dotnet-control-work-1): та сама сутність `Room`, унікальне поле `Number`.

## Запуск тестів

Потрібні .NET 10 SDK та запущений Docker Desktop у режимі Linux containers (або Docker Engine на Linux).

```sh
dotnet test
```

Testcontainers автоматично запускає PostgreSQL `postgres:16-alpine` на випадковому порту. При першому запуску потрібен інтернет для пакетів та образів. Схема створюється міграціями під час старту API. Локальна БД та її рядок підключення не використовуються. Після кожного тесту записи видаляються, після завершення класу контейнер звільняється. Кожен клас має окремий контейнер.

```sh
dotnet test --filter FullyQualifiedName~RoomsFilterTests
dotnet test --filter FullyQualifiedName~CreatedAtFrom_Inclusive
```

## Тестові проєкти

- `tests/Tests.Common`: WebApplicationFactory, PostgreSQL Testcontainers, базовий клас, тестова автентифікація. Залежить від Api.
- `tests/Tests.Data`: чотири фабрики кімнат з різними полями та новими ідентифікаторами. Залежить лише від Domain.
- `tests/Api.Tests.Integration`: HTTP-тести CRUD і фільтрації. Залежить від Tests.Common та Tests.Data.

`TestAuthHandler` автентифікує користувача з роллю `admin`; клієнт надсилає `Authorization: TestScheme`, автоматичні перенаправлення вимкнено. Маршрути КР №1 залишаються публічними.

## POST /rooms/filter

Усі параметри nullable. Відсутні та null не впливають на результат. Передані умови поєднуються через логічне **І**.

| Поле | Параметри JSON | Умова |
|---|---|---|
| Number | number | Частковий збіг без урахування регістру |
| Floor | floorFrom, floorTo | Включний діапазон |
| PricePerNight | pricePerNightFrom, pricePerNightTo | Включний діапазон |
| Type | type | Standard, Deluxe, Suite, Family |
| IsAvailable | isAvailable | Точний збіг true / false |
| CreatedAt | createdAtFrom, createdAtTo | Включний діапазон UTC |

```json
{
  "number": "north",
  "floorFrom": 1,
  "floorTo": 3,
  "pricePerNightTo": 500,
  "type": "Deluxe",
  "isAvailable": false,
  "createdAtFrom": "2026-01-01T00:00:00Z"
}
```

Кожна межа застосовується незалежно. `{}` повертає всі записи; відсутність збігів — 200 та `[]`. Дати без часового поясу трактуються як UTC, зміщення нормалізуються до UTC. Невідомий тип повертає 400. `%` та `_` у номері трактуються буквально.

Фільтр виконується у PostgreSQL: `IQueryable`, `AsNoTracking`, умовні `Where` і один `ToListAsync`. Інтерфейс запитів приймає доменний `RoomType?`, API повертає лише DTO.

## Перевірки

- CRUD: успішне створення й оновлення всіх полів, незмінний унікальний номер, конфлікт 409, невідомий id 404, порожній/довгий номер, недопустимі поверх і ціна, читання списку та одного запису. Перевіряються HTTP-відповіді й фактичний стан БД, зокрема його незмінність після помилок.
- Фільтри: кожен із дев'яти параметрів окремо; true і false; регістр; включні числові й часові межі; обидві межі разом; однакові часові межі; UTC; комбінація умов; порожній фільтр; відсутність збігів; буквальний `%`; зворотний діапазон; невідомий тип.
- У кожному успішному тесті фільтра: 200, точний набір ідентифікаторів, усі поля DTO, незмінність БД. Звичайні фільтри обов'язково відбирають непорожню підмножину.
- CreatedAt задається тільки фабрикою; часові межі читаються з БД після збереження, щоб врахувати мікросекундну точність PostgreSQL.
- Arrange / Act / Assert; спільний метод фільтра виконує запит та всі перевірки для кожного сценарію.

GitHub Actions запускає інтеграційні тести на Ubuntu з Docker при push і pull request.

## Основа з КР №1

Контрольна робота з ASP.NET Core у предметній області «Готель». Основна сутність —
`Room`, унікальне поле — номер кімнати.

## Реалізовано

- Clean Architecture: `Domain`, `Application`, `Infrastructure`, `Api`.
- Strongly typed `RoomId`, приватний конструктор, фабричний метод `Room.New`.
- CQRS через MediatR, `Either`/`Option` через LanguageExt.
- FluentValidation та єдиний validation pipeline.
- PostgreSQL + EF Core, окреме налаштування сутності й автоматичні міграції.
- REST API для перегляду, створення та оновлення кімнат.
- Swagger, CORS і правильні HTTP-коди `201`, `400`, `404`, `409`, `500`.

## Запуск

Потрібні .NET 10 SDK і запущений Docker Desktop.

```powershell
docker run --name hotel-postgres -p 5432:5432 -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=hotel_db -d postgres:16-alpine
dotnet run --project src/Api
```

Swagger відкривається за адресою застосунку з додаванням `/swagger`.

## API

- `GET /rooms`
- `GET /rooms/{id}`
- `POST /rooms`
- `PUT /rooms/{id}`

Приклади запитів є у `src/Api/Api.http`.
