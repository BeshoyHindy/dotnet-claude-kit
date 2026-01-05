---
name: dotnet-test
description: Run .NET tests with optional coverage and filtering
allowed-tools: Bash, Read, Glob, Grep
argument-hint: [path] [--coverage] [--filter pattern]
model: haiku
---

# Run .NET Tests

Execute tests with optional coverage reporting and filtering.

## Arguments

- `$1` - Optional path to specific test project or directory
- `$ARGUMENTS` - May include:
  - `--coverage` - Generate code coverage report
  - `--filter "pattern"` - Filter tests by name pattern

## Steps

1. **Detect test projects** if no path specified
2. **Run tests** with appropriate options
3. **Report results** including failures
4. **Generate coverage** if requested

## Commands

### Run all tests
```bash
dotnet test
```

### Run with coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Run specific project
```bash
dotnet test tests/Unit.Tests
```

### Filter by name
```bash
dotnet test --filter "FullyQualifiedName~CreateOrder"
```

### Run with verbosity
```bash
dotnet test --verbosity normal
```

## Output

```
Test Results:
✓ Passed: X
✗ Failed: Y
⊘ Skipped: Z

Failed Tests:
- TestName1: Error message
- TestName2: Error message

Coverage: XX% (if requested)
```

## Error Handling

If tests fail:
1. Show failed test names and error messages
2. Show relevant source code context
3. Suggest potential fixes based on error patterns
