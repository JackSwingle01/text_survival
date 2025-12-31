/**
 * Mock fixture data for all game overlays
 * Structured to match actual DTO schemas
 */

export const fixtures = {
    // ==================== INVENTORY ====================
    inventory: {
        title: 'Your Inventory',
        currentWeightKg: 12.3,
        maxWeightKg: 20,
        weapon: 'Stone-Tipped Spear',
        weaponDamage: 8,
        armor: [
            { slot: 'Head', name: 'Hide Cap', insulation: 0.15 },
            { slot: 'Chest', name: 'Heavy Furs', insulation: 0.35 },
            { slot: 'Hands', name: 'Hide Wraps', insulation: 0.10 },
            { slot: 'Legs', name: 'Hide Leggings', insulation: 0.20 },
            { slot: 'Feet', name: 'Hide Boots', insulation: 0.18 }
        ],
        totalInsulation: 0.98,
        tools: [
            { name: 'Flint Knife', damage: 4, maxDurability: 100 },
            { name: 'Stone Axe', damage: 6, maxDurability: 120 },
            { name: 'Bow Drill', damage: null, maxDurability: 80 }
        ],
        toolWarnings: [
            { name: 'Flint Knife', durabilityRemaining: 45 },
            { name: 'Stone Axe', durabilityRemaining: 87 },
            { name: 'Bow Drill', durabilityRemaining: 22 }
        ],
        fuel: [
            { name: 'Pine Logs', display: '3.2 kg' },
            { name: 'Birch Logs', display: '1.8 kg' },
            { name: 'Sticks', display: '0.9 kg' },
            { name: 'Birch Bark', display: '0.2 kg' }
        ],
        food: [
            { name: 'Cooked Venison', display: '1.2 kg' },
            { name: 'Dried Meat', display: '0.8 kg' },
            { name: 'Berries', display: '0.3 kg' }
        ],
        water: 2.4,
        materials: [
            { name: 'Hide', display: '2 pieces' },
            { name: 'Bone', display: '1.1 kg' },
            { name: 'Sinew', display: '0.2 kg' },
            { name: 'Plant Fiber', display: '0.5 kg' },
            { name: 'Flint', display: '3 pieces' }
        ],
        medicinals: [
            { name: 'Willow Bark', display: '0.1 kg' },
            { name: 'Yarrow', display: '0.05 kg' }
        ]
    },

    // ==================== CRAFTING ====================
    crafting: {
        title: 'CRAFTING',
        categories: [
            {
                categoryKey: 'FireStarting',
                categoryName: 'Fire-Starting',
                craftableRecipes: [
                    {
                        name: 'Hand Drill',
                        requirements: [
                            { materialName: 'Sticks', available: 5, required: 2, isMet: true }
                        ],
                        toolRequirements: [],
                        craftingTimeDisplay: '10 minutes'
                    }
                ],
                uncraftableRecipes: [
                    {
                        name: 'Bow Drill',
                        requirements: [
                            { materialName: 'Sticks', available: 5, required: 3, isMet: true },
                            { materialName: 'Plant Fiber', available: 0, required: 1, isMet: false }
                        ],
                        toolRequirements: [],
                        craftingTimeDisplay: '15 minutes'
                    }
                ]
            },
            {
                categoryKey: 'CuttingTool',
                categoryName: 'Cutting Tools',
                craftableRecipes: [
                    {
                        name: 'Stone Knife',
                        requirements: [
                            { materialName: 'Flint', available: 3, required: 1, isMet: true },
                            { materialName: 'Plant Fiber', available: 2, required: 1, isMet: true }
                        ],
                        toolRequirements: [],
                        craftingTimeDisplay: '20 minutes'
                    },
                    {
                        name: 'Stone Axe',
                        requirements: [
                            { materialName: 'Stone', available: 4, required: 2, isMet: true },
                            { materialName: 'Sticks', available: 5, required: 2, isMet: true },
                            { materialName: 'Plant Fiber', available: 2, required: 1, isMet: true }
                        ],
                        toolRequirements: [],
                        craftingTimeDisplay: '30 minutes'
                    }
                ],
                uncraftableRecipes: []
            },
            {
                categoryKey: 'Equipment',
                categoryName: 'Clothing & Gear',
                craftableRecipes: [],
                uncraftableRecipes: [
                    {
                        name: 'Hide Cap',
                        requirements: [
                            { materialName: 'Cured Hide', available: 0, required: 1, isMet: false }
                        ],
                        toolRequirements: [
                            { toolName: 'Knife', isAvailable: true, isBroken: false, durability: 45 }
                        ],
                        craftingTimeDisplay: '25 minutes'
                    }
                ]
            }
        ]
    },

    // ==================== EVENT (Choice Phase) ====================
    eventChoice: {
        name: 'Wolf Tracks',
        description: 'You spot fresh wolf tracks crossing your path. The prints are large, deliberate. They lead toward the ridge ahead.',
        choices: [
            {
                id: 'follow_tracks',
                label: 'Follow the tracks',
                description: 'See where they lead',
                meta: '+15 min',
                isAvailable: true
            },
            {
                id: 'avoid_tracks',
                label: 'Take a wide detour',
                description: 'Stay clear of the wolf',
                meta: '+20 min',
                isAvailable: true
            },
            {
                id: 'continue_careful',
                label: 'Continue carefully',
                description: 'Watch for danger',
                meta: '+5 min',
                isAvailable: true
            }
        ]
    },

    // ==================== EVENT (Outcome Phase) ====================
    eventOutcome: {
        name: 'Wolf Tracks',
        description: 'You spot fresh wolf tracks crossing your path. The prints are large, deliberate. They lead toward the ridge ahead.',
        outcome: {
            message: 'You edge around the tracks, eyes scanning the treeline. Something shifts in your peripheral vision — movement, gone as quickly as it appeared. The hair on your neck rises.',
            timeAddedMinutes: 20,
            effectsApplied: ['Fear +0.2', 'Tension: Stalked'],
            damageTaken: [],
            itemsGained: [],
            itemsLost: [],
            tensionsChanged: ['+Stalked'],
            statsDelta: {
                energyDelta: -5,
                calorieDelta: -80,
                hydrationDelta: -15,
                temperatureDelta: -1.2
            }
        }
    },

    // ==================== FORAGE ====================
    forage: {
        locationQuality: 'abundant',
        clues: [
            {
                focusId: 'berries',
                icon: 'nutrition',
                description: 'Berry bushes dot the clearing'
            },
            {
                focusId: 'fungi',
                icon: 'nature',
                description: 'Shelf fungi on dead wood'
            },
            {
                focusId: 'medicinal',
                icon: 'ecg_heart',
                description: 'Willow saplings near water'
            }
        ],
        focusOptions: [
            { id: 'general', label: 'No focus', description: 'Search for anything useful' },
            { id: 'berries', label: 'Berries', description: 'Food' },
            { id: 'fungi', label: 'Fungi', description: 'Food and medicine' },
            { id: 'medicinal', label: 'Medicinal plants', description: 'Healing materials' }
        ],
        timeOptions: [
            { id: 'quick', label: 'Quick search', meta: '10 min', description: 'Brief look around' },
            { id: 'thorough', label: 'Thorough search', meta: '30 min', description: 'Careful examination' },
            { id: 'extensive', label: 'Extensive search', meta: '60 min', description: 'Systematic foraging' }
        ],
        warnings: []
    },

    // ==================== HUNT (Stalking) ====================
    huntStalking: {
        animalName: 'Caribou',
        animalDescription: 'A young bull, healthy and alert',
        animalActivity: 'Grazing near the treeline',
        animalState: 'idle',
        currentDistanceMeters: 85,
        previousDistanceMeters: null,
        isAnimatingDistance: false,
        minutesSpent: 0,
        statusMessage: null,
        choices: [
            {
                id: 'stalk_closer',
                label: 'Stalk closer',
                description: 'Move carefully downwind',
                meta: '+10 min',
                isAvailable: true
            },
            {
                id: 'wait_opportunity',
                label: 'Wait for opportunity',
                description: 'Let it move closer',
                meta: '+15 min',
                isAvailable: true
            },
            {
                id: 'abandon_hunt',
                label: 'Abandon hunt',
                description: 'Too risky',
                meta: null,
                isAvailable: true
            }
        ]
    },

    // ==================== HUNT (Alert) ====================
    huntAlert: {
        animalName: 'Caribou',
        animalDescription: 'A young bull, healthy and alert',
        animalActivity: 'Head raised, scanning',
        animalState: 'alert',
        currentDistanceMeters: 45,
        previousDistanceMeters: 85,
        isAnimatingDistance: true,
        minutesSpent: 10,
        statusMessage: 'It senses something. You freeze.',
        choices: [
            {
                id: 'stay_still',
                label: 'Stay absolutely still',
                description: 'Wait for it to relax',
                meta: '+5 min',
                isAvailable: true
            },
            {
                id: 'rush_attack',
                label: 'Rush attack',
                description: 'Close distance fast',
                meta: null,
                isAvailable: true,
                disabledReason: 'Too far'
            }
        ]
    },

    // ==================== ENCOUNTER (Predator Approach) ====================
    encounter: {
        predatorName: 'Grey Wolf',
        currentDistanceMeters: 35,
        previousDistanceMeters: 50,
        isAnimatingDistance: true,
        boldnessLevel: 0.6,
        boldnessDescriptor: 'aggressive',
        threatFactors: [
            { id: 'bleeding', icon: 'water_drop', description: 'You\'re bleeding' },
            { id: 'no_weapon', icon: 'block', description: 'No weapon ready' }
        ],
        statusMessage: 'The wolf circles, eyes locked on you. It steps closer.',
        choices: [
            {
                id: 'stand_ground',
                label: 'Stand your ground',
                description: 'Face it down',
                isAvailable: true
            },
            {
                id: 'back_away',
                label: 'Back away slowly',
                description: 'Don\'t turn your back',
                isAvailable: true
            },
            {
                id: 'drop_meat',
                label: 'Drop your meat',
                description: 'Maybe it takes the bait',
                isAvailable: false,
                disabledReason: 'No meat to drop'
            },
            {
                id: 'run',
                label: 'Run for camp',
                description: 'Risky but might work',
                isAvailable: true
            }
        ]
    },

    // ==================== COMBAT (Intro Phase) ====================
    combatIntro: {
        phase: 'Intro',
        animalName: 'Grey Wolf',
        narrativeMessage: 'The wolf lunges forward with terrifying speed. Your heart hammers as you ready your spear.',
        distanceMeters: 8,
        distanceZone: 'Close'
    },

    // ==================== COMBAT (Player Choice Phase) ====================
    combatChoice: {
        phase: 'PlayerChoice',
        animalName: 'Grey Wolf',
        animalHealthDescription: 'wounded',
        animalConditionNarrative: 'Blood mats the wolf\'s shoulder. It favors one leg.',
        distanceZone: 'Close',
        distanceMeters: 6,
        previousDistanceMeters: null,
        behaviorState: 'Circling',
        behaviorDescription: 'The wolf circles left, testing for weakness',
        playerVitality: 0.75,
        playerEnergy: 0.6,
        playerBraced: false,
        threatFactors: [
            { id: 'bleeding', icon: 'water_drop', description: 'You\'re bleeding' },
            { id: 'weapon', icon: 'swords', description: 'Spear ready' }
        ],
        actions: [
            {
                id: 'thrust',
                label: 'Thrust spear',
                description: 'Attack while it circles',
                meta: null,
                isAvailable: true
            },
            {
                id: 'brace',
                label: 'Brace for charge',
                description: 'Set spear against attack',
                meta: null,
                isAvailable: true
            },
            {
                id: 'back_away',
                label: 'Back away',
                description: 'Create distance',
                meta: null,
                isAvailable: true
            }
        ]
    },

    // ==================== COMBAT (Narrative Phase) ====================
    combatNarrative: {
        phase: 'PlayerAction',
        animalName: 'Grey Wolf',
        distanceZone: 'Close',
        distanceMeters: 6,
        behaviorState: 'Circling',
        narrativeMessage: 'You thrust the spear. The wolf twists aside — your blade grazes its ribs. It snarls, backing a step.'
    },

    // ==================== COMBAT (Outcome Phase) ====================
    combatOutcome: {
        phase: 'Outcome',
        animalName: 'Grey Wolf',
        outcome: {
            result: 'victory',
            message: 'The wolf collapses, breath rattling. You stand over it, spear ready, until it goes still. Your hands shake.',
            rewards: ['Wolf Carcass (22 kg)', 'Grey Wolf Pelt']
        }
    },

    // ==================== FIRE (Starting Mode) ====================
    fireStarting: {
        mode: 'starting',
        tools: [
            { id: 'bow_drill', name: 'Bow Drill', successBonus: 25, durability: 45, maxDurability: 100 },
            { id: 'hand_drill', name: 'Hand Drill', successBonus: 10, durability: 30, maxDurability: 80 }
        ],
        tinders: [
            { id: 'birch_bark', name: 'Birch Bark', successBonus: 20, amount: 3 },
            { id: 'dry_grass', name: 'Dry Grass', successBonus: 10, amount: 5 }
        ],
        fuels: [
            { id: 'pine_logs', name: 'Pine Logs', weight: 3.2 },
            { id: 'birch_logs', name: 'Birch Logs', weight: 1.8 }
        ],
        fire: {
            isActive: false,
            temperatureF: 32,
            heatOutputF: 0,
            phase: 'Dead',
            pitType: 'Open',
            windProtection: 0.0,
            fuelEfficiency: 1.0,
            totalKg: 0,
            maxCapacityKg: 15,
            burningKg: 0,
            minutesRemaining: 0,
            burnRateKgPerHour: 0,
            finalSuccessPercent: 0,
            charcoalKg: 0.3
        }
    },

    // ==================== FIRE (Tending Mode) ====================
    fireTending: {
        mode: 'tending',
        tools: [],
        tinders: [],
        fuels: [
            { id: 'pine_logs', name: 'Pine Logs', weight: 3.2 },
            { id: 'birch_logs', name: 'Birch Logs', weight: 1.8 },
            { id: 'oak_logs', name: 'Oak Logs', weight: 2.1 }
        ],
        fire: {
            isActive: true,
            temperatureF: 720,
            heatOutputF: 85,
            phase: 'Steady',
            pitType: 'Stone',
            windProtection: 0.7,
            fuelEfficiency: 1.3,
            totalKg: 4.2,
            maxCapacityKg: 20,
            burningKg: 2.1,
            minutesRemaining: 142,
            burnRateKgPerHour: 0.9,
            finalSuccessPercent: null,
            charcoalKg: 0.8
        }
    },

    // ==================== COOKING ====================
    cooking: {
        waterLiters: 2.4,
        rawMeatKg: 3.2,
        cookedMeatKg: 1.5,
        options: [
            {
                id: 'cook_meat_quick',
                label: 'Cook meat (quick)',
                icon: 'outdoor_grill',
                timeMinutes: 15,
                isAvailable: true,
                disabledReason: null
            },
            {
                id: 'cook_meat_thorough',
                label: 'Cook meat (thorough)',
                icon: 'outdoor_grill',
                timeMinutes: 30,
                isAvailable: true,
                disabledReason: null
            },
            {
                id: 'melt_snow',
                label: 'Melt snow for water',
                icon: 'water_drop',
                timeMinutes: 20,
                isAvailable: true,
                disabledReason: null
            },
            {
                id: 'render_fat',
                label: 'Render fat to tallow',
                icon: 'science',
                timeMinutes: 45,
                isAvailable: false,
                disabledReason: 'No fat available'
            }
        ],
        lastResult: null
    },

    // ==================== TRANSFER ====================
    transfer: {
        playerTitle: 'Carrying',
        playerCurrentWeightKg: 12.3,
        playerMaxWeightKg: 20,
        playerItems: [
            { id: 'raw_meat_1', category: 'Food', name: 'Raw Venison', weight: 2.1, isAggregated: false, count: 1 },
            { id: 'hide_1', category: 'Materials', name: 'Hide', weight: 1.2, isAggregated: false, count: 1 },
            { id: 'bone_stack', category: 'Materials', name: 'Bone', weight: 0.8, isAggregated: true, count: 3 }
        ],
        storageTitle: 'Camp Storage',
        storageCurrentWeightKg: 35.6,
        storageMaxWeightKg: 999,
        storageItems: [
            { id: 'cooked_meat_stack', category: 'Food', name: 'Cooked Meat', weight: 3.5, isAggregated: true, count: 7 },
            { id: 'dried_meat_stack', category: 'Food', name: 'Dried Meat', weight: 1.8, isAggregated: true, count: 5 },
            { id: 'pine_logs_stack', category: 'Fuel', name: 'Pine Logs', weight: 12.0, isAggregated: true, count: 15 },
            { id: 'birch_logs_stack', category: 'Fuel', name: 'Birch Logs', weight: 8.3, isAggregated: true, count: 10 },
            { id: 'flint_stack', category: 'Materials', name: 'Flint', weight: 2.1, isAggregated: true, count: 8 }
        ]
    },

    // ==================== BUTCHER ====================
    butcher: {
        animalName: 'Caribou',
        decayStatus: 'good',
        remainingKg: 45.2,
        isFrozen: true,
        modeOptions: [
            {
                id: 'butcher_quick',
                label: 'Quick butchering',
                description: 'Fast but wasteful',
                meta: '20 min',
                isAvailable: true
            },
            {
                id: 'butcher_careful',
                label: 'Careful butchering',
                description: 'Maximize yield',
                meta: '45 min',
                isAvailable: true
            },
            {
                id: 'butcher_field_dress',
                label: 'Field dress only',
                description: 'Remove organs, butcher later',
                meta: '15 min',
                isAvailable: true
            }
        ],
        warnings: []
    },

    // ==================== HAZARD ====================
    hazard: {
        hazardDescription: 'The ice here looks thin. You can see dark water beneath.',
        quickTimeMinutes: 5,
        carefulTimeMinutes: 15,
        injuryRisk: 0.35
    },

    // ==================== CONFIRM ====================
    confirm: {
        prompt: 'Drop your meat? The wolf might take it and leave you alone.'
    },

    // ==================== DEATH ====================
    death: {
        causeOfDeath: 'You bled out from your wounds.',
        timeSurvived: 'Day 12, 3:45 PM',
        finalVitality: 0,
        finalCalories: 450,
        finalHydration: 35,
        finalTemperature: 89.2
    }
};
