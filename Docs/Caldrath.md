# Caldrath - The World of *Dark Before Dawn*

A fantasy world is growing colder as the fire that lights its sky fades. Free
People defend their homes, neutral powers look to their own survival, and Dark
Servants seek dominion over what remains. That is the background a player needs
before starting a game.

The setting supports the game: every nation has one fixed alignment, while
race and avatar role remain separate. Use familiar fantasy terms and explain
people and places by what they do.

## 1. The Ember and the Waning

Caldrath is a hearth-world. It was never lit by a star — it was **kindled**, once,
by powers that came from outside it and have not been seen since. What they left
behind is the **Ember**: a slow fire at the centre of the sky that has warmed and
lit the world through every age recorded.

The Ember is going out.

Not quickly. Not in a season, or a reign, or a life. But the winters run longer
than they did, the harvests come in thinner, the dark of the year has crept a
week further into spring within living memory, and the old star-charts no longer
match the sky. Scholars in three kingdoms have independently arrived at the same
arithmetic and independently declined to publish it.

This age is called **the Waning**.

The question the whole world is arguing about — with councils, with coin, with
armies — is what comes after it. People disagree about whether to restore the light, prepare for the cold, or use the crisis to seize power. These disagreements shape the conflict; they do not define anyone's race or nation.

### The Kindling

The **Kindling** is the claim that the Ember can be relit: that the powers who
lit it left a way to do it again, that the way is findable, and that it is worth
any cost. It is prophecy to some, engineering to others, and a recruiting slogan
to more than a few. Nobody has produced proof. Nobody has produced a disproof
either, which is the entire problem.

Believers hold that the night before the relighting will be the deepest the world
has known — that it gets worse, measurably and terribly worse, right before it
gets better. **The dark before the dawn.** It is the most useful sentence anyone
in Caldrath has ever written, because it explains away every defeat. Both sides
quote it.

---

## 2. The three alignments

The alignments are **Free People**, **Neutral**, and **Dark Servants**. They are
allegiances, not races, nations, or three compulsory beliefs about the Ember.

| Alignment | Card value | Meaning |
|---|---|---|
| **Free People** | `0` | Those who resist domination and defend the freedom of their peoples. Many hope to restore the Ember; others are fighting for their homes today. |
| **Neutral** | `2` | Those who serve neither side: independent realms, travellers, creatures, and powers with interests of their own. They may help or oppose either side. |
| **Dark Servants** | `1` | Those who serve the powers seeking to dominate the world. Some welcome the coming darkness; others seek power, act from fear, or obey their masters. |

Individuals can be brave, selfish, cruel, or conflicted without needing a new
alignment name. The Ember gives them something to struggle over, not a test of
membership. "Dawnward", "Unaligned", and "Nightborn" are retired alignment labels.

## 3. Nations, avatars, and races

These describe different things:

| Term | What it means |
|---|---|
| **Alignment** | Which side a nation and all its decks belong to. |
| **Nation** | The realm or faction a deck belongs to. All its decks share one alignment; its peoples can have different races. |
| **Avatar** | The character representing the chosen deck. Being an avatar is a role, not a race. |
| **Race** | What an individual character is: Human, Elf, Dwarf, Halfling, Celestial, and so on. |
| **Character group** | A data grouping for related character cards. It does not establish shared ancestry or prove that the cards are separate people. |

**An avatar does not have to be a Celestial.** A human ruler, an elven leader, a
dwarven lord, or another suitable character can represent a nation. Their race
belongs to the character and must not be inferred from the nation, alignment,
shared name, or avatar role. This principle does not automatically make every
character selectable; deck selection remains governed by the game's rules.

**Celestial** is the replacement term for Maia. It describes those particular
beings, not all leaders, spellcasters, or avatars. There is no additional race
called "Emberwrought".

The previous account of three companies of spirits, each carrying a coal, is
removed. It incorrectly made every nation a Celestial company and every avatar
one of its members. Shared names and alternate character cards do not require
that explanation. Alternate forms or titles can represent the same individual;
distinct characters remain distinct where the design establishes that.

### The current nations and avatars

The current deck families are **Free Realms**, **Tower Council**, and **Shadow
Dominion**: alliances of different peoples, not Celestial companies. Each nation
has exactly one alignment, shared by every deck in that nation:

| Nation | Alignment | Value |
|---|---|---|
| Free Realms | Free People | `0` |
| Tower Council | Neutral | `2` |
| Shadow Dominion | Dark Servants | `1` |

Village Tyrants is Neutral, like every other Tower Council deck. Its human
enforcers act from self-interest rather than serving the Shadow Dominion.

Every playable deck now has an explicit `avatarCharacter` pointing to a character
present in its `cardRefs`. The roster selects existing characters without
changing anyone's race, stats, or artwork. Titles below describe the decks;
existing `deckId` values remain stable internal identifiers.

| Nation | Deck | Avatar | Race | Alignment | Card ID |
|---|---|---|---|---|---|
| Free Realms | Dawn Rally | Orren Dawnbringer | Celestial | Free People | `50066` |
| Free Realms | Elven Wisdom | Elder Rowan | Elf | Free People | `50049` |
| Free Realms | Border Kingdoms | King Alden | Human | Free People | `50150` |
| Free Realms | Roadside Mischief | Mat Heatherthorn | Human | Free People | `50107` |
| Free Realms | Mountain Halls | Thorgrim | Dwarf | Free People | `50151` |
| Tower Council | Tower Council | Vaskar the Pale | Celestial | Neutral | `50128` |
| Tower Council | Arcane Studies | Corvan | Celestial | Neutral | `50120` |
| Tower Council | Ironworks | Ironjaw | Orc | Neutral | `50106` |
| Tower Council | Village Tyrants | Bill Thistlewood | Human | Neutral | `50025` |
| Shadow Dominion | Watchful Shadow | The Sleepless Eye | Celestial | Dark Servants | `50143` |
| Shadow Dominion | False Gifts | The Giftbearer | Celestial | Dark Servants | `50144` |
| Shadow Dominion | Haunted Wood | The Black Rider | Wraithlord | Dark Servants | `50099` |
| Shadow Dominion | Eastern Warbands | The Eastern Warlord | Easterner (Human) | Dark Servants | `50161` |
| Shadow Dominion | Frozen Throne | The Wraith King | Wraithlord | Dark Servants | `50115` |

**Dawn Rally**, led by Orren Dawnbringer, is the Halfling-focused Free Realms
deck. It includes Merric, the other Halfling adventurers, Halfling archers and
slingers, Hedge Wardens, their villages, and their travel and courage support.
Orren remains a Celestial leading and protecting those people.

**Roadside Mischief** is purely human-based, led by Mat Heatherthorn. Its units
are human townsfolk, adventurers, and rangers, including Firstbloods (humans of
the old blood). It uses rumours, concealment, theft, and practical tricks. It has
no Halflings, Halfling-specific support, or nonhuman units. The Halfling package
has moved to Dawn Rally; the other removed characters and encounters have homes
in appropriate decks such as Elven Wisdom and Dawn Rally.

Orren, Vaskar, and Nharok remain individual Celestials. Their related character
cards depict titles, guises, or stages of those individuals, not separate members
of three spirit companies. `orren` and `vaskar` group their respective forms;
`sharrow` identifies Vaskar's fallen form, and `nharok` groups Nharok's forms.
Orren Dawnbringer, Vaskar the Pale, the Sleepless Eye, and the Giftbearer remain
appropriate Celestial avatars. Corvan is a separate Celestial scholar.

The other forms remain available as existing support cards. For example, a
Dwarf leads Mountain Halls while Orren Stonefriend can accompany him. The Black
Rider leads Haunted Wood; the Hollow Sorcerer remains a separate Celestial card
in that deck. The Wraith King leads Frozen Throne; the Iron Crown remains a
Nharok form. Selecting a new avatar neither deletes nor transforms those cards.

## 4. The peoples

Use recognizable names first. A player should not need a glossary to discover
that a character is an elf or a halfling.

| Name used in prose | Existing race value | Description |
|---|---|---|
| **Human** | `Common = 0` | Humans of the ordinary realms. |
| **Elf** | `Elf = 1` | An ancient people with long memories. |
| **Dwarf** | `Dwarf = 2` | People of the mountain halls, known for craft and endurance. |
| **Halfling** | `Halfling = 3` | Small folk of farms, villages, and country roads. |
| **Celestial** | `Celestial = 4` | A spirit being; the renamed Maia race. Neither nation nor alignment determines this race. |
| **Wraithlord** | `Wraithlord = 7` | A once-mortal lord bound to an unnatural existence. |
| **Fire demon** | `Emberfiend = 10` | An ancient creature of fire and destruction. |
| **Humans of the old blood** | `Firstbloods = 12` | Long-lived humans descended from an older people. |
| **Skinchanger** | `Skinchanger = 13` | Someone who can take an animal form. |
| **Tree guardian** | `Woldwarden = 16` | A living guardian of the forests. |

Orc, Troll, Goblin, Spider, Dragon, Undead, Wildman, Southerner, Easterner, Beast,
Machine, and Vampire keep their existing terms. The table explains vocabulary;
it does not change enum names, numeric values, or any individual card's race.

Use **Elf**, **Halfling**, and **Celestial** instead of "Vaelen", "Hollowkin", and
"Emberwrought". Say "horse-lords" or "southern soldiers" when that is all the
reader needs to know. Do not introduce another people's name just to replace an
already clear description.

## 5. Writing and naming

Keep the Ember, the Waning, and the hope of a new dawn as the story background.
Characters can disagree about the fading light without explaining the entire
mythology on every card.

- Use familiar words for races, alignments, roles, and creatures.
- Introduce a place with its purpose: "Thornspire, the wizard's tower".
- Prefer readable place names such as Longwater, Ironreach, and Swanhold.
- Give a character a clear name or useful title. Do not manufacture a shared
  prefix to make every avatar sound like part of a supernatural family.
- Write about concrete motives: protecting a town, hoarding food, holding a
  mountain pass, seeking a cure, or gaining power.
- Keep invented names when they help identify someone or somewhere. Avoid
  near-spelling replacements and extra cultural labels that add memorization
  without helping the player understand the card.

For example: "An elven scholar searching for a way to restore the Ember" gives
us race, role, and motive in one sentence. No new vocabulary is needed.

## Data and migration reference

[`CaldrathMigration.json`](CaldrathMigration.json) defines the current roster by
stable card ID and deck ID, readable names, and specific text corrections.
[`CaldrathRenameManifest.json`](CaldrathRenameManifest.json) maps inherited names
to their current equivalents. Run the migration with
`python AgentScripts/migrate_caldrath.py`; validate it with
`python Tests/validate_caldrath.py`.

The migration populates all 14 playable decks' avatar fields, updates nation
labels in both the manifest and deck files, and rewrites deck themes around the
actual leaders. It also updates race vocabulary, prominent place names, linked
starting settlements, and inherited names in visible card text. Artwork lookup
keys are retained, with old names pinned when a renamed card relied on its name
for artwork. Card IDs, deck IDs, resource paths, individual races, costs, skills, and effects
remain unchanged. The current composition correction moves the Halfling package
to Dawn Rally and rebuilds Roadside Mischief around humans. Village Tyrants now
uses Neutral (`2`). These authorized deck changes are recorded in `deckEdits`
and `nationAlignments`; the migration updates references and counts together.

The completed catalog migration reviews all **1,034 cards**. Every card now has
a reference in at least one playable deck; a meta ownership pool does not count
as usage. The 224 previously unused cards received 285 thematic deck references,
including all Allies and Objects and the missing Actions and Spells. A final tag
audit also moved Travel in Safety to Dawn Rally and removed Through Nightwood
Shadows from Mischief; the latter remains in Mountain Halls. The full
catalog now has **2,190 playable-deck references**.

See [the complete card catalog](CardCatalog.md) for every card name, stable ID,
and deck assignment. `cardNames` is the complete name ledger in the migration
plan; `coverageAssignments` records why each previously unused card was placed.
Validation checks complete usage, name migration, fixed nation alignments, the
human-only Mischief composition, and the Halfling package in Dawn Rally.
Regenerate the report with `python AgentScripts/report_caldrath.py`.

Personal and settlement names still identify characters and places; they do not
introduce additional races or alignment systems. The `mithril` resource keyword
and existing art and implementation identifiers remain in use. This migration
does not replace the inherited artwork.

---

## Map reference: existing names

These are the names currently used by the card data, kept here for lookup. Players should meet places through play, with a short description of what they are, rather than needing to learn this list. These names are not a requirement to invent more names in the same style.

### The Marches — the north-west, where the Ember fails first

| Region | Was | Holdings |
|---|---|---|
| **Thornhollow** | The Shire | Hollyburrow, Millpond, Bramblemarch, Crook Hollow, Great Delving, The Elderwood, Stockleigh, The Deepholes |
| **North Kingdom** | Arthedain | Windtop, Crossway, Silverwatch, Lakewatch |
| **The Barrowfells** | The North Downs | Old Kingshold, Kingsfast, Marlbourne High |
| **West Downs** | Cardolan | The Cairnfields, Blackwold, Greymouth Haven, Stoneford, Old Bridge |
| **Troll Hills** | Rhudaur | High Bluff, Fenngate, The Troll Thickets |
| **Hollowvale** | Rivendell | Hidden Refuge, The Wedge |
| **West Coast** | Lindon | Starlight Tower, Northhaven, Southhaven, The Last Quays |
| **Bluecrags** | Ered Luin | Broadforge, Deepforge, Copperhall, Stonehall |
| **Elderforge** | Eregion | Songhill, The Swanfens, Old Elven City |
| **The Rimewater** | Bay of Forochel | Rimewatch |
| **The Palewall** | Misty Mountains | Redhorn Pass, The Skyrie, The Under Gate, **Deepdelve**, Kraghold |
| **Grimhold** | Angmar | **Frostkeep**, Goblin Mountain, Mount Frostfang, The Giantmoors, Blackdelve |

### The Middle Lands

| Region | Was | Holdings |
|---|---|---|
| **Longwater** | Anduin | Bearhall, Dragonwatch, The Irisfields, Rivermeet, Blossomford, The Crag |
| **Wyldmoor** | Dunland | Ironford, Hillcross, Moorhall, Moor Market, Moorfarm |
| **Windgate** | Gap of Rohan | Glimmerhold, Wealdwatch Camp, The Deepgard, **Thornspire**, Westerfold |
| **Horse Plains** | Rohan | Aldenburg, The Forest Circle, Dunbarrow, Easterfold, **Kingshall**, Stowbury |
| **The Whitespine** | White Mountains | The Oathstone, Stonefield, The Dead Roads |
| **Upper Sunlands** | North Gondor | Oldgrowth Forest, Sunwatch, **Sunspire**, Mornspire, Kingsquay |
| **Lower Sunlands** | South Gondor | Swanhold, Whitestrand, Riverhaven, Blackroot Post |
| **Longstrand** | Anfalas | Elven Haven, Shorewatch |
| **Greenmarch** | Ithilien | Blackfort, The Sunset Window, Blackfort Camp, Starholt |
| **Fenmire** | Nindalf | The Seeing Hill, Foamisle, The Drowned Fen |
| **Scorchlands** | Brown Lands | The Hearing Hill, Burnt Crossing, Scorched Hold |

### The North-East

| Region | Was | Holdings |
|---|---|---|
| **Wildermark** | Rhovanion | Woodbridge, Dalemoot, **Lonely Mountain (Lost)**, Merehaven |
| **North Nightwood** | N. Mirkwood | Greenhaven, Silverbourne, The Woodking's Halls |
| **South Nightwood** | S. Mirkwood | Woodhill, **Blackwood Keep**, Mossgable, Woodfolk Steading |
| **Nightwood Peaks** | Mtns of Mirkwood | Highwood, Raven Stair |
| **Goldenwood** | Lothlórien | Amberhold, The Golden Mound, Silver Valley |
| **Ironreach** | Iron Hills | Ironhall, Deepwatch, Deep Quarry |
| **The Blightheath** | Withered Heath | Silverfast, Frostwatch Keep, Northdelve |
| **Wine Country** | Dorwinion | Vineyard Landing, Vintners' Hall |
| **Amber Sea** | Sea of Rhûn | Amberport, Reaver Hold, Amberwatch, Amber Isle |
| **Eastlands** | Rhûn | Redwall, Starwatch, Copper Gorge, Eastport |

### Ashlands — the Dark Servants' heartland

| Region | Was | Holdings |
|---|---|---|
| **Cinderplain** | Gorgoroth | **Black Tower**, Wraithtower, Cinder Camp, **The Emberwound** |
| **The Emberpit** | Udûn | The Iron Jaws, **The Ironmaw** |
| **Slave Fields** | Nurn | Slave Barracks, Slavefort, Slave Mills, Slave Fields Watch, Slave Fields Storehouse, Slave Fields Garrison |
| **Ashflats** | Lithlad | Ash Camp, Ashen Spire, Ashhold, Cinderfort |
| **The Weftmarch** | Ungol | Weftspire, The Weft Stair, **The Pale City**, Pale Outpost, The Weaver's Lair |
| **The Battlewaste** | Dagorlad | Ashwatch, Warwatch, War Camp |

### The South

| Region | Was | Holdings |
|---|---|---|
| **Near Southlands** | Near Harad | Southhaven Isle, Oasis Keep, Palmford, Southford |
| **Far Southlands** | Far Harad | Southwatch Tower, Dune Market, Sandfort, Dustfort, Far Southport |
| **Corsair Coast** | Umbar | Saltwatch, Tidefort, Pearl Harbour, The Corsair Coast Havens, South Quay, Corsair Keep |
| **Eastern Steppe** | Khand | Redstone Camp, Steppe Camp, Horse Market, Eastern Harbour, Dustwell, Redrock Fort, Great Steppe Camp |

---
