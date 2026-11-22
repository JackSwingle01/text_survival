# The UI shows what the game knows; text never predicts a consequence

A fact the player needs lives in the UI, by information hierarchy: bars, icons and colour first, hover
and click for the detail. The prototype UI (`dev/prototypes/ui/index.html`) shows the style. Text is
kept for refusals, status, what happened, and once-per-game tutorials. A full pack is the bar filling,
then a refusal. Deep snow is on the tile and its sheet. Bleeding is an effect in the status bar. No
text warns of a consequence ahead of time ("the fire will die before you wake"): the player reads the
facts and learns the consequences, which is how player knowledge becomes progression. The old game
mixed the two, with pre-commit warnings on foraging, sleep and the fire; those warnings are not
restored, and the facts they carried move into the UI instead.

**Considered Options:** restore the old warning text (the parity audit recommended it for sleep, the
fire and foraging); facts in the UI and no predicting warnings (chosen).
