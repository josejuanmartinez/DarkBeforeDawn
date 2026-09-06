# Cards

The card rendering system, ported from **RetroLOTR / Runeboard** (`C:\Users\jjmca\RetroLOTR`).

## What came across

| | |
|---|---|
| **Art** | `Assets/Art/Cards` — all 1033 images (2.5 GB), folders and import settings unchanged. Plus `Art/Fonts`, `Art/Materials`, `Art/Other`, `Art/UI`, which the prefabs reference. |
| **Prefabs** | `Assets/GameObjects/Reusable` — `Card`, `TokenCard`, `TokenCardMasked`, `CardPlayed`, `Hover`, `Tooltip`. |
| **Shaders** | `UI80sCartoon` (the Bakshi material) and `UIZoom` (ZoomImage). |
| **Scripts** | This folder, plus `Assets/Editor`. |

The prefabs kept their original `.meta` GUIDs, and so did every ported script, so all the
inspector wiring survives the move — no reauthoring was needed and none is needed now.

## The one structural change

Runeboard's `Card.cs` was 2178 lines that reached directly for `Game.Instance`, `Board.Instance`,
`DeckManager`, `ActionsManager`, `Illustrations`, `Colors`, `CursorManager`, `Sounds` and
`HexPathRenderer`. Its transitive closure was **200 files / 56k lines** — about two thirds of that
codebase — so nothing short of importing the whole game would have compiled.

Everything about how a card *looks and behaves as UI* was kept. Everything that made it a Runeboard
component now goes through **`CardServices`**: a set of optional hooks with working defaults.

With nothing installed, the prefabs render correctly, every card reads as playable, and clicks are
inert. Install a hook when this project grows a real rule for it:

```csharp
CardServices.Art         // ICardArtSource        — sprite lookup (a CardArtLibrary asset)
CardServices.Palette     // ICardPalette          — card-type border colors
CardServices.Playability // ICardPlayabilitySource — is this card playable, and why not
CardServices.Interaction // ICardInteractionHandler — what a click actually does
CardServices.Feedback    // ICardFeedback          — hover sounds, cursor changes
```

`SimpleResourcePlayability` is a ready-made playability source for the common case: costs, no
characters yet.

## Getting a card on screen

1. Put a `CardServicesInstaller` on a GameObject in the scene and assign
   `Assets/Art/CardArtLibrary.asset`.
2. Drop `Card.prefab` (or `TokenCard.prefab`) under a Canvas.
3. Either call `card.Initialize(myCardData)` from code, or add a `CardDataProvider`, type a card
   name and press **Apply** in the inspector to render it at edit time.

`CardDataProvider` resolves names through `CardCatalog`, which reads the `Resources/Cards.json`
manifest and the deck files it points at.

## How the decks are laid out

Cards are stored exactly once. Each card type has a **meta deck** that owns every card of that type
— `Resources/Cards/Meta/EventCards.json`, `ObjectCards.json`, and so on, marked `isMetaDeck` in the
manifest. Every other deck is a **reference deck**: it holds no card data, just a `cardRefs` list of
ids pointing into those pools. A card four factions can draw is one object referenced four times,
not four copies that drift apart.

Card ids encode their type: `cardId / 10000` is the `CardTypeEnum` ordinal, so an id alone says
which meta deck owns it (`CardCatalog.CardTypeForId`, `MetaDeckIdFor`). That also means changing a
card's type moves it to a different meta deck and renumbers it — do that through the Deck Manager's
Type dropdown, which repoints every reference, not by hand.

Two lookups, and the difference matters: `FindCardByName` is the convenient one but a handful of
names are deliberately shared by cards of different types (`Athelas` is both an Object and an
Event), and it can only return one of them. `FindCardById` is exact. `GetDeckCards(deckId)` returns
a deck's cards as clones stamped with that deck's own id, alignment and badge — the same card drawn
from Gandalf's deck and from Sauron's differs only in the badge it wears.

Edit decks through **Tools > Cards > Deck Manager** rather than by hand: a card edit there lands on
the meta deck that owns it, and the window tells you how many decks that change will reach.

## Art lookup

`CardArtLibrary` replaces `Illustrations`, which streamed ~1000 sprites through Addressables under
the label `default`. It is a plain asset holding direct references, so there is no extra package, no
async load, and no startup window where lookups return null.

`Assets/Art/CardArtLibrary.asset` ships pre-built with all 1033 card sprites and 110 UI sprites.
Rebuild it after adding or renaming art:

> **Tools > Cards > Rebuild Card Art Library**

Run it once now if you want the 13 sliced multi-sprite UI textures (`WhiteCloud`, `DarkCloud`, the
alignment banners) in the name lookup too — they're referenced directly by the prefabs either way,
so nothing is broken without it.

Lookup keys are normalized exactly as Runeboard did (strip diacritics, drop non-alphanumerics,
lowercase), so a card named `Gap of Rohan` still finds `GapOfRohan.jpg`.

## What was deliberately left behind

- **Playing, discarding and drawing.** `TryPlayCard` walked the action pipeline, consumed from a
  leader's deck, applied map reveals and granted resources. `Card.Play()` now delegates to
  `CardServices.Interaction` and runs the fly-out animation if the card was consumed.
- **`CardData`'s gameplay half** — combat effects, encounter outcomes, the object bonuses that fed
  `Duel.cs` and `Army.cs`. Field names for everything kept are unchanged, so Runeboard's deck JSON
  still deserializes; dropped fields are simply ignored.
- **Encounter target hexes.** With no hex board, an unrevealed encounter shows its `?` cover and
  `description` rather than naming a hex. `Card.RevealEncounterCard()` still plays the fade.
- **`CardBloomWheel`** (808 lines) and `CardPlayFlight` / `CardPlayFailure`. These are a *layout*
  and its animations, not the card face, and both were heavily tied to characters, armies and hexes.
  The hooks they need survive on `Card`: `SetTokenTint`, `LastKnownPlayable`,
  `CreateTokenVisualClone`, `CreateRealCardVisualClone` and `SnapshotCenterPreviewSource`.
- **`MaterialManager`** (skin sweeping). `SkinResweepOnEnable` survives with its GUID so the prefabs
  don't carry a missing script; assign `SkinResweep.Apply` to give it behavior.

## Known issue inherited from the source

`Card.prefab` has two missing sprite references (GUIDs `118a3ddc…` and `ea67fc94…`). They are
already missing in Runeboard — the art was deleted there at some point — so nothing was lost in the
move. Assign replacements or clear those Image slots.
