# Board card presentation

Open `Assets/Scenes/TCGBoard.unity`. The `Board` component on **TCG Board** holds all twelve visualizers, the existing Card/TokenCard prefabs, shared preview, and hand settings. The scene starts with empty collections; match setup supplies the data. `defaultHandSize` (5) sets the hand's baseline sizing; use it as the opening draw count in match logic. `maximumHandSize` (7) is enforced by the hand collection API.

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

Use the corresponding `opponent*` references for the opponent. `SetCards` copies the collection, rejects null entries, and rejects oversized hands without changing existing data. `Cards` is a read-only list; CardData objects remain the match model's objects. If card data changes, call `SetCards` again to refresh the visuals. The Inspector's serialized card collections also support initial data.

- Full rows preserve aspect ratio, fit the available height, and shrink/overlap as needed. A hovered card opens an enlarged copy above the board; its original slot does not move.
- Token grids choose rows and columns to maximize token size. Environmental tokens stay in one horizontal row. Neither layout paginates. Very large collections necessarily produce small tokens.
- Decks display only the last card and total count. Hover opens a full preview with previous/next buttons; browsing changes the preview selection, never the pile order. Buttons stop at the ends. Reopening starts at the last card. Empty piles show a subtle placeholder and zero count.
- One shared preview uses sorting order 300. Deck previews accept pointer input across their face and buttons, with a short exit grace period. Other previews let pointer input pass through to the source slots. Previews clamp to the canvas bounds and close when their source collection changes or disappears.

The visualizers manage presentation only; drawing, playing, discarding, and game rules belong to match logic. Card clicks currently do not invoke gameplay. Generated full-card instances normalize the legacy prefab's mismatched root/art dimensions and border ordering. The original prefab remains unchanged. A CardServicesInstaller supplies the existing art library.

Scene wiring is saved in TCGBoard; the one-time editor setup tools have been removed.

## Regression checks

With this project connected to Unity CLI, enter Play mode in TCGBoard, then run:

```powershell
unity command eval_file --file (Resolve-Path Tests/BoardPresentationChecks.cs).Path --format json
unity command eval_file --file (Resolve-Path Tests/BoardResizeChecks.cs).Path --format json
```

These checks temporarily populate the board with catalog data. Exit Play mode afterward to restore the empty scene. They check hand limits and atomic rejection, empty/single decks, pointer-enter and arrow-click event handlers, deck order and bounds, preview reset/cleanup, dense token/row containment, 36 responsive zone sizes, and four edge previews. Screenshots were also reviewed at 1920x1080. They do not simulate a complete game or verify every possible pointer trajectory.
