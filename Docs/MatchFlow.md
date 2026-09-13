# Tournament match flow

Enter Play in `Assets/Scenes/TCGBoard.unity`. `TowerMatchController` presents five 3D floors, rolls two six-sided dice with automatic tie rerolls, and starts the higher roller. The human controls Orren. The second floor deliberately says **The Sleepless Eye vs ?**, as requested; higher pairings remain **? vs ?**.

Known tower leaders display their existing card artwork in framed portrait slots with names underneath: Orren Dawnbringer and The Sleepless Eye on floor one, and The Sleepless Eye on floor two. Unrevealed slots retain a question mark. Portraits resolve through the same card-art library as the board avatars and preserve the artwork's proportions.

## Controls

- Replenish continues automatically after the last draw animation. Any stage without a legal action advances automatically, including empty Events and Recover Objects. **Next stage** is shown only while you have optional actions remaining. During an opponent attack, select your ready defender, then the attacking enemy; use **Resolve combat** to decline any remaining blocks.
- Settlements (population centres) are never drawn or played: every PC in the deck sits in a pool from the first turn. During **Select Destination** (after Realms) a popup shows the settlements whose land you hold as a pile, one face at a time — browse with the arrows (or ← →), **Travel here** to go there for the turn, or **Stay** / **Skip**. The **DESTINATION** zone only ever shows that one token.
- Characters, encounters and objects are played at the destination and only there: a character or encounter must be born there (`startingPC` / `birthplaces`), an object must be of a kind the settlement trades in (`objectType` against the PC's `objectTypes`). One such play taps the destination for the rest of the turn (it shows **USED**); Undo untaps it. Armies deploy anywhere.
- Pin a hand card and select **Play card** during its deployment stage. Objects prompt for a friendly character. An encounter's face only says where it can be investigated; investigating it spends the card (`MatchRules.ResolveEncounter` is the outcome hook, and an unregistered outcome still allows the play).
- Click a glowing land during Muster or Events to tap it for mana (or pin it and use **Tap land**). Mana can be gathered as cards are needed; Realms never allows land tapping. The opponent gathers mana during deployment as needed. Muster and Events remain available when ready lands can fund a valid card, and empty stages still skip.
- Click your armies/characters during stage 5 to commit attacks. Cards tagged `ChooseTarget` prompt for an enemy target.
- Stage 7 prompts for each dead character's object. Select a surviving friendly character, or offer it to the enemy and select an eligible enemy. If neither side has a character, the object is discarded.
- The opponent plays automatically, except that the human chooses human blockers and human object recipients. It travels to the settlement that the most cards in its hand can be played at, staying on a tie.
- A pulsing mint border identifies legal cards/tokens for the current action, including object recipients, attack targets and blockers. Illegal actions have no button in the preview. New non-Mounted units enter untapped and show **NEW / ATTACK NEXT TURN**; they can defend immediately.

The tower reuses the Golden Vale's original limestone, sunlit stone, ink, blue slate and verdigris materials. Dice use dedicated fine-grain ivory and slate surfaces derived from that palette, with polished gold details instead of enlarged masonry textures. Board updates preserve surviving card-view instances: only cards whose tapped state changes animate, and only the new turn owner's non-Halted units untap. Material counters read the current match pools, including after a match reset.

## Rules implemented

1. Refill the active hand to its `HandLimit` (default 5; accepts 6/7), ready cards except Halted (the destination untaps too).
2. Clear previous environmental cards; deploy lands and environments.
3. Select Destination: choose one settlement from the pool whose region's land is on the board, or stay. The destination is the only PC on the field; the stage skips itself when there is nothing to choose.
4. Tap lands as needed for the existing seven materials, serving as mana. Pay printed costs for characters, armies, objects and encounters. Characters and encounters must be born at the destination, objects must be of a kind it trades in, and objects attach only to characters. The first such play taps the destination; armies ignore it.
5. Event stage and an explicit effect execution hook.
6. Tap attacking units. New units wait a turn unless Mounted; Fear prevents attacks. Target selection is an explicit ability hook/tag.
7. Ready defenders can each tap to block one attacker; multiple defenders may block the same attacker. Normal damage is simultaneous, assigned in blocker selection order up to lethal before the next blocker. Unblocked damage reduces life.
8. Resolve objects from all simultaneous deaths, then hand over the turn. The ending player's untapped lands automatically tap and grant their materials. Both mana pools persist across turns; the next player's lands untap as usual.

Combat uses the same base attack/defense calculation as the card face, including troop/character fallback values. Life defaults to 20 as a configurable prototype win condition. An empty deck currently draws no cards; it does not itself cause a loss.

## Content boundaries

The human plays Orren Dawnbringer using the `orren_the_kindled` deck against `the_sleepless_eye`. Match and tower labels use Orren; the avatar displays his full name, Orren Dawnbringer.

`useStarterDeck` selects the implemented deployment card types (lands, environments, characters, armies, objects, encounters) and gives an opening with lands and an inexpensive army; settlements are split out into the pool before the deck is built. It preserves card identities and costs. This is a development deck, not a balanced final deck. Disable the option to use the complete catalog deck.

Existing events describe the older hex-board game; their executable effects are absent. Unsupported events cannot be spent or discarded by playing them. Register `MatchRules.ResolveEvent` to implement effects; the starter selection excludes these cards until their effects are authored. Action, Spell and Ally cards likewise have no stage in the requested flow and are excluded from the starter selection.

The full keyword system (first strike, flying restrictions, triggered/probabilistic effects, ongoing status damage, equipment bonuses, etc.) remains to be implemented; this is the basic combat and turn-flow foundation. The destination is the only notion of location: there is no movement between settlements beyond picking a new one each turn. Later floor opponent identities/decks are intentionally unauthored. A first-floor victory returns to the tower and reports that boundary, rather than inventing a next match.

## Validation

`Tests/ManaCounterChecks.cs` verifies all fourteen visible material counters after human/opponent land taps, prevents duplicate grants, checks spending and mana carryover, and verifies binding to replacement match pools. Run after the opening in Play mode, then restart Play.

`unity command eval_file --file Tests/MatchRulesChecks.cs` checks stage gates, refill to seven, Halted, land/PC prerequisites, attachment validation, unsupported-event preservation, summon timing, tapping, multiple blockers, simultaneous deaths, mandatory spoils, and turn handoff. Live Play mode checks cover the cinematic transition, opponent initiative, board synchronization and UI captures. The cinematic uses `TournamentRenderer.asset` through a dedicated camera; the pipeline's default renderer stays unchanged.

`Tests/MatchEdgeChecks.cs` also passes targeted combat, environmental cleanup, no-recipient object recovery, printed stat fallback and lethal victory. `Tests/MatchBoardChecks.cs` passes the live board-to-rules integration checks (run in Play mode after the opening); it disables automatic opponent updates for deterministic testing, so restart Play afterward.

`Tests/MatchStageFeedbackChecks.cs` validates read-only legality, stage availability, new-unit readiness, mana ownership and untap boundaries. `Tests/MatchFeedbackLiveChecks.cs` checks automatic advancement, highlighted cards, removal of illegal preview buttons, direct attacks and preservation of token identities/rotations across actions and stages. The coroutine writes its result to `Temp/MatchFeedbackLiveResult.txt`; restart Play after running it.

Realms advances through Select Destination to Muster: there is no separate Gather Mana stage. `Tests/ManaTimingChecks.cs` checks this transition and funding during Muster and Events. `Tests/DestinationChecks.cs` covers the settlement pool, land-gated choices, the single destination token, destination-gated plays and tapping, undo, persistence across turns and the opponent's travel preference.
