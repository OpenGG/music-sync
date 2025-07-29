#!/usr/env/bin bash

# NCM Decryption Bash Script Plugin
# This script decrypts .ncm files using the 'ncmdump' tool.
#
# Usage: ncmdump.sh <input_file> <output_directory>
#
# $1: Input file path (the .ncm file to be decrypted)
# $2: Output directory path (where the decrypted file will be saved)
#
# Success: Exits with code 0.
# Failure: Exits with a non-zero code, prints error message to stderr.

INPUT_FILE="$1"
OUTPUT_DIR="$2"
NCMDUMP_BIN="ncmdump" # Make sure 'ncmdump' is in your system's PATH, or provide its full path

# Basic input validation
if [ -z "$INPUT_FILE" ] || [ -z "$OUTPUT_DIR" ]; then
    echo "Usage: $0 <input_file> <output_directory>" >&2
    exit 1
fi

if [ ! -f "$INPUT_FILE" ]; then
    echo "Error: Input file '$INPUT_FILE' not found." >&2
    exit 1
fi

if [ ! -d "$OUTPUT_DIR" ]; then
    echo "Error: Output directory '$OUTPUT_DIR' does not exist or is not a directory." >&2
    exit 1
fi

# Check if ncmdump command exists
if ! command -v "$NCMDUMP_BIN" &> /dev/null
then
    echo "Error: ncmdump not found. Please ensure it's installed and in your PATH." >&2
    exit 1
fi

# Determine the output filename based on the input file's name, but with .mp3 extension
# We use 'basename' to get just the filename, then replace its extension
BASENAME_NO_EXT=$(basename "$INPUT_FILE" | sed 's/\.[^.]*$//') # Removes extension from filename
OUTPUT_FILENAME="${BASENAME_NO_EXT}.mp3" # Assuming ncmdump always outputs mp3
OUTPUT_FILE_PATH="${OUTPUT_DIR}/${OUTPUT_FILENAME}"

echo "Attempting to decrypt NCM: $(basename "$INPUT_FILE") to ${OUTPUT_FILE_PATH}"

# Execute the decryption command
# ncmdump usually takes input and output file path, not output directory.
# So we construct the full output file path here.
"$NCMDUMP_BIN" "$INPUT_FILE" "$OUTPUT_FILE_PATH"
EXIT_CODE=$?

if [ $EXIT_CODE -eq 0 ]; then
    echo "Successfully decrypted $(basename "$INPUT_FILE")"
else
    echo "Error: ncmdump failed with exit code $EXIT_CODE for $(basename "$INPUT_FILE")" >&2
fi

exit $EXIT_CODE
