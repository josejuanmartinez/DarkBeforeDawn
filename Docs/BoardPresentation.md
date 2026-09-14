# Board card presentation

Open `Assets/Scenes/TCGBoard.unity` and enter Play mode. The `Board` component on **TCG Board** holds all twelve visualizers, the existing Card/TokenCard prefabs, shared preview, and hand settings. Authored prefab instances supply the starting collections; match setup can replace them through the API below. `defaultHandSize` (5) sets the hand's baseline sizing. `maximumHandSize` (7) is enforced by the hand collection API.

`BoardPresentation` loads the selected `BoardSkin` from the scene's **Skin Manager** and builds the runtime layout and visual treatment. `Assets/Resources/Skins/Default.asset` contains the current design: dark translucent surfaces over the existing animated valley, bronze opponent zones, teal player zones, a shared environment row, live counts and a separately framed hand. Presentation runs before the visualizers adopt the authored cards. The scene's authored instances are retained; styling is rebuilt each time Play starts.

## Skins

Select **Skin Manager** in the Hierarchy. Its Inspector contains the skin library, an **Active Skin** dropdown, and the selected asset's parameters. **Duplicate Active Skin** creates a separate asset, registers it, and selects it. You can also use **Assets > Create > Cards > Board Skin**, add the asset to the list, and select it.

Each skin exposes Colors (including an optional full-card type palette), Typography (font references and sizes), Chrome (header/footer labels, outlines, insets and spacing), Cards (piece dimensions, text margins and shadows), Tokens (captions and highlight timing), Piles (stack geometry and empty-state styling), Preview (dimensions, buttons and timing), and all twelve Zones (labels, bounds and accent roles). Zone bounds use normalized minimum/maximum coordinates. Card-piece sizes and offsets are canvas units. Hand capacity and card collections remain board/gameplay settings.

Changing the active skin or editing the active asset refreshes the UI during Play. The old generated UI is removed, card order and pile selection are retained, and open inspections close. Asset edits persist after exiting Play; duplicate Default before experimenting. Edit mode continues to show the authored scene layout. Choose the skin in Edit mode to save the scene's startup selection.

```csharp
skinManager.SetSkin(skinAsset); // registered asset
skinManager.SetSkin(0);         // library index
skinManager.ApplyActiveSkin();  // refresh after changing asset fields from code
```

`Tests/BoardSkinChecks.cs` verifies default loading, invalid selection rejection, repeated switching, absence of duplicate cards/chrome, colors, geometry, preview parameters, collection order and pile selection preservation. It passed 73 assertions. Restart Play after running it to discard its temporary skin library entries.

Full cards use a 300×410 frame, type-colored accents, separate title and rules panels, and a dark requirements ribbon. Tokens retain their artwork and type glow and gain a name caption. Piles show layered card stock. UI labels and card rules use the bundled sans-serif fonts; the masthead retains the existing display font.

### Player materials and avatars

Each player has a separate visible pool for Leather, Mounts, Timber, Iron, Steel, Mithril and Gold.
Click a land to tap it and add its printed grants; the same land cannot produce again until
readied. Click a card in your hand to play it and pay its printed material cost.
Failed payment changes neither the pool nor the hand. Joker costs consume any remaining materials
in the displayed order after specific costs. Character gold uses the existing character cost rule.

The single **END TURN** button on your materials panel empties both floating pools and readies the
next side's lands. It becomes **END OPPONENT TURN** for the manual return handoff; there is no
opponent-side button or automatic opponent controller yet. Material totals show their matching
card sprites, with the written names retained underneath. `board.HumanMaterials` and
`board.OpponentMaterials` expose the same `TrySpend(CardData)` operation for match controllers.
Skin refreshes preserve land tap state, including independent copies of the same card.

Set **Human Avatar Card Name** and **Opponent Avatar Card Name** on Board to catalog names.
The scene uses Orren Dawnbringer and Nharok as examples. Avatars show full cards and support the
same hover/pin inspection as other cards. `BoardSkin.players` controls their bounds and materials
panel typography. The avatar is separate from hand and battlefield collections.

Playing currently pays and moves cards: lands, settlements, armies/characters/allies and
environmental cards go to their board zones; actions/events/spells/objects go to discard.
Effect resolution, object attachment, targeting and non-material play restrictions are not implemented.
`Tests/PlayerMaterialsChecks.cs` checks payment atomicity, ownership, duplicate land taps,
skin refresh, turn reset and avatar inspection through Unity CLI in Play mode.

```csharp
// Populate from your match model. Collections retain the supplied order.
board.hand.SetCards(openingHand);
board.humanLands.SetCards(lands);
board.humanPopulationCenters.SetCards(populationCenters);
board.humanArmies.SetCards(armiesAndCharacters);
board.environmental.SetCards(environmentalCards);
board.humanDiscard.SetCards(discardPile);
board.humanVictoryPoints.SetCards(victoryPointPile);

bool added = board.hand.TryAdd(drawnCard); // false when full or null
board.hand.Remove(playedCard);
board.humanDiscard.TryAdd(playedCard);    // last item becomes pile face
```

Use the corresponding `opponent*` references for the opponent. `SetCards` copies the collection, rejects null entries, and rejects oversized hands without changing existing data. `Cards` is a read-only list; CardData objects remain the match model's objects. If card data changes, call `SetCards` again to refresh the visuals. Author initial cards with Card.prefab under the hand and pile anchors, and TokenCard.prefab under other anchors, setting each CardDataProvider's card name.

- Full rows preserve aspect ratio, fit the available height, and shrink/overlap as needed. A hovered card opens an enlarged copy above the board; its original slot does not move.
- Token grids choose rows and columns to maximize token size. Environmental tokens stay in one horizontal row. Neither layout paginates. Very large collections necessarily produce small tokens.
- Decks display only the last card and total count. Hover opens a full preview with previous/next buttons; browsing changes the preview selection, never the pile order. Buttons stop at the ends. Reopening starts at the last card. Empty piles show a subtle placeholder and zero count.
- One shared preview uses sorting order 300, fades in, and opens beside the source where space permits. Deck previews accept pointer input across their face and buttons, with a short exit grace period. Non-deck previews let pointer input pass through. Previews clamp to the canvas bounds and close when their source collection changes or disappears.
- The preview is hover-only and never sticks: a click on a card either acts on it (plays a playable hand card, taps a land, takes the match action the footer names) or does nothing. The footer says why a card cannot act, or what a click would do. Piles keep previous/next controls and show the selected index and total.

The visualizers manage presentation only; drawing, playing, discarding, and game rules belong to match logic. Card clicks act through the board or the match controller and never inspect. Generated full-card instances apply the reading layout to runtime clones. A CardServicesInstaller supplies the existing art library.

Scene wiring is saved in TCGBoard; the one-time editor setup tools have been removed.

## Regression checks

With this project connected to Unity CLI, enter Play mode in TCGBoard, then run:

```powershell
unity command eval_file --file (Resolve-Path Tests/BoardPresentationChecks.cs).Path --format json
unity command eval_file --file (Resolve-Path Tests/BoardResizeChecks.cs).Path --format json
unity command eval_file --file (Resolve-Path Tests/BoardInspectionChecks.cs).Path --format json
```

The first two checks temporarily populate the board with catalog data. Restart Play mode afterward to restore the authored scene before running the inspection checks. Validation passed: 1,137 layout/collection assertions, 36 responsive zone sizes, four edge previews, and 62 inspection checks covering real UI raycast results and dispatched pointer/click events. Escape dismissal was checked through a queued keyboard event. Visual review covered the QHD Game view (captured at 1920×1080) and a 1366×768 Game view, including token and deck inspections. These checks do not simulate a complete match or every possible pointer trajectory.

Review images: `Board-Refined.png`, `Board-Deck-Inspection.png`, and `Board-Token-Inspection.png` in this folder.
