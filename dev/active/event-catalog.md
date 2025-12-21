
# Document 2: Event Catalog

## Organization

Events are organized by primary context and category. Each event lists:
- Trigger: Required conditions and weight modifiers
- Description: What the player sees
- Choices: Available options with conditions
- Outcomes: Weighted results for each choice with mechanical effects
- Connections: What systems this event integrates with

---

## Category 1: Stalker Arc (Tension Chain)

### Event: Strange Sound Nearby

Trigger: Always available, weight modifiers for HasMeat (+1.8x), Bleeding (+2.0x), InAnimalTerritory (+1.5x), Night (+1.3x)

Description: "You freeze. Something moved nearby — a rustle, a crack, a scrape. Then silence."

Choices:

Investigate
- 40%: Nothing. Wind, settling snow. Time +8min
- 25%: Small animal flees. Time +5min
- 20%: Fresh tracks, fresh scat. Something IS here. Time +10min, Creates Tension: Stalked (0.3)
- 15%: You find it. It finds you. Time +5min, Spawns Encounter (close range, high boldness)

Freeze and Listen
- 40%: Silence stretches. Eventually relax. Time +6min
- 30%: Movement, then nothing. It's leaving. Time +8min
- 15%: It's circling. You can hear it breathing. Time +10min, Creates Tension: Stalked (0.4), Fear effect
- 15%: Cold seeps in while you stand frozen. Time +12min, Cold effect (-6°, 20min)

Make Noise 
- 40%: Shout echoes. Nothing responds. Time +2min
- 30%: Something crashes away through brush. Time +3min
- 15%: Your voice cracks. You sound like prey. Time +2min, Fear effect (0.2), Creates Tension: Stalked (0.2)
- 15%: The noise provokes it. Time +5min, Spawns Encounter (medium range, elevated boldness)

Drop the Meat (requires HasMeat, costs 2 Food)
- 50%: Meat hits snow. You back away. It doesn't follow. Time +3min
- 30%: You hear it feeding as you leave. Time +5min, Fear effect (0.15)
- 20%: It wants more than the meat. Time +8min, Spawns Encounter (close range, very high boldness)

---

### Event: Stalker Circling

Trigger: Requires Stalked tension, weight increases with severity

Description: "You catch movement in your peripheral vision. Again. It's pacing you, staying just out of clear sight. Testing."

Choices:

Confront It Now
- Spawns Encounter using current tension severity to set boldness
- Resolves Stalked tension (fight determines outcome)

Try to Lose It
- 35%: You double back, cross water, break your trail. It works. Time +25min, Resolves Stalked
- 35%: It stays with you. You've wasted time and energy. Time +20min, Escalates Stalked (+0.2)
- 20%: You get turned around trying to lose it. Time +35min, Cold effect, Stalked persists
- 10%: Your evasion leads you somewhere unexpected. Time +30min, Discovers Location (random), Stalked persists

Keep Moving, Stay Alert
- 40%: You maintain distance. Exhausting but stable. Time +10min, Exhausted effect (0.15, 60min)
- 30%: It's getting bolder. Time +8min, Escalates Stalked (+0.15)
- 20%: It backs off. Maybe lost interest. Time +5min, Reduces Stalked severity (-0.1)
- 10%: It commits. Time +5min, Spawns Encounter (medium range)

Return to Camp (requires reasonable distance)
- 60%: You make it back. Fire deters it. Resolves Stalked, expedition ends early
- 25%: It follows to camp perimeter but won't approach fire. Resolves Stalked, expedition ends, Fear effect
- 15%: It's bolder than you thought. Attacks before you reach safety. Spawns Encounter, expedition aborts

---

### Event: The Predator Revealed

Trigger: Requires Stalked tension with severity > 0.5

Description: "You finally see it clearly. {TensionDetails — Wolf/Bear/etc.}. It's watching you from {distance}. Not hiding anymore."

Choices:

Stand Your Ground
- Transitions to full predator encounter with current state
- Player has initiative (chose the confrontation)

Bait and Ambush (requires HasMeat, HasWeapon)
- Set up ambush using meat as bait
- If successful: encounter with player advantage (surprise, prepared)
- If failed: encounter with predator advantage (saw through the trap)
- Costs meat either way

Calculated Retreat
- Slow, deliberate backward movement
- Various outcomes based on predator type and boldness
- Can resolve tension or escalate to attack

---

### Event: Ambush

Trigger: Requires Stalked tension with severity > 0.7, or Stalked + Injured + far from camp

Description: "It's done waiting."

Choices: None. This is the consequence of letting tension escalate too far.

Outcome:
- Spawns Encounter at close range with predator at high boldness
- Predator gets first action
- Resolves Stalked (one way or another)

---

## Category 2: Camp Infrastructure

### Event: Vermin Raid

Trigger: AtCamp, Food in storage > 2kg, weight increases at night

Description: "Scratching from your supply cache. Something small has found your food stores."

Choices:

Set a Trap (requires PlantFiber)
- 50%: Trap catches it. Small meat gained, problem solved. Time +15min, costs 1 PlantFiber, +0.3kg meat
- 30%: Trap fails. It escapes with some food. Time +15min, costs 1 PlantFiber, lose 0.5kg food
- 15%: Catch it — and it's not alone. Time +20min, Creates Tension: Infested (0.3), costs 1 PlantFiber
- 5%: Perfect catch. Quality fur for insulation. Time +15min, costs 1 PlantFiber, +fur material

Flood the Nest (requires Water > 1L)
- 60%: Drowned. Problem solved, but food is wet. Time +10min, costs 1 Water, some food spoiled
- 25%: They flee. Might come back. Time +10min, costs 1 Water
- 15%: The water reveals a larger tunnel system. Time +15min, costs 1 Water, Creates Tension: Infested (0.4)

Kill It Directly (requires weapon or good Manipulation)
- 45%: Quick kill. Small meat. Time +5min, +0.2kg meat
- 35%: It escapes. Took some food with it. Time +8min, lose 0.3kg food
- 15%: You destroy supplies in the attempt. Time +10min, lose 1kg food
- 5%: It bites you. Time +5min, damage to hand, possible Creates Tension: WoundUntreated (0.2)

Ignore It
- 50%: It takes what it wants and leaves. Lose 0.5-1kg food
- 30%: It's still there in the morning. Creates Tension: Infested (0.3)
- 20%: Attracts something larger. Creates Tension: Stalked (0.2) or larger scavenger event

---

### Event: Infestation Spreads

Trigger: Requires Infested tension

Description: "More scratching. More droppings. They're breeding in your walls."

Choices:

Aggressive Extermination (requires fire or smoke)
- 60%: Smoke them out. Infestation cleared, but shelter damaged. Time +30min, Resolves Infested, shelter quality -0.2
- 30%: Partially successful. Severity reduced. Time +25min, Reduces Infested (-0.3)
- 10%: Fire spreads. Shelter badly damaged. Time +20min, Resolves Infested, major shelter damage or destruction

Rebuild Storage
- Move food to new, secured location
- Time +45min, costs materials
- Doesn't resolve infestation but protects remaining food
- Infestation eventually dies out without food source

Abandon Camp (drastic)
- Move camp to new location
- Resolves Infested but massive time/resource cost
- Old camp location retains Infested status

---

### Event: Shelter Groans

Trigger: AtCamp, HasShelter, heavy precipitation or high wind, weight increases with storm severity

Description: "A crack from above. Your shelter is taking strain. Snow load or wind — something's giving."

Choices:

Brace It Now
- 55%: You hold it together. Shelter survives. Time +10min, Exhausted effect
- 30%: Partial collapse. Shelter damaged but standing. Time +15min, Modifies ShelterFeature (quality -0.3)
- 10%: You brace it successfully and spot a weakness to reinforce later. Time +12min, shelter quality +0.1 after repair
- 5%: It comes down on you. Time +5min, damage (blunt), Removes ShelterFeature, Cold exposure

Reinforce with Materials (requires Fuel)
- 70%: Solid reinforcement. Shelter improved. Time +20min, costs 2 Fuel, Modifies ShelterFeature (quality +0.2)
- 20%: Uses more material than expected but works. Time +25min, costs 3 Fuel
- 10%: The materials aren't enough. Still needs bracing. Time +15min, costs 2 Fuel, Creates Tension: ShelterWeakened (0.3)

Evacuate
- Leave shelter before it collapses
- 70%: Get out clean. Shelter collapses behind you. Time +3min, Removes ShelterFeature, you're exposed but unharmed
- 30%: Almost clear. Debris catches you. Time +5min, minor damage, Removes ShelterFeature

---

### Event: Choking Smoke

Trigger: AtCamp, FireBurning, HighWind or poor ventilation (shelter without proper opening)

Description: "The wind shifts. Thick smoke floods back into your space. You can't breathe."

Choices:

Douse the Fire
- Fire immediately extinguished
- Safe lungs, but now cold
- Time +2min, Removes HeatSourceFeature (or sets to embers), temperature drops rapidly

Endure and Ventilate
- 50%: You create an opening, redirect the smoke. Time +15min, Coughing effect (0.3, 60min)
- 30%: Takes longer, more smoke inhaled. Time +25min, Coughing effect (0.5, 90min)
- 15%: You improve the ventilation permanently. Time +20min, Coughing effect (0.2, 30min), shelter gains ventilation (reduces future smoke events)
- 5%: You pass out briefly. Time +10min, damage (internal), severe Coughing effect

Evacuate Temporarily
- Leave shelter, wait for wind to shift
- Time +30-60min depending on weather
- Severe Cold exposure while waiting
- Fire may die during absence

---

### Event: Embers Scatter

Trigger: AtCamp, Fire in Dying or Embers phase, wind event

Description: "A gust catches the dying fire. Embers scatter across your camp."

Choices:

Stomp Them Out
- 60%: Quick response. Minor burns to feet. Time +3min, minor damage to feet
- 30%: All contained, no harm. Time +5min
- 10%: Miss one. Something catches fire. Time +8min, lose some supplies or shelter damage

Protect the Fire
- Prioritize keeping embers together for relight
- 50%: Save the embers but some scatter. Time +5min, one supply item damaged
- 40%: Protect fire successfully. Time +8min
- 10%: Wind wins. Fire goes out, embers scattered. Time +5min, Removes HeatSourceFeature, need full restart

Let It Burn Out
- Accept fire loss, protect other things
- Embers die, fire must be fully restarted
- But camp and supplies safe

---

## Category 3: Body Events

### Event: The Shakes

Trigger: Low calories + Low temperature, or has Hypothermia effect beginning

Description: "It's not just the cold. Your blood sugar has crashed. Your hands are trembling so violently you can barely hold anything."

Choices:

Eat Immediately (requires HasFood)
- 70%: Warmth spreads through you. Shaking stops. Time +5min, costs 1 Food, Fed effect (0.3, 90min)
- 20%: Takes the edge off. Still shaky. Time +5min, costs 1 Food
- 10%: You eat too fast. Nauseous. Time +8min, costs 1 Food, Nauseous effect (0.2, 30min)

Eat Immediately (when NoFood — different outcome set)
- Panic. There's nothing to eat.
- Fear effect, Shaken effect
- Situation becomes urgent

Warm Up by Fire (requires NearFire)
- 60%: Heat helps. Shaking subsides. Time +20min, removes Cold effects
- 30%: Takes longer but works. Time +35min
- 10%: You doze off by the fire. Time +60min, well rested but time lost

Push Through
- 40%: Mind over matter. Shaking fades to background. Time +0, Clumsy effect (0.3, 60min — Manipulation reduced)
- 35%: You drop something. Minor setback. Time +5min, Clumsy effect
- 15%: Can't function. Forced rest. Time +30min
- 10%: You push through and acclimate. Time +0, Hardened effect (minor cold resistance, 2hr)

---

### Event: Gut Wrench

Trigger: Ate raw meat or questionable food within last 2 hours

Description: "Your stomach twists. The {food type} isn't sitting right. At all."

Choices:

Induce Vomiting
- Lose the calories (300-500), lose hydration (0.5L)
- But avoid worse effects
- Time +10min, immediate discomfort but no lasting effect

Bear It
- 40%: Passes eventually. Nauseous effect (0.4, 2hr)
- 30%: Worse than expected. Nauseous effect (0.6, 3hr), some damage (internal)
- 20%: Your body handles it. IronGut buff (raw food tolerance for 24hr)
- 10%: Serious food poisoning. Severe Nauseous effect, capacity penalties, ongoing damage

Herbal Treatment (requires PlantFiber or medicinal plants)
- 70%: Settles your stomach. Nauseous effect (0.2, 30min). Costs materials.
- 20%: Doesn't help much. Nauseous effect (0.4, 90min). Costs materials.
- 10%: Makes it worse somehow. Nauseous effect (0.5, 2hr). Costs materials.

---

### Event: Frozen Fingers

Trigger: Extended work in cold, low temperature, no gloves/hand protection

Description: "Your fingers have gone white. You can't feel them properly. This is frostbite territory."

Choices:

Warm Them Now
- 60%: Painful but effective. Feeling returns. Time +10min, minor damage to hands
- 25%: Takes longer. More pain. Time +20min, moderate damage to hands
- 10%: Catch it in time. No lasting damage. Time +8min
- 5%: Too late for some tissue. Time +15min, significant damage to hands, permanent minor Manipulation reduction

Tuck and Continue
- Hands under arms, keep working as able
- 50%: Circulation returns slowly. FrozenFingers effect (Manipulation -30%, 45min)
- 30%: Still losing feeling. FrozenFingers effect (severe), need to stop soon
- 20%: Body heat isn't enough. Frostbite damage accumulates

Use Fire (requires NearFire)
- 80%: Direct heat restores circulation. Time +8min
- 15%: Too close. Minor burn but fingers saved. Time +8min, small burn damage
- 5%: Numb fingers don't feel the heat. Burn damage before you notice

---

### Event: Muscle Cramp

Trigger: Low calories or low hydration, recent exertion, cold muscles

Description: "Sharp pain shoots through your leg. The muscle seizes, locks up. You can't put weight on it."

Choices:

Work It Out
- 50%: Cramp releases. Sore but mobile. Time +8min, Sore effect (0.15, 60min)
- 25%: Takes a while but releases. Time +15min, Sore effect (0.2, 45min)
- 15%: Won't release fully. Limping. Time +12min, SprainedAnkle effect (0.3)
- 10%: Make it worse forcing it. Time +10min, damage (muscle strain), SprainedAnkle effect (0.45)

Push Through
- 30%: Movement helps. Cramp fades. Time +5min, Sore effect (0.1, 30min)
- 35%: Gets worse before better. Time +12min, SprainedAnkle effect (0.25)
- 20%: Something tears. Time +8min, damage (muscle tear), SprainedAnkle effect (0.5)
- 15%: Leg gives out. You fall. Time +15min, damage (fall), SprainedAnkle effect (0.6)

Eat Something (requires HasFood)
- 55%: Food helps. Cramp releases quickly. Time +8min, costs 1 Food, Fed effect
- 30%: Doesn't help the cramp but you feel steadier. Time +10min, costs 1 Food, Sore effect
- 15%: Hard to eat through the pain. Nauseous. Time +12min, costs 1 Food, Nauseous effect (0.25, 30min)

Apply Heat (requires NearFire)
- 70%: Heat loosens it. Cramp releases smoothly. Time +10min
- 20%: Takes a while but warmth helps. Time +18min
- 10%: Too close. Minor burn, but cramp's gone. Time +12min, minor burn damage

---

### Event: Old Ache

Trigger: Player has history of healed injury (tracked), or heavy load carried, or prolonged cold exposure

Description: "The damp cold settles into your joints. An old injury flares up, or your body simply protests the abuse."

Choices:

Stretch and Rest
- Rest for an hour. Lose the time.
- Removes fatigue, restores some capacity
- Time +60min, positive effect

Work Through It
- 60%: Discomfort but manageable. Stiff effect (Moving -15%, 6hr)
- 30%: Worse than expected. Stiff effect (Moving -25%, 4hr)
- 10%: Your body knows better than you. Forced rest anyway. Time +30min, then Stiff effect

Adjust Load
- Drop weight, change how you carry things
- Time +10min
- Reduces severity but requires inventory management

---

### Event: Toothbreaker

Trigger: Eating frozen food, tough/dried meat, or gnawing on bones

Description: "You bite down on something hard. A crack echoes in your skull. That was either the food or your tooth."

Choices:

Spit It Out
- Lose the rest of the food (partial calorie loss)
- Mouth checked, no tooth damage
- Time +2min

Swallow Through Blood
- Get the calories
- 60%: Tooth cracked but holding. Pain effect (minor, 24hr), eating malus
- 30%: Tooth fine, just cut your gum. Minor damage, heals
- 10%: Tooth broken. Pain effect (moderate), eating significantly impaired, may need extraction later

Check Carefully
- Time +5min to examine
- Know the actual damage before deciding
- Then choose how to proceed

---

### Event: Vision Blur

Trigger: Hydration < 30% or cold extremity effects affecting blood flow to head

Description: "Your vision swims. Hard to focus. The world keeps sliding sideways."

Choices:

Rub Eyes and Push On
- 50%: Clears momentarily. VisionBlur effect (Manipulation -15%, 30min)
- 30%: Doesn't help. VisionBlur effect (Manipulation -25%, 45min)
- 20%: Makes it worse. VisionBlur effect (severe), minor damage from eye irritation

Rest Eyes
- Close eyes, rest for 15 minutes
- Time +15min, effect avoided or minimized
- But you're vulnerable while eyes closed

Snow-Wipe Face
- Cold shock to restore alertness
- 70%: Works. Cold effect but vision clear. Minor Cold exposure
- 30%: Too cold. Cold effect (significant), vision still blurry

Drink Water (requires HasWater)
- If dehydration is cause, this fixes it
- 60%: Hydration helps. Vision clears. Time +5min, costs water
- 40%: Not just dehydration. Still blurry. Time +5min, costs water, VisionBlur effect persists

---

## Category 4: Travel/Expedition Events

### Event: Treacherous Footing

Trigger: Traveling, weight modifiers for Injured (+1.5x), Slow (+1.3x), poor visibility

Description: "The ground ahead is wrong — ice beneath snow, loose rocks, unstable surface."

Choices:

Test Each Step
- 70%: Find a safe path through. Time +10min
- 20%: Despite caution, ground shifts. Catch yourself. Time +12min, minor damage
- 10%: Too slow. Cold accumulates. Time +15min, Cold effect

Detour
- 70%: Costs time but avoids hazard. Time +18min
- 20%: Detour has its own problems. Time +25min
- 10%: Get turned around. Time +30min, Cold effect

Push Through
- 40%: Make it through quickly. Time +3min
- 30%: Foot breaks through. Wrenched but mobile. Time +5min, damage (blunt)
- 20%: Slip hard. Ankle twists. Time +10min, SprainedAnkle effect (0.4)
- 10%: Complete fall. Hurt badly. Time +15min, significant damage, SprainedAnkle effect (0.5)

---

### Event: Exposed Position

Trigger: Traveling or Working, Outside, HighWind

Description: "You've wandered into an exposed area. Wind cuts through you. No cover nearby."

Choices:

Move Fast
- 45%: Through it quickly. Cold but moving. Time +5min, Cold effect (-10°, 20min)
- 30%: Longer than expected. Wind brutal. Time +10min, Cold effect (-15°, 30min)
- 15%: Stumble in wind. Fall. Time +8min, Cold effect, damage
- 10%: Wind knocks you down. Disoriented. Time +15min, severe Cold effect, damage

Find Cover First
- 50%: Small windbreak. Helps. Time +12min, Cold effect (minor)
- 30%: Nothing useful. Wasted time in wind. Time +15min, Cold effect (moderate)
- 15%: Decent hollow. Warm up before continuing. Time +18min, Cold effect (minimal)
- 5%: "Shelter" funnels wind. Worse than open. Time +10min, Cold effect (severe)

Emergency Fire (requires HasFuelPlenty)
- 45%: Fire catches. Warmth floods back. Time +15min, costs 2 Fuel
- 30%: Wind makes it hard. Uses more fuel. Time +20min, costs 3 Fuel, Cold effect (minor)
- 15%: Won't catch. Wasted fuel. Time +12min, costs 2 Fuel, Cold effect (moderate)
- 10%: Sparks in wind. Burn yourself. Time +10min, costs 2 Fuel, burn damage

Turn Back
- 70%: Backtracking costs time but avoids worst. Time +20min, Cold effect (minor)
- 20%: Way back harder than remembered. Time +30min, Cold effect (moderate)
- 10%: Get turned around. Lost time, cold. Time +40min, Cold effect (significant)

---

### Event: Natural Shelter Spotted

Trigger: Working or exploring, terrain-dependent (rocky, forested)

Description: "A defensible overhang. A dense thicket. A hollow in the hillside. Natural shelter, if improved."

Choices:

Improve It Now (requires materials — Fuel or PlantFiber)
- 60%: Solid work. This is shelter now. Time +45min, costs materials, Adds ShelterFeature (quality 0.5) to location
- 25%: Takes longer but done right. Time +60min, costs materials, Adds ShelterFeature (quality 0.6)
- 10%: Not as good as hoped. Partial shelter. Time +40min, costs materials, Adds ShelterFeature (quality 0.3)
- 5%: Collapses during construction. Wasted effort. Time +30min, costs materials, no shelter

Note for Later
- Discovers Location: "Sheltered Hollow" added to zone as connected location
- When visited, player can choose to develop it
- Time +5min

Use Temporarily
- Shelter benefit for this expedition/work session only
- No permanent change
- Time +10min, reduced Cold exposure for remainder of work

Ignore
- Nothing. But you know it's there.
- Time +0

---

### Event: Water Source

Trigger: Exploring or working, random with terrain modifiers (valleys more likely)

Description: "You hear it before you see it — running water, or the promising crack of ice over a stream."

Choices:

Investigate Thoroughly
- 50%: Fresh water source. Location gains WaterFeature. Time +20min, Adds WaterFeature to location
- 20%: Water AND game trails. Animals need water too. Time +25min, Adds WaterFeature, Adds/Enhances AnimalTerritoryFeature
- 15%: Thin ice. You break through. Time +15min, Cold effect (severe), damage, possible equipment loss
- 10%: Contaminated or mineral. Not drinkable. Time +20min
- 5%: Perfect source. Clear, accessible, sheltered. Time +25min, Adds WaterFeature (high quality), Discovers Location if not already here

Drink Now
- 70%: Safe. Immediate hydration benefit. Time +5min, hydration restored
- 20%: Questionable. Drink anyway. Time +5min, hydration restored, 30% chance Nauseous later
- 10%: Bad water. Time +5min, Nauseous effect, possible worse

Mark and Continue
- Discovers Location: "Stream" or "Frozen Pond" added if not already here
- Can return later
- Time +3min

Ignore
- Nothing happens
- But you might need water later and you passed this up

---

### Event: Old Campsite

Trigger: Working or exploring, random

Description: "Signs of a previous camp. Fire ring, flattened snow. Someone survived here. For a while."

Choices:

Search Thoroughly
- 35%: Picked clean. Wasted effort. Time +20min
- 25%: Some scraps they couldn't carry. Time +25min, RewardPool: AbandonedCamp
- 15%: Signs of struggle. Blood in snow. You take what's left. Time +20min, RewardPool: BasicSupplies, Fear effect
- 12%: A cache they hid and never returned for. Time +30min, RewardPool: HiddenCache
- 8%: You find remains. Human. You take their gear. Time +15min, RewardPool: AbandonedCamp (better), Fear effect (significant)
- 5%: Something is still here. Watching. You leave fast. Time +10min, Fear effect, Creates Tension: Stalked (0.3), expedition may abort

Scavenge Fast
- 40%: Fire ring has usable charcoal. Time +8min, RewardPool: Tinder/Charcoal
- 35%: Nothing obvious. At least you didn't waste much time. Time +8min
- 20%: A few sticks they left stacked. Time +10min, RewardPool: BasicSupplies
- 5%: Cut yourself on hidden debris. Time +8min, damage (sharp), possible Creates Tension: WoundUntreated (0.2)

Use the Infrastructure
- 55%: Good setup. Site prepared for camp. Time +0, Adds partial ShelterFeature or Adds HeatSourceFeature (prepared fire ring, easier start)
- 30%: Serviceable. Needs repairs. Time +10min, partial feature
- 10%: Worse than starting fresh. They did it wrong. Time +15min
- 5%: Something's wrong here. Bad feeling. Time +8min, Fear effect

Keep Distance
- 70%: You skirt the site. Probably nothing useful anyway. Time +5min
- 20%: Circle wide. Costs time but feels safer. Time +12min
- 10%: Something about the site sticks with you. Time +5min, Fear effect (minor)

---

## Category 5: Wildlife/Echoes

### Event: Rustle at Camp Edge

Trigger: AtCamp, HasMeat or HasFood, quiet period (no recent action), night weight modifier

Description: "Rustling at the camp perimeter. Something drawn by the scent of {food type}."

Choices:

Investigate
- 40%: Weak rabbit. Easy catch. Time +10min, small meat gained
- 30%: Fox. Retreats but not far. Time +8min, Creates Tension: Stalked (0.15) (minor)
- 20%: Nothing there now. Tracks suggest small scavenger. Time +8min
- 10%: Something larger than expected. Time +5min, Fear effect, Creates Tension: Stalked (0.4)

Throw Rock/Make Noise (requires Stone or none)
- 60%: Scares it off. Time +2min
- 25%: Miss. Noise brings it closer briefly, then flees. Time +3min
- 15%: Provokes aggression in something larger than expected. Time +5min, Spawns Encounter (medium distance, moderate boldness)

Ignore
- 50%: Passes by. Nothing taken. Time +0
- 35%: Steals some food. Time +0, lose 0.3-0.5kg food
- 15%: Emboldens it. Comes back later. Creates Tension: Stalked (0.2)

---

### Event: Distant Carcass Stench

Trigger: Wind direction favorable, InAnimalTerritory or adjacent to

Description: "The wind brings a smell — death, recent. Something died nearby, or something killed nearby."

Choices:

Scout Toward It
- 35%: Find the carcass. Some meat left. Time +30min, RewardPool: BasicMeat (possibly spoiled)
- 25%: Find the carcass. Something's already there. Time +25min, Spawns Encounter (scavenger, may be deferential)
- 20%: Tracks lead to a hunting ground. Time +35min, Discovers Location: "Killing Ground" with enhanced AnimalTerritory
- 15%: Can't find it. Wind shifted. Time +30min
- 5%: Find it. And what killed it. Time +20min, Spawns Encounter (predator, defensive of kill)

Mark the Direction
- Note for later investigation
- Discovers Location: "Carcass Site" (direction-based)
- Time +3min

Avoid the Area
- Something that kills is probably around
- Adjust route to avoid
- Time +15min (detour)

---

### Event: Raven Call

Trigger: Daytime, low food, open terrain

Description: "Ravens circling overhead. They've spotted something — or someone. They're watching you."

Choices:

Follow Them
- Ravens often lead to carcasses or resources
- 40%: They lead you to a small carcass. Time +25min, RewardPool: BasicMeat/Bones
- 30%: They lead nowhere. Wasting your time. Time +30min
- 20%: They lead you to another predator's kill. Time +25min, Spawns Encounter or Creates Tension: Stalked
- 10%: They lead you to something unexpected. Time +30min, Discovers Location or RewardPool: Relics

Try to Catch One (requires trap materials or throwing weapon)
- 30%: Catch one. Small meat, feathers. Time +20min, small meat, feathers (insulation material)
- 50%: Miss. They're too clever. Time +15min
- 20%: They remember. Ravens are vindictive. Raven harassment for rest of day (minor distraction penalties)

Ignore
- They're just birds
- Time +0
- But they might have led you somewhere useful

---

### Event: Something Watching

Trigger: Working, HasPredators in territory, weight modifiers for HasMeat (+3x), Injured (+2x)

Description: "The hair on your neck stands up. Something is watching from the shadows."

Choices:

Make Noise
- 60%: Whatever it was slinks away. Time +5min
- 25%: It doesn't retreat. Testing you. Time +10min, Fear effect, Creates Tension: Stalked (0.3)
- 10%: Your noise provokes it. Time +5min, Spawns Encounter
- 5%: Nothing there. Just paranoia. Time +3min, but Shaken effect

Finish Quickly and Leave
- Cut work short. Get out.
- Time +3min (aborts current work)
- Expedition continues but this location is "hot"

Try to Spot It
- 40%: Just a fox. Watching but not threatening. Time +8min
- 35%: See it now — keeping distance. Time +10min, Fear effect, Creates Tension: Stalked (0.25)
- 15%: Make eye contact. It takes that as challenge. Time +5min, Spawns Encounter
- 10%: Can't see it but you KNOW it's there. Time +10min, Fear effect (significant), Creates Tension: Stalked (0.4)

---

## Category 6: Discovery/Opportunity

### Event: Glint in the Ashes

Trigger: AtCamp, tending fire for 10+ minutes, random opportunity

Description: "A glint catches your eye in the ash pile. Something half-buried."

Choices:

Dig Carefully (requires cutting tool)
- 45%: Usable stone tool (sharp rock or similar). Time +5min, tool gained
- 30%: Bone fragment. Useful for crafting. Time +5min, bone material
- 15%: Nothing but charred debris. Time +5min
- 10%: Cut yourself on something sharp. Time +5min, minor damage to hand

Stir the Ashes
- 65%: Charred tinder. Useful. Time +3min, small tinder gain
- 25%: Nothing. Time +3min
- 10%: Scatter embers. Minor hazard. Time +5min, possible minor burn

Ignore
- Probably nothing
- Time +0

---

### Event: Melting Reveal

Trigger: AtCamp, Fire burning for extended period, ground not frozen solid

Description: "The heat from your fire has melted the permafrost beneath. Something's emerging from the ground."

Choices:

Excavate
- 40%: Previous occupant's cache. Time +15min, RewardPool: HiddenCache
- 30%: Bones. Old. Animal. Useful for tools. Time +12min, RewardPool: Bones
- 15%: Nothing useful. Rocks and frozen dirt. Time +15min
- 10%: Something strange. A relic from before. Time +20min, RewardPool: Relics
- 5%: Disturb something you shouldn't have. Time +10min, Fear effect (superstitious? real?)

Let It Emerge
- Wait and see what the heat reveals
- Time +30min (passive)
- Less control but less effort

Cover It Back Up
- You don't want to know
- Time +5min
- Whatever it was stays buried

---

### Event: Unexpected Yield

Trigger: Working (foraging, crafting, any camp work), random positive

Description: "As you work, you notice something useful you almost missed."

Choices: (Single positive outcome — no real choice, just flavor)

Take It

Outcomes based on context:
- If foraging: Extra materials found. RewardPool: CraftingMaterials
- If crafting: Leftover materials salvaged. Return partial cost
- If tending fire: Reusable ember, quality charcoal. Tinder gained
- If butchering: Extra usable sinew/bone. RewardPool: Bones

This is a pure positive event to break up the grind.

---

### Event: Driftwood/Debris

Trigger: Near water location, or after storm, random opportunity

Description: "Debris washed up or blown in — wood, branches, maybe more."

Choices:

Haul It In
- 60%: Good fuel. Time +15min, +2-4kg wood (fuel)
- 25%: Mixed quality. Some usable. Time +15min, +1-2kg wood
- 10%: Excellent find. Dry, quality wood. Time +20min, +3-5kg quality fuel
- 5%: Something else caught in the debris. Time +20min, fuel + RewardPool: BasicSupplies

Check for More
- Investigate source, might be more
- 30%: Find the source — recurring opportunity. Time +30min, fuel gained, Discovers Location: debris field
- 50%: Just this batch. Time +20min, fuel gained
- 20%: Nothing more. Time +25min, minor fuel

Leave It
- Not worth the effort right now
- Time +0
- But fuel is always valuable...

---

## Category 7: Psychological/Perception Events

### Event: Fugue State

Trigger: Performing repetitive work for 2+ hours continuous

Description: "You blink, and the sun has moved. You don't remember the last hour. You kept working, but you were somewhere else."

Choices: None — this happens TO you.

Outcome:
- Time advances +90-120 minutes automatically
- Work output increased (you were efficient while dissociated)
- But: Hydration and Hunger drained significantly more than normal (you forgot to maintain yourself)
- Possible: Missed an event that would have fired (you were "away")

This represents the psychological cost of monotonous survival — your mind protects itself by checking out, but your body pays for the neglect.

---

### Event: Paranoia

Trigger: Night, low fire or no fire, solitude, recent fear effects

Description: "You are certain—absolutely certain—you see eyes reflecting at the edge of the firelight."

Choices:

Throw Fuel on Fire
- Fire immediately boosted (consumes 2 Fuel)
- Fear subsides
- Time +3min
- Light reveals: nothing there (probably)

Investigate
- Step out into the dark
- 60%: Nothing. Your mind playing tricks. Time +8min, Shaken effect
- 25%: Something WAS there but fled. Time +10min, Creates Tension: Stalked (0.2)
- 15%: Something is there. Spawns Encounter

Huddle by Fire
- Do nothing. Try to ignore it.
- Cannot sleep for next 4 hours (Fear prevents rest)
- Time +0
- Maybe it goes away. Maybe it doesn't.

Wait and Watch
- Stay alert, see if it moves
- 50%: Nothing moves. Eventually you relax. Time +30min, Exhausted effect (from tension)
- 30%: You see it move away. Real but not attacking. Time +20min, Fear effect, Creates Tension: Stalked (0.15)
- 20%: It was never there. But you've wasted time being afraid. Time +25min, Shaken effect

---

### Event: Moment of Clarity

Trigger: Random low chance, weighted higher when struggling (low stats but surviving)

Description: "Your mind clears. For a brief moment, everything makes sense. You see your situation with perfect clarity."

Choices:

Act on It
- 70%: You notice something you'd been missing — better route, overlooked resource, more efficient approach. Time +5min, small mechanical benefit (RewardPool: BasicSupplies, or time saved on next action)
- 20%: The clarity shows you a problem you hadn't acknowledged. Time +5min, knowledge but possibly Fear effect if situation is bad
- 10%: You see a solution to something that's been bothering you. Time +5min, resolves a minor ongoing issue or reveals a Discovered Location

Rest in the Feeling
- Don't force it. Let clarity come naturally.
- Time +15min
- General positive effect (Rested or Focused, minor)

Immediately Apply to Work
- 60%: Increased efficiency. Next work action time reduced.
- 30%: Overreach. Clarity fades and you've just wasted the moment. Time +0
- 10%: Breakthrough. Something you craft or do gains quality bonus.

---

## Category 8: Wound/Infection Arc

### Event: The Wound Festers

Trigger: Requires WoundUntreated tension, triggers after X hours since injury

Description: "The wound on your {body part} is red, swollen. Hot to the touch. This is infection."

Choices:

Clean It Properly (requires Water)
- 70%: Thorough cleaning. Infection stopped. Time +15min, costs Water, Resolves WoundUntreated, minor ongoing healing effect
- 20%: Cleaned but damage done. Time +15min, costs Water, Resolves WoundUntreated, but Fever effect (mild) remains
- 10%: Too late for just cleaning. Time +10min, costs Water, Escalates WoundUntreated (+0.3), need more aggressive treatment

Cauterize (requires NearFire)
- 60%: Brutal but effective. Time +10min, additional burn damage but Resolves WoundUntreated
- 25%: Effective but traumatic. Time +10min, burn damage, Fear effect, Resolves WoundUntreated
- 10%: Not thorough enough. Time +10min, burn damage, Reduces WoundUntreated severity but doesn't resolve
- 5%: You can't do it. The pain stops you. Time +5min, tension remains

Herbal Treatment (requires MedicinalPlants or PlantFiber)
- 50%: Poultice draws out infection. Time +20min, costs materials, Resolves WoundUntreated
- 30%: Helps but slow. Time +20min, costs materials, Reduces WoundUntreated severity (-0.3)
- 20%: Not effective. Infection continues. Time +20min, costs materials, tension remains

Ignore It
- Infection spreads. Escalates WoundUntreated (+0.2)
- Time +0 but consequences coming
- At high severity: Fever effect, capacity penalties, possible death

---

### Event: Fever Sets In

Trigger: WoundUntreated severity > 0.6

Description: "You're burning up. Chills and sweats. The infection has spread."

Choices:

Aggressive Treatment (requires Fire + Water or Medicinal materials)
- All-out effort to fight the infection
- High time cost, resource cost
- Best chance to resolve before it kills you
- Resolves WoundUntreated if successful, severe Exhausted effect either way

Rest and Fight It
- Your body vs. the infection
- 40%: Body wins. Fever breaks after 6-12 hours. Resolves WoundUntreated, severe Exhausted
- 40%: Stalemate. Fever continues. Tension remains.
- 20%: Body loses. Escalates WoundUntreated, condition critical

Keep Working
- Deny the fever, push on
- Severe capacity penalties (Fever effect)
- Infection continues to spread
- Death possible if not addressed

---

## Summary Statistics

Total Events: 45+

By Category:
- Stalker Arc: 4 (tension chain)
- Camp Infrastructure: 5
- Body Events: 8
- Travel/Expedition: 5
- Wildlife/Echoes: 5
- Discovery/Opportunity: 6
- Psychological: 3
- Wound/Infection Arc: 2

System Integrations:
- Tension creation: 15+ events
- Predator encounter spawning: 8+ events
- Feature addition: 5+ events
- Location discovery: 6+ events
- Tool/equipment targeting: 3+ events
- Chained events: 5+ chains

Context Distribution:
- Camp-only: ~10 events
- Expedition-only: ~12 events
- Always available: ~23 events

---

This catalog provides a foundation. Events can be added incrementally — start with core tension arcs (Stalker, Infection) and camp variety, then expand to discovery and psychological events.