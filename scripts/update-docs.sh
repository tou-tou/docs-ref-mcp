#!/bin/bash
# Update all git submodules in docs/repos directory

echo "========================================"
echo "Updating all documentation repositories"
echo "========================================"

# Change to the repository root
cd "$(dirname "$0")"

# Initialize submodules if not already initialized
echo "Initializing submodules..."
git submodule init

# Update all submodules to their latest commit
echo "Updating submodules..."
git submodule update --remote --merge

# Show status of each submodule
echo ""
echo "Current status of documentation repositories:"
echo "--------------------------------------"
git submodule status

echo ""
echo "Update completed!"

# Optional: Show which repositories were updated
echo ""
echo "Repository details:"
git submodule foreach 'echo "- $name: $(git log -1 --oneline)"'