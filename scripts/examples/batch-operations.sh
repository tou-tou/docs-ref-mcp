#!/bin/bash
# batch-operations.sh
# Example of batch operations using DocsRef CLI

# Set the path to docsref executable
DOCSREF="${DOCSREF:-dotnet run --}"

# Function to process search results
process_search_results() {
    local pattern="$1"
    local output_file="$2"
    
    echo "Searching for: $pattern"
    $DOCSREF docs grep "$pattern" --output json > "$output_file.json" 2>/dev/null
    
    if [ -f "$output_file.json" ] && command -v jq >/dev/null 2>&1; then
        # Extract file list and save
        jq -r '.files[].file' "$output_file.json" > "$output_file.txt"
        echo "Found $(wc -l < "$output_file.txt") files matching '$pattern'"
        echo "Results saved to: $output_file.txt and $output_file.json"
    else
        $DOCSREF docs grep "$pattern" | grep -E "^\S+:$" | sed 's/:$//' > "$output_file.txt"
        echo "Results saved to: $output_file.txt"
    fi
}

# Create output directory
OUTPUT_DIR="search_results_$(date +%Y%m%d_%H%M%S)"
mkdir -p "$OUTPUT_DIR"

echo "Batch search operations - Results will be saved to: $OUTPUT_DIR"
echo ""

# Search for various patterns
process_search_results "TODO|FIXME|HACK" "$OUTPUT_DIR/todos"
process_search_results "throw\s+new\s+\w*Exception" "$OUTPUT_DIR/exceptions"
process_search_results "Console\.(Write|WriteLine)" "$OUTPUT_DIR/console_output"
process_search_results "using\s+System\.Threading" "$OUTPUT_DIR/threading"

echo ""
echo "Generating summary report..."

# Create summary report
{
    echo "# Search Results Summary"
    echo "Generated: $(date)"
    echo ""
    
    for file in "$OUTPUT_DIR"/*.txt; do
        if [ -f "$file" ]; then
            basename="${file##*/}"
            pattern="${basename%.txt}"
            count=$(wc -l < "$file")
            echo "- $pattern: $count matches"
        fi
    done
    
    echo ""
    echo "## Repository Statistics"
    $DOCSREF docs summary
} > "$OUTPUT_DIR/summary.md"

echo "Summary report saved to: $OUTPUT_DIR/summary.md"

# Example: Export specific files based on search results
echo ""
echo "Example: Exporting first 5 files with TODOs..."
if [ -f "$OUTPUT_DIR/todos.txt" ]; then
    head -5 "$OUTPUT_DIR/todos.txt" | while read -r file; do
        if [ -n "$file" ]; then
            echo "Exporting: $file"
            output_name=$(echo "$file" | tr '/' '_')
            $DOCSREF docs get "$file" > "$OUTPUT_DIR/export_$output_name" 2>/dev/null
        fi
    done
fi

echo ""
echo "Batch operations completed. Check $OUTPUT_DIR for results."