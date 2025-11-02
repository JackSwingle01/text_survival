---
description: Playtest the game and document bugs and suggestions
argument-hint: Optional - specific areas to focus testing on (leave empty for general playtest)
---

You are going to playtest the Text Survival RPG game to identify bugs, balance issues, and improvement opportunities.

## Step 1: Read Testing Documentation

Read `TESTING.md` to understand how to use the `play_game.sh` script and TEST_MODE for interactive playtesting.

## Step 2: Plan Your Playtest

Based on $ARGUMENTS (or general testing if no arguments provided):
- Identify key game systems to test (survival, crafting, combat, inventory, etc.)
- Plan test scenarios (e.g., "survive first day", "craft basic tools", "test combat")
- Review the testing checklist in TESTING.md for specific features to verify

## Step 3: Execute Playtest

Use `play_game.sh` to play through the game:
- Test core gameplay loops
- Try edge cases and unusual actions
- Monitor for crashes, exceptions, or unexpected behavior
- Evaluate balance (is survival too hard/easy? Are skills useful?)
- Assess immersion and UX quality
- Check if features match the Ice Age thematic direction

## Step 4: Document Issues in ISSUES.md

For each bug or problem discovered, update **ISSUES.md** following the format in TESTING.md:
- 🔴 **Breaking Exceptions** - Crashes that prevent gameplay
- 🟠 **Bugs** - Incorrect behavior
- 🟡 **Questionable Functionality** - May not be intended behavior
- 🟢 **Balance & Immersion Issues** - Gameplay feel problems

**Include**:
- Clear title describing the problem
- Severity level (High/Medium/Low)
- Reproduction steps from your playtest
- Expected vs. actual behavior
- Impact on gameplay
- Suggested solutions (if applicable)

## Step 5: Document Suggestions in SUGGESTIONS.md

For improvement ideas, enhancement suggestions, or new features discovered during play, update **SUGGESTIONS.md**:
- Quality of life improvements
- New features or mechanics
- Content additions (items, recipes, NPCs, locations)
- UI/UX enhancements
- Balance adjustments
- Thematic improvements for Ice Age setting

**Include**:
- Clear title describing the suggestion
- Category (QoL, Feature, Content, Balance, Thematic, etc.)
- Description of the improvement
- Rationale (why this would improve the game)
- Implementation notes (if you have ideas)

## Step 6: Summarize Results

After playtesting, provide a summary:
- What systems were tested
- Number of issues found (by severity)
- Number of suggestions documented
- Overall game state assessment
- Priority recommendations for fixes

## Focus Area: $ARGUMENTS

**Important**:
- Be thorough but efficient - focus on discovering new issues
- Think like a player - does the game feel good to play?
- Always stop the game with `./play_game.sh stop` when done
