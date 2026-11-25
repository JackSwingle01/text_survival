# #127: Dirty, and washing

Hypothesis:
- Changes: `Effects` (EffectKind.Dirty, appended; effects.json row, names.json word), `Body` survival tick (ProcessDirty beside ProcessBloody: time plus sweat severity; Dirty joins the Set levels; `Body.Wash` clears Dirty and Bloody and wets the body), `Work` (CutMinute adds Dirty; the Wash activity: refusal, session, minute, finishing reason), `Scent.Of` (DirtyLevel × Dirty), `Activity` (Wash record), `Sim` (Finishing, HoldOf), `Verbs.At` (offer), `WorldEvent`/`Narration` (Washed), Web `ViewBuilder.KindOf`, Cli, save fixture.
- Affects: a person's scent (now higher the longer they go unwashed, so wolves smell a person from a little further); Bloody's only remover besides drying; save format hash (new enum value, new derived types).
- Stays green: ScentTests, SurvivalTests (Bloody drying), WorkTests (Butcher, Drink), VerbsTests (every verb true), SaveTests after fixture regen, ContentStrictTests, ArchitectureTests.
- First red: DirtyTests.ADayOfRest_LeavesAPersonDirty fails to compile on EffectKind.Dirty.

Numbers (sources):
- Dirty from time 0.004/hour (~0.1 a day; fully dirty in about ten days unwashed, a pace set so a week's trip shows, no field source).
- Dirty from sweat +0.04/hour at full sweat severity (a day's heavy sweating doubles a day's grime; judgement).
- Dirty from butchering 0.1/hour of cutting (a three-hour field dressing leaves you as dirty as three days' living; judgement).
- Scent: DirtyLevel 0.2 at full Dirty, a quarter of Bloody's 0.8: grime adds to the body's own odour, never as much as fresh blood. A fully dirty person (0.1 + 0.2) is smelled three times as far as a clean one.
- Wash: 5 minutes and 0.5 L of carried water, from the old game (`Actions/Handlers/ConsumptionHandler.cs`, WashTimeMinutes = 5, 0.5 L); open water, a pond or snow underfoot need no carried water.
- Wetting: +0.15 Wet per unit of severity washed (Dirty + Bloody), so the old game's typical wash of Bloody 0.5 adds about its 0.05-0.1; washing off full blood and full grime adds 0.3.

Revised (review): sweat severity only opens above a 99 °F core and stays near 0.06, so 0.04/h × severity is lost in the time term → Dirty feeds on sweat produced instead, 0.1 per litre of sweat (a hard hour's 500 ml adds 0.05, half a day of living).
Revised (review): Dirty grows from zero every minute, so "You are dirty." would be told on the first minute and the minute after every wash → Dirty's start and end are not narrated; the Washed line tells the wash.
Revised (review): a near-clean wash would add almost no Wet, and the AC says it leaves the person wet → wetting is 0.05 plus 0.15 per unit of severity washed (the old game's flat 0.05 as the floor).
Revised (review): the Washed event would be filed in tile journals → excluded in Journal.Files like Drank; given the water icon in Words.
Revised (review): carried water under 0.5 L would be taken short → refused below 0.5 L with what is missing; the water is taken at the end of the wash, so an interrupted wash costs nothing.
Revised (review): Wash offered only where water is at hand would hide its refusal → Wash is always offered to a person with a pack, carrying its refusal.
Revised (review): butchering's Bloody rate is per mode in butchering.json → Dirty from carcass work stays one constant (0.1/h of cutting): the mode changes how much blood, not how long your hands are in the carcass, and single numbers stay in C# (architecture rule).
Revised (review): the ticket asks for the save format version to be bumped; there is no version since save-format.md (2026-11-24): the format hash changes with EffectKind.Dirty and the Wash and Washed types → regenerate the fixture, and set Dirty on the sample world so it is proven to survive.
Revised (review): ScentSource is not set for Dirty: Dirty's most (0.2) is under ScentSourceLevel (0.5), and every person carries some Dirty, so the tag would say nothing.
Follow-up: NPCs never wash, so every NPC settles at full Dirty (scent 0.3 against 0.1) and prey sense NPC hunters about three times as far; an NPC wash (in Npc.cs's needs) and a fresh NPC bench against the baselines branch would settle it.
Revised: seams decided without a person (unattended run): Body.Update for Dirty building and capacities, Scent.Of with Senses.Finds for the sensing test, Sim.Step and Verbs.At for Wash and butchering, the save round trip for Dirty → tests written only there.
Revised: WhereThingsGo and the comment ratchet asked for an ActionGen Act, WashedIn written by name, history-free comments and fewer comment lines in Activity.cs, Scent.cs and Work.cs → added Act.Wash (not Splittable: a split wash would restart its five minutes each step, as Drink would), the string-enum converter, and trimmed the comments.
Revised: NPC bench on this branch, 20 seeds, 60 days, Release: 15 alive (dead 5, 7, 42, 101, 1212), against the baselines branch's 15 alive (dead 5, 7, 101, 808, 1212; measured at c70b3c2, before this branch's base). Same survival count; 42 now dies of the cold on day 47 and 808 lives, which reads as seed chaos, not a trend in hunting.
