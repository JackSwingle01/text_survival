# Event Conditions Review

Now that activity-based event conditions exist, review all events to add appropriate filters.

## New Conditions Available

- `IsSleeping` - Player is sleeping
- `IsResting` - Player is resting by fire
- `IsCampWork` - Tending fire, eating, cooking, crafting
- `IsExpedition` - Traveling, foraging, hunting, exploring

## Review Checklist

For each event in `Actions/Events/`, ask:
1. Does this event make sense during sleep? (If not, add `!IsSleeping` condition)
2. Is this expedition-only? (Add `IsExpedition` requirement)
3. Is this camp-only? (Add `!IsExpedition` requirement)
4. Should this only happen during specific activities?

## Examples

- "Fresh Tracks" - should require `IsExpedition` (can't find tracks at camp)
- "Embers Dying" - should require `!IsExpedition` (only relevant at camp)
- "Nightmare" - should require `IsSleeping`
- "Smoke Spotted" - should require `IsCampWork` or `IsResting` (near fire)

## Files to Review

```
Actions/Events/*.cs
```

Check each event's `Conditions` list and add activity filters where appropriate.
