# Card keyword and icon help

Ability labels are underlined TMP links. Hover the words or the adjacent icon to see the ability's rule. Class, attack, defense, material and status icons also have explanations. A land's `Plains ground ⟨glyph⟩` line (`terrain:` link) explains what that ground means for a company crossing the region, with the ground's glyph in the popup's title and the kinds of army that fight there (`CardKeywordGlossary.TerrainHost`, curated from the catalog's armies per terrain: wolves, spiders and elves in the forest, dwarves and goblins in the mountains, and so on); an army's `Ambush: ⟨glyph⟩` line (`ambush:` link) says the unit can ambush an opponent travelling across regions of that terrain and who it fights alongside there. A `card:Name` link (`PcDescriptionBuilder.CardLink`) shows the named card itself on hover: a settlement's region (its land card) and dwellers (the army), an encounter's birthplaces, and the playable cards named in the Select Destination popup. The terrain glyphs (`plains`, `forest`, `hills`, `mountains`, `swamp`, `desert`, `shore`, `wastelands`) come from `environment_terrain_features`, the fallback sheet of `common_spritesheet`; `CardKeywordGlossary.TerrainSprite` maps `TerrainEnum` to them. Ability/status links take precedence over generic sprite help, so the mounts icon explains Mounted in an ability row and Mounts in a resource row.

Full Characters show their class levels beside attack/defense. Tokens show up to two class levels per line above their status and combat rows. Status icons appear only on Characters and Armies with active effects. Empty rows are hidden. Tokens group more than four statuses into rows of up to four icons with explicit line spacing.

`CardData.statusEffects` holds active effects and is independently copied by `Clone()`. Use `Card.SetStatusEffects(effects)` for a card, or `BoardCardView.SetStatusEffects(effects)` for a board slot (including stripped token clones). Pass null or an empty list to clear effects. The board method also refreshes an open inspection preview. These APIs update presentation; they do not apply damage, stat modifiers or effect durations.

The glossary follows `CardDisplayEnums.cs`. Bonuses whose magnitude is not defined there are described without inventing a number.

## Sprites

All referenced sprites resolve. `Discouraged` currently reuses `discouraging` because there is no sprite named `discouraged`. Add that sprite to `common_spritesheet` for a dedicated status icon, then change `CardKeywordGlossary.StatusSprite`. Blessed reuses `light`; Strengthened uses the existing `strengthened` sprite despite the legacy enum spelling `Strenghtened`. The previous shared ability and character-role sprite aliases remain in use.

## Verification

Run `unity command eval_file Tests/CardKeywordChecks.cs` in the connected Editor. It checks every enum explanation, TMP word/icon hit detection, icon precedence, status cloning, all class/status glyphs on the three card prefabs, and live status changes on stripped board tokens. `Tests/CardAbilityChecks.cs` checks full descriptions across the card catalog.

`Tests/CardKeywordVisualChecks.cs` renders full/token badge examples and the actual popup to `Tests/CardKeywordVisual.png`, and checks popup clamping.
