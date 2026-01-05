# Validation with FluentValidation

FluentValidation provides a fluent API for defining validation rules.

## Installation

```bash
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

## Validator Definition

```csharp
public sealed class CreateOrderValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer is required");

        RuleFor(x => x.OrderNumber)
            .NotEmpty()
            .MaximumLength(50)
            .WithMessage("Order number must be 1-50 characters");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("At least one item is required");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty();
                item.RuleFor(i => i.Quantity).GreaterThan(0);
            });
    }
}
```

## Async Validation

```csharp
public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator(IDbContext db)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MustAsync(async (email, ct) =>
                !await db.Customers.AnyAsync(c => c.Email == email, ct))
            .WithMessage("Email already registered");
    }
}
```

## Registration

```csharp
services.AddValidatorsFromAssembly(typeof(CreateOrderValidator).Assembly);
```

## Pipeline Integration

```csharp
public sealed class ValidationDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await inner.HandleAsync(command, ct);

        var context = new ValidationContext<TCommand>(command);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            var message = string.Join("; ",
                failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}"));
            return Error.Validation(message);
        }

        return await inner.HandleAsync(command, ct);
    }
}
```

## Custom Rules

```csharp
public static class CustomRules
{
    public static IRuleBuilderOptions<T, string> PhoneNumber<T>(
        this IRuleBuilder<T, string> builder)
    {
        return builder
            .Matches(@"^\+[1-9]\d{7,14}$")
            .WithMessage("Invalid phone number format");
    }

    public static IRuleBuilderOptions<T, decimal> Money<T>(
        this IRuleBuilder<T, decimal> builder)
    {
        return builder
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 4, true);
    }
}

// Usage
RuleFor(x => x.Phone).PhoneNumber();
RuleFor(x => x.Amount).Money();
```

## Conditional Validation

```csharp
RuleFor(x => x.ShippingAddress)
    .NotNull()
    .When(x => x.RequiresShipping)
    .WithMessage("Shipping address required for physical orders");

// For validators needing current time, inject TimeProvider
public sealed class SubscriptionValidator : AbstractValidator<CreateSubscriptionCommand>
{
    public SubscriptionValidator(TimeProvider timeProvider)
    {
        When(x => x.OrderType == OrderType.Subscription, () =>
        {
            RuleFor(x => x.BillingCycle).NotNull();
            RuleFor(x => x.StartDate)
                .GreaterThan(DateOnly.FromDateTime(timeProvider.GetUtcNow().DateTime));
        });
    }
}
```
