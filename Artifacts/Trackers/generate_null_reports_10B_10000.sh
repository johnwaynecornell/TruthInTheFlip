#!/bin/bash

if [ -z "$1" ]; then
    echo "Usage: $0 <farm_executable>" >&2
    echo "Example: $0 TruthInTheFlip_Farm_Experimental" >&2
    exit 1
fi

FARM_CMD="$1"

# Loop through all .tkr files in the current directory
for file in *.tkr; do
    # Ensure that files actually exist matching the pattern
    [ -e "$file" ] || continue
    
    # Extract the extensionless name (e.g., BSP2_NET1)
    base_name="${file%.tkr}"
    
    # Define the output file name
    output_file="${base_name}.null_report_10B_10000.txt"
    
    # Check if the output file already exists
    if [ ! -f "$output_file" ]; then
        echo "Processing $file -> $output_file"
        
        # Run your command substituting the tracker file and the output file name
        "$FARM_CMD" null_report 10000 897234 conditioned file "$file" by_total 10B by_total 10B > "$output_file"
    else
        echo "Skipping $file (Output $output_file already exists)"
    fi
done
