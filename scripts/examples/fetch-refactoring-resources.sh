#!/bin/bash
# fetch-refactoring-resources.sh
# Script to download popular refactoring and code smell resources

# Set the path to docsref executable
DOCSREF="${DOCSREF:-dotnet run --}"

echo "Fetching refactoring and code smell resources..."

# First, show available resources
echo "Available resources:"
$DOCSREF web suggest

echo ""
echo "Downloading resources..."

# Fetch Refactoring Guru pages
echo "1. Fetching Refactoring Guru - Code Smells..."
$DOCSREF web fetch "https://refactoring.guru/refactoring/smells" --category "code-smells"

echo "2. Fetching Refactoring Guru - Refactoring Techniques..."
$DOCSREF web fetch "https://refactoring.guru/refactoring/techniques" --category "refactoring"

# Fetch Martin Fowler's catalog
echo "3. Fetching Martin Fowler - Refactoring Catalog..."
$DOCSREF web fetch "https://refactoring.com/catalog/" --category "refactoring"

# Fetch SourceMaking pages
echo "4. Fetching SourceMaking pages..."
$DOCSREF web fetch-batch "https://sourcemaking.com/refactoring/smells,https://sourcemaking.com/refactoring" --category "refactoring"

# Fetch Clean Code summary
echo "5. Fetching Clean Code summary..."
$DOCSREF web fetch "https://gist.github.com/wojteklu/73c6914cc446146b8b533c0988cf8d29" --category "clean-code"

# Fetch SOLID principles
echo "6. Fetching SOLID Principles..."
$DOCSREF web fetch "https://www.digitalocean.com/community/conceptual-articles/s-o-l-i-d-the-first-five-principles-of-object-oriented-design" --category "design-principles"

echo ""
echo "Listing downloaded documents..."
$DOCSREF web list

echo ""
echo "Done! You can now read these documents offline using:"
echo "  $DOCSREF docs get \"docs/web/[category]/[filename].md\""