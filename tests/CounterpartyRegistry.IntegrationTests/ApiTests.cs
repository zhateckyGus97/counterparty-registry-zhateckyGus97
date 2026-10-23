using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CounterpartyRegistry.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CounterpartyRegistry.IntegrationTests;

public sealed class ApiTests : IClassFixture<SqlServerFixture>, IAsyncLifetime
{
    private readonly SqlServerFixture _database;
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ApiTests(SqlServerFixture database)
    {
        _database = database;
        _factory = new ApiFactory(database.ConnectionString);
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        // Check isolation before clearing data or making HTTP requests.
        Assert.Equal(_database.ConnectionString, context.Database.GetConnectionString());
        await context.ClientFounders.ExecuteDeleteAsync();
        await context.Clients.ExecuteDeleteAsync();
        await context.Founders.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task ClientCrud_ReturnsLocation_UpdatesListsAndDeletes()
    {
        using var created = await _client.PostAsJsonAsync("/api/clients", new
        {
            inn = " 7701234560 ", name = " Альфа ", type = "LegalEntity"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        using var fetched = await _client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var original = await fetched.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetGuid();
        Assert.Equal("Альфа", original.GetProperty("name").GetString());
        Assert.Equal("LegalEntity", original.GetProperty("type").GetString());
        Assert.Equal(TimeSpan.Zero, original.GetProperty("createdAt").GetDateTimeOffset().Offset);

        using var updated = await _client.PutAsJsonAsync($"/api/clients/{id}", new
        {
            inn = "7701234560", name = "Бета", type = "LegalEntity"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await CreateClientAsync("123456789047", "ИП", "IndividualEntrepreneur");

        var page = await _client.GetFromJsonAsync<JsonElement>("/api/clients?type=LegalEntity&page=1&pageSize=1");
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal("Бета", page.GetProperty("items")[0].GetProperty("name").GetString());

        using var deleted = await _client.DeleteAsync($"/api/clients/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal("", await deleted.Content.ReadAsStringAsync());
        using var missing = await _client.GetAsync($"/api/clients/{id}");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task SharedFounderCrud_UsesOneRecord_AndRequiresExplicitUnlinking()
    {
        var first = await CreateClientAsync();
        var second = await CreateClientAsync("1000000010", "Бета");
        using var created = await _client.PostAsJsonAsync("/api/founders", new
        {
            inn = "123456789047", fullName = "Иванов Иван"
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        var founder = await created.Content.ReadFromJsonAsync<JsonElement>();
        var founderId = founder.GetProperty("id").GetGuid();
        using var firstLink = await _client.PostAsync($"/api/clients/{first}/founders/{founderId}", null);
        using var secondLink = await _client.PostAsync($"/api/clients/{second}/founders/{founderId}", null);
        Assert.Equal(HttpStatusCode.OK, firstLink.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondLink.StatusCode);

        using var updated = await _client.PutAsJsonAsync($"/api/founders/{founderId}", new
        {
            inn = "123456789047", fullName = "Петров Иван"
        });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        foreach (var clientId in new[] { first, second })
        {
            var client = await _client.GetFromJsonAsync<JsonElement>($"/api/clients/{clientId}");
            Assert.Equal("Петров Иван", client.GetProperty("founders")[0].GetProperty("founder").GetProperty("fullName").GetString());
        }
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/founders");
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());

        using var duplicate = await _client.PostAsync($"/api/clients/{first}/founders/{founderId}", null);
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "client.founder_already_linked");
        using var linkedDelete = await _client.DeleteAsync($"/api/founders/{founderId}");
        await AssertProblemAsync(linkedDelete, HttpStatusCode.Conflict, "founder.has_clients");

        using var deletedClient = await _client.DeleteAsync($"/api/clients/{first}");
        Assert.Equal(HttpStatusCode.NoContent, deletedClient.StatusCode);
        using var retainedFounder = await _client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, retainedFounder.StatusCode);
        using var removedLink = await _client.DeleteAsync($"/api/clients/{second}/founders/{founderId}");
        Assert.Equal(HttpStatusCode.OK, removedLink.StatusCode);
        using var deletedFounder = await _client.DeleteAsync($"/api/founders/{founderId}");
        Assert.Equal(HttpStatusCode.NoContent, deletedFounder.StatusCode);
        using var missing = await _client.GetAsync($"/api/founders/{founderId}");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task Entrepreneur_CannotHaveFounders()
    {
        var clientId = await CreateClientAsync("123456789047", "ИП", "IndividualEntrepreneur");
        var founderId = await CreateFounderAsync();
        using var response = await _client.PostAsync($"/api/clients/{clientId}/founders/{founderId}", null);
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "client.founders_not_allowed");
    }

    [Fact]
    public async Task ChangingType_RequiresRemovingFounderLinks()
    {
        var clientId = await CreateClientAsync();
        var founderId = await CreateFounderAsync();
        using var link = await _client.PostAsync($"/api/clients/{clientId}/founders/{founderId}", null);
        Assert.Equal(HttpStatusCode.OK, link.StatusCode);
        var request = new { inn = "123456789047", name = "ИП", type = "IndividualEntrepreneur" };
        using var rejected = await _client.PutAsJsonAsync($"/api/clients/{clientId}", request);
        await AssertProblemAsync(rejected, HttpStatusCode.Conflict, "client.has_founders");
        using var unlink = await _client.DeleteAsync($"/api/clients/{clientId}/founders/{founderId}");
        Assert.Equal(HttpStatusCode.OK, unlink.StatusCode);
        using var allowed = await _client.PutAsJsonAsync($"/api/clients/{clientId}", request);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task DuplicateInns_ReturnConflict()
    {
        await CreateClientAsync();
        using var client = await _client.PostAsJsonAsync("/api/clients", new
        {
            inn = " 7701234560 ", name = "Бета", type = "LegalEntity"
        });
        await AssertProblemAsync(client, HttpStatusCode.Conflict, "client.inn_conflict");
        await CreateFounderAsync();
        using var founder = await _client.PostAsJsonAsync("/api/founders", new
        {
            inn = "123456789047", fullName = "Другое имя"
        });
        await AssertProblemAsync(founder, HttpStatusCode.Conflict, "founder.inn_conflict");
    }

    [Theory]
    [InlineData("7701234561", "Альфа", "LegalEntity")]
    [InlineData("123456789047", "Альфа", "LegalEntity")]
    [InlineData("0000000000", "Альфа", "LegalEntity")]
    [InlineData(null, "Альфа", "LegalEntity")]
    [InlineData("7701234560", "   ", "LegalEntity")]
    [InlineData("7701234560", null, "LegalEntity")]
    [InlineData("7701234560", "Альфа", null)]
    [InlineData("7701234560", "Альфа", "Unknown")]
    public async Task InvalidClientInput_ReturnsValidationProblem(string? inn, string? name, string? type)
    {
        using var response = await _client.PostAsJsonAsync("/api/clients", new { inn, name, type });
        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateObject());
        await using var check = _database.CreateContext();
        Assert.Equal(0, await check.Clients.CountAsync());
    }

    [Theory]
    [InlineData("7701234560", "Иванов Иван")]
    [InlineData("123456789048", "Иванов Иван")]
    [InlineData("123456789047", "   ")]
    [InlineData(null, "Иванов Иван")]
    public async Task InvalidFounderInput_ReturnsValidationProblem(string? inn, string? fullName)
    {
        using var response = await _client.PostAsJsonAsync("/api/founders", new { inn, fullName });
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Theory]
    [InlineData(true, "Inn")]
    [InlineData(false, "Name")]
    public async Task InvalidClientUpdate_ReturnsFieldError_WithoutChangingSavedData(bool invalidInn, string field)
    {
        var id = await CreateClientAsync();
        using var response = await _client.PutAsJsonAsync($"/api/clients/{id}", new
        {
            inn = invalidInn ? "7701234561" : "7701234560",
            name = invalidInn ? "Новое имя" : new string('А', 201),
            type = "LegalEntity"
        });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty(field).EnumerateArray());
        await using var check = _database.CreateContext();
        var client = await check.Clients.SingleAsync();
        Assert.Equal("7701234560", client.Inn);
        Assert.Equal("Альфа", client.Name);
        Assert.Equal(client.CreatedAt, client.UpdatedAt);
    }

    [Theory]
    [InlineData(true, "Inn")]
    [InlineData(false, "FullName")]
    public async Task InvalidFounderUpdate_ReturnsFieldError_WithoutChangingSavedData(bool invalidInn, string field)
    {
        var id = await CreateFounderAsync();
        using var response = await _client.PutAsJsonAsync($"/api/founders/{id}", new
        {
            inn = invalidInn ? "123456789048" : "123456789047",
            fullName = invalidInn ? "Новое ФИО" : new string('А', 201)
        });

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
        Assert.NotEmpty(problem.GetProperty("errors").GetProperty(field).EnumerateArray());
        await using var check = _database.CreateContext();
        var founder = await check.Founders.SingleAsync();
        Assert.Equal("123456789047", founder.Inn);
        Assert.Equal("Иванов Иван", founder.FullName);
        Assert.Equal(founder.CreatedAt, founder.UpdatedAt);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"inn\":\"7701234560\",\"name\":\"Alpha\",\"type\":1}")]
    public async Task MalformedOrIncompleteBody_Returns400(string json)
    {
        using var response = await _client.PostAsync("/api/clients", new StringContent(json, Encoding.UTF8, "application/json"));
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?pageSize=-1")]
    [InlineData("?page=2147483647&pageSize=100")]
    [InlineData("?type=Unknown")]
    [InlineData("?type=99")]
    public async Task InvalidQuery_Returns400(string query)
    {
        using var response = await _client.GetAsync("/api/clients" + query);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task NameLimits_AreAppliedAfterTrimming()
    {
        using var allowed = await _client.PostAsJsonAsync("/api/clients", new
        {
            inn = "7701234560", name = " " + new string('А', 200) + " ", type = "LegalEntity"
        });
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        using var rejected = await _client.PostAsJsonAsync("/api/founders", new
        {
            inn = "123456789047", fullName = new string('А', 201)
        });
        await AssertProblemAsync(rejected, HttpStatusCode.BadRequest, "validation_error");
    }

    [Theory]
    [InlineData("/api/clients/00000000-0000-0000-0000-000000000000")]
    [InlineData("/api/founders/00000000-0000-0000-0000-000000000000")]
    public async Task EmptyId_Returns400(string path)
    {
        using var response = await _client.GetAsync(path);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_error");
    }

    [Fact]
    public async Task MissingObjectAndUnknownRoute_Return404ProblemDetails()
    {
        using var missing = await _client.GetAsync($"/api/clients/{Guid.NewGuid()}");
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "not_found");
        using var route = await _client.GetAsync("/api/clients/not-a-guid");
        await AssertProblemAsync(route, HttpStatusCode.NotFound, "not_found");
    }

    [Fact]
    public async Task UnsupportedMethod_Returns405ProblemDetails()
    {
        using var response = await _client.PatchAsync($"/api/clients/{Guid.NewGuid()}", null);
        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed");
    }

    [Fact]
    public async Task UnsupportedBodyContentType_Returns415ProblemDetails()
    {
        using var response = await _client.PostAsync("/api/clients", new StringContent("plain text"));
        await AssertProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "unsupported_media_type");
    }

    [Theory]
    [InlineData("unexpected")]
    [InlineData("argument")]
    public async Task UnexpectedErrors_Return500_WithoutLeakingImplementationDetails(string kind)
    {
        using var response = await _client.GetAsync("/__tests/errors/" + kind);
        await AssertProblemAsync(response, HttpStatusCode.InternalServerError, "internal_error");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Internal implementation details", body);
        Assert.DoesNotContain("stackTrace", body);
    }

    [Fact]
    public async Task Swagger_DescribesEndpoints_AndUiIsAvailable()
    {
        var document = await _client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/clients", out _));
        Assert.True(paths.TryGetProperty("/api/founders", out _));
        Assert.True(paths.TryGetProperty("/api/clients/{id}/founders/{founderId}", out _));
        Assert.True(paths.GetProperty("/api/clients").GetProperty("post")
            .GetProperty("responses").TryGetProperty("409", out _));
        Assert.Equal("string", document.GetProperty("components").GetProperty("schemas")
            .GetProperty("ClientType").GetProperty("type").GetString());
        using var ui = await _client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    private async Task<Guid> CreateClientAsync(string inn = "7701234560", string name = "Альфа", string type = "LegalEntity")
    {
        using var response = await _client.PostAsJsonAsync("/api/clients", new { inn, name, type });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateFounderAsync()
    {
        using var response = await _client.PostAsJsonAsync("/api/founders", new
        {
            inn = "123456789047", fullName = "Иванов Иван"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.Equal(code, problem.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        return problem;
    }
}
