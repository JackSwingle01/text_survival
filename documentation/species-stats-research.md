# Species stats and a fight energy model (research, 2026-11-21)

Realistic body stats for every species in `rebuild/Sim/Content/species.json`, compared with the
game, plus an energy model for fights. It exists because the combat constants will be tuned by an
optimiser, and perhaps later by evolved behaviour whose fitness is survival + calories − energy −
injuries (`dev/active/rebuild-roadmap.md`, combat grilling round 1). Both will exploit any
unrealistic number. This document measures and proposes; it changes no code.

**Short answer.**

- The game's speeds have the wrong order. Its prey (caribou, bison, megaloceros) move 3 m a round,
  as slowly as a person and half as fast as a wolf. In reality they outsprint wolves. Wolves win by
  endurance and by picking weak animals, and they quit early when a chase is not closing.
- Fights cost almost nothing. A 30 s round of hard running costs a person 15–17 kcal; the game
  charges about 1.2 kcal (activity factor 2.0). Dodge and Flee are paid from the sleep pool
  (`EnergyMinutes`), which is not what running out of breath is.
- Herds have no calorie budget. Hunger is a unitless 0–1 number, so fleeing costs a prey animal
  nothing, and the "landscape of fear" trade-off the user wants cannot appear.
- Several species are mis-cast. The cave bear was mostly herbivorous. The game's 300 kg "SaberTooth"
  fits neither *Homotherium* (about 190 kg, the Eurasian one) nor *Smilodon* (American only).
  Animals' `musclePct` of 0.55–0.70 is about 1.5× real.
- Real hunting succeeds rarely. Wolves killed 6 of 77 moose they tested (7.8%), and none of the 24
  that stood their ground. A person cannot outrun any animal in the table except a mammoth.

**Confidence tags** used in every table:

- **V**: read in a primary source (or its abstract or table) during this session;
- **R**: recalled from the literature, not re-read this session (check before pinning a test to it);
- **D**: derived here, by scaling law or judgment, with the working shown.

---

## 1. Shared physiology: scaling laws used to fill gaps

| Quantity | Formula | Tag | Source |
|---|---|---|---|
| Basal metabolic rate | BMR ≈ 70 · M^0.75 kcal/day (M in kg). Across 619 species the fitted exponent is 0.69. | R / V (exponent) | Kleiber 1932; exponent from the [Painter 2005 summary](https://link.springer.com/content/pdf/10.1186/1742-4682-2-31.pdf) |
| Field metabolic rate | FMR ∝ M^0.734 (79 mammal species). In practice FMR ≈ 1.6–3 × BMR. Hunter-gatherer humans are about 1.6–1.7× (Hadza men about 2,650 kcal/day). | V (exponent), R (Hadza) | Nagy, Girard & Brown 1999, *Annu Rev Nutr* 19:247, exponent via [Glazier's table](https://pmc.ncbi.nlm.nih.gov/articles/PMC2602708/table/tbl1); Pontzer et al. 2012, *PLoS ONE* |
| Minimum net cost of transport | COT ≈ 10.7 · M^−0.32 J kg⁻¹ m⁻¹. Speed-independent, so net running power = COT · v. | V | Taylor, Heglund & Maloiy 1982, *J Exp Biol* 97:1, quoted by [this JEB paper](https://cob.silverchair.com/jeb/article-pdf/215/14/2349/1280330/2349.pdf) |
| Postural (zero-speed) power | ≈ 6.0 · M^−0.30 W/kg, the intercept of the same regression | R | Taylor et al. 1982 |
| Maximum aerobic power | VO2max ∝ M^0.872, so mass-specific ∝ M^−0.13. "Athletic" species (dog, horse, pronghorn) run about 2–2.5× their size's prediction. | V (exponent), R (athletic factor) | Weibel & Hoppeler 2005, *J Exp Biol* 208:1635, [abstract](https://scilit.com/publications/9a0b4a4b84716037157e647a46b7ff78) |
| Units | 1 ml O₂ kg⁻¹ min⁻¹ = 0.335 W/kg. 1 W for 30 s = 30 J. 1 kcal = 4,184 J. | — | 20.1 J per ml O₂ |
| Max speed vs size | Speed is hump-shaped in mass: the largest animals run out of quick-release energy before they finish accelerating. Acceleration time is the limit. | V (concept) | Hirt et al. 2017, *Nat Ecol Evol* 1:1116, [abstract](https://jetzlab.yale.edu/node/241) |
| Anaerobic reserve (human) | W′ ≈ 15–25 kJ (about 200–350 J/kg). Recovery is exponential with τ = 546·e^(−0.01·D_CP) + 316 s, where D_CP is how far below critical power you are; at rest τ is about 6 min. Phosphocreatine half-time is about 30 s after moderate work. | V (concept), R (numbers) | Chorley et al. 2023, [EJSS](https://hira.hope.ac.uk/4141/1/European%20Journal%20of%20Sport%20Science%20-%202023%20-%20Chorley%20-%20A%20dynamic%20model%20of%20the%20bi%E2%80%90exponential%20reconstitution%20and%20expenditure.pdf); Skiba et al. 2012 (τ formula) |
| Escape economics | Flee when the expected cost of staying (risk × cost of being caught) exceeds the cost of fleeing (lost feeding, energy, lost patch) | R | Ydenberg & Dill 1986, *Adv Study Behav* 16:229; Cooper & Frederick 2007 |

**Kleiber BMR and the Taylor COT** for each species, at the chosen masses (D):

| Species | Mass kg | BMR kcal/day | BMR W/kg | COT J/kg/m (Taylor) |
|---|---|---|---|---|
| Human male | 68 | 1,660 | 1.18 | 2.7 predicted; **3.8–4.2 measured** (R, Margaria 1963, di Prampero 1986) |
| Human female | 55 | 1,410 | 1.24 | 3.8–4.2 measured |
| Wolf | 45 | 1,220 | 1.31 | 3.3 |
| Caribou | 110 | 2,380 | 1.05 | 2.4 |
| Bison (cow / bull) | 500 / 850 | 7,400 / 11,000 | 0.72 / 0.63 | 1.4 / 1.2 |
| Megaloceros | 550 | 7,960 | 0.70 | 1.4 |
| Mammoth (cow / bull) | 4,000 / 6,000 | 35,200 / 47,700 | 0.44 / 0.38 | 0.75 / 0.68 |
| Brown bear | 250 | 4,400 | 0.85 | 1.8 |
| Cave bear (male) | 400 | 6,260 | 0.76 | 1.5 |
| *Homotherium* | 190 | 3,580 | 0.91 | 2.0 |
| Cave hyena | 80 | 1,870 | 1.13 | 2.7 |
| Fox | 6 | 270 | 2.2 | 6.0 |
| Hare | 3 | 160 | 2.6 | 7.5 |

Humans cost about 1.5× the quadruped line to run. That is real, and it matters: we are poor
sprinters and expensive runners, and our edge is heat dissipation over hours.

---

## 2. Per-species tables

Each table gives the range found, the value chosen for the game, and a tag. Speeds are given in
m/s; 1 m/s = 3.6 km/h. "Endurance speed" is the pace that can be held for 20 minutes or more.

### 2.1 Human (Upper Palaeolithic hunter)

| Field | Range found | Chosen ♂ / ♀ | Tag | Source |
|---|---|---|---|---|
| Body mass | UP Europe: ♂ 65–72 kg, ♀ 52–58 kg; stature ♂ about 174 cm, ♀ about 162 cm | 68 / 55 kg | R | Holt & Formicola 2008; Ruff et al. 2018 |
| Fat fraction | Forager ♂ 10–14%, ♀ 20–26% | 0.12 / 0.22 | R | Pontzer et al. 2012 (Hadza) |
| Skeletal muscle fraction | ♂ about 38%, ♀ about 31% | 0.38 / 0.31 | R | Janssen et al. 2000, *J Appl Physiol* |
| Top sprint | Elite peak 12.4 m/s; fit non-elite 8–9; untrained 6–7. Held at peak for about 2–4 s, near-maximal for about 15 s. | 8.5 / 7.3 m/s | R | IAAF Berlin 2009 biomechanics |
| Acceleration | v(t) = vmax(1 − e^(−t/τ)), τ ≈ 1.0–1.3 s, so about 90% of vmax in 3 s | τ = 1.2 s | R | Furusawa, Hill & Parkinson 1927 |
| Endurance | Persistence hunts: 25–35 km in 3 h 35 min to 4 h 57 min at 6.3–10 km/h, at 39–42 °C | 2.8 m/s for hours; 3.3 m/s for about 1 h | **V** | [Liebenberg 2006, *Curr Anthropol* 47:1017](https://originalwisdom.com/wp-content/uploads/bsk-pdf-manager/2019/05/Liebenberg_2006_PersistenceHuntingByModernHunterGatherers.pdf) |
| Persistence hunting | 5 of 10 hunts succeeded, all on kudu. Per-attempt success: persistence 100% (2/2), dogs 60%, club and spear 45%, springhare probe 14%, bow 5%. About 400 ethnohistoric cases worldwide, used alongside drives, traps and ambush, not as the main method. | — | **V** | Liebenberg 2006; [Morin & Winterhalder 2024, *Nat Hum Behav*](https://www.nature.com/articles/s41562-024-01877-w) |
| Running cost | 3.8–4.2 J/kg/m whatever the speed; walking about 2.0–2.3 J/kg/m at its best speed | 4.0 | R | Margaria 1963; di Prampero 1986 |
| VO2max | Fit ♂ 45–55 ml/kg/min, ♀ 38–45 (15–18 and 13–15 W/kg) | 50 / 42 | R | standard exercise physiology |
| Anaerobic reserve | W′ about 200–350 J/kg; recovery τ about 6 min at rest | 300 J/kg | R | Skiba 2012 |
| Spear, hand-thrown | Replicas of 760–800 g, thrown by javelin athletes: release 11.8–20.1 m/s (mean 15.9); impact 71–179 J aimed (mean 108), 444 J at most; hit rate on a hay-bale target 25% at 10 m, 25% at 15 m, 17% at 20 m. Ethnographic mean wooden spear 701 g. | 15 m/s, 100 J, effective ≤ 15 m | **V** | [Milks, Parker & Pope 2019, *Sci Rep* 9:820](https://pmc.ncbi.nlm.nih.gov/articles/PMC6347593) |
| Spear, ethnographic distance | Churchill's survey of 96 societies: thrown spears cast from 5–10 m (another paper's citation of it gives 7.8 ± 2.2 m, n = 14). Hand-delivered spears go with large prey and hunts that use terrain. | — | V (abstract) | Churchill 1993, *Archeol Pap AAA* 4:11, [record](https://core.tdar.org/document/418402/weapon-technology-prey-size-selection-and-hunting-methods-in-modern-hunter-gatherers-implications-for-hunting-in-the-palaeolithic-and-mesolithic) |
| Atlatl dart | Measured release ≤ 35 m/s (radar, chronograph); earlier claims of 28–64 m/s are too high. Darts are lighter than spears, so their impact energy overlaps a hand-thrown spear's or is lower. Effective range about 20–40 m. | 30 m/s, 60–100 J, effective ≤ 30 m | V (velocity), R (range) | Eren et al. 2017, *PaleoAmerica*; Hutchings & Brüchert 1997, *Antiquity*, [summary](https://core.tdar.org/document/416355/spearthrower-performance-ethnographic-and-experimental-research) |
| Spear thrust | Thrust reach about 2.5–3 m (arm plus a 2–2.5 m shaft). Two-handed thrusting forces run to several hundred newtons and more. | reach 2.5 m | R | Schmitt, Churchill & Hylander 2003, *J Archaeol Sci* |
| Stone-tipped spear wounds | The animal dies of bleeding and organ damage, not on impact. Penetration and lethality track the tip's cross-section and edge. Wounded large prey run hundreds of metres to kilometres; hunters track the blood trail. | — | R | Hughes 1998; Sisk & Shea 2011, *J Archaeol Sci* |
| Senses | Vision is good in daylight and poor at night; smell is weak | — | R | — |
| Activity | Diurnal | — | — | — |

### 2.2 Grey wolf (Pleistocene)

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Body mass | Minnesota ♀ about 61 lb (28 kg), ♂ about 78 lb (35 kg), some over 100 lb (45 kg). Alaska and Yukon males 40–55 kg. Beringian wolves were about the size of the modern Alaskan interior wolf, with heavier skulls and teeth. | 45 kg (♂ 48, ♀ 40) | **V** (MN), R (AK), V (Beringian, via abstract) | [Mech 1966, *Wolves of Isle Royale*](https://npshistory.com/series/fauna/7/sec4.htm); [Leonard et al. 2007, *Curr Biol*](https://repository.si.edu/items/4b76d81b-c234-49b9-9115-2c80bce53da4) |
| Fat / muscle | Lean: body fat about 5–12% (highest in autumn); skeletal muscle about 45–50% | 0.10 / 0.48 | R / D | — |
| Top sprint | Clocked at 27 mph (12 m/s) over 200 yd. A claimed 35–40 mph over 4 miles is not credible. Overviews give 55–70 km/h. | 14 m/s | **V** (12 m/s) | Mech 1966, quoting Young & Goldman and Stenlund |
| Endurance | Usual travel is a trot of about 5 mph (8 km/h); arctic wolves average 8.7 km/h. Rapid pursuit lasts at least 20 min. | trot 2.4 m/s; chase 8 m/s | **V** (trot) | Mech 1966; [Mech, USGS](https://pubs.usgs.gov/publication/5222641) |
| Daily travel | Averaged 9 miles a day over a month; 31 miles a day on travel days; 45 miles at most in 24 h | — | **V** | Mech 1966 |
| VO2max | Dogs reach 100–150 ml/kg/min; wolves are taken to be similar (an athletic canid) | 120 (40 W/kg) | R / D | Weibel & Taylor's athletic-species work |
| Bite force | Canine bite 593 N at 34.7 kg (bite force quotient 136); dire wolf 893 N at 50.8 kg | 700 N at 45 kg | **V** | [Wroe, McHenry & Thomason 2005, *Proc R Soc B*](https://pmc.ncbi.nlm.nih.gov/articles/PMC1564077) |
| Reach | Jaw only; it bites from contact, slashes and holds, and goes for the flanks, rump and nose of large prey | 0.3 m | R | — |
| Injuries inflicted | Deep punctures and tearing to the hindquarters and flanks; prey dies of blood loss and exhaustion | Pierce | R | Mech 1966 hunting accounts |
| Scent detection | Most moose were found within 300 yd (275 m) upwind; some 125 yd downwind or 200 yd crosswind; once about 1.5 mi (2.4 km). 29 of 160 moose within range were never found, some within 150 yd. | 275 m upwind, 115 m downwind | **V** | Mech 1966 |
| Hunting success | Isle Royale: of 131 moose detected, 77 tested, 7 wounded, 6 killed: **7.8%** of those tested. Yellowstone: a lone wolf has about a 2% chance of killing an adult elk and almost none of a bison. Elk success levels off above 4 wolves; bison success keeps rising to about 13. | — | **V** | Mech 1966, Table 17; [MacNulty et al., NPS](https://www.nps.gov/yell/learn/ys-24-1-understanding-the-limits-to-wolf-hunting-ability.htm) |
| Giving up | Usually quit when a moose kept a 100 yd lead for 10–15 s, unless they were gaining. 20 pursuits were abandoned. With a 150 yd lead, moose were overtaken within 200–300 yd. Against a moose standing at bay they left within 30 s to 5 min. | quit if gap > 90 m and not closing for 12 s | **V** | Mech 1966 |
| Chase length | Usually a few hundred metres. Caribou chases of 3–4 mi (Murie) and 5 mi (Crisler) are reported. | — | **V** (as cited by Mech) | Mech 1966 |
| Group size | Isle Royale pack of 15–16; typically 4–10 | 6 | **V** / R | Mech 1966 |
| Activity | Crepuscular and nocturnal; flexible in arctic summer | — | R | — |
| Toward humans | Attacks on people are rare and mostly by rabid or habituated wolves | — | R | Linnell et al. 2002, NINA report 731 |

### 2.3 Caribou / reindeer

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Body mass | ♀ 80–120 kg, ♂ 150–200 kg (Grant's caribou) | 110 kg (♀ 90, ♂ 160) | R | — |
| Fat | About 4% in late winter up to 15–20% in prime bulls before the rut | 0.08 (season 0.04–0.18) | R | — |
| Muscle | About 45–50% | 0.47 | D | — |
| Top sprint | Popular sources give 60–80 km/h; no measured value found. Adults outrun wolves over short distances, and calves run at nearly wolf speed on level ground. | 16 m/s | R (speed), V (relative) | Murie, *Wolves of Mount McKinley*, as summarised in search results |
| Endurance | The longest land migrations of any mammal; runs for kilometres | 7 m/s chase, 1.5 m/s travel | D | — |
| VO2max | No measured value found; taken as an athletic ungulate | 80 (27 W/kg) | D | — |
| Cratering cost | 118 J per digging stroke in light snow, 219 J in crusted snow, 481 J in snow packed by snowmobile. Grazing costs +17%, browsing +18% and walking +46% over standing. | — | **V** | Fancy & White 1985, *J Wildl Manage* 49:987 (abstract) |
| Weapons | Antlers (both sexes), hooves. Mostly flight. | Blunt | R | — |
| Flight from people | Feral reindeer did not flee a person farther away than 310 m (March), 351 m (July) or 180 m (Sept–Oct). Larger groups fled at shorter distances. Wild forest reindeer fled at a mean 192 m, against 68 m for semi-domestic ones. Herds that meet people often respond at shorter distances. | 150–300 m in the open | **V** | [Reimers et al. 2006, *Wildl Biol*](https://bioone.org/journals/wildlife-biology/volume-12/issue-4/0909-6396(2006)12[403:FBFRRT]2.0.CO;2/Flight-by-feral-reindeer-Rangifer-tarandus-tarandus-in-response-to/10.2981/0909-6396(2006)12[403:FBFRRT]2.0.CO;2.full); Rangifer 2013 |
| Group size | Tens to hundreds in winter; post-calving aggregations of 10⁴–10⁵ | 10–40 | R | — |
| Vigilance | The group-size effect on vigilance appears only in adult females that are foraging, which shows a foraging–vigilance trade-off | — | V (abstract) | Newfoundland caribou study, [MUN thesis](https://research.library.mun.ca/9048/1/Soulliere_ColleenE.pdf) |
| Activity | No daily rhythm in the polar summer and winter; feeding comes in bouts round the clock | — | R | van Oort et al. 2005, *Nature* |

### 2.4 Steppe bison (*Bison priscus*)

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Body mass | Up to about 900 kg. Late-Pleistocene North American bison averaged 910 ± 50 kg, against 665 ± 21 kg today. | ♂ 850, ♀ 500 (herd average about 600) | V (summaries) | [Martin et al. 2018](https://pmc.ncbi.nlm.nih.gov/articles/PMC5938452); Yukon Beringian note |
| Fat / muscle | Autumn fat about 10–15% | 0.12 / 0.45 | D | — |
| Top sprint | Plains bison are popularly given 55–60 km/h; no measurement found | 15 m/s | R | — |
| Endurance | Good; can gallop for kilometres | 6 m/s | D | — |
| VO2max | Like cattle and other non-athletic bovids, about 40 ml/kg/min | 40 (13 W/kg) | D | — |
| Weapons | Horns (*B. priscus* horn cores spread widely), head butts, trampling. Prime adults are dangerous to wolves. | Pierce / Blunt, reach 0.8 m | R | — |
| Hide | Very thick on the head, neck and hump, with dense winter wool | — | R | — |
| Toward wolves | Stand or bunch more than they run; wolf success rises with pack size up to about 13 | — | **V** | MacNulty et al., NPS |
| Intake | Highest herbaceous intake about 68 g dry matter/min (Hudson & Frank 1987); halves as forage thins to 780 kg/ha, with bite rate rising above 60/min. Tallgrass, early season: 0.6 g a bite × 38 bites/min = 21 g/min. | See §4 | **V** (as quoted) | [Raynor et al. 2015, *Ecology*](https://ars.usda.gov/ARSUserFiles/30123025/4.%20Raynor_et_al-2015-Ecology.pdf); Hudson & Frank 1987 |
| Vigilance | Bulls about 7% of the time; cows more where wolves are present | — | **V** | [Laundré et al. 2001, *Can J Zool*](https://www.esf.edu/biology/faculty/documents/Laundreetal2001elkandbison.pdf) |
| Group size | Cow groups of 20–50; bulls alone or in small groups | 20 | R | — |
| Activity | Diurnal, with feeding peaks at dawn and dusk | — | R | — |

### 2.5 Bears: brown bear and cave bear

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Brown bear mass | Interior ♂ 180–360 kg, ♀ 100–200 kg | 250 kg | R | — |
| Cave bear mass | ♂ 400–500 kg, ♀ 225–250 kg (Christiansen 1999). Older estimates: a large male 410–440 kg (Kurtén); averages ♂ 319, ♀ 244 (Viranta). | ♂ 450, ♀ 240 | V (secondary) | Christiansen 1999, *Ann Zool Fenn* 36:93 |
| Cave bear diet | Mostly herbivorous on isotope evidence, with some omnivory in places | Den defender, not a predator | R | Bocherens et al. (several papers) |
| Fat | Spring 10–15%, autumn 30–40% | 0.25 (season 0.12–0.38) | R | — |
| Top sprint | Grizzlies about 48 km/h (13.3 m/s); reported this way in a gait paper and clocked from a car in Yellowstone in 1930 | 13 m/s (cave bear 11) | V (secondary) | [JEB gait paper](https://cob.silverchair.com/jeb/article-pdf/1915887/jeb121806.pdf); [NPS Nature Notes](https://www.nps.gov/parkhistory/online_books/yell/vol14-1-2a.htm) |
| Endurance | Poor: short charges, and bears overheat | 4 m/s | R | — |
| Field metabolic rate | Polar bears run about 1.6× the rate earlier models assumed | — | **V** | [Pagano et al. 2018, *Science*](https://pubs.usgs.gov/publication/70199153) |
| Bite force | Brown bear canine bite 751 N at 128.8 kg (bite force quotient 78) | 1,000 N at 250 kg | **V** | Wroe et al. 2005 |
| Reach / weapons | Paw swipe and bite; reach about 1 m standing; severe tearing and crushing to the head and scalp | Sharp, reach 1.0 m | R | — |
| Smell | Very keen, kilometres | 1–2 km | R | — |
| Group size, activity | Solitary; crepuscular | — | R | — |

### 2.6 Megaloceros (giant deer)

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Body mass | 450–600 kg, perhaps 700 kg or more; shoulder height about 2.1 m | 550 kg | V (summaries) | Britannica; [van der Made 2010](https://www.mncn.csic.es/sites/default/files/inline-files/2010%20Van%20der%20Made%20-%20Megaloceros%20-%20Elefantenreich.pdf) |
| Antlers | Span about 3.5–3.65 m, about 40 kg (males only) | reach 1.5 m (stag) | V | Guinness record |
| Speed, endurance | Not measured. Built as an open-country runner, by analogy with red deer and elk. | 15 m/s / 6 m/s | D | — |
| Group, activity | Unknown; by red-deer analogy, cow groups of 5–30 and stags alone in winter | — | D | — |

### 2.7 Woolly mammoth

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Body mass | Bulls about 6 t, cows about 4 t (volumetric estimates) | ♂ 6,000, ♀ 4,000 kg | R (check Larramendi's supplementary table) | [Larramendi 2016, *Acta Palaeontol Pol* 61:537](https://app.pan.pl/article/item/app001362014.html) |
| Fat / hair | Fat layer up to about 8 cm; outer hair up to 90 cm; underwool 2.5–8 cm. The skin was no thicker than a modern elephant's (about 2–3 cm). | fat 0.10, muscle 0.40 | V (summaries), R (elephant skin) | [NHM](https://nhm.ac.uk/discover/were-all-mammoths-woolly.html); Britannica |
| Top speed | Asian elephants reach 6.8 m/s (25 km/h) without ever leaving the ground; 20 of 42 exceeded 5 m/s | 6.5 m/s | **V** | [Hutchinson et al. 2003, *Nature* 422:493](https://ideas.repec.org/a/nat/nature/v422y2003i6931d10.1038_422493a.html) |
| Endurance | Walks at 1.5–2 m/s and keeps a brisk pace for a long time | 3 m/s | R | — |
| Weapons | Tusks (bulls up to about 4 m) and trunk; tramples and tosses | reach 2.5 m | R | — |
| Toward predators | By elephant analogy, ignores or mobs single small predators; calves are kept in the middle | — | R | — |
| Group, activity | Matriarchal family groups of 6–20; bulls alone or in bachelor groups. Feeds 60–75% of the day, night as well as day. | 10 | R | elephant ethology |

### 2.8 Sabre-tooth: *Homotherium* (recommended) or *Smilodon*

The game's `SaberTooth` is generic. An Ice Age Eurasian setting implies *Homotherium latidens*;
*Smilodon* lived only in the Americas.

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| *Homotherium* mass | 145–230 kg (Anyonge 1993); about 190 kg; shoulder about 1.1 m | 190 kg | V (museum page) | [Illinois State Museum](https://iceage.museum.state.il.us/node/45) |
| *Smilodon fatalis* mass | 160–280 kg | (280 if kept) | V (abstract) | Christiansen & Harris 2005, [FLMNH](https://www.floridamuseum.ufl.edu/florida-vertebrate-fossils/species/smilodon-fatalis/) |
| Bite force | *Smilodon* canine bite 976 N at 199.6 kg (bite force quotient 78, low). The killing bite was a canine shear driven by the neck muscles. Lion: 1,768 N. | 1,000 N | **V** | Wroe et al. 2005 |
| Speed | *Homotherium* had long forelimbs; genomes suggest a social, more cursorial, possibly diurnal hunter. A felid sprint is short (lions about 15 m/s, chases under 200 m). | 14 m/s, 4 m/s, short sprint | R | Barnett et al. 2020, *Curr Biol* |
| Injuries inflicted | Deep slashing punctures to the throat and abdomen; fast bleeding | Sharp/Pierce | R | — |
| Group, activity | Possibly small groups; ambush or a short rush | — | R | — |

### 2.9 Cave hyena (game `Hyena`)

| Field | Range found | Chosen | Tag | Source |
|---|---|---|---|---|
| Mass | Spotted hyena 69 kg (Wroe's sample); the cave hyena was larger, about 80–100 kg | 85 kg | V / R | Wroe et al. 2005 |
| Bite force | Spotted hyena canine bite 773 N (bite force quotient 117) | 900 N | **V** | Wroe et al. 2005 |
| Speed / endurance | Spotted hyenas run about 60 km/h and chase over kilometres | 15 / 7 m/s | R | Kruuk 1972 |
| Group, activity | Clans; nocturnal; hunt as much as they scavenge | — | R | — |

### 2.10 Small game and scavengers

| Species | Mass | Top speed | Bite / defence | Notes | Tag |
|---|---|---|---|---|---|
| Red fox / arctic fox | 5–8 / 3–5 kg | about 13–14 m/s | 164 N (red, 8.1 kg) / 178 N (arctic, 8.2 kg), **V** Wroe 2005 | Solitary, crepuscular | R except bite |
| Hare (game `Rabbit`; Ice Age north = mountain hare) | 2–5 kg | about 16–20 m/s claimed | none | Nocturnal or crepuscular; bursts of a few seconds, zig-zags | R (speed unverified) |
| Ptarmigan | 0.4–0.6 kg | flies | none | Cut from bodies (`Species.cs`) | — |
| Raven | 1–1.5 kg | flies | none | The game folds it into a 3 kg scavenger flock | — |

---

## 3. The game against reality

Game values are from `species.json`, `Body.cs`, `BodySurvival.cs`, `Sim.cs`, `Combat.cs` and
`CombatAI.cs` at the working tree of 2026-11-21.

### 3.1 Speed: the biggest gap

`moveAllowanceM` is metres per combat action at full Moving (`CombatAI.Allowance`). In the table,
"realistic ratio" is top sprint divided by the male human's 8.5 m/s, and "realistic allowance" is
that ratio scaled so the human stays at 3 m.

| Species | Game allowance | Ratio to human | Realistic ratio | Realistic allowance | Gap |
|---|---|---|---|---|---|
| Human | 3 | 1.0 | 1.0 | 3 | — |
| Wolf | 6 | 2.0 | 1.65 | 5 | Slightly fast; wolves have no endurance edge |
| Bear | 5 | 1.67 | 1.55 | 4.6 | Close |
| CaveBear | 4 | 1.33 | 1.3 | 3.9 | Close |
| SaberTooth | 7 | 2.33 | 1.65 | 4.9 | Too fast; the fastest thing in the game |
| Hyena | 6 | 2.0 | 1.75 | 5.3 | Close |
| **Caribou** | 3 | 1.0 | **1.9** | **5.6** | **Half speed: real caribou outsprint wolves** |
| **Megaloceros** | 3 | 1.0 | 1.75 | 5.3 | **Too slow** |
| **Bison** | 3 | 1.0 | 1.75 | 5.3 | **Too slow** |
| Mammoth | 3 | 1.0 | 0.76 | 2.3 | Slightly fast at top speed, but sustains it |
| Rabbit (hare) | 3 | 1.0 | 2.0 | 6.0 | **Too slow** |
| Fox | 3 | 1.0 | 1.6 | 4.8 | Too slow |
| Raven | 6 | 2.0 | flies | — | Fine as a stand-in |

**The time scale is wrong as well as the ratios.** A round is one minute today and 30 s by the
roadmap. In 30 s a person sprinting covers 150–200 m and a wolf 300–400 m. 3–6 m a round is about
half a second of running. The allowances work only as "metres per step of tactical thinking",
and the grid is 50 m across. The energy model below runs on continuous time, so it does not care
how long a round is. It needs only that each round says how many seconds it covers, and the
closed-form catch-up the roadmap decided on is where most of the running happens.

The survey's speed experiment (survey §5) showed that making caribou faster than wolves took pack
success from 100% to 2%. That is because speed is the only axis. With an anaerobic reserve and an
endurance speed, the same ordering gives "the pack runs down the weak one, the strong ones get
away", which is what Mech saw (worked example in §5.4).

### 3.2 Body composition and metabolism

| Field | Game | Realistic | Gap |
|---|---|---|---|
| Human mass, fat, muscle | 75 kg, 0.15, 0.30 | ♂ 68 kg, 0.12, 0.38; ♀ 55 kg, 0.22, 0.31 | Muscle low for men; one sex only |
| Animal `musclePct` | 0.50–0.70 (saber-tooth 0.70) | About 0.40–0.50 | **About 1.4× too high.** `Body.Strength` and `Body.Speed` read `MusclePct` directly and treat 0.30 as baseline, so a wolf at 0.60 scores far above its mass. |
| Wolf fat | 0.20 | 0.05–0.12 | Too fat |
| Caribou fat | 0.15 | 0.04–0.18 by season | Fine as an autumn value; there are no seasons |
| Mammoth fat | 0.35 | About 0.08–0.12 (an 8 cm layer on a 6 t body) | Too fat |
| Bear fat | 0.30 / 0.35 | Spring 0.12, autumn 0.38 | Fine as autumn |
| Cave bear role | `Predator`, `bear` temperament | Mostly herbivorous; dangerous in its den | **Mis-cast** |
| Saber-tooth mass | 300 kg | *Homotherium* about 190 kg | Too heavy, and not named for a species |
| Basal metabolism | Katch–McArdle on lean mass for every body: 370 + 21.6·lean kg (`BodySurvival.cs:156`) | Kleiber 70·M^0.75 | Close from 40 to 120 kg. Bison 800 kg: 14,200 against 10,500 kcal/day. **Mammoth: 84,600 against 47,700.** Fox and hare are fine only through the 370 constant. |
| Calorie store | `MaxCaloriesKcal = 2000` for every body | Scales with mass (gut contents and glycogen about 0.5–1 day of FMR) | Absurd for a mammoth, which would empty it in about 35 minutes |
| Herd hunger | Unitless 0–1. Prey herds get hungry in about 5.5 h (+0.003/min) and recover at 0.008/min while grazing (`Fauna.cs:78-84`). Herd bodies never run metabolism. | kcal in, kcal out | **No calorie budget, so flight is free** |

### 3.3 Energy in a fight

| Field | Game | Realistic | Gap |
|---|---|---|---|
| Fight metabolic factor | `Fight => 2.0` (`Sim.cs:250`): a human pays about 2.4 kcal/min, 1.2 kcal per 30 s round | Hard sprint 35 W/kg × 68 kg ≈ 17 kcal per 30 s; a standing spear fight at 8–12 METs ≈ 5–7 kcal per 30 s | **5–15× under-charged** |
| Dodge / Flee cost | 10 / 20 `EnergyMinutes` (the sleep pool) | Draws on an anaerobic reserve of about 15–30 s of sprinting that refills in minutes | Wrong pool. Fatigue that refills in 6 minutes is spent like sleep debt. |
| Load | `DodgeChance` uses `Body.Speed` without the load; `Allowance` ignores load (survey §4) | Running cost rises roughly in proportion to total mass carried (Taylor et al. 1980); top speed falls | Missing |
| Wounds | `Allowance` scales with Moving | Plus blood loss lowering aerobic power (oxygen carried) and pain | Partly there |

### 3.4 Fighting, reach and senses

| Field | Game | Realistic | Gap |
|---|---|---|---|
| Damage scale | Animals 0.02–1.2; weapons 6–12 (`items.json`) | — | Known (body review B9): two scales |
| Animal damage ordering | Mammoth 1.2 > saber 0.9 > cave bear 0.8 > bear 0.6 > bison 0.55 > megaloceros 0.5 > wolf 0.45 > hyena 0.4 | Canine bite force: lion 1,768 N, *Smilodon* 976, hyena 773, brown bear 751, wolf 593. Hyenas bite harder than wolves; bears do their damage with paws and mass. | Hyena too weak against the wolf; otherwise plausible |
| Reach | None: Close and Near zones only | Spear 2.5 m, bear 1 m, mammoth tusk 2.5 m, megaloceros antler 1.5 m, wolf 0.3 m | A spear's first-strike advantage is lost (the survey's initiative idea) |
| Thrown spear | Linear falloff to 20 m (stone 25 m), base 0.70–0.75: about 35% at 10 m and 17% at 15 m | Athletes against a static bale: 25% at 10 m, 25% at 15 m, 17% at 20 m. Ethnographic throws from 5–10 m. | **Close.** Moving, alert prey would do worse. |
| Detection range | `DetectionChance`: 100 m about 0%, 30 m about 50% | Wild reindeer flee a walking person at up to 180–350 m in the open; wolves smell prey at 115–275 m and sometimes 2 km | **Detection range 3–10× short in the open**. Realistic spear hunting of herds needed cover, wind and drives (Churchill: hand-delivered spears go with terrain-dependent hunting). |
| Hunt success | Survey §2: careful melee hunts kill any caribou 47–57% and any bison 59%; throwing hunts 2–6%. Targets 50–70% | Wolves 7.8% of moose tested; bow hunts 5% per attempt; spear and club 45% per attempt, but on chosen, often small, prey | Targets are gameplay, not realism (see the design questions) |
| Morale vs a big prey animal | Bison, megaloceros and mammoth use bands | Moose that stood at bay were never killed (0 of 24); wolves left within 30 s to 5 min | The bands need a strong "hold" for large prey facing small predators (§4.3) |

---

## 4. Feeding, vigilance and the landscape of fear

The coordinator's addition: time a prey animal spends Unaware and grazing earns calories, so
alertness and flight cost food. A big animal should ignore a small predator that cannot hurt it,
and an animal that bolts at every shadow should starve.

### 4.1 Intake per minute of feeding (metabolisable energy)

Dry matter (DM) intake rates, then ME ≈ 4.4 kcal/g gross × digestibility × 0.82 (the ME/DE ratio).
Digestibility is about 55–65% for summer graminoids and lichen, and 40–50% for winter grass and
browse. That gives about 2.2–2.6 kcal ME per g DM in summer and 1.6–1.9 in winter (R, standard
ruminant nutrition). Each estimate is checked against the requirement (FMR spread over a
plausible feeding time).

| Species | Intake while feeding, summer | Winter | Check | Tag |
|---|---|---|---|---|
| Bison (600 kg herd average) | 25–35 g DM/min average, 68 peak → **70 kcal/min** | 20–25 g/min × 1.7 → **35 kcal/min** | Eats about 2% of body mass a day (12 kg DM) in 9–10 h of feeding, so about 21 g/min average. FMR about 2 × 8,500 = 17,000 kcal/day, which takes 4–8 h at these rates. | V (g/min: Hudson & Frank, Raynor), D (kcal) |
| Caribou (110 kg) | About 7–8 g DM/min × 2.6 → **20 kcal/min** | Lichen about 5–6 g/min × 2.4 → **13 kcal/min**, less cratering at 10–30 strokes/min × 118–219 J ≈ 0.3–1.5 kcal/min | Summer intake 4–5 kg DM/day over about 10 h; winter 2.5–3 kg over about 8 h. Winter FMR about 4,500 kcal/day. | R (daily intakes), V (cratering J), D |
| Megaloceros (550 kg) | About 18 g/min × 2.3 → **40 kcal/min** | About 12 g/min × 1.7 → **20 kcal/min** | 2% of body mass a day over 10 h | D |
| Mammoth (5,000 kg) | About 75 g DM/min × 2.0 (hindgut, low digestibility) → **150 kcal/min** | **90 kcal/min** | Elephants eat about 1–1.5% of body mass as DM a day over 16 h; FMR about 1.7 × 41,000 = 70,000 kcal/day | R (elephant), D |
| Hare (3 kg) | About 0.3 g/min × 2.2 → **0.7 kcal/min** | **0.4 kcal/min** | About 5% of body mass a day over 8 h | D |

Large animals eat far more per minute in absolute terms. Per kg of body and per minute, the order
is reversed (hare 0.23, caribou 0.18, bison 0.12, mammoth 0.03 kcal/kg/min): a small animal's
reserves last hours, a mammoth's days. That is why the same minute of lost feeding means more to
a small animal.

### 4.2 Time budget and vigilance

| Measure | Value | Tag | Source |
|---|---|---|---|
| Feeding share of the day | Bison and cattle 35–45% (8–11 h); caribou 40–50% in summer, 35–45% in winter plus cratering; elephants 60–75% | R | — |
| Vigilance while feeding, low risk | Bull bison about 7%, bull elk about 12%, unchanged by wolves | **V** | Laundré et al. 2001 |
| Vigilance under wolf risk | Cow elk with calves rose from 20.1% to 43.0% over 5 years as wolves spread; cows without calves from 11.5% to 30.5%; peak 47.5 ± 4.1% | **V** | Laundré et al. 2001 |
| Cost of that vigilance | Elk in wolf areas had lower faecal nitrogen (1.7% against 2.1%): a poorer diet | **V** | Hernández & Laundré 2005 |
| Group-size effect | Yearlings and cows without calves scanned less and fed more in bigger herds; bulls were unaffected; cows with calves did not relax with herd size. The authors put it down to individual risk more than shared detection. | V (abstract) | [Childress & Lung 2003, *Anim Behav* 66:389](https://www.sciencebase.gov/catalog/item/4f4e48c8e4b07f02db54169b) |
| Functional form | Individual vigilance falls roughly as n^(−0.5) to a floor; the exponent is a modelling choice | R | Roberts 1996, *Anim Behav* review; Elgar 1989 |

### 4.3 Flight initiation distance by the threat's size

There is little direct data on predator-to-prey size ratio and FID. What there is:

- **Moose against wolves:** 24 of 109 moose stood at bay throughout (22%), and none was killed or
  wounded. Every one of the 5 fatal attacks seen from the start was on a running moose (**V**,
  Mech 1966, Tables 16–17). A big animal that holds its ground beats a small predator, and
  running is what gets it killed.
- **Bison:** bulls keep vigilance at 7% even among wolves (**V**). Lone wolves almost never kill
  adults (**V**).
- **Reindeer and people:** the response distance shrinks with group size and with familiarity
  (**V**, Reimers 2006, 2010). Larger groups flee later.
- **Across species:** bigger species flee at greater distances in birds (Blumstein 2006, **V**
  abstract). For ungulates, Stankowich's meta-analysis found no clear body-size effect in the
  snippets seen (Stankowich 2008, *Biol Conserv* 141:2159).
- **Theory** (Ydenberg & Dill 1986): flee at the distance where the risk of staying equals the cost
  of going. Nothing more is needed. A small predator gives a big prey animal low risk, so the
  break-even distance is short or zero, and the animal faces it or ignores it.

**Proposed rule (D).** Each prey fighter compares two rates, in kcal-equivalents per minute:

```
risk(d)      = P_lethal(predator vs me) · H(d) · D_death        // cost of staying, per minute
H(d)         = hazard of being attacked this minute, rising as distance d falls
               (0 beyond the predator's charge range; 1 at contact)
P_lethal     = from the predator's threat ÷ my threat (the combat margin already computes this)
D_death      = value of a life, in kcal-equivalents: L · FMR_day, where L is the days of life
               at stake (a tuning constant, the same for every species)
cost_flee    = I_feed · (T_flight + T_settle) + E_run(T_flight) + C_patch
```

The animal flees when `risk(d) > cost_flee / T_horizon`, holds or faces the threat when
`P_lethal` is small, and stays Wary (feeding at a reduced rate) in between. FID emerges as the `d`
where the two cross. That distance grows with the predator's relative size and with the prey's
vulnerability (a calf, a wound, being alone), and shrinks with group size (H is shared) and with
hunger (I_feed is worth more when reserves are low).

---

## 5. The fight energy model

### 5.1 State per fighter

Everything except the reserve is derived from the body and the species row.

| Symbol | Meaning | Source |
|---|---|---|
| `M` | body mass + carried load (kg) | body, pack |
| `a` | anaerobic reserve, 0–1 (stored) | new field on the body (it persists after the fight) |
| `v_max` | top sprint, m/s | species row |
| `v_aer` | endurance speed, m/s (holdable for 20 min or more) | species row |
| `t_s` | seconds at `v_max` that empty a full reserve | species row |
| `τ_rec` | reserve recovery time constant at rest, s | species row (derived, below) |
| `τ_acc` | acceleration time constant, s | species row |
| `C` | net running cost, J/kg/m | species row (Taylor, or human measured) |
| `P_rest` | resting plus postural power, W/kg | BMR × 1.3 |

### 5.2 Equations (per time step `dt` seconds, or closed form over a chase)

**Capacity now**, with wounds, load and condition:

```
cond     = Moving · (0.5 + 0.5·BloodPct) · (1 − 0.3·Tired) · (1 − 0.2·Hungry)
loadPen  = 1 − k_load · load/M_body                    // k_load ≈ 1.0 for top speed (D)
v_top    = (v_aer + (v_max − v_aer) · sqrt(a)) · cond · loadPen
v_aer'   = v_aer · cond · (1 − 0.5·load/M_body)
```

`sqrt(a)` keeps most of the top speed until the reserve is nearly empty, then falls off sharply
(D). Blood loss lowers both speeds through oxygen delivery.

**Reserve drain and refill** at the speed `v` actually chosen:

```
if v > v_aer':  da/dt = −(v − v_aer') / (v_max − v_aer) / t_s
else:           da/dt = (1 − a) / τ_rec · (1 − v / v_aer')     // slower recovery while moving
```

A strike, a grapple or a dodge is a burst. Each costs a fixed slice of reserve, `Δa_burst`
(about 2–3 s of sprint, so about 0.1–0.15 for a person). An empty reserve means no burst: a spent
fighter cannot dodge. This replaces `DodgeEnergyMinutes` and `FleeEnergyMinutes`.

**Calories** per step, charged through the existing `CaloriesDeltaKcal`:

```
P(v)     = P_rest + C · v · (M / M_body)          // W per kg of body; the load is carried
P_burst  = 20 · BMR_W/kg  per burst-second        // strikes and grapples are about 20 METs
kcal     = M_body · (P(v)·dt + P_burst·t_burst) / 4184
```

Anaerobic work is charged when it is done (oxygen debt counted then, not repaid later), which
keeps one ledger. In a fight, `ActivityFactor` is replaced by this sum. The tick still adds the
thermal side: the extra heat is metabolism, so a hard fight warms you (`BodySurvival`'s heat
balance already reads metabolism).

**Long-term fatigue** stays `EnergyMinutes`. A full drain of the reserve also costs about 5 energy
minutes (D), so repeated all-out fights make you Tired, and Tired then lowers `cond`.

**Acceleration** (only needed inside the grid): `v(t) = v_target − (v_target − v0)·e^(−dt/τ_acc)`.

### 5.3 Constants per species (D, from §1–2)

`t_s` comes from an anaerobic capacity of about 300 J/kg divided by the excess power at top
speed, `C·v_max + P_rest − P_aer,max`, then set by judgment where the formula and the data disagree: bears and felids
are short sprinters (formula 40 s, chosen 15–20), bison gets 25 s (formula 41), and the mammoth's
formula never empties (its top speed is a mechanical limit, not an aerobic one), so 90 s is a guess. `τ_rec` is the human's 360 s scaled by (M/68)^0.25
(physiological time).

| Species | v_max m/s | v_aer m/s | t_s s | τ_rec s | τ_acc s | C J/kg/m | Burst Δa |
|---|---|---|---|---|---|---|---|
| Human ♂ | 8.5 | 3.3 | 15 | 360 | 1.2 | 4.0 | 0.12 |
| Human ♀ | 7.3 | 2.9 | 15 | 340 | 1.2 | 4.0 | 0.12 |
| Wolf | 14 | 8 | 40 | 325 | 1.0 | 3.3 | 0.06 |
| Caribou | 16 | 7 | 20 | 410 | 1.0 | 2.4 | 0.10 |
| Bison | 15 | 6 | 25 | 620 | 1.6 | 1.3 | 0.08 |
| Megaloceros | 15 | 6 | 20 | 610 | 1.6 | 1.4 | 0.08 |
| Mammoth | 6.5 | 3 | 90 | 1,100 | 3.0 | 0.7 | 0.05 |
| Brown bear | 13 | 4 | 20 | 500 | 1.4 | 1.8 | 0.08 |
| Cave bear | 11 | 3.5 | 20 | 560 | 1.6 | 1.5 | 0.08 |
| *Homotherium* | 14 | 4 | 15 | 470 | 1.2 | 2.0 | 0.10 |
| Cave hyena | 15 | 7 | 40 | 380 | 1.0 | 2.7 | 0.06 |
| Fox | 13.5 | 5 | 15 | 200 | 0.6 | 6.0 | 0.10 |
| Hare | 17 | 4 | 10 | 160 | 0.5 | 7.5 | 0.15 |

Sanity checks:

- A person sprinting flat out lasts 15 s (about 125 m).
- At 6 m/s a person lasts about 29 s; real people run 400 m in 60–75 s, so `t_s` might be 20–25.
  Tune.
- The person's `v_aer` of 3.3 m/s matches the persistence-hunt pace (6.3–10 km/h) at the low end
  of a 4-hour effort.
- Fight costs per 30 s for a 68 kg person: standing and fencing (a slow 1 m/s plus 3 burst-seconds)
  ≈ 2.7 + 1.2 ≈ 4 kcal, against about 5.7 kcal for 10 METs; flat sprint ≈ 17 kcal. A wolf loping at 8 m/s ≈ 9 kcal.

### 5.4 Closed-form chase (the roadmap's catch-up)

Each side's speed is piecewise: `v_top` falling to `v_aer'` once its reserve empties at time
`t_e = a·t_s·(v_max − v_aer)/(v_top − v_aer')` (approximately; the sqrt makes it slightly later).
The gap is `g(t) = g0 − ∫(v_pursuer − v_prey) dt`, which is piecewise linear and can be solved for
`g = 0` directly. The pursuer quits at the first `t` where `g(t) > 90 m` and `dg/dt ≥ 0` has held
for 12 s, or when the prey stands (Mech's rules, §2.2).

**Worked example (D).** Wolf against a healthy caribou, 100 m apart, both rested:

- 0–20 s: the caribou runs 16 m/s, the wolf 14 m/s; the gap grows to 140 m.
- At 20 s the caribou's reserve is empty and it drops to 7 m/s. The wolf runs on at 14 m/s until
  40 s and gains 140 m: the gap is 0 at about 40 s, so the wolf catches it, just.
- With a 150 m start, or a bigger reserve, the wolf reaches its own 40 s mark with a 50 m gap. Now
  the wolf runs 8 m/s and the caribou 7, so it closes 1 m/s: a long chase, the kind Murie
  reported at 3–4 miles.
- A caribou calf, or one with 0.8 Moving (`v_aer` 5.6), is caught early.
- A caribou that never panicked (prey that walks off at `v_aer` and keeps its reserve) is nearly
  uncatchable.

This is the "test the herd, run down the weak one" pattern, and it is the opposite of today's
"the bigger allowance always wins". The real success rate is low (7.8% of moose tested), so most
hunts in this model should end in the quit rule.

**Person against a wolf**: a person cannot escape a wolf by running (3.3 against 8 m/s endurance,
8.5 against 14 sprint). They live by not being worth it: standing, fire, a spear's reach, and the
wolf's own risk sum (§4.3 applies to predators too: a wolf's `P_lethal` against an armed adult is
high, so it holds off). That matches the record that wolves rarely attack people.

### 5.5 The intake term and learned-behaviour fitness

**In the world tick (herds)** (D):

```
I_feed  = intake(species, season) · forage_level(tile, diet)            // kcal/min, §4.1
intake  = Unaware & Grazing:  I_feed · (1 − v_base(n))
          Wary:                I_feed · (1 − v_wary(n))   with v_wary ≈ 0.5–0.7
          Pressing / Standoff / Giving ground / Fleeing:  0
v(n)    = v_floor + (v_0 − v_floor) / sqrt(n)                            // §4.2
          (bison bull: v_floor 0.05, v_0 0.12; cow with calf: v_floor 0.3, v_0 0.5)
burn    = FMR_rate(activity) + fight/flight kcal from §5.2
reserve kcal += intake − burn          // herd Hunger becomes reserve kcal / reserve capacity
```

Herd hunger then derives from a kcal store sized by mass (about 0.5–1 day of FMR before fat is
touched), not from a unitless rate. Vigilance and flight cost food directly. A herd that panics
at every sighting falls behind on intake, its hunger rises, and the existing hunger rules make it
bolder and slower.

**In the fitness** of the learned-behaviour experiment, normalise everything to days of the
species' own FMR, so a hare and a mammoth sit on one scale:

```
fitness = S · survived
        + (Σ intake_kcal − Σ (burn_kcal − rest_kcal)) / FMR_day
        − W · Σ injury_days
```

- `survived`: 1 or 0. `S` is the days of life at stake (the same `L` as `D_death` in §4.3, so the
  policy's risk sum and its reward agree).
- Only burn above resting is charged, so standing still is free.
- `injury_days`: expected days of lost Moving or Manipulation from the wound (the healing model
  already knows this). `W` converts them to food days (a lame animal forages at reduced intake).

A bison that bolts from a single wolf loses about 35–70 kcal for every minute it is not grazing,
plus the cost of running. A bison that stands loses nothing, because `P_lethal` is near zero.
Evolution would rediscover the Isle Royale result. A hare's tiny absolute intake is still large
per FMR-day, and its `P_lethal` against a fox is high, so it flees early.

---

## 6. Where realism and fun conflict

1. **A person cannot outrun anything that wants them** except a mammoth. Fleeing on foot must
   be a matter of deterrence (the predator's give-up rule and risk sum), not of speed. Fleeing an
   attacking wolf by sprint fails in reality; the game should show odds that say so.
2. **Real hunts mostly fail.** Wolves 7.8% per moose tested; bow hunts 5% per attempt. The
   roadmap's 50–70% meat for a careful hunt is a gameplay target. Realistic stats plus realistic
   detection ranges would push it far down unless hunting gets realistic help: cover, wind,
   drives, ambush at terrain features, which is how hand-held spears were actually used.
3. **Wounded prey run.** A stone-tipped spear kills by bleeding over minutes, and the animal leaves
   the 50 m grid. The `WoundedPrey` tension exists for this; the fight's end should hand over to it
   rather than resolve on the grid.
4. **Big prey that stands wins** against small predators. Moose that stood were never killed.
   Mammoth and bison fights that the player starts will be hard and long; that is the
   pull-goal fantasy, but it is dangerous.
5. **Detection.** Open-country reindeer flee walking people at 150–300 m. The game's 30 m for
   50% detection makes stalking easy. Realistic numbers make stalking without cover almost
   pointless, which is true and teaches the right lesson, but needs the terrain and wind levers.
6. **Persistence hunting** is real but takes 3–5 hours, beyond combat's scope. It could be a
   multi-hour tracking activity rather than a fight.

---

## 7. Open data gaps

- No measured top or endurance speeds for caribou, bison or wolves in a peer-reviewed source
  found this session; the Garland 1983 compilation (*J Zool* 199:157) is the primary table but its
  PDF is a scan that could not be read. Read it before pinning speeds.
- The Taylor 1982 postural intercept, Nagy's FMR coefficient, Weibel's VO2max coefficient and the
  Hirt 2017 parameters were not read in full.
- Caribou VO2max, megaloceros anything, and *Homotherium* locomotion are estimates.
- Mammoth mass by sex should be read from Larramendi 2016's supplementary table.
