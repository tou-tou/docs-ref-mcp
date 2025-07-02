# Update all git submodules in docs/repos directory (PowerShell version)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Updating all documentation repositories" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Change to the repository root
Set-Location $PSScriptRoot

# Initialize submodules if not already initialized
Write-Host "Initializing submodules..." -ForegroundColor Yellow
git submodule init

# Update all submodules to their latest commit
Write-Host "Updating submodules..." -ForegroundColor Yellow
git submodule update --remote --merge

# Show status of each submodule
Write-Host ""
Write-Host "Current status of documentation repositories:" -ForegroundColor Green
Write-Host "--------------------------------------" -ForegroundColor Green
git submodule status

Write-Host ""
Write-Host "Update completed!" -ForegroundColor Green

# Optional: Show which repositories were updated
Write-Host ""
Write-Host "Repository details:" -ForegroundColor Yellow
git submodule foreach 'echo "- $name: $(git log -1 --oneline)"'