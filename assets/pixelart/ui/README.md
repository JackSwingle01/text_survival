# UI icon set

Eighty-three transparent 16×16 sprites authored with `tools/PixelArtCli`. Shared palette,
dark outlines, and upper-left highlights match the existing pixel-art pipeline.
The desktop UI loads these once at startup and releases them before the window closes.
They appear beside survival stats, inventory category buttons and item rows, and
foraging focus buttons, food selections, storage transfers, and fire controls.
Item rows prefer resource/gear icons and fall back to category symbols.

| File | Picture | Intended UI placement |
| --- | --- | --- |
| energy | Amber lightning bolt | StatsPanel: Energy |
| food | Berries | StatsPanel: Food; inventory Food category and Berries; forage food focus |
| foraging | Gathering basket | Forage general focus; inventory All category |
| fuel | Split wood | Inventory Fuel category; forage fuel focus; firewood items |
| gear | Stone axe | Inventory Gear category and stone axe tools |
| materials | Flint stone | Inventory Material category; forage materials focus; flint items |
| medicine | Leaf sprig | Inventory Medicine category; forage medicine focus |
| temperature | Thermometer | StatsPanel: Body Temp |
| vitality | Heart | StatsPanel: Vitality |
| water | Droplet | StatsPanel: Water; inventory Water resource |

## Second set

| File | Picture | UI use |
| --- | --- | --- |
| bone | Pale bone | Bone and ivory resources |
| clothing | Fur-trimmed tunic | Worn equipment |
| fire | Amber flame | Fire controls, fire-making tools, torches and ember carriers |
| fish | Silver-blue fish | Raw, cooked, and dried fish |
| hide | Animal pelt | Raw, scraped, cured, and mammoth hide |
| knife | Stone knife | Knives and scrapers |
| meat | Cut of meat | Raw, cooked, and dried meat |
| roots | Leafy tuber | Edible roots |
| rope | Coiled cord | Rope, plant fiber, sinew, and cordage tools |
| spear | Stone-tipped spear | Spear tools and weapons |

The second-set preview is `assets/previews/ui-icons-set2.png`, alphabetical in
this table's order. `UiIcons.ForResource`, `ForGear`, and `ForConsumable` share
mappings across menus. Icons represent item families: text still distinguishes
preparation, material, equipment slot, and warnings. Items without specific
art keep the first set's category symbols.

## Main HUD set

| File | Picture | UI use |
| --- | --- | --- |
| backpack | Hide backpack | Inventory action, Carry stat, salvage work |
| bandage | Cloth wrap | Injuries heading |
| clock | Clock face | Top-bar time, Wait action |
| journal | Bound journal | Discovery Log, examine/trail work |
| moon | Crescent | Night indicator, Sleep action |
| precipitation | Rain cloud | Precipitation and cooling details |
| shelter | Hide tent | Camp, shelter details and camp/tent actions |
| storage | Wooden chest | Camp Storage and cache work |
| sun | Golden sun | Day indicator and solar warming |
| wind | Wind strokes | Wind, weather front and wind chill |

Preview: `assets/previews/ui-icons-set3.png`, alphabetical as above.
Phase 2 wires icons into HUD navigation, core actions, travel, contextual work, and history controls. Food uses food; treatment uses medicine; crafting uses gear. Rare work uses a gear fallback pending Phase 5. Combat controls were completed in Phase 4 (below).

| File | Picture | UI use |
| --- | --- | --- |
| person | Blue bust | Survivor heading and People action |
| location | Blue map pin | Current location and destination headings; trail work |

The new symbols are shown in `assets/previews/ui-phase2-icons.png`. Unavailable controls retain their category art with a small blocked badge.

## Fire set

| File | Picture | UI use |
| --- | --- | --- |
| charcoal | Black angular chunks | Charcoal available and Collect Charcoal |
| ember | Banked coals over a glow | Ember carrier, Collect Ember, ember time remaining |
| sticks | Pile of loose twigs | Kindling: the stick count, the kindling fuel row, missing-kindling warnings |
| tinder | Teased tuft of fibre | The tinder section, tinder and birch bark fuel rows, missing-tinder warning |
| torch | Lit torch on a shaft | Light Torch |

Preview: `assets/previews/ui-icons-set4.png`, alphabetical as above. These five
exist to separate things the fire screen used to draw with one flame: an ember
is not a fire, kindling is not a log, and tinder is not either. `FuelIcon` in
`Desktop/UI/FireOverlay.cs` maps a fuel resource to its icon.

From the repository root:

```sh
dotnet run --project tools/PixelArtCli -- render-all assets/pixelart/ui assets/icons/ui
dotnet run --project tools/PixelArtCli -- sheet assets/pixelart/ui assets/previews/ui-icons.png --scale 8 --cols 5
```

The complete preview is alphabetical, left to right, top to bottom.
Render validates pixel dimensions, palette entries, and CONNECTED checks for
all forty-two sources (CONNECTED checks are omitted for intentionally separate sun rays, wind strokes, and rain drops). The generated contact sheet has been visually reviewed.
Load native PNGs from `assets/icons/ui`, use point filtering, and display at
16×16 or 32×32 for crisp whole-pixel scaling. The sheet is for review only.

## Shared control set (Phase 1)

| File | Picture | Current use |
| --- | --- | --- |
| text-size | Large and small type glyphs | UI size presets |
| warning | Amber triangle and exclamation mark | Shared caution symbol, available for in-place warnings |
| blocked | Red stop disc with pale bar | Focusable unavailable shared controls |
| confirm | Green check | Selected UI size and Apply size |
| cancel | Red cross | Close UI settings |
| back | Blue left arrow | Shared back action, available for later screen migration |
| continue | Blue right arrow | Shared continuation action, available for later screen migration |

`assets/previews/ui-controls.png` shows this batch. Sources were rendered and
visually checked with PixelArtCli. Shared icon buttons use 16px art at Compact
and 32px at Comfortable/Large, wrap labels within the available width, and
retain native keyboard focus. Legacy item-row icons still use 16px pending the
remaining layout phases.

## Phase 3 actions

| File | Meaning | Controls |
| --- | --- | --- |
| transfer | Opposing blue arrows | Explicit transfer after selecting source item and amount |
| drop | Downward amber arrow into a tray | Drop one, all, or gear |
| equip | Green arrow into a socket | Equip weapon |
| unequip | Amber arrow out of a socket | Unequip weapon/clothing, remove accessory |

Execution controls reuse food, water, fire, gear and foraging art; exits use back.
The new assets pass CONNECTED checks and were reviewed in a 6× contact sheet
(`assets/previews/ui-phase3-icons.png`) and at their 32px display size. Full native
screen review is pending because the Phase 3 preview run could not find a monitor.

## Phase 4 combat and inspection

| File | Picture / use |
| --- | --- |
| block | Blue shield; block and careful crossing |
| dodge | Blue figure evading left |
| shove | Open palm with force arrow; also unarmed attack |
| throw | Hand launching a stone |
| intimidate | Threatening face and outward marks |
| flee | Running figure and exit arrow |
| call-help | Green signal flag with cross |
| call-retreat | Red signal flag with return arrow |
| inspect | Magnifying glass; inspection and assess |
| target | Gold reticle; selected combat target |

All ten pass CONNECTED checks. Reviewed `assets/previews/ui-phase4-icons.png` at
6× and a 32px contact sheet. Full-screen rendering remains pending because the
native preview runner cannot find a monitor. Attack reuses existing gear art.

## Phase 5 remaining coverage

| Files | Actual use |
| --- | --- |
| follow, unfollow | Companion invite / dismiss |
| give, ask | Companion transfers; gain outcomes |
| beasts | Discovery Beasts category |
| net, trap, ice-cutting | Net, snare and ice work |
| salvage, burial, dig, project | Salvage, burial, excavation and crafting projects |
| curing | Curing rack controls and details |
| blood, capacities, effects | Survivor/companion condition and event damage/effects |
| tracks, bedding, decay | Location details and trail work |
| help, sound, mute, restart, recenter | More / Help controls and system log entries |
| cost | Event choice costs |

The 25 new sprites were reviewed in `assets/previews/ui-phase5-icons.png` (6×)
and a 32px contact sheet. Twenty-two have CONNECTED assertions; sound, mute and
recenter intentionally have separate marks. Native screenshots at Comfortable
and Large now work and were reviewed. Missing loaded textures display a visible
placeholder and log the missing key. A coverage test checks every concrete work
strategy, resource/discovery icon mapping and literal shared icon reference.
