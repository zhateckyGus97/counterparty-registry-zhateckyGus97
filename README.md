# CounterpartyRegistry

API реестра контрагентов: юридические лица, индивидуальные предприниматели и учредители.

## Технологии

- .NET 8 / ASP.NET Core
- SQL Server и Entity Framework Core
- OpenAPI / Swagger

## Текущее состояние

Реализован CRUD API клиентов и учредителей: Domain, Application, EF Core / SQL Server, миграция, связь «многие ко многим», пагинация, валидация ИНН, обработка конфликтов и ProblemDetails. Для ручной проверки подключён Swagger.

## Локальный запуск

Требуются SDK с поддержкой .NET 8, ASP.NET Core Runtime 8 и Docker Desktop с Linux containers. Из корня репозитория в PowerShell:

```powershell
. ./scripts/Use-LocalDatabase.ps1
docker compose up -d --wait sqlserver
dotnet tool restore
dotnet ef database update --project src/CounterpartyRegistry.Infrastructure --startup-project src/CounterpartyRegistry.Api
dotnet run --project src/CounterpartyRegistry.Api --no-launch-profile --urls http://localhost:5080
```

Откройте [Swagger](http://localhost:5080/swagger). Пароль создаётся локальным скриптом и хранится в игнорируемом .env. Миграции применяются отдельной командой, не при старте API.

Принятые решения, ограничения и требования к следующим слоям описаны в [доменной модели](docs/domain-model.md).

Пошаговый разбор сервисов и разделения ответственности находится в [описании Application](docs/application-layer.md).

Маршруты, примеры запросов и формат ошибок описаны в [HTTP API](docs/api.md).

## Проверка

```powershell
dotnet build CounterpartyRegistry.sln
dotnet test tests/CounterpartyRegistry.UnitTests
```

Запуск SQL Server, миграций и приложения, а также интеграционных тестов описан в [локальном окружении](docs/local-database.md).

## CI

GitHub Actions собирает solution в Release и запускает unit- и интеграционные тесты с SQL Server для PR в master и после push / merge в master. TRX-отчёты сохраняются в артефактах запуска. Подробнее: [автоматические проверки](docs/ci.md).
