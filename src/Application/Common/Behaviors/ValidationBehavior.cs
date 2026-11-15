using FluentValidation;
using FluentValidation.Results;
using GPMH.Application.Common.Exceptions;
using MediatR;

namespace GPMH.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (!validators.Any())
        {
            return await next().ConfigureAwait(false);
        }

        ValidationContext<TRequest> context = new(request);
        ValidationResult[] results = await Task
            .WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false);

        ValidationFailure[] failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length == 0)
        {
            return await next().ConfigureAwait(false);
        }

        Dictionary<string, string[]> errors = failures
            .GroupBy(f => f.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(f => f.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        throw new DomainValidationException(errors);
    }
}

