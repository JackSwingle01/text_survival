# Realistic Fire Temperature System - Task List

**Last Updated:** 2025-11-02

## Completed Tasks ✅

### Core Implementation
- ✅ Create FuelType enum with 6 fuel types (Tinder, Kindling, Softwood, Hardwood, Bone, Peat)
- ✅ Create FuelProperties struct with peak temp, burn rate, min fire temp, ignition bonus, startup time
- ✅ Create FuelDatabase static class mapping FuelType → FuelProperties
- ✅ Add 6 fuel type properties to ItemProperty enum (Fuel_Tinder through Fuel_Peat)
- ✅ Add FuelMassKg property to Item class
- ✅ Add GetFuelType() and IsFuel() helper methods to Item class
- ✅ Update all existing fuel items in ItemFactory with fuel properties
- ✅ Create new fuel items: MakeHardwoodLog(), MakePeatBlock()

### HeatSourceFeature Refactor
- ✅ Convert from hour-based (FuelRemaining) to mass-based (FuelMassKg) fuel management
- ✅ Add fuel mixture tracking (Dictionary<FuelType, double>)
- ✅ Implement GetCurrentFireTemperature() with phase-based logic
- ✅ Implement GetActiveFireTemperature() with physics multipliers
- ✅ Implement GetEmberTemperature() with square root decay
- ✅ Implement GetWeightedPeakTemperature() for fuel mixtures
- ✅ Implement GetFireSizeMultiplier() (0.7x to 1.1x based on mass)
- ✅ Implement GetStartupMultiplier() with sigmoid curve
- ✅ Implement GetDeclineMultiplier() with power curve
- ✅ Implement GetEffectiveHeatOutput() with proper scaling formula
- ✅ Implement CanAddFuel() with temperature requirement checking
- ✅ Implement AddFuel(Item, double) with fuel mixture tracking
- ✅ Add legacy AddFuel(Item) for backward compatibility
- ✅ Add FuelRemaining property for backward compatibility
- ✅ Implement GetFirePhase() for UI display
- ✅ Update fuel consumption to proportionally reduce fuel mixture
- ✅ Update ember transition to calculate from total burn time

### Fire-Starting Action
- ✅ Detect tinder in inventory
- ✅ Apply +15% success bonus when tinder available
- ✅ Display tinder availability in UI
- ✅ Update success rate calculation to include tinder bonus
- ✅ Update fire creation to use new fuel system (tinder + kindling)
- ✅ Update fire relighting to use new fuel system

### Fire Management UI
- ✅ Update "Manage Fire" display with fire phase and temperature
- ✅ Add color-coded phase display (Red/Yellow/DarkYellow/DarkGray)
- ✅ Show heat contribution in °F
- ✅ Show fuel remaining in kg and minutes
- ✅ Show capacity (current/max kg)
- ✅ Update fuel selection to show fuel types
- ✅ Add CanAddFuel() checking to fuel list
- ✅ Display temperature requirements for blocked fuels
- ✅ Add user feedback when fuel can't be added
- ✅ Update "add fuel" result message with temperature and phase

### Integration Updates
- ✅ Update Program.cs starting fire initialization
- ✅ Update CraftingSystem.cs campfire recipe
- ✅ Fix all compilation errors
- ✅ Verify build succeeds

## In Progress Tasks 🔄

None - all implementation tasks complete

## Pending Tasks ⏳

### High Priority - Testing & Validation
- ⏳ **Runtime testing** - Play through fire mechanics
  - Start fire with different tools
  - Test tinder bonus (+15% success)
  - Verify material consumption (success/failure)
  - Test temperature progression (ignition → building → steady → dying → embers)
  - Test fuel type restrictions (can't add hardwood to weak fire)
  - Verify UI displays accurate information
  - Check survival balance impact

- ⏳ **Edge case testing**
  - Mixed fuel types in one fire
  - Adding fuel during different fire phases
  - Fire at exactly max capacity
  - Ember relight mechanics
  - Very small fires (< 1kg)
  - Very large fires (> 8kg)
  - Tool breakage during fire-starting
  - Fire in different biomes/temperatures

- ⏳ **Balance validation**
  - Time to reach survival-viable temperature (target: 10-15 min)
  - Fuel efficiency (kg per hour of warmth)
  - Difficulty vs previous system
  - Day-1 viability in Forest biome
  - Overnight survival feasibility

### Medium Priority - Polish & Enhancement
- ⏳ **Update Look Around menu** - Show fire status in location description
  - Currently shows basic fire info
  - Should show phase, temperature, heat contribution
  - File: `Actions/ActionFactory.cs` - Look Around action

- ⏳ **Crafting recipe temperature requirements**
  - Add `.RequiringFire(minTemperature)` support to RecipeBuilder
  - Update recipes with temperature requirements:
    - Cooking: 400-500°F (any active fire)
    - Bone hardening: 700°F+ (hot fire required)
    - Advanced crafting: 800°F+ (very hot fire required)
  - Files: `Crafting/RecipeBuilder.cs`, `Crafting/CraftingSystem.cs`

- ⏳ **Weather integration** - Cold/wind affects fire temperature
  - Consider weather effects on heat output
  - Possible: Wind increases fuel consumption
  - Possible: Rain reduces temperature or extinguishes

### Low Priority - Documentation
- ⏳ **Update fire-management-system.md**
  - Document new physics model
  - Add temperature progression charts
  - Document fuel type characteristics
  - Add fire-building strategy guide
  - Include formulas and constants

- ⏳ **Update CURRENT-STATUS.md**
  - Summarize implementation
  - Note files changed
  - Document testing approach
  - List known issues if any

- ⏳ **Update README.md design philosophy** (if needed)
  - Add fire temperature system to design philosophy
  - Document fuel type progression
  - Explain temperature requirements rationale

## Future Enhancements (Backlog) 🎯

### Fuel Type Expansion
- 🎯 Add Dung fuel (low temp, smoky, available in plains)
- 🎯 Add Coal/Charcoal as craftable fuel (high temp, long burn)
- 🎯 Add Animal Fat as accelerant (temp boost but fast burn)
- 🎯 Add Green Wood (lower temp, more smoke, needs drying)

### Advanced Fire Mechanics
- 🎯 Fire size affects visibility radius at night
- 🎯 Smoke production based on fuel type (alerts NPCs/animals)
- 🎯 Fire quality affects cooking outcomes
- 🎯 Ash collection as crafting material
- 🎯 Fire pit improvements (stone ring, clay liner)

### Temperature-Based Effects
- 🎯 Metal working requires forge temperatures (1000°F+)
- 🎯 Pottery firing temperature requirements
- 🎯 Food preservation in smoke
- 🎯 Boiling water for purification

### Performance Optimizations
- 🎯 Cache temperature calculations if performance issues
- 🎯 Consolidate fuel mixture more aggressively (< 0.01kg threshold)
- 🎯 Optimize weighted average calculations

## Known Issues 🐛

### Non-Critical
- ⚠️ Tinder shows "~0 min" burn time in fuel list
  - Not a bug - tinder burns in seconds (3.0 kg/hr on 0.02kg)
  - Expected behavior for ignition material
  - Consider: Hide burn time for tinder, show "Quick ignition" instead

- ⚠️ Pre-existing build warnings (not related to fire system)
  - Player.cs:47 - Nullability warning
  - SurvivalData.cs:16 - Non-nullable field warning

## Testing Checklist 📋

### Fire-Starting Tests
- [ ] Start fire with Hand Drill (30% base)
- [ ] Start fire with Bow Drill (50% base)
- [ ] Start fire with Flint & Steel (90% base)
- [ ] Verify tinder bonus applies (+15%)
- [ ] Verify tinder bonus displays in UI
- [ ] Test success path (tinder + kindling + tool consumed, 3 XP)
- [ ] Test failure path (tinder + tool consumed, 1 XP, kindling kept)
- [ ] Test fire-starting without tinder (no bonus)

### Fuel Management Tests
- [ ] Add tinder to cold fire (should work)
- [ ] Add kindling to cold fire (should work)
- [ ] Add softwood to cold fire (should block - needs 400°F)
- [ ] Build fire: tinder → kindling → softwood → hardwood
- [ ] Verify temperature increases at each step
- [ ] Try adding hardwood too early (should block with message)
- [ ] Try adding bone to medium fire (should block - needs 600°F)
- [ ] Add fuel to fire at capacity (should show overflow warning)
- [ ] Add sharp tool to fire (should show warning prompt)

### Temperature Progression Tests
- [ ] Start fire, observe Igniting phase (0-5 min)
- [ ] Watch temperature build to peak (5-20 min)
- [ ] Verify Roaring phase when fuel > 50%
- [ ] Verify Steady phase when fuel 30-50%
- [ ] Let fuel drop below 30%, verify Dying phase
- [ ] Let fuel deplete, verify Embers phase
- [ ] Verify ember duration = 25% of burn time
- [ ] Let embers expire, verify Cold phase

### Fire Size Tests
- [ ] Small fire (< 1kg): Verify 0.7x temp multiplier
- [ ] Medium fire (2-5kg): Verify 1.0x temp multiplier
- [ ] Large fire (> 5kg): Verify 1.1x temp multiplier
- [ ] Very small fire (< 0.5kg): Check stability
- [ ] At capacity fire (12kg): Check overflow handling

### Survival Impact Tests
- [ ] Start game in Forest biome
- [ ] Build fire, check body temperature response
- [ ] Compare heat contribution to location temp
- [ ] Verify fire + shelter insulation interaction
- [ ] Test overnight survival with fire
- [ ] Test fire dying during sleep
- [ ] Test ember relight vs cold restart

### UI/UX Tests
- [ ] Verify fire phase colors display correctly
- [ ] Check temperature readout accuracy
- [ ] Verify heat contribution calculation
- [ ] Test fuel list formatting
- [ ] Check temperature requirement warnings
- [ ] Verify "can't add fuel" message clarity
- [ ] Test post-add fuel status display

## Balance Tuning Notes 🎛️

### If Fires Start Too Slowly
Adjust in `HeatSourceFeature.cs:112-127`:
- Reduce inflection point (currently 60% of startup time)
- Increase steepness (currently 0.4)
- Increase initial temp (currently 0.4 = 40% of peak)

### If Fires Too Powerful
Adjust in `HeatSourceFeature.cs:180-195`:
- Increase heat output divisor (currently 90.0)
- Reduce fire size multipliers
- Lower peak temperatures in FuelDatabase

### If Fuel Burns Too Fast/Slow
Adjust in `Items/FuelType.cs:72-130`:
- Change `BurnRateKgPerHour` for specific fuel types
- Tinder: Currently 3.0 kg/hr (burns very fast)
- Hardwood: Currently 0.7 kg/hr (burns slow)

### If Temperature Requirements Too Strict/Lenient
Adjust in `Items/FuelType.cs:72-130`:
- Change `MinFireTemperature` for fuel types
- Softwood: Currently 400°F
- Hardwood: Currently 500°F
- Bone: Currently 600°F

## Session Handoff Notes 🤝

**Current State:** Implementation complete, compilation successful, ready for testing

**Next Steps:**
1. Run `dotnet run` and playtest fire mechanics
2. Follow testing checklist above
3. Adjust balance constants if needed
4. Update documentation with findings

**No Blockers** - System is fully integrated and functional

**Build Command:** `dotnet build` (succeeds with 0 errors)
**Test Command:** `dotnet run` or `TEST_MODE=1 dotnet run`

**Critical Files:**
- `Items/FuelType.cs` - Fuel properties (tune here first)
- `Environments/LocationFeatures.cs/HeatSourceFeature.cs` - Physics calculations
- `Actions/ActionFactory.cs` - UI and user interaction

**Don't Change Without Testing:**
- Heat output formula divisor (90.0) - affects all fires
- Startup/decline curve parameters - affects feel
- Fire size thresholds - affects progression balance
