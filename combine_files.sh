#!/bin/bash

# Check if the correct number of arguments is provided
if [ $# -ne 2 ]; then
    echo "Usage: $0 <source_directory> <output_file>"
    exit 1
fi

SOURCE_DIR="$1"
OUTPUT_FILE="$2"

# Check if source directory exists
if [ ! -d "$SOURCE_DIR" ]; then
    echo "Error: Source directory '$SOURCE_DIR' does not exist."
    exit 1
fi

# Create or truncate the output file
> "$OUTPUT_FILE"

# Function to process each .cs file
process_file() {
    local file="$1"
    
    echo "Processing: $file"
    
    # Add file header to the output file
    echo "File: $file" >> "$OUTPUT_FILE"
    echo "--------------------------------------------------------------------------------" >> "$OUTPUT_FILE"
    
    # Add file contents to the output file
    cat "$file" >> "$OUTPUT_FILE"
    
    # Add separator after file content
    echo "================================================================================" >> "$OUTPUT_FILE"
    echo "" >> "$OUTPUT_FILE"
}

# Find all .cs files recursively and process them
find "$SOURCE_DIR" -type f -name "*.cs" | while read -r file; do
    process_file "$file"
done

echo "Successfully combined all .cs files into '$OUTPUT_FILE'"