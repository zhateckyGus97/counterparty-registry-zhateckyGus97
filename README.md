# CounterpartyRegistry

API реестра контрагентов: юридические лица, индивидуальные предприниматели и учредители.

## Технологии

- .NET 8 / ASP.NET Core
- SQL Server и Entity Framework Core
- OpenAPI / Swagger

## Текущее состояние

Реализован Domain: клиент, общий справочник учредителей, связь «многие ко многим», проверки ИНН и правила изменения сущностей. Сценарии Application, SQL Server / EF Core и HTTP API будут реализованы следующими этапами.

Принятые решения, ограничения и требования к следующим слоям описаны в [доменной модели](docs/domain-model.md).

## Проверка

```powershell
dotnet build CounterpartyRegistry.sln
dotnet test CounterpartyRegistry.sln
```
