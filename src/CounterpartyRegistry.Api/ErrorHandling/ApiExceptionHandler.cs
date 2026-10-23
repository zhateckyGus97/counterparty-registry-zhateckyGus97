using CounterpartyRegistry.Application.Exceptions;
using CounterpartyRegistry.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CounterpartyRegistry.Api.ErrorHandling;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var status = StatusCodes.Status500InternalServerError;
        var title = "Внутренняя ошибка сервера";
        var detail = "Не удалось выполнить запрос.";
        var code = "internal_error";

        switch (exception)
        {
            case DomainValidationException:
                status = StatusCodes.Status400BadRequest;
                title = "Некорректные данные запроса";
                detail = exception.Message;
                code = "validation_error";
                break;
            case EntityNotFoundException:
                status = StatusCodes.Status404NotFound;
                title = "Объект не найден";
                detail = exception.Message;
                code = "not_found";
                break;
            case ConflictException conflict:
                status = StatusCodes.Status409Conflict;
                title = "Конфликт данных";
                detail = conflict.Message;
                code = conflict.Code;
                break;
            case DomainRuleException domainRule:
                status = StatusCodes.Status409Conflict;
                title = "Операция нарушает бизнес-правило";
                detail = domainRule.Message;
                code = domainRule.Code;
                break;
        }

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Request {TraceId} failed", httpContext.TraceIdentifier);
        else
            _logger.LogWarning("Request {TraceId} rejected: {Code}", httpContext.TraceIdentifier, code);

        ProblemDetails problem = exception is DomainValidationException validation
            ? new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                [validation.FieldName] = new[] { validation.Message }
            })
            : new ProblemDetails();
        problem.Type = "about:blank";
        problem.Status = status;
        problem.Title = title;
        problem.Detail = detail;
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }
}
