# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 0.1.x   | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security vulnerability in dotnet-claude-kit, please report it responsibly.

### How to Report

1. **Do NOT** create a public GitHub issue for security vulnerabilities
2. Email the maintainers directly or use GitHub's private vulnerability reporting
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Any suggested fixes (optional)

### What to Expect

- **Acknowledgment**: Within 48 hours
- **Initial Assessment**: Within 7 days
- **Resolution Timeline**: Depends on severity
  - Critical: 7 days
  - High: 14 days
  - Medium: 30 days
  - Low: 60 days

### Security Considerations for Users

This plugin provides code templates and patterns. When using them:

1. **Review Generated Code**: Always review code before deploying to production
2. **Secrets Management**: Never commit secrets; use environment variables or secret managers
3. **Dependencies**: Keep NuGet packages updated for security patches
4. **Authentication Tokens**: The `authentication` skill templates use placeholder secrets - replace with strong, unique values
5. **Database Security**: The EF Core patterns assume proper connection string security

### Code Template Security

The asset files in this plugin follow security best practices:

- JWT tokens use secure algorithms (HS256 minimum, RS256 preferred)
- SQL queries use parameterized queries (no string concatenation)
- Input validation patterns prevent injection attacks
- Exception handling doesn't leak sensitive information

However, users are responsible for:

- Implementing proper authentication/authorization for their use case
- Securing connection strings and API keys
- Following OWASP guidelines for their specific application
- Regular security audits of production code

## Security Updates

Security updates will be released as patch versions (e.g., 0.1.1, 0.1.2).

Subscribe to releases to be notified of security updates.
