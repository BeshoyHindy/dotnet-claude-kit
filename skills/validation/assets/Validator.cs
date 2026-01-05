// Validation Interfaces and Base Class
// Copy to: src/Application/Common/Validation/
// Requires: None (standalone)

namespace YourNamespace.Application.Common.Validation;

/// <summary>
/// Validation result containing success status and any errors.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private ValidationResult(bool isValid, IReadOnlyList<ValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    public static ValidationResult Success() => new(true, []);

    public static ValidationResult Failure(params ValidationError[] errors) => new(false, errors);

    public static ValidationResult Failure(IEnumerable<ValidationError> errors) => new(false, errors.ToList());
}

/// <summary>
/// Single validation error with property name and message.
/// Used for input/command validation in the Application layer.
/// Note: This is distinct from DomainValidationError in the exception-handling skill,
/// which is used for domain-level validation exceptions.
/// </summary>
public sealed record ValidationError(string PropertyName, string ErrorMessage);

/// <summary>
/// Validator interface for a specific type.
/// </summary>
public interface IValidator<T>
{
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken ct = default);
}

/// <summary>
/// Example validator implementation.
/// </summary>
public abstract class Validator<T> : IValidator<T>
{
    private readonly List<Func<T, CancellationToken, Task<ValidationError?>>> _rules = [];

    protected void RuleFor<TProperty>(
        Func<T, TProperty> selector,
        Func<TProperty, bool> predicate,
        string propertyName,
        string errorMessage)
    {
        _rules.Add((instance, _) =>
        {
            var value = selector(instance);
            return Task.FromResult(predicate(value)
                ? null
                : new ValidationError(propertyName, errorMessage));
        });
    }

    protected void RuleForAsync<TProperty>(
        Func<T, TProperty> selector,
        Func<TProperty, CancellationToken, Task<bool>> predicate,
        string propertyName,
        string errorMessage)
    {
        _rules.Add(async (instance, ct) =>
        {
            var value = selector(instance);
            var isValid = await predicate(value, ct);
            return isValid ? null : new ValidationError(propertyName, errorMessage);
        });
    }

    public async Task<ValidationResult> ValidateAsync(T instance, CancellationToken ct = default)
    {
        var errors = new List<ValidationError>();

        foreach (var rule in _rules)
        {
            var error = await rule(instance, ct);
            if (error is not null)
            {
                errors.Add(error);
            }
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
