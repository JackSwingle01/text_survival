#!/bin/bash
# Helper script for Claude to play the game via file I/O

GAME_DIR="$(cd "$(dirname "$0")" && pwd)"
TMP_DIR="$GAME_DIR/.test_game_io"
INPUT_FILE="$TMP_DIR/game_input.txt"
OUTPUT_FILE="$TMP_DIR/game_output.txt"
READY_FILE="$TMP_DIR/game_ready.txt"
PID_FILE="$TMP_DIR/game.pid"
LOG_FILE="$TMP_DIR/game_log.txt"

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

# Function to start the game
start_game() {
    # Create test directory if it doesn't exist
    mkdir -p "$TMP_DIR"

    if [ -f "$PID_FILE" ]; then
        local pid=$(cat "$PID_FILE")
        if ps -p "$pid" > /dev/null 2>&1; then
            echo "Game is already running (PID: $pid)"
            return 1
        else
            echo "Cleaning up stale PID file..."
            rm "$PID_FILE"
        fi
    fi

    echo "Starting game in TEST_MODE..."
    cd "$GAME_DIR"
    TEST_MODE=1 dotnet run > "$LOG_FILE" 2>&1 &
    local pid=$!
    echo $pid > "$PID_FILE"

    echo "Game started with PID: $pid"
    echo "Waiting for game to initialize..."
    sleep 2

    if is_ready; then
        echo "✓ Game is ready for input"
        return 0
    else
        echo "⚠ Game may still be starting up..."
        return 0
    fi
}

# Function to stop the game
stop_game() {
    if [ ! -f "$PID_FILE" ]; then
        echo "No PID file found. Attempting to kill any running instances..."
        pkill -f "TEST_MODE=1 dotnet run"
        return 0
    fi

    local pid=$(cat "$PID_FILE")

    if ps -p "$pid" > /dev/null 2>&1; then
        echo "Stopping game (PID: $pid)..."
        kill "$pid"
        sleep 1

        if ps -p "$pid" > /dev/null 2>&1; then
            echo "Process didn't stop gracefully, force killing..."
            kill -9 "$pid"
        fi

        rm "$PID_FILE"
        echo "✓ Game stopped"
    else
        echo "Game process not running (stale PID file)"
        rm "$PID_FILE"
    fi
}

# Function to check game status
status_game() {
    if [ -f "$PID_FILE" ]; then
        local pid=$(cat "$PID_FILE")
        if ps -p "$pid" > /dev/null 2>&1; then
            echo "✓ Game is running (PID: $pid)"
            if is_ready; then
                echo "✓ Game is ready for input"
            else
                echo "⚠ Game is busy (not ready for input)"
            fi
            return 0
        else
            echo "✗ Game is not running (stale PID file)"
            return 1
        fi
    else
        echo "✗ Game is not running"
        return 1
    fi
}

# Function to restart the game
restart_game() {
    echo "Restarting game..."
    stop_game
    sleep 1
    start_game
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
        start)
            start_game
            ;;
        stop)
            stop_game
            ;;
        restart)
            restart_game
            ;;
        status)
            status_game
            ;;
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
        log)
            if [ -f "$LOG_FILE" ]; then
                tail -n "${2:-50}" "$LOG_FILE"
            else
                echo "No log file found"
            fi
            ;;
        help)
            echo "Game I/O Helper Script"
            echo ""
            echo "Process Management:"
            echo "  $0 start             - Start the game in TEST_MODE"
            echo "  $0 stop              - Stop the game"
            echo "  $0 restart           - Restart the game"
            echo "  $0 status            - Check if game is running"
            echo ""
            echo "Game Interaction:"
            echo "  $0 send <command>    - Send a command to the game"
            echo "  $0 read              - Read all output"
            echo "  $0 tail [N]          - Read last N lines of output (default 20)"
            echo "  $0 ready             - Check if game is ready for input"
            echo "  $0 log [N]           - View last N lines of game log (default 50)"
            echo ""
            echo "Examples:"
            echo "  $0 start                    - Start the game"
            echo "  $0 send 1                   - Send menu choice '1'"
            echo "  $0 tail                     - View recent output"
            echo "  $0 stop                     - Stop the game"
            ;;
        *)
            # Send the command directly
            send_command "$*"
            ;;
    esac
else
    # No arguments - show help
    $0 help
fi
