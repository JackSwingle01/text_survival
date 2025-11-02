#!/bin/bash
# Helper script for Claude to play the game via file I/O

GAME_DIR="$(cd "$(dirname "$0")" && pwd)"
TMP_DIR="$GAME_DIR/tmp"
INPUT_FILE="$TMP_DIR/game_input.txt"
OUTPUT_FILE="$TMP_DIR/game_output.txt"
READY_FILE="$TMP_DIR/game_ready.txt"

# Function to send a command and wait for response
send_command() {
    local cmd="$1"

    # Clear output file before sending command to prevent reading stale output
    > "$OUTPUT_FILE"

    # Clear any queued input to prevent command stacking
    > "$INPUT_FILE"

    # Wait for game to be ready before sending command
    local timeout=20
    local elapsed=0
    while [ $elapsed -lt $timeout ]; do
        if [ -f "$READY_FILE" ] && [ "$(cat "$READY_FILE" 2>/dev/null)" = "READY" ]; then
            break
        fi
        sleep 0.1
        elapsed=$((elapsed + 1))
    done

    if [ $elapsed -ge $timeout ]; then
        echo "WARNING: Game not ready, sending command anyway" >&2
    fi

    # Now send the actual command
    echo "$cmd" > "$INPUT_FILE"

    # Wait briefly for game to process (test mode is fast)
    sleep 0.2

    # Wait for game to be ready again (check ready file)
    timeout=20
    elapsed=0
    while [ $elapsed -lt $timeout ]; do
        if [ -f "$READY_FILE" ] && [ "$(cat "$READY_FILE" 2>/dev/null)" = "READY" ]; then
            return 0
        fi
        sleep 0.1
        elapsed=$((elapsed + 1))
    done

    echo "WARNING: Command may not have been processed (timeout)" >&2
    return 1
}

# Function to read current output
read_output() {
    if [ -f "$OUTPUT_FILE" ]; then
        cat "$OUTPUT_FILE"
    fi
}

# Function to read last N lines of output
read_output_tail() {
    local lines=${1:-20}
    if [ -f "$OUTPUT_FILE" ]; then
        tail -n "$lines" "$OUTPUT_FILE"
    fi
}

# Function to check if game is ready for input
is_ready() {
    [ -f "$READY_FILE" ] && [ "$(cat "$READY_FILE" 2>/dev/null)" = "READY" ]
}

# Export functions for use in bash -c
export -f send_command
export -f read_output
export -f read_output_tail
export -f is_ready
export INPUT_FILE OUTPUT_FILE READY_FILE

# If called with arguments, execute them as a command
if [ $# -gt 0 ]; then
    case "$1" in
        send)
            shift
            send_command "$*"
            ;;
        read)
            read_output
            ;;
        tail)
            read_output_tail "${2:-20}"
            ;;
        ready)
            if is_ready; then
                echo "READY"
            else
                echo "NOT READY"
            fi
            ;;
        *)
            # Send the command directly
            send_command "$*"
            ;;
    esac
else
    echo "Game I/O Helper Script"
    echo "Usage:"
    echo "  $0 send <command>    - Send a command to the game"
    echo "  $0 read              - Read all output"
    echo "  $0 tail [N]          - Read last N lines (default 20)"
    echo "  $0 ready             - Check if game is ready for input"
    echo ""
    echo "Example:"
    echo "  $0 send 1            - Send menu choice '1'"
    echo "  $0 send ENTER        - Send enter key"
fi
