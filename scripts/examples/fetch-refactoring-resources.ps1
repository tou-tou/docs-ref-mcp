# fetch-refactoring-resources.ps1
# PowerShell script to download popular refactoring and code smell resources

# Set the path to docsref executable
$docsref = if ($env:DOCSREF) { $env:DOCSREF } else { "dotnet run --" }

Write-Host "Fetching refactoring and code smell resources..." -ForegroundColor Green

# First, show available resources
Write-Host "`nAvailable resources:" -ForegroundColor Yellow
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web suggest

Write-Host "`nDownloading resources..." -ForegroundColor Yellow

# Fetch Refactoring Guru pages
Write-Host "`n1. Fetching Refactoring Guru - Code Smells..." -ForegroundColor Cyan
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web fetch "https://refactoring.guru/refactoring/smells" --category "code-smells"

Write-Host "`n2. Fetching Refactoring Guru - Refactoring Techniques..." -ForegroundColor Cyan
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web fetch "https://refactoring.guru/refactoring/techniques" --category "refactoring"

# Fetch Martin Fowler's catalog
Write-Host "`n3. Fetching Martin Fowler - Refactoring Catalog..." -ForegroundColor Cyan
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web fetch "https://refactoring.com/catalog/" --category "refactoring"

# Fetch SourceMaking pages
Write-Host "`n4. Fetching SourceMaking pages..." -ForegroundColor Cyan
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web fetch-batch "https://sourcemaking.com/refactoring/smells,https://sourcemaking.com/refactoring" --category "refactoring"

# Fetch Clean Code summary
Write-Host "`n5. Fetching Clean Code summary..." -ForegroundColor Cyan
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web fetch "https://gist.github.com/wojteklu/73c6914cc446146b8b533c0988cf8d29" --category "clean-code"

# Fetch SOLID principles
Write-Host "`n6. Fetching SOLID Principles..." -ForegroundColor Cyan
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web fetch "https://www.digitalocean.com/community/conceptual-articles/s-o-l-i-d-the-first-five-principles-of-object-oriented-design" --category "design-principles"

Write-Host "`nListing downloaded documents..." -ForegroundColor Yellow
& $docsref.Split()[0] $docsref.Split()[1..($docsref.Split().Length-1)] web list

Write-Host "`nDone! You can now read these documents offline using:" -ForegroundColor Green
Write-Host "  $docsref docs get `"docs/web/[category]/[filename].md`"" -ForegroundColor White