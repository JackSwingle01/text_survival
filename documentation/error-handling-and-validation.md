# Error Handling and Validation

C# exception handling, guard clauses, input validation, error propagation, and common patterns.

## Table of Contents

- [Error Handling Philosophy](#error-handling-philosophy)
- [Try-Catch Best Practices](#try-catch-best-practices)
- [Guard Clauses](#guard-clauses)
- [Custom Exceptions](#custom-exceptions)
- [Input Validation](#input-validation)
- [Error Propagation](#error-propagation)
- [Common Pitfalls](#common-pitfalls)

---

## Error Handling Philosophy

**Fail Fast**: Detect errors early, close to their source.

**Clear Messages**: Errors should explain WHAT went wrong and WHY.

**Appropriate Level**: Handle errors at the right abstraction level.

**Defensive Programming**: Validate inputs, check preconditions, prevent invalid states.

---

## Try-Catch Best Practices

### Principle 1: Catch Specific Exceptions

```csharp
// ✅ GOOD: Catch specific exceptions
try
{
    player.Body.Damage(damageInfo);
}
catch (ArgumentNullException ex)
{
    Output.WriteError($"Invalid damage info: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Output.WriteError($"Cannot apply damage: {ex.Message}");
}

// ❌ BAD: Catch all exceptions
try
{
    player.Body.Damage(damageInfo);
}
catch (Exception ex)  // Too broad!
{
    Output.WriteError("Something went wrong");
}
```

### Principle 2: Don't Swallow Exceptions

```csharp
// ❌ WRONG: Silent failure
try
{
    player.TakeItem(item);
}
catch
{
    // Nothing - error is hidden!
}

// ✅ CORRECT: Log or rethrow
try
{
    player.TakeItem(item);
}
catch (Exception ex)
{
    Output.WriteError($"Failed to take item: {ex.Message}");
    throw;  // Rethrow if can't handle
}
```

### Principle 3: Finally for Cleanup

```csharp
// ✅ GOOD: Use finally for cleanup
var file = OpenFile("save.dat");
try
{
    file.Write(saveData);
}
catch (IOException ex)
{
    Output.WriteError($"Save failed: {ex.Message}");
}
finally
{
    file.Close();  // Always executes
}
```

### Principle 4: Don't Catch What You Can't Handle

```csharp
// ❌ WRONG: Catch but can't handle
try
{
    var result = DivideByZero();
}
catch (DivideByZeroException ex)
{
    // Can't actually fix this...
    Output.WriteError("Math error");
    return 0;  // Wrong default
}

// ✅ CORRECT: Let it propagate or prevent it
if (divisor != 0)
{
    var result = numerator / divisor;
}
else
{
    throw new ArgumentException("Divisor cannot be zero", nameof(divisor));
}
```

---

## Guard Clauses

**Purpose**: Validate preconditions at method entry, fail fast if invalid.

### Pattern: Check and Throw

```csharp
public void ApplyDamage(Actor target, DamageInfo info)
{
    // Guard clauses at top of method
    if (target == null)
        throw new ArgumentNullException(nameof(target));

    if (info == null)
        throw new ArgumentNullException(nameof(info));

    if (info.Amount < 0)
        throw new ArgumentException("Damage amount cannot be negative", nameof(info));

    if (!target.IsAlive)
        throw new InvalidOperationException("Cannot damage dead actor");

    // Main logic after guards
    target.Body.Damage(info);
}
```

### Common Guard Patterns

**Null Check**:
```csharp
if (value == null)
    throw new ArgumentNullException(nameof(value));

// Or use null-coalescing
var safeValue = value ?? throw new ArgumentNullException(nameof(value));
```

**String Validation**:
```csharp
if (string.IsNullOrWhiteSpace(name))
    throw new ArgumentException("Name cannot be empty", nameof(name));
```

**Range Validation**:
```csharp
if (severity < 0 || severity > 1)
    throw new ArgumentOutOfRangeException(nameof(severity), "Severity must be between 0 and 1");
```

**State Validation**:
```csharp
if (!IsInitialized)
    throw new InvalidOperationException("Object must be initialized before use");
```

### Builder Validation in Build()

```csharp
public CraftingRecipe Build()
{
    // Validate required fields
    if (string.IsNullOrWhiteSpace(_name))
        throw new InvalidOperationException("Recipe name is required");

    if (_requiredProperties.Count == 0)
        throw new InvalidOperationException("Recipe must have at least one requirement");

    if (_resultItems.Count == 0 && _locationFeatureResult == null && _newLocationResult == null)
        throw new InvalidOperationException("Recipe must have a result");

    // Build if valid
    return new CraftingRecipe(_name, _description)
    {
        RequiredProperties = _requiredProperties,
        // ...
    };
}
```

---

## Custom Exceptions

### When to Create Custom Exceptions

**✅ Create custom exception when**:
- Domain-specific error with special handling
- Need additional error context/data
- Distinct error category

**❌ Don't create custom exception when**:
- Built-in exception is sufficient
- No special handling needed
- Adds complexity without benefit

### Custom Exception Pattern

```csharp
public class InsufficientMaterialsException : Exception
{
    public ItemProperty MissingProperty { get; }
    public double RequiredAmount { get; }
    public double AvailableAmount { get; }

    public InsufficientMaterialsException(
        ItemProperty property,
        double required,
        double available)
        : base($"Insufficient {property}: need {required}, have {available}")
    {
        MissingProperty = property;
        RequiredAmount = required;
        AvailableAmount = available;
    }
}

// Usage
if (availableStone < requiredStone)
{
    throw new InsufficientMaterialsException(
        ItemProperty.Stone,
        requiredStone,
        availableStone
    );
}
```

### Standard Built-In Exceptions

Prefer built-in exceptions when appropriate:

- `ArgumentNullException` - Argument is null
- `ArgumentException` - Invalid argument value
- `ArgumentOutOfRangeException` - Value out of valid range
- `InvalidOperationException` - Operation invalid in current state
- `NotImplementedException` - Feature not yet implemented
- `NotSupportedException` - Operation not supported

---

## Input Validation

### User Input Validation

```csharp
public bool TryParseUserChoice(string input, int maxChoice, out int choice)
{
    choice = 0;

    // Null/empty check
    if (string.IsNullOrWhiteSpace(input))
    {
        Output.WriteWarning("Please enter a number.");
        return false;
    }

    // Parse check
    if (!int.TryParse(input, out choice))
    {
        Output.WriteWarning($"'{input}' is not a valid number.");
        return false;
    }

    // Range check
    if (choice < 1 || choice > maxChoice)
    {
        Output.WriteWarning($"Please enter a number between 1 and {maxChoice}.");
        return false;
    }

    return true;
}
```

### Property Validation

```csharp
private double _severity;
public double Severity
{
    get => _severity;
    set
    {
        if (value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Severity must be 0-1");

        _severity = value;
    }
}
```

### Collection Validation

```csharp
public void AddItems(IEnumerable<Item> items)
{
    if (items == null)
        throw new ArgumentNullException(nameof(items));

    var itemList = items.ToList();  // Enumerate once

    if (itemList.Count == 0)
        throw new ArgumentException("Must provide at least one item", nameof(items));

    if (itemList.Any(item => item == null))
        throw new ArgumentException("Collection contains null items", nameof(items));

    foreach (var item in itemList)
    {
        _inventory.Add(item);
    }
}
```

---

## Error Propagation

### When to Catch and Rethrow

```csharp
public void ProcessCrafting(Player player, CraftingRecipe recipe)
{
    try
    {
        recipe.ConsumeIngredients(player);
        recipe.GenerateResults(player);
    }
    catch (InsufficientMaterialsException ex)
    {
        // Add context and rethrow
        Output.WriteError($"Cannot craft {recipe.Name}: {ex.Message}");
        throw new InvalidOperationException(
            $"Crafting failed for {recipe.Name}",
            ex  // Include inner exception
        );
    }
}
```

### When to Catch and Handle

```csharp
public void ExecutePlayerAction(IGameAction action, GameContext ctx)
{
    try
    {
        action.Execute(ctx);
    }
    catch (InvalidOperationException ex)
    {
        // Handle at appropriate level
        Output.WriteError($"Action failed: {ex.Message}");
        // DON'T rethrow - this is the right level to handle
    }
}
```

### When to Let Propagate

```csharp
public void DamageBodyPart(BodyPart part, double amount)
{
    // Let ArgumentNullException propagate
    // Caller should ensure valid part
    part.CurrentHealth -= amount;

    if (part.CurrentHealth <= 0)
    {
        part.IsDestroyed = true;
    }
}
```

---

## Common Pitfalls

### ❌ Pitfall 1: Empty Catch Blocks

```csharp
// WRONG: Silently swallows errors
try
{
    File.Delete(filePath);
}
catch
{
    // Nothing - file might still exist!
}

// CORRECT: At least log the error
try
{
    File.Delete(filePath);
}
catch (Exception ex)
{
    Output.WriteWarning($"Could not delete file: {ex.Message}");
    // Decide if should rethrow based on context
}
```

### ❌ Pitfall 2: Using Exceptions for Control Flow

```csharp
// WRONG: Exceptions for normal logic
try
{
    var item = inventory.GetItem(index);
    return item;
}
catch (IndexOutOfRangeException)
{
    return null;  // Don't use exceptions for this
}

// CORRECT: Check before access
if (index >= 0 && index < inventory.Count)
{
    return inventory.GetItem(index);
}
return null;
```

### ❌ Pitfall 3: Generic Error Messages

```csharp
// WRONG: Unhelpful message
throw new Exception("Error occurred");

// CORRECT: Specific, actionable message
throw new InvalidOperationException(
    $"Cannot craft {recipe.Name}: missing {missingProperty} " +
    $"(need {required}, have {available})"
);
```

### ❌ Pitfall 4: Not Validating at Boundaries

```csharp
// WRONG: Trust external input
public void SetPlayerName(string name)
{
    _playerName = name;  // What if name is null or empty?
}

// CORRECT: Validate at public API boundary
public void SetPlayerName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty", nameof(name));

    _playerName = name;
}
```

---

## Exception Handling in Async Code

### Async/Await Pattern

```csharp
// ✅ GOOD: Proper async exception handling
public async Task SaveGameAsync(SaveData data)
{
    try
    {
        await File.WriteAllTextAsync("save.json", JsonSerialize(data));
    }
    catch (IOException ex)
    {
        Output.WriteError($"Save failed: {ex.Message}");
        throw;
    }
}

// ❌ BAD: Async void (exceptions hard to catch)
public async void SaveGame(SaveData data)  // Avoid async void!
{
    await File.WriteAllTextAsync("save.json", JsonSerialize(data));
}
```

---

## Validation Checklist

When writing a new method:

- [ ] Are all parameters validated (null, range, state)?
- [ ] Are guard clauses at the top of the method?
- [ ] Are error messages specific and helpful?
- [ ] Are custom exceptions used appropriately (or built-in sufficient)?
- [ ] Is exception handling at the right abstraction level?
- [ ] Are errors logged/displayed to user when appropriate?
- [ ] Are resources cleaned up (finally blocks, using statements)?
- [ ] Is the method testable with invalid inputs?

---

## Related Files

**Examples in Codebase**:
- `Crafting/RecipeBuilder.cs` - Guard clauses in Build() (RecipeBuilder.cs:102-122)
- `Bodies/Body.cs` - Null checks and validation
- `Actions/ActionBuilder.cs` - Builder validation
- `Effects/EffectBuilder.cs` - Severity clamping

**Related Guides**:
- [builder-patterns.md](builder-patterns.md) - Validation in Build() methods
- [complete-examples.md](complete-examples.md) - Error handling in practice

---

**Last Updated**: 2025-11-01
**Skill Status**: In Progress - Resource files being created
