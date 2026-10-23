using CounterpartyRegistry.Application.Abstractions;
using CounterpartyRegistry.Application.Services;
using CounterpartyRegistry.Infrastructure;
using CounterpartyRegistry.Api.ErrorHandling;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Задайте ConnectionStrings__DefaultConnection. Для локального запуска используйте scripts/Use-LocalDatabase.ps1.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IFounderService, FounderService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Некорректные данные запроса",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["code"] = "validation_error";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        var result = new BadRequestObjectResult(problem);
        result.ContentTypes.Add("application/problem+json");
        return result;
    });

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions.TryAdd("code", context.ProblemDetails.Status switch
    {
        404 => "not_found",
        405 => "method_not_allowed",
        415 => "unsupported_media_type",
        _ => "http_error"
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "CounterpartyRegistry API",
    Version = "v1",
    Description = "Клиенты (ЮЛ / ИП), общий справочник учредителей и связи между ними."
}));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

public partial class Program { }
