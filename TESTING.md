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

## Complete Testing Workflow

### Overview

When playtesting, you should:
1. **Test game systems** using `play_game.sh`
2. **Document bugs** in **ISSUES.md**
3. **Document suggestions** in **SUGGESTIONS.md**
4. **Summarize findings** and recommend priorities

---

## Reporting Issues Found During Testing

**CRITICAL**: When testing reveals bugs, balance problems, crashes, or unexpected behavior, **YOU MUST UPDATE ISSUES.md**.

### When to Report an Issue

Document in **ISSUES.md** when you find:
- 🔴 **Breaking Exceptions**: Crashes, null references, or errors that stop gameplay
- 🟠 **Bugs**: Features not working as intended (e.g., incorrect damage calculations)
- 🟡 **Questionable Functionality**: Features that work but may not be intended (e.g., "Press any key" requiring specific input)
- 🟢 **Balance & Immersion Issues**: Gameplay too hard/easy, progression too fast/slow, survival impossible/trivial

### How to Report Issues

1. **Open ISSUES.md** and add the issue under the appropriate severity category
2. **Include key details**:
   - Clear title describing the problem
   - Severity level (High/Medium/Low or Critical/Game-Breaking)
   - Status (🔴 ACTIVE, ✅ FIXED, ⏸️ DEFERRED)
   - Reproduction steps from your test session
   - Expected vs. actual behavior
   - Impact on gameplay
   - Root cause hypothesis (if known)
   - Suggested solutions (if you have ideas)
3. **Update testing blockers** if the issue prevents further testing
4. **Cross-reference** with the testing checklist above

### Example Issue Report Format

```markdown
### "You are still feeling cold" Spam During Foraging

**Severity:** High - UX Bug
**Location:** Likely SurvivalProcessor.cs or body update loop
**Status:** 🔴 **ACTIVE** (discovered 2025-11-01)

**Reproduction:**
1. Start new game
2. Select Forage option
3. Observe output during 1-hour forage

**Observed:**
- "You are still feeling cold" message repeats 15-20 times
- Makes output unreadable

**Expected:**
- Status messages should be suppressed or summarized during foraging
- Should NOT spam the same message dozens of times

**Root Cause Hypothesis:**
- Body.Update() is being called in a loop (every minute?) during foraging
- Each update outputs temperature status

**Suggested Fix:**
- Pass a "quiet mode" flag to Body.Update() during long actions
- OR suppress repeated identical messages in Output.WriteLine()

**Priority:** High - significantly impacts user experience
```

**Important:** Always check ISSUES.md before testing to see known problems and avoid re-testing broken features.

---

## Documenting Suggestions

For improvement ideas, enhancement suggestions, or new features discovered during play, update **SUGGESTIONS.md**.

### When to Document a Suggestion

Add to **SUGGESTIONS.md** when you identify:
- **Quality of Life Improvements**: UI/UX enhancements that make the game smoother
- **Gameplay Enhancements**: New mechanics or features that add depth
- **Content Additions**: Items, recipes, NPCs, locations that fit the theme
- **Balance Adjustments**: Tweaks to make gameplay more engaging
- **Thematic Improvements**: Changes that enhance Ice Age authenticity
- **Performance Optimizations**: Technical improvements (only if issues observed)

### How to Document Suggestions

1. **Open SUGGESTIONS.md** and add under the appropriate category
2. **Include key details**:
   - Clear title describing the suggestion
   - Category (Quality of Life, Gameplay, Content, Balance, Thematic, Technical)
   - Current behavior (what exists now)
   - Suggested enhancement (what could be better)
   - Rationale (why this would improve the game)
   - Priority (High/Medium/Low based on impact vs. effort)
   - Implementation notes (if you have technical ideas)

### Example Suggestion Format

```markdown
### Add "Take All" Option When Looking Around

**Current Behavior:**
- Look around shows items on ground: "Dry Grass", "Large Stick"
- Player must manually navigate to pick up items (likely through separate menu)

**Suggested Enhancement:**
- Add option in "Look Around" submenu: "Take all items" or "Pick up [item name]"
- Streamlines early game material gathering workflow
- Reduces menu navigation clicks

**Priority:** Medium - nice QoL, not critical

**Example Flow:**
\```
>>> CLEARING <<<
You see:
  Dry Grass
  Large Stick

1. Take all items
2. Take Dry Grass
3. Take Large Stick
4. Leave items
\```
```

---

## Testing Best Practices

### Before Testing
1. **Read ISSUES.md** - Don't re-test known broken features
2. **Check CURRENT-STATUS.md** - Understand what's implemented
3. **Plan test scenarios** - Focus on specific systems or general playtest
4. **Start clean** - Use `./play_game.sh stop` to kill any running instances

### During Testing
1. **Think like a player** - Does the game feel good to play?
2. **Test edge cases** - Try unusual actions, not just happy paths
3. **Monitor for crashes** - Watch for exceptions or unexpected behavior
4. **Evaluate balance** - Is survival too hard/easy? Are skills useful?
5. **Assess immersion** - Does it match the Ice Age thematic direction?
6. **Take notes** - Jot down issues/suggestions as you find them

### After Testing
1. **Stop the game** - Always use `./play_game.sh stop` when done
2. **Document issues** - Add all bugs found to ISSUES.md
3. **Document suggestions** - Add all improvements to SUGGESTIONS.md
4. **Summarize results** - What systems tested, issues found, priority recommendations
5. **Update status** - Mark features as tested in CURRENT-STATUS.md

---

## Common Testing Pitfalls

**DON'T manually write to test files:**
- ❌ `echo "1" > /tmp/test_game_input.txt` (wrong location!)
- ❌ `echo "1" > .test_game_io/game_input.txt` (bypasses synchronization!)
- ✅ `./play_game.sh send 1` (correct way!)

**DON'T run multiple game instances:**
- Check with `./play_game.sh status` first
- Always stop before starting new session
- File I/O conflicts cause weird behavior

**DON'T trust stale output:**
- Game might still be processing previous command
- Wait for READY state before reading output
- The script handles this automatically

**DO use strategic testing:**
- Test one example from each category (not every single item)
- Focus on code coverage, not exhaustive testing
- Prioritize core gameplay loops over edge features
