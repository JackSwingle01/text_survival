#!/bin/bash
# Simple test to view the map display

./play_game.sh start
sleep 2

# Send command 5 to navigate
echo "5" > .test_game_io/game_input.txt
sleep 1

# Copy output before it gets cleared
cp .test_game_io/game_output.txt map_output1.txt

# Send command 1 to use map
sleep 1
echo "1" > .test_game_io/game_input.txt
sleep 2

# Save the map output
cp .test_game_io/game_output.txt map_output2.txt

./play_game.sh stop

echo "Map output saved to map_output2.txt"
cat map_output2.txt
