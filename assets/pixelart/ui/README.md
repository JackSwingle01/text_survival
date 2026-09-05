# UI icon set

Thirty-five transparent 16×16 sprites authored with `tools/PixelArtCli`. Shared palette,
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
| bandage | Cloth wrap | Treat Wounds action, Injuries heading |
| clock | Clock face | Wait action, fire time remaining |
| journal | Bound journal | Discovery Log, examine/trail work |
| moon | Crescent | Night indicator, Sleep action |
| precipitation | Rain cloud | Precipitation and cooling details |
| shelter | Hide tent | Camp, shelter details and camp/tent actions |
| storage | Wooden chest | Camp Storage and cache work |
| sun | Golden sun | Day indicator and solar warming |
| wind | Wind strokes | Wind, weather front and wind chill |

Preview: `assets/previews/ui-icons-set3.png`, alphabetical as above.
The location actions use width-aware labels with full text tooltips; work icons
map to strategy types. Existing art covers Food & Water, Crafting, foraging,
hunting, fishing, butchering, and the curing rack. Combat controls are unchanged.

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
all thirty-five sources (CONNECTED checks are omitted for intentionally separate sun rays, wind strokes, and rain drops). The generated contact sheet has been visually reviewed.
Load native PNGs from `assets/icons/ui`, use point filtering, and display at
16×16 or 32×32 for crisp whole-pixel scaling. The sheet is for review only.
