using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TaskTracker.Domain.Common;

namespace TaskTracker.Application.Common.Behaviours;

/// <summary>
/// MediatR pipeline behaviour: logs every request with timing.
/// Cross-cutting concern extracted from handlers (SRP, OCP).
/// Order: Logging → Validation → Handler
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        _logger.LogInformation("[MediatR] Handling {RequestName}", name);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            _logger.LogInformation("[MediatR] {RequestName} completed in {Ms}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "[MediatR] {RequestName} failed after {Ms}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

/// <summary>
/// MediatR pipeline behaviour: runs FluentValidation before any handler executes.
/// Returns Result.Failure with collected error messages instead of throwing exceptions.
/// Only runs when TResponse is a Result type (safe to apply globally).
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        // Aggregate all field messages into one string
        var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));

        // Return Result.Failure<T> or Result.Failure depending on response type
        var responseType = typeof(TResponse);
        if (responseType == typeof(Result))
            return (TResponse)(object)Result.Failure(errors);

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var innerType  = responseType.GetGenericArguments()[0];
            var failMethod = typeof(Result)
                .GetMethod(nameof(Result.Failure), 1, new[] { typeof(string) })!
                .MakeGenericMethod(innerType);
            return (TResponse)failMethod.Invoke(null, new object[] { errors })!;
        }

        return await next();
    }
}
