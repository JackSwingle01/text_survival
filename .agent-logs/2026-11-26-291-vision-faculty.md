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
