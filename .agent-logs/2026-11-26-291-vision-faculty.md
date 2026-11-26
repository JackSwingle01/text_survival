# #291: Vision: a faculty for how far a body sees

Hypothesis:
- Changes: BodyTiers (Faculty.Vision, Faculties.Vision, VisionCrippledBy, CrippledBy.NoWorkingEye), Body (Body.Vision, CrippledBy case), Crippled (prefix and phrase), Memory (Sight.ViewOf, parameter renamed to vision), Scent (Senses.Now), CapacityReadTests (two entries off), BalanceReport (vision column), GLOSSARY (Faculty, Crippled).
- Affects: view distance of a person and an animal. A fresh body has sight 1 and consciousness 1, so its view is unchanged; a tired, hungry, hurt or bleeding body now sees less far, because consciousness falls with those effects (Tired already lowers sight, so it counts twice: the spec's formula). Perception, Vitality and every verb's refusal and timing are unchanged.
- Stays green: FacultiesTests, SightTests, MemoryTests, ScentTests, CapacityReadTests, SaveFixtureTests, the fast suite.
- First red: FacultiesTests' new vision tests fail to compile on Body.Vision and Faculty.Vision.

Revised: Vitality's mean reads four faculties by hand → vision stays out of it, since the ticket says vision is read only for view distance and adding it would move vitality.
Revised: the design review found Tired, Pain and Dazed lower both sight and consciousness, so vision falls twice → kept, it is the formula the ticket names (sight times consciousness); the PR says so.
Revised: Sight.Visible and RangeTiles take the same eyes parameter BudgetTiles does → renamed to vision in all three, and ViewOf passes vision to Visible too.
Revised: MemoryTests.Eyes and SightTests read the sight capacity to compare against the view → they read Body.Vision now, which also takes their two entries off the exception list.
Revised: a view at budget zero still held the tile underfoot (CanSee's from == to), but the ticket asks an unconscious body to have no tiles in view → Sight.Visible returns nothing at vision zero (keyed on vision, not budget, so a whiteout or a near-zero vision that rounds to no range still sees its own tile); SightTests' weather-and-eyes test changed from [Centre] to empty at vision 0.
Revised: FacultiesTests' two any-body properties (every faculty a real number at or above zero; at zero exactly when it names a reason) → each gained vision, and the second was renamed DexterityPerceptionAndVision_….
Revised: the hypothesis named GLOSSARY's Crippled entry → Crippled needed nothing; Unconscious (vision at zero too) and Vitality (its faculties but vision) changed instead.
Revised (review): the pinned-view test's comment and name told history ("before vision") → they say what holds: a fresh body's view is what whole eyes see.
Revised (review): GLOSSARY's Unconscious gave the limbs as the cause of vision at zero → it names the eyes going unread too.
Revised (review): the standards review asked the vision parameter be visionPct by the Pct suffix rule → kept vision: it carries a faculty, and faculties go unsuffixed (Body.Perception, Body.Speed).
Revised (review): the standards review called Crippled's "you cannot see" prefix and VisionCrippledBy speculative, since no verb needs vision → kept: the ticket asks for the reasons, and without a prefix Crippled.Refusal would throw for the fifth faculty.
Revised (review): the spec review found an animal still counts its own tile as seen at budget zero (Senses.Sees: tile == From) → left as is: an animal's view is its budget, which is zero, and Finds on its own tile also carries food it stands on; changing that is outside view distance.
Revised (review): the spec review read the criterion "no verb's refusal or timing changes" against a predator coming into sight later for a tired body → that is view distance doing what the ticket asks; no refusal or work time reads vision. The PR says so.
Follow-up: a tired, hungry or hurt body now sees less far, which can change when an NPC or a wolf spots something; the NPC bench (NPC_BENCH=1) was not run here. The nightly bench against the baselines branch shows it.
