# UI Design: The Map Is the Screen

*Status: designed and prototyped. Implement in rebuild phase 5 (the desktop front end in
`documentation/clean-room-architecture.md`), not in the current ImGui HUD, which phase 5 replaces.
Companion to [short-loop-plan.md](short-loop-plan.md), whose features this UI presents.*

**Reference prototype:** `dev/prototypes/ui/index.html` (published:
https://claude.ai/artifact/61NCjqy1Fjg8SgiHkvfLcn). It is a self-contained mock with scripted data, on
the game's own pixel art. Treat it as the visual and behavioural reference, not as code to port.

---

## Why

Two prototypes taught the lesson:

1. The first showed a slice of the game's information. It felt responsive and satisfying, but it
   left out much of what the real HUD shows.
2. The second showed all of it, permanently. That was information overload: the facts became
   wallpaper, and the things that mattered right now were lost among the things that didn't.

What already worked in both was the **fire odds sheet**: it shows frostnip, shivering and wind exactly
when you're about to strike, because that's when they matter. The design generalises that.

**Principle: show information where the decision is, and only when it matters.** On a calm day the
screen is almost empty. When it fills up, that is itself the warning.

---

## The Information Hierarchy

Every piece of information has one permanent home, and at most one other place where it appears when
it matters.

```
ui/
├── always/                         # on screen at all times; only what changes most turns
│   ├── clock                       # 1:05 PM  (hover → day, season, weather)
│   ├── daylight-left               # short bar; amber in the last hour
│   ├── feels-like                  # one number (hover → breakdown)
│   ├── vitals/                     # bars, no numbers; colour shifts when low
│   │   ├── warmth
│   │   ├── food
│   │   ├── water
│   │   └── rest
│   ├── location                    # place name · terrain
│   └── verbs                       # the dock
│
├── alerts/                         # max 3 chips, by danger; gone once the problem is
│   ├── shivering → hypothermia     # body temp < 97°F → < 95°F
│   ├── bleeding                    # any blood loss
│   ├── wet                         # wetness cuts warmth noticeably
│   ├── injured-<part>              # injury cuts a capacity > ~10%
│   ├── exhausted | starving | dehydrated   # severe stage only (mild = bar colour)
│   ├── overloaded                  # carry over the limit
│   ├── wolf-nearby                 # sighting within a few tiles (no "stalked": seen in the world)
│   ├── cold-front                  # sharp turn for the worse within ~12h
│   ├── gear-failing                # anything below 20% condition
│   └── toasts/                     # one-off events, not chips
│       ├── job-ready
│       └── skill-up
│
├── at-decision/                    # inside the odds or preview of the choice it affects
│   ├── start-fire/                 # tool · tinder · skill · shivering · hand injuries
│   │                               # · wet hands · wind/shelter · dark
│   ├── tend-fire/                  # the fire panel: burn time against the night,
│   │                               # "burns about 3 h · dawn in 9 h"
│   ├── travel/                     # crossing time · hazard & injury risk (moving, legs)
│   │                               # · snow depth · carry slowdown · daylight vs trip
│   │                               # · camp fire time left (as last seen)
│   ├── leave-camp/                 # fire time left · jobs on the fire · weather coming
│   ├── forage/                     # what's left · likely finds · hidden finds searched
│   │                               # · perception (dark, sight) · daylight
│   ├── sleep/                      # fire layout vs night length · shelter & bedding
│   │                               # · overnight temp (front) · watch · predators sighted nearby
│   ├── eat-drink/                  # calories · hydration · raw/spoiled risk · days of food
│   ├── craft/                      # materials · odds · skill
│   ├── hunt-fight/                 # weapon · injuries · moving · animal state
│   ├── treat/                      # injury/effect · medicine · odds
│   └── dress/                      # warmth per slot · wetness
│
├── on-demand/                      # one key or click away
│   ├── body/            [B]        # injuries by part · damaged organs · capacities
│   │                               # · blood · effects + modifiers + fix · body temp
│   │                               # & trend · feels-like breakdown · clothing · skills
│   ├── place/           [X/click]  # ground & footing · shelter · bedding · cache · fire
│   │                               # fuel · on the fire · forage · game · water · traps
│   │                               # · curing · carcass · tracks · people · exits
│   ├── pack/            [I]        # items · weights · tool condition · capacity
│   ├── people/          [P]        # activity · needs · opinion · memories
│   ├── journal/         [J]        # log · discoveries
│   └── weather/         [hover]    # air temp · wind · sky · precipitation · front detail
│
├── map/                            # anything with a location; read at a glance
│   ├── camp-fire                   # as last seen, fading with age
│   ├── sightings                   # with age
│   ├── tracks                      # within perception range
│   ├── named-places
│   ├── ground-knowledge            # known · remembered · unexplored
│   ├── job-rings                   # at camp
│   └── people                      # where they are
│
└── cut/                            # not shown anywhere by default
    ├── numbers-on-bars             # hover only
    ├── capacities-at-100%
    ├── healthy-organs
    ├── blood-when-full
    ├── hud-inventory-summary
    └── repeated-flavour-text
```

### Placement rules for anything new

1. If it doesn't change a decision, it goes no higher than `on-demand/`.
2. If it has a location, it goes on the map.
3. If it's only sometimes relevant, it goes in `alerts/` or `at-decision/`.
4. Nothing lives in two permanent places.

### Open placements

- **Carry load:** on demand plus an alert. If playtests show players watching it constantly while
  foraging, promote it to a single always-on bar.
- **Daylight left:** always on for now. It could move to the decisions it affects (travel, forage).

---

## Screen Layout

```
 1:05 PM  ☀ ▮▮▮▯▯ 3h35 light   🌡 −1°F feels like
┌───────────────────────────────────────────────┬─────────────────┐
│                                               │  drawer         │
│                   the map                     │  (only while a  │
│                                               │  task or sheet  │
│                                               │  is open)       │
│ ┌────────────┐ [Wolf nearby]                  │                 │
│ │▮▮▮▮▮▯ warm │ [Shivering]                    │                 │
│ │▮▮▮▮▯▯ food │ [Wet boots]                    │                 │
│ │▮▮▮▯▯▯ water│ [+1 more]                      │                 │
│ │▮▮▮▮▮▯ rest │                                │                 │
│ └────────────┘                                │                 │
└───────────────────────────────────────────────┴─────────────────┘
 [F Start fire] [G Forage] [C Return to camp]   Birch Stand · forest  [B][I][P][J]
```

- **Top strip:** clock, daylight bar, feels-like. Nothing else.
- **Map:** fills the screen. While the drawer is open the camera shifts so the player stays in the
  visible part.
- **Status (bottom-left, over the map):** the four vitals, with the alert chips beside them.
- **Drawer (right, over the map):** one at a time. It holds tasks (fire, forage, sleep) and sheets
  (body, place, pack, people, journal). Esc closes it.
- **Dock:** the verbs for this place, then where you are (opens Place) and the four sheet buttons.
- **Receipt:** a centred card over the map (see below).

On a phone the same pieces stack: top strip, map, then the dock with verbs two per row. The drawer
covers the map while open.

---

## Components

Each lists what it shows and where its data comes from in the rebuild.

### Vitals

Four bars for warmth, food, water and rest, with no numbers. A bar turns orange-red below 35% and
pulses red below 15%. Warmth is ember-coloured while a fire is warming you, frost-blue otherwise.
Hover shows the number and trend (for warmth: body °F and °F/hr).

- **Data:** the player agent's body state.

### Alerts

Chips beside the vitals, ordered by danger, at most three, plus a "+N more" chip that lists the rest
on hover. Clicking any chip opens the Body sheet. A chip disappears when its condition clears: back
at the fire, *Shivering* and *Wet boots* go away on their own.

- **Colours:** danger (blood red), cold (frost), warning (ochre).
- **Triggers:** the thresholds in `alerts/` above. Mild stages never get a chip; the vitals bar
  colour already says it.
- **No fire alert.** A fire that won't last the night is not a chip or a warning. The fire panel
  shows the fact, "burns about 3 h · dawn in 9 h", and the player reads it (decided 2026-11-22,
  decision "The UI shows what the game knows").
- **Data:** derived each tick from body state, the knowledge model (sightings) and the
  weather forecast. Nothing stored.

### Odds sheet

Shown at the decision for every bet (fire, craft, treat; cook later). The total is large; a segmented
bar shows base and additions in solid colour and subtractions hatched; a factor list gives each term.
Conditions appear here as factors, by the same names used in alerts and the Body sheet (*Shivering*,
*Frostnip*, *Damp hands*). This is what makes those facts matter without a permanent dashboard.

- **Data:** chance functions that return their factors (short-loop plan, feature 1).

### Trip preview

Opens when a tile is clicked (or an exit is picked). It shows:

- **Time:** total minutes, and each thing adding to it (terrain and snow, injuries, load).
- **Injury risk** on hazardous ground, with its factors, and a choice of **Careful** (slower) or **Go**.
- **Daylight left** against the trip ("back before dark" or "tight").
- **Your camp fire,** as you last saw it when away; time left when leaving camp.

The dock's travel buttons use the same calculation, so their times always agree with the preview.

- **Data:** `Travel` step costs and hazard terms as factors, daylight from weather, the camp fire
  from the player's knowledge (not the truth).

### Receipt

"While you were away" on returning to camp; "Morning" on waking. One card, timestamped lines with an
icon and the actor, a **Collected** row for anything gathered automatically, and Continue (Space).
The entrance animation must never start from opacity 0: the card has to be visible even if the
animation doesn't run.

- **Data:** the tile journal plus the knowledge diff (short-loop plan, feature 4).

### Drawer sheets

- **Body:** the full vitals with numbers and trends; effects with their modifiers; injuries with a
  pixel body diagram; impaired capacities; the feels-like breakdown; clothing by slot; threats;
  skills.
- **Place:** for where you stand, everything known about the tile and its exits; for any other tile,
  the trip preview first, then what's known, marked "as you last saw it" when out of sight.
- **Pack, People, Journal:** as in the prototype.

### Map

Remembered ground is desaturated; unexplored ground is dark, with a dithered edge. The camp fire shows
as last seen with its age ("Roaring · 2h ago"); sightings carry their age; job rings sit over the camp
when it's in view. The selected tile gets ochre corner brackets; hover gets bone-white ones, plus a
tooltip of what you know.

- **Data:** the player's beliefs only (clean-room decision 7).

---

## Visual Language

**Concept:** the world is cold, and the interface is the warm inside of a hide shelter. The map keeps
the art's pale snow; the panels around it are smoked hide.

**Colour tokens** (from the prototype; the accent colours are sampled from the sprites):

| Token | Value | Use |
|---|---|---|
| `ink` | `#16110d` | panel ground |
| `ink-2` | `#211913` | raised surfaces |
| `ink-3` | `#2d231b` | wells, empty bar tracks |
| `seam` | `#4a3726` | borders |
| `bone` | `#eadfc8` | text |
| `bone-2` | `#b3a68e` | secondary text |
| `bone-3` | `#7d715f` | muted text |
| `ember` | `#f5811f` | the active thing, the fire, primary buttons |
| `ochre` | `#ffc93c` | rare finds, ready, level-ups, selection |
| `blood` | `#d13b1f` | failure, danger |
| `minus` | `#cf6f4c` | factors that subtract |
| `lichen` | `#9fbb86` | factors that add |
| `frost` | `#8fa6b7` | cold |

**Type:**

- **Pixelify Sans** for headings, labels, keys and button text.
- **Barlow Semi Condensed** for body text and **all numbers**, semibold with tabular figures. Numbers
  never use the pixel font: it was hard to read.
- Turn off ligatures. The pixel face draws "fi" as a single glyph ("Start Are"), and CSS
  `font-variant-ligatures` alone did not stop it.

**Scale:** body text about 18px, small labels 13px, icons 20px (40px in headers and the dock). The
first pass at 15px/11px was too small to read comfortably.

**Shapes:** notched corners (a clip-path that cuts each corner by a few pixels) to echo the pixel
grid; hard borders; no soft shadows or rounded corners.

**Map scale:** 64px tiles. The 96px terrain tiles are downscaled; 16px sprites draw at an exact 3×
with nearest-neighbour filtering, so they stay crisp. Time is in 12-hour format.

**Motion:** bars fill in steps; failures shake once; new finds flash briefly. Respect reduced-motion
settings.

---

## Keyboard

| Key | Action |
|---|---|
| F | Start or tend a fire; strike again when the fire panel is open |
| G | Forage; search when the forage panel is open |
| R | Search again (forage) |
| U | Build up the fire |
| Z | Sleep (at camp); confirm when the sleep panel is open |
| C / T | Return to camp / travel out (as offered in the dock) |
| B, X, I, P, J | Body, Place, Pack, People, Journal |
| Enter | Go (trip preview) |
| Space / Enter | Continue (receipt) |
| Esc | Close the drawer |

---

## Verifying It

The prototype has `?shot=<state>` hooks that drive it into one state for a headless screenshot:
`fire`, `failed`, `forage`, `foraged`, `receipt`, `tend`, `sleep`, `body`, `here`, `travel`, `far`,
`pack`, `people`, `log`.

Two lessons from checking it:

- **Look before shipping.** Two broken versions went out that a single screenshot would have caught (a
  blank map from a name clash, and a receipt card invisible because its animation started at
  opacity 0).
- **Headless Chrome on macOS won't make a window narrower than 500px.** Test phone width by loading the
  page in a 390px iframe.

For phase 5, the same habit applies: screenshot each state at desktop and phone sizes and read the
console before calling it done.

---

## Additions to the Rebuild's Phase 5 Gate

- The UI renders the player's beliefs: a test leaves camp with the fire roaring, lets it die, and the
  map still shows it roaring until the player sees camp again.
- A calm screen (no alert conditions) shows only the always-on layer.
- No alert shows for a mild condition; at most three alerts show at once.
- Every travel time shown in the dock equals the trip preview's total for the same destination.
- Every odds sheet's factors sum to its total.
