# Testing Guide for Text Survival RPG

## Quick Start: Test Mode with File I/O

The game supports a **TEST_MODE** that uses file-based I/O for automated testing. This is the recommended way for Claude Code to play and test the game.

### Using the Helper Script (Recommended)

The `play_game.sh` script manages the game process and handles all I/O operations automatically.

#### Process Management

```bash
# Start the game in TEST_MODE (handles everything automatically)
./play_game.sh start

# Check if game is running
./play_game.sh status

# Stop the game
./play_game.sh stop

# Restart the game (stop + start)
./play_game.sh restart
```

#### Game Interaction

Once the game is started:

```bash
# Send a menu choice
./play_game.sh send 1

# View recent game output (last 20 lines)
./play_game.sh tail

# View more lines of output
./play_game.sh tail 50

# View all output
./play_game.sh read

# Check if game is ready for input
./play_game.sh ready

# View game log (errors, warnings)
./play_game.sh log

# Get help
./play_game.sh help
```

### Example Play Session

```bash
# Start the game
./play_game.sh start

# Look around, take items from sack
./play_game.sh send 1     # Look around
./play_game.sh send x     # Press any key to continue
./play_game.sh send 1     # Look in sack
./play_game.sh send 3     # Take all

# View results
./play_game.sh tail

# Forage for materials
./play_game.sh send 2     # Forage
./play_game.sh send 2     # Finish foraging

# Check status
./play_game.sh tail

# Stop when done
./play_game.sh stop
```

### Manual Test Mode (Advanced)

If you need to start the game manually:

```bash
TEST_MODE=1 dotnet run &
```

The game will:
- Run in the background
- Create a `./.test_game_io/` directory with I/O files
- Skip all sleep delays for fast execution
- Wait for commands via `.test_game_io/game_input.txt`
- Write output to `.test_game_io/game_output.txt`

## Manual Testing

For full interactive testing without test mode, just run:
```bash
dotnet run
```

## Testing Checklist for Crafting/Foraging Overhaul

### Phase 1-3 Implementation Tests

**Starting Conditions:**
- [ ] Player starts with Tattered Chest Wrap (0.02 insulation)
- [ ] Player starts with Tattered Leg Wraps (0.02 insulation)
- [ ] Player has NO knife
- [ ] Player has NO armor
- [ ] Total starting insulation ~0.04 (very cold!)

**Foraging - Forest:**
- [ ] Can find Dry Grass (common, 50%)
- [ ] Can find Bark Strips (very common, 70%)
- [ ] Can find Plant Fibers (common, 60%)
- [ ] Can find Tinder Bundle (rare, 20%)

**Foraging - Cave:**
- [ ] Can find Handstone (moderate, 40%)
- [ ] Can find Sharp Stone (uncommon, 30%)
- [ ] **CANNOT** find any organic materials (intentional challenge)

**Fire-Making Recipes:**
- [ ] Hand Drill appears in crafting menu
  - [ ] Requires 0.5kg Wood + 0.05kg Tinder
  - [ ] Shows 30% base success chance
  - [ ] Success chance increases with Firecraft skill
  - [ ] Failed attempts consume materials
  - [ ] Failed attempts grant 1 XP
  - [ ] Successful attempts grant (skill level + 2) XP
- [ ] Bow Drill appears at Firecraft 1+
- [ ] Flint & Steel appears when materials available

**Skill Check System:**
- [ ] Success chance = Base + (Skill - DC) * 0.1
- [ ] Never below 5% or above 95%
- [ ] Displayed as percentage when crafting

## Common Issues

**Console Input in Normal Mode:** The game uses `Console.ReadKey()` which requires interactive terminal. Use TEST_MODE=1 for automated testing.

**Test Files:** Test mode creates `./.test_game_io/` directory with I/O files. This is gitignored and safe to delete. The helper script manages cleanup automatically.

**Multiple Game Instances:** Always use `./play_game.sh stop` before starting a new session to prevent multiple instances running simultaneously.

**Stale Process:** If the game crashes, use `./play_game.sh stop` to clean up. The script will detect and kill stale processes automatically.

## Reporting Issues Found During Testing

**CRITICAL**: When testing reveals bugs, balance problems, crashes, or unexpected behavior, **YOU MUST UPDATE ISSUES.md**.

### When to Report an Issue

Document in ISSUES.md when you find:
- **Breaking Exceptions**: Crashes, null references, or errors that stop gameplay
- **Bugs**: Features not working as intended (e.g., energy depleting instantly)
- **Questionable Functionality**: Features that work but feel wrong (e.g., "Press any key" requiring specific input)
- **Balance Issues**: Gameplay too hard/easy, progression too fast/slow, survival impossible/trivial

### How to Report

1. **Open ISSUES.md** and add the issue under the appropriate category
2. **Include key details**:
   - Clear title describing the problem
   - Severity (High/Medium/Low)
   - Reproduction steps from your test session
   - Expected vs. actual behavior
   - Impact on gameplay
   - Suggested solutions if you have ideas
3. **Update testing blockers** if the issue prevents further testing
4. **Cross-reference** with the testing checklist above

### Example Issue Report Format

```markdown
### Energy Depletes to 0% Instantly

**Severity:** High
**Reproduction:**
1. Start new game
2. Look around clearing
3. Take items from sack
4. Energy drops from 83% → 0%

**Expected:** Gradual energy depletion over time
**Actual:** Instant depletion after a few menu actions
**Impact:** Game becomes unplayable
```

This helps track what needs fixing before the feature can be considered complete. Always check ISSUES.md before testing to see known problems and avoid re-testing broken features.
