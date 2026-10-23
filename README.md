# CounterpartyRegistry

REST API реестра контрагентов: юридические лица, индивидуальные предприниматели и учредители. Реализовано на ASP.NET Core 8 с Entity Framework Core и SQL Server.

## Возможности

- Создание, просмотр, обновление и удаление клиентов и учредителей.
- Связь «многие ко многим»: один учредитель может участвовать в нескольких юридических лицах. У ИП учредителей быть не может.
- Пагинация списков и фильтрация клиентов по типу.
- Проверка формата и контрольных цифр ИНН, обязательности и длины полей. Уникальность ИНН обеспечивается проверками сервиса и индексами БД.
- Автоматическое заполнение дат создания и изменения в UTC.
- Единый формат ошибок ProblemDetails и обработка конфликтов при сохранении.
- Swagger UI для ручной проверки API.

## Архитектура

| Проект | Ответственность |
| --- | --- |
| `CounterpartyRegistry.Domain` | Сущности Client, Founder, ClientFounder, правила изменения и валидация полей. Не зависит от EF Core и ASP.NET Core. |
| `CounterpartyRegistry.Application` | Сценарии работы с клиентами и учредителями, DTO, интерфейсы сервисов, репозиториев и Unit of Work. |
| `CounterpartyRegistry.Infrastructure` | DbContext, конфигурации EF Core, миграции, репозитории и преобразование известных ошибок SQL Server. |
| `CounterpartyRegistry.Api` | Контроллеры, входные HTTP-модели, обработка ошибок, регистрация зависимостей и Swagger. |

Связь клиента с учредителем представлена отдельной сущностью ClientFounder. Добавление и удаление связи выполняется через Client. Удаление клиента удаляет его связи, сохраняя общих учредителей. Удалить учредителя можно после удаления всех его связей.

Разделение слоёв позволяет добавлять сценарии и точки входа, сохраняя общие доменные правила. Изменения в БД сохраняются одним вызовом Unit of Work в рамках сценария.

## Локальный запуск

Требуются .NET SDK 8 или новее, ASP.NET Core Runtime 8 и Docker Desktop в режиме Linux containers. Инструкция рассчитана на PowerShell.

Из корня репозитория при запущенном Docker Desktop:

```powershell
dotnet restore CounterpartyRegistry.sln
. ./scripts/Use-LocalDatabase.ps1
docker compose up -d --wait sqlserver
dotnet tool restore
dotnet ef database update --project src/CounterpartyRegistry.Infrastructure --startup-project src/CounterpartyRegistry.Api
dotnet run --project src/CounterpartyRegistry.Api --no-launch-profile --urls http://localhost:5080
```

Swagger UI: [http://localhost:5080/swagger](http://localhost:5080/swagger). OpenAPI-документ: [swagger.json](http://localhost:5080/swagger/v1/swagger.json).

Скрипт создаёт `.env` со случайным паролем SQL Server, задаёт строку подключения в текущей сессии и сохраняет её в User Secrets проекта API для запуска из Visual Studio. `.env` игнорируется Git; `.env.example` содержит только шаблон настроек. В новом терминале перед запуском API повторите `. ./scripts/Use-LocalDatabase.ps1`.

Docker Compose запускает только SQL Server. API запускается командой `dotnet run`. Порт БД по умолчанию — `14333`; данные сохраняются в Docker volume. Миграции применяются отдельной командой, а не при старте приложения. Версия инструмента `dotnet-ef` закреплена в `dotnet-tools.json`.

Для ручной проверки создайте клиента и учредителя, затем свяжите их через `POST /api/clients/{id}/founders/{founderId}`. Запрос `GET /api/clients/{id}` вернёт карточку клиента со связанными учредителями. Примеры тел запросов приведены в [описании API](docs/api.md#пример-проверки-через-swagger).

## Тесты

Unit-тесты проверяют доменные правила, валидацию и сценарии Application. Для них Docker не требуется:

```powershell
dotnet test tests/CounterpartyRegistry.UnitTests --configuration Release
```

Интеграционные тесты проверяют HTTP API через WebApplicationFactory и работу с настоящим SQL Server: миграции, ограничения БД, транзакции и конкурентные изменения. Каждый тестовый класс использует отдельную временную БД, которая удаляется после завершения. Локальная БД приложения не очищается.

Для запуска всех тестов из корня репозитория:

```powershell
. ./scripts/Use-LocalDatabase.ps1
docker compose up -d --wait sqlserver
dotnet test CounterpartyRegistry.sln --configuration Release
```

## CI

Workflow [CI](.github/workflows/ci.yml) собирает solution в Release и запускает unit- и интеграционные тесты с SQL Server для pull request в `master` и после push в `master`, включая merge. Push только в `develop` без открытого PR в `master` не запускает проверки. TRX-отчёты сохраняются в артефактах запуска.

## Принятые допущения и ограничения

- Учредитель считается физическим лицом с 12-значным ИНН. Его данные общие для всех связанных клиентов; изменение ФИО отражается в каждой карточке клиента.
- Проверка ИНН локальная: она не подтверждает существование контрагента в реестре ФНС.
- SQL Server rowversion защищает от изменений между загрузкой сущности и сохранением на сервере. Версия карточки пока не передаётся в HTTP-запросах, поэтому защита от сохранения устаревшей формы через ETag / If-Match не реализована.
- Аутентификация и внешние банковские интеграции не входят в реализованный CRUD API. Swagger включён во всех окружениях для проверки задания; локальное подключение к SQL Server использует `sa`.

## Документация

- [Доменная модель и бизнес-правила](docs/domain-model.md)
- [Сценарии и контракты Application](docs/application-layer.md)
- [Маршруты, примеры запросов и ошибки API](docs/api.md)
- [Локальное окружение, Docker и миграции](docs/local-database.md)
- [Автоматические проверки и защита ветки](docs/ci.md)
