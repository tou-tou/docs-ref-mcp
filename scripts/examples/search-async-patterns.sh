#!/bin/bash
# search-async-patterns.sh
# Example script showing how to search for async patterns in the codebase

# Set the path to docsref executable
DOCSREF="${DOCSREF:-dotnet run --}"

echo "Searching for async/await patterns in the codebase..."
echo ""

# Search for async method declarations
echo "=== Async method declarations ==="
$DOCSREF docs grep "async\s+(Task|ValueTask)" --output json | \
  jq -r '.files[] | "\(.file): \(.matches | length) matches"' 2>/dev/null || \
  $DOCSREF docs grep "async\s+(Task|ValueTask)"

echo ""
echo "=== Await usage ==="
$DOCSREF docs grep "await\s+\w+" | head -20

echo ""
echo "=== Files with most async patterns ==="
if command -v jq >/dev/null 2>&1; then
  $DOCSREF docs grep "(async|await)" --output json | \
    jq -r '.files[] | {file: .file, count: (.matches | length)} | "\(.count)\t\(.file)"' | \
    sort -nr | head -10
else
  echo "Install jq for better JSON output formatting"
  $DOCSREF docs grep "(async|await)" | grep -E "^\S+:$" | head -10
fi

echo ""
echo "=== Search for specific async patterns in UniTask ==="
$DOCSREF docs grep "UniTask" --output json | \
  jq -r '.totalMatches' 2>/dev/null && echo " UniTask references found" || \
  $DOCSREF docs grep "UniTask" | wc -l | awk '{print $1 " UniTask references found"}'