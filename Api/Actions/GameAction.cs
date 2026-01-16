// using System.Text.Json.Serialization;
// using text_survival.Actors.Animals;

// namespace text_survival.Api.Actions;

// /// <summary>
// /// Base class for all game actions. Actions are typed commands that transform game state.
// /// The frontend sends these to the backend; the backend applies them and returns new state.
// /// </summary>
// [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
// // Navigation
// [JsonDerivedType(typeof(MoveAction), "move")]
// [JsonDerivedType(typeof(CancelTravelAction), "cancelTravel")]
// [JsonDerivedType(typeof(HazardChoiceAction), "hazardChoice")]
// [JsonDerivedType(typeof(TravelContinueAction), "travelContinue")]
// [JsonDerivedType(typeof(ImpairmentConfirmAction), "impairmentConfirm")]
// // Camp
// [JsonDerivedType(typeof(ManageFireAction), "manageFire")]
// [JsonDerivedType(typeof(OpenInventoryAction), "openInventory")]
// [JsonDerivedType(typeof(OpenStorageAction), "openStorage")]
// [JsonDerivedType(typeof(OpenCraftingAction), "openCrafting")]
// [JsonDerivedType(typeof(OpenEatingAction), "openEating")]
// [JsonDerivedType(typeof(SleepAction), "sleep")]
// [JsonDerivedType(typeof(WaitAction), "wait")]
// // Fire
// [JsonDerivedType(typeof(SelectFireToolAction), "selectFireTool")]
// [JsonDerivedType(typeof(SelectTinderAction), "selectTinder")]
// [JsonDerivedType(typeof(AttemptStartFireAction), "attemptStartFire")]
// [JsonDerivedType(typeof(AddFuelAction), "addFuel")]
// [JsonDerivedType(typeof(CloseFireAction), "closeFire")]
// [JsonDerivedType(typeof(LightEmberCarrierAction), "lightEmberCarrier")]
// [JsonDerivedType(typeof(CollectCharcoalAction), "collectCharcoal")]
// // Cooking
// [JsonDerivedType(typeof(OpenCookingAction), "openCooking")]
// [JsonDerivedType(typeof(CookMeatAction), "cookMeat")]
// [JsonDerivedType(typeof(MeltSnowAction), "meltSnow")]
// [JsonDerivedType(typeof(CloseCookingAction), "closeCooking")]
// // Inventory/Storage
// [JsonDerivedType(typeof(CloseInventoryAction), "closeInventory")]
// [JsonDerivedType(typeof(EquipWeaponAction), "equipWeapon")]
// [JsonDerivedType(typeof(UnequipWeaponAction), "unequipWeapon")]
// [JsonDerivedType(typeof(TransferToStorageAction), "transferToStorage")]
// [JsonDerivedType(typeof(TransferToPlayerAction), "transferToPlayer")]
// [JsonDerivedType(typeof(CloseStorageAction), "closeStorage")]
// // Crafting
// [JsonDerivedType(typeof(SelectCraftingCategoryAction), "selectCraftingCategory")]
// [JsonDerivedType(typeof(CraftAction), "craft")]
// [JsonDerivedType(typeof(CloseCraftingAction), "closeCrafting")]
// // Eating
// [JsonDerivedType(typeof(ConsumeFoodAction), "consumeFood")]
// [JsonDerivedType(typeof(DrinkWaterAction), "drinkWater")]
// [JsonDerivedType(typeof(CloseEatingAction), "closeEating")]
// // Expedition work
// [JsonDerivedType(typeof(StartForageAction), "startForage")]
// [JsonDerivedType(typeof(StartHuntAction), "startHunt")]
// [JsonDerivedType(typeof(StartHarvestAction), "startHarvest")]
// [JsonDerivedType(typeof(StartChopAction), "startChop")]
// [JsonDerivedType(typeof(CheckSnaresAction), "checkSnares")]
// [JsonDerivedType(typeof(SetSnareAction), "setSnare")]
// [JsonDerivedType(typeof(StartButcherAction), "startButcher")]
// // Hunt
// [JsonDerivedType(typeof(HuntApproachAction), "huntApproach")]
// [JsonDerivedType(typeof(HuntThrowAction), "huntThrow")]
// [JsonDerivedType(typeof(HuntAbandonAction), "huntAbandon")]
// [JsonDerivedType(typeof(HuntStrikeAction), "huntStrike")]
// [JsonDerivedType(typeof(HuntWaitAction), "huntWait")]
// [JsonDerivedType(typeof(HuntAssessAction), "huntAssess")]
// // Encounter (start)
// [JsonDerivedType(typeof(StartEncounterAction), "startEncounter")]
// // Combat (start)
// [JsonDerivedType(typeof(StartCombatAction), "startCombat")]
// // Butcher
// [JsonDerivedType(typeof(ButcherModeAction), "butcherMode")]
// [JsonDerivedType(typeof(CancelButcherAction), "cancelButcher")]
// // Encounter
// [JsonDerivedType(typeof(EncounterStandAction), "encounterStand")]
// [JsonDerivedType(typeof(EncounterBackAction), "encounterBack")]
// [JsonDerivedType(typeof(EncounterDropMeatAction), "encounterDropMeat")]
// [JsonDerivedType(typeof(EncounterAttackAction), "encounterAttack")]
// [JsonDerivedType(typeof(EncounterRunAction), "encounterRun")]
// // Combat
// [JsonDerivedType(typeof(CombatActionChoice), "combatAction")]
// [JsonDerivedType(typeof(CombatContinueAction), "combatContinue")]
// // Event
// [JsonDerivedType(typeof(EventChoiceAction), "eventChoice")]
// // Modal/Universal
// [JsonDerivedType(typeof(ContinueAction), "continue")]
// [JsonDerivedType(typeof(ConfirmAction), "confirm")]
// // Discovery Log
// [JsonDerivedType(typeof(OpenDiscoveryLogAction), "openDiscoveryLog")]
// [JsonDerivedType(typeof(CloseDiscoveryLogAction), "closeDiscoveryLog")]
// public abstract record GameAction;

// // ============================================
// // NAVIGATION
// // ============================================

// /// <summary>Move to adjacent tile on the grid.</summary>
// public record MoveAction(int X, int Y) : GameAction;

// /// <summary>Cancel an in-progress travel action.</summary>
// public record CancelTravelAction : GameAction;

// /// <summary>Choose quick or careful travel through hazardous terrain.</summary>
// public record HazardChoiceAction(bool QuickTravel) : GameAction;

// /// <summary>Continue or abort travel after an event interruption.</summary>
// public record TravelContinueAction(bool Continue) : GameAction;

// /// <summary>Confirm or cancel travel despite movement impairment.</summary>
// public record ImpairmentConfirmAction(bool Proceed) : GameAction;

// // ============================================
// // CAMP ACTIONS
// // ============================================

// /// <summary>Open fire management overlay.</summary>
// public record ManageFireAction : GameAction;

// /// <summary>Open inventory overlay.</summary>
// public record OpenInventoryAction : GameAction;

// /// <summary>Open storage transfer overlay.</summary>
// public record OpenStorageAction : GameAction;

// /// <summary>Open crafting overlay.</summary>
// public record OpenCraftingAction : GameAction;

// /// <summary>Open eating overlay.</summary>
// public record OpenEatingAction : GameAction;

// /// <summary>Sleep for specified duration.</summary>
// public record SleepAction(int DurationMinutes) : GameAction;

// /// <summary>Wait/rest for specified duration.</summary>
// public record WaitAction(int Minutes) : GameAction;

// // ============================================
// // FIRE
// // ============================================

// /// <summary>Select a fire-starting tool.</summary>
// public record SelectFireToolAction(string ToolId) : GameAction;

// /// <summary>Select tinder for fire starting.</summary>
// public record SelectTinderAction(string TinderId) : GameAction;

// /// <summary>Attempt to start the fire with selected tool and tinder.</summary>
// public record AttemptStartFireAction : GameAction;

// /// <summary>Add fuel to the fire.</summary>
// public record AddFuelAction(string FuelId, int Count = 1) : GameAction;

// /// <summary>Close fire management overlay.</summary>
// public record CloseFireAction : GameAction;

// /// <summary>Light an ember carrier from the fire.</summary>
// public record LightEmberCarrierAction(string CarrierId) : GameAction;

// /// <summary>Collect charcoal from the fire pit.</summary>
// public record CollectCharcoalAction : GameAction;

// // ============================================
// // COOKING
// // ============================================

// /// <summary>Open cooking overlay.</summary>
// public record OpenCookingAction : GameAction;

// /// <summary>Cook raw meat at the fire.</summary>
// public record CookMeatAction : GameAction;

// /// <summary>Melt snow for water at the fire.</summary>
// public record MeltSnowAction : GameAction;

// /// <summary>Close cooking overlay.</summary>
// public record CloseCookingAction : GameAction;

// // ============================================
// // INVENTORY / STORAGE
// // ============================================

// /// <summary>Close inventory overlay.</summary>
// public record CloseInventoryAction : GameAction;

// /// <summary>Equip a weapon from tools.</summary>
// public record EquipWeaponAction(string ToolId) : GameAction;

// /// <summary>Unequip current weapon.</summary>
// public record UnequipWeaponAction : GameAction;

// /// <summary>Transfer item to storage.</summary>
// public record TransferToStorageAction(string ItemId) : GameAction;

// /// <summary>Transfer item from storage to player.</summary>
// public record TransferToPlayerAction(string ItemId) : GameAction;

// /// <summary>Close storage overlay.</summary>
// public record CloseStorageAction : GameAction;

// // ============================================
// // CRAFTING
// // ============================================

// /// <summary>Select a crafting category to filter recipes.</summary>
// public record SelectCraftingCategoryAction(string CategoryId) : GameAction;

// /// <summary>Craft a recipe.</summary>
// public record CraftAction(string RecipeId) : GameAction;

// /// <summary>Close crafting overlay.</summary>
// public record CloseCraftingAction : GameAction;

// // ============================================
// // EATING
// // ============================================

// /// <summary>Consume a food item.</summary>
// public record ConsumeFoodAction(string ItemId) : GameAction;

// /// <summary>Drink water.</summary>
// public record DrinkWaterAction : GameAction;

// /// <summary>Close eating overlay.</summary>
// public record CloseEatingAction : GameAction;

// // ============================================
// // EXPEDITION WORK
// // ============================================

// /// <summary>Start foraging with specified focus and time.</summary>
// public record StartForageAction(string FocusId, string TimeId) : GameAction;

// /// <summary>Start hunting a specific animal.</summary>
// public record StartHuntAction : GameAction
// {
//     /// <summary>The animal to hunt (set programmatically, not from JSON).</summary>
//     [JsonIgnore]
//     public Animal? Target { get; init; }

//     /// <summary>The herd the animal came from, if any.</summary>
//     [JsonIgnore]
//     public Herd? SourceHerd { get; init; }

//     public StartHuntAction() { }
//     public StartHuntAction(Animal target, Herd? sourceHerd = null)
//     {
//         Target = target;
//         SourceHerd = sourceHerd;
//     }
// }

// /// <summary>Start harvesting a specific resource.</summary>
// public record StartHarvestAction(string ResourceId) : GameAction;

// /// <summary>Start chopping wood.</summary>
// public record StartChopAction : GameAction;

// /// <summary>Check snare line at current location.</summary>
// public record CheckSnaresAction : GameAction;

// /// <summary>Set a snare at current location.</summary>
// public record SetSnareAction : GameAction;

// /// <summary>Start butchering a carcass.</summary>
// public record StartButcherAction : GameAction;

// // ============================================
// // HUNT
// // ============================================

// /// <summary>Approach the prey during hunt.</summary>
// public record HuntApproachAction : GameAction;

// /// <summary>Throw weapon at prey.</summary>
// public record HuntThrowAction : GameAction;

// /// <summary>Abandon the hunt.</summary>
// public record HuntAbandonAction : GameAction;

// /// <summary>Strike prey at melee range.</summary>
// public record HuntStrikeAction : GameAction;

// /// <summary>Wait and watch prey.</summary>
// public record HuntWaitAction : GameAction;

// /// <summary>Assess the target.</summary>
// public record HuntAssessAction : GameAction;

// // ============================================
// // ENCOUNTER
// // ============================================

// /// <summary>Start an encounter with a predator.</summary>
// public record StartEncounterAction : GameAction
// {
//     /// <summary>The predator for this encounter.</summary>
//     [JsonIgnore]
//     public Animal? Predator { get; init; }

//     public StartEncounterAction() { }
//     public StartEncounterAction(Animal predator) => Predator = predator;
// }

// // ============================================
// // COMBAT
// // ============================================

// /// <summary>Start combat with an enemy.</summary>
// public record StartCombatAction : GameAction
// {
//     /// <summary>The enemy to fight.</summary>
//     [JsonIgnore]
//     public Animal? Enemy { get; init; }

//     public StartCombatAction() { }
//     public StartCombatAction(Animal enemy) => Enemy = enemy;
// }

// // ============================================
// // BUTCHER
// // ============================================

// /// <summary>Select butchering mode and start.</summary>
// public record ButcherModeAction(string ModeId) : GameAction;

// /// <summary>Cancel butchering.</summary>
// public record CancelButcherAction : GameAction;

// // ============================================
// // ENCOUNTER
// // ============================================

// /// <summary>Stand ground against predator.</summary>
// public record EncounterStandAction : GameAction;

// /// <summary>Back away slowly from predator.</summary>
// public record EncounterBackAction : GameAction;

// /// <summary>Drop meat to distract predator.</summary>
// public record EncounterDropMeatAction : GameAction;

// /// <summary>Attack the predator.</summary>
// public record EncounterAttackAction : GameAction;

// /// <summary>Run away from predator.</summary>
// public record EncounterRunAction : GameAction;

// // ============================================
// // COMBAT
// // ============================================

// /// <summary>Select a combat action.</summary>
// public record CombatActionChoice(string ActionId) : GameAction;

// /// <summary>Continue/advance combat (for auto-advance phases).</summary>
// public record CombatContinueAction : GameAction;

// // ============================================
// // EVENT
// // ============================================

// /// <summary>Select a choice in an event.</summary>
// public record EventChoiceAction(string ChoiceId) : GameAction;

// // ============================================
// // MODAL / UNIVERSAL
// // ============================================

// /// <summary>Dismiss a modal (discovery, weather change, etc.).</summary>
// public record ContinueAction : GameAction;

// /// <summary>Confirm or deny a prompt.</summary>
// public record ConfirmAction(bool Confirmed) : GameAction;

// // ============================================
// // DISCOVERY LOG
// // ============================================

// /// <summary>Open discovery log overlay.</summary>
// public record OpenDiscoveryLogAction : GameAction;

// /// <summary>Close discovery log overlay.</summary>
// public record CloseDiscoveryLogAction : GameAction;
