## Description

Brief description of changes and their purpose.

## Type of Change

- [ ] New skill
- [ ] New agent
- [ ] New command
- [ ] Bug fix
- [ ] Enhancement to existing component
- [ ] Documentation update
- [ ] Refactoring (no functional changes)

## Related Issue

Fixes #(issue number)

## Changes Made

- Change 1
- Change 2
- Change 3

## Checklist

### For All Changes
- [ ] I have read the [CONTRIBUTING.md](CONTRIBUTING.md) guide
- [ ] My changes follow the existing code style
- [ ] I have updated CHANGELOG.md under `[Unreleased]`
- [ ] All code blocks use consistent language tags (`csharp`)

### For New Skills
- [ ] SKILL.md follows pattern-first design (no framework lock-in)
- [ ] Uses `YourNamespace.{Layer}.{Feature}` for namespaces
- [ ] Includes Source reference with official documentation link
- [ ] Framework-specific content is in `references/` folder
- [ ] Asset files have `// Copy to:` header comments
- [ ] All async methods include CancellationToken
- [ ] Uses TimeProvider instead of DateTime/DateTimeOffset

### For Code Examples
- [ ] Uses C# 12+ features (primary constructors, collection expressions)
- [ ] Uses Result<T> pattern for operations that can fail
- [ ] Uses IReadOnlyCollection<T> instead of List<T> for public properties
- [ ] Includes proper XML documentation on public APIs

### For New Agents
- [ ] Includes clear purpose and capabilities
- [ ] Documents when to invoke
- [ ] Specifies model tier (opus/sonnet/haiku)
- [ ] Follows existing agent format

## Testing

Describe how you tested your changes.

## Screenshots (if applicable)

Add screenshots to help explain your changes.

## Additional Notes

Any other information that reviewers should know.
