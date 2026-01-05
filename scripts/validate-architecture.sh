#!/bin/bash
# Validate Clean Architecture layer dependencies
# Usage: ./validate-architecture.sh [solution-dir]

set -e

SOLUTION_DIR="${1:-.}"

echo "🔍 Validating Clean Architecture in: $SOLUTION_DIR"
echo ""

# Check for common violations
VIOLATIONS=0

# Domain should not reference Infrastructure or Application
if grep -r "Infrastructure" "$SOLUTION_DIR/src/Domain" 2>/dev/null | grep -v ".csproj" | head -5; then
    echo "❌ Domain references Infrastructure"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

if grep -r "Application" "$SOLUTION_DIR/src/Domain" 2>/dev/null | grep -v ".csproj" | head -5; then
    echo "❌ Domain references Application"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

# Application should not reference Infrastructure
if grep -r "Infrastructure" "$SOLUTION_DIR/src/Application" 2>/dev/null | grep -v ".csproj" | grep -v "Interfaces" | head -5; then
    echo "❌ Application references Infrastructure (not via interfaces)"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

# Check for EF Core in Domain
if grep -r "Microsoft.EntityFrameworkCore" "$SOLUTION_DIR/src/Domain" 2>/dev/null | head -5; then
    echo "❌ Domain references EF Core"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

# Check for data annotations in Domain
if grep -rE "\[Table\]|\[Column\]|\[Key\]|\[Required\]" "$SOLUTION_DIR/src/Domain" 2>/dev/null | head -5; then
    echo "⚠️  Domain uses data annotations (prefer Fluent API)"
    VIOLATIONS=$((VIOLATIONS + 1))
fi

echo ""
if [ $VIOLATIONS -eq 0 ]; then
    echo "✅ No architecture violations found"
else
    echo "⚠️  Found $VIOLATIONS potential violation(s)"
fi

exit $VIOLATIONS
