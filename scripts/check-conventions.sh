#!/bin/bash
# Check .NET naming and coding conventions
# Usage: ./check-conventions.sh [solution-dir]

set -e

SOLUTION_DIR="${1:-.}"

echo "🔍 Checking conventions in: $SOLUTION_DIR"
echo ""

ISSUES=0

# Check for DateTime.Now usage (should use TimeProvider)
echo "Checking for DateTime.Now usage..."
if grep -rn "DateTime\.Now" "$SOLUTION_DIR/src" 2>/dev/null --include="*.cs" | head -10; then
    echo "⚠️  Use TimeProvider instead of DateTime.Now"
    ISSUES=$((ISSUES + 1))
fi

# Check for .Result or .Wait() usage (async blocking)
echo ""
echo "Checking for async blocking (.Result/.Wait())..."
if grep -rn "\.Result\|\.Wait()" "$SOLUTION_DIR/src" 2>/dev/null --include="*.cs" | grep -v "test" | head -10; then
    echo "⚠️  Avoid blocking on async with .Result or .Wait()"
    ISSUES=$((ISSUES + 1))
fi

# Check for public List<T> (should be IReadOnlyCollection<T>)
echo ""
echo "Checking for exposed List<T> properties..."
if grep -rn "public List<" "$SOLUTION_DIR/src" 2>/dev/null --include="*.cs" | head -10; then
    echo "⚠️  Consider using IReadOnlyCollection<T> instead of List<T>"
    ISSUES=$((ISSUES + 1))
fi

# Check for missing CancellationToken in async methods
echo ""
echo "Checking async methods for CancellationToken..."
if grep -rn "async Task[^(]*(" "$SOLUTION_DIR/src" 2>/dev/null --include="*.cs" | grep -v "CancellationToken" | grep -v "test" | head -10; then
    echo "⚠️  Consider adding CancellationToken to async methods"
    ISSUES=$((ISSUES + 1))
fi

echo ""
if [ $ISSUES -eq 0 ]; then
    echo "✅ No convention issues found"
else
    echo "⚠️  Found $ISSUES type(s) of convention issues"
fi
