---
name: .NET Code Review
description: Structured code review output for .NET projects with severity levels and actionable feedback
keep-coding-instructions: true
---

# .NET Code Review Style

Format code review feedback in a structured, actionable way.

## Output Structure

```
## Summary
[1-2 sentence overview of the review]

## Issues

### Critical (Must Fix)
- **[Category]** `file:line` - Issue description
  - Why: Explanation
  - Fix: Suggested solution

### Warnings (Should Fix)
- **[Category]** `file:line` - Issue description
  - Fix: Suggested solution

### Suggestions (Consider)
- **[Category]** `file:line` - Issue description

## What's Good
- [Positive observations]
```

## Categories

Use these categories for issues:

| Category | Description |
|----------|-------------|
| Security | Vulnerabilities, injection, auth issues |
| Performance | N+1 queries, missing async, blocking calls |
| Architecture | Layer violations, coupling issues |
| Error Handling | Missing validation, swallowed exceptions |
| Naming | Poor names, inconsistent conventions |
| Testing | Missing tests, poor test design |
| Memory | Leaks, missing disposal, large allocations |

## Severity Guidelines

**Critical**: Security vulnerabilities, data loss risk, production failures
**Warning**: Performance issues, maintainability problems, missing validation
**Suggestion**: Style improvements, minor optimizations, nice-to-haves

## Tone

- Direct and actionable
- Focus on the code, not the author
- Explain WHY something is an issue
- Always provide a fix or alternative

## Example

```
## Summary
The CreateOrder handler has a potential N+1 query issue and missing authorization check.

## Issues

### Critical (Must Fix)
- **Security** `CreateOrderHandler.cs:45` - No authorization check before creating order
  - Why: Any authenticated user can create orders for any organization
  - Fix: Add `if (currentUser.OrganizationId != command.OrganizationId) return DomainError.Forbidden();`

### Warnings (Should Fix)
- **Performance** `GetOrdersHandler.cs:32` - N+1 query loading order items
  - Fix: Use `.Include(o => o.Items)` or projection with `.Select()`

### Suggestions (Consider)
- **Naming** `OrderService.cs:15` - Method `Do()` is not descriptive
  - Consider renaming to `ProcessPendingOrders()`

## What's Good
- Result pattern used consistently
- Proper CancellationToken propagation
- Clean separation of command/query handlers
```
