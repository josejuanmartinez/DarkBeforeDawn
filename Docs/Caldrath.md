# Caldrath — The World of *Dark Before Dawn*

The setting bible. Everything the player reads on a card — characters, settlements,
regions, armies — lives in this world. Objects, Encounters, Events and Spells still
carry inherited names; see [Known residue](#known-residue).

The mechanical shape of the game is unchanged. This is a reskin with its own
mythology, not a redesign: three rival mage-powers, a three-way alignment split,
the same peoples in the same places doing the same things.

---

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
armies — is what comes after it. Three answers, and they are the three factions.

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

## 2. The three factions

The alignment split is unchanged (`alignment` 0 / 2 / 1 in the card data). Only
the names are new, and they describe *what a card believes about the Ember*.

### The Dawnward — `alignment: 0`

Those who spend themselves to see the Ember relit. Not "the good"; the Dawnward
include grim, ruthless, and frankly unpleasant people. What unites them is a
willingness to pay now for a light they will not live to see.

Their weakness is the one you would expect: a faction that believes the worst
night precedes the dawn can be talked into accepting almost any amount of
suffering as evidence that things are going well.

### The Unaligned — `alignment: 2`

Realms, wanderers, monsters and old powers that answer to no vigil. Most of
Caldrath is here — most people, most of the time, are trying to get a harvest in
rather than settle a cosmological argument.

The Unaligned are not neutral out of cowardice. Some have concluded the Ember's
death is simply a fact, like a river changing course, and that the correct
response is to move. Others were here before the Ember and expect to be here
after it. The rest have noticed that both crusading factions burn through
villages at roughly the same rate.

### The Nightborn — `alignment: 1`

Those who would let the Ember die, and rule what the dark leaves.

The Nightborn are not nihilists, and this is the thing their enemies never
understand. They believe the Kindling is a lie — a comfortable story that keeps
the world docile while it freezes. Their reading is that the dark is *coming
regardless*, that it cannot be bargained with, and that the only rational act
is to build the thing that survives it: to become creatures of the night early,
by choice, while there is still time to choose.

They are recruiting well. They have the better argument about the evidence, and
everyone can feel the winters.

---

## 3. The Emberwrought

Long ago, spirits were sent into Caldrath carrying coals of the original fire —
not to rule, but to keep the Ember's memory until it was needed. There were
several. Three still matter.

They are the **Emberwrought** (`RacesEnum.Celestial`), and each is a playable
power with its own subdecks. Every one of them has concluded the others are the
reason the world is dying.

### Orren Greycloak — the Dawnward power

`nation: Orren` · Wanders. Owns nothing. Turns up where it is least convenient
and most necessary, which is the same place. Carries his coal openly and spends
it in small amounts on other people, which is the slowest possible strategy and
the only one that has ever worked.

| Persona | Deck | The angle |
|---|---|---|
| **Orren Greycloak** | *(base)* | The wanderer himself |
| **Orren Farwalker** | `orren_farwalker` | Refined spellcraft, lore, wards, precise intervention |
| **Orren Stormherald** | `orren_stormherald` | The ill omen among Men; realms hardening for war |
| **Orren Stonefriend** | `orren_stonefriend` | Mountain-halls, treasure, endurance, old alliances |
| **Orren the Meddler** | `orren_greycloack` | Mischief, roadside incidents, small folk punching up |
| **Orren the Kindled** | `orren_the_kindled` | The return: renewal, courage, cleansing, decisive rally |

### Vaskar — the Unaligned power

`nation: Vaskar` · The one who read the arithmetic first and correctly. Vaskar
studied the Ember longer and more rigorously than anyone alive, concluded it
cannot be relit, and drew the reasonable conclusion that the world needs a
strong hand to manage the descent. Everything after that followed logically, one
defensible step at a time, until he was somewhere indefensible.

His seat is **Thornspire**, a black needle of a tower in the Windgate.

| Persona | Deck | The angle |
|---|---|---|
| **Vaskar** | *(base)* | The high mage entire |
| **Vaskar the Pale** | `vaskar_the_pale` | Rhetoric, control, persuasion, the Voice |
| **Vaskar Manyhued** | `vaskar_manyhued` | Layered wizardry, vanity, power by synthesis |
| **The Pale Hand** | `the_pale_hand` | The war engine: furnaces, industry, relentless assault |
| **Sharrow** | `sharrow` | The fallen ruin of him — coercion, spite, no magic left |

**Skeil** is his whisperer at foreign courts.

### Nharok — the Nightborn power

`nation: Nharok` · Let his coal go out on purpose. Nharok's position is the
Nightborn position stated by the only being with standing to state it: he has
*held* the Ember's fire and reports that it is finite, failing, and not
renewable. He offers the dark instead — organised, survivable, and his.

His seat is **Nharoth** on the Cinderplain, under the smoke of **the Emberwound**,
the wound in the earth where his coal was put out.

| Persona | Deck | The angle |
|---|---|---|
| **Nharok** | *(base)* | The Nightborn power entire |
| **The Sleepless Eye** | `the_sleepless_eye` | Surveillance, dread, control auras, board lock |
| **The Giftbearer** | `the_giftbearer` | Subversion and false gifts; conquest through trust |
| **The Eastern Shadow** | `the_eastern_shadow` | Dark diplomacy; the swelling of eastern war-bands |
| **The Iron Diadem** | `the_iron_diadem` | The witch-realm of Grimhold; terror, slaving, sorcery |

### Zhamor — the fourth seat

`nation: Zhamor` · First and greatest of the **Wraithlords**, and the only one
who acts on his own account. Zhamor holds **Dol Vorrath** in the South Nightwood
and runs a horror-court of spirits, spiders, dragons and slow magical decay.
Nominally Nharok's. Not reliably so.

The other eight Wraithlords: **Vorhal** (the Iron Diadem's own crowned wraith),
**Aduneth**, **Akhorrin**, **Dendrath**, **Hoarveth**, **Jindhur**, **Ovathir**,
**Rhenn the Defiled**.

---

## 4. The peoples

`RacesEnum` values are unchanged — only three display names moved.

| Race | Standing | Note |
|---|---|---|
| **Common** | The bulk of the world | Men of every realm |
| **Vaelen** *(Elf)* | Oldest of the speaking kindreds | Remember the Kindling; will not say what they remember |
| **Dwarf** | Deep-delvers | Of the line of **Durn**. Their halls are warm without the Ember, which they mention often |
| **Hollowkin** *(Halfling)* | Small folk of Thornhollow | Have opinions about cosmology and stronger ones about lunch |
| **Emberwrought** *(Celestial)* | The coal-bearers | See §3 |
| **Wraithlord** *(was Nazgul)* | The Nine | Chose the dark early, as the Nightborn doctrine advises |
| **Emberfiend** *(was Balrog)* | Fire that outlived its purpose | **The Deepflame** sleeps under Durndelve |
| **Woldwarden** *(was Ent)* | Tree-shepherds | **Barkfather** at the Dern Ring. Older than the argument |
| **Firstbloods** | Long-lived Men of the old blood | Rangers of the Marches |
| **Skinchanger** | Shape-takers of the Longwater | |
| Orc · Troll · Goblin · Spider · Dragon · Undead · Wildman · Southerner · Easterner · Beast · Machine · Vampire | | Unchanged |

Two peoples are named in prose but are not races: the **Andarim**, the black-blooded
lords of drowned **Andareth**; and the **Mearhost**, the horse-lords of Mearhold.

---

## 5. The world map

43 regions, grouped as the world groups them.

### The Marches — the north-west, where the Ember fails first

| Region | Was | Holdings |
|---|---|---|
| **Thornhollow** | The Shire | Hollyburrow, Millpond, Bramblemarch, Crook Hollow, Great Delving, The Elderwood, Stockleigh, The Deepholes |
| **Ardwyn** | Arthedain | Windtop, Crossway, Silverwatch, Tarnbar |
| **The Barrowfells** | The North Downs | Aumeneth, Kingsfast, Marlbourne High |
| **Caldwold** | Cardolan | The Cairnfields, Blackwold, Greymouth Haven, Stoneford, Tharrow |
| **Ruhdar** | Rhudaur | Kamreth, Fenngate, The Troll Thickets |
| **Hollowvale** | Rivendell | Imravel, The Wedge |
| **Aelmere** | Lindon | Vaelstir, Northhaven, Southhaven, The Last Quays |
| **Bluecrags** | Ered Luin | Bargost, Norgrad, Tallenvar, Durnhall |
| **Elderforge** | Eregion | Songhill, The Swanfens, Ostvaelin |
| **The Rimewater** | Bay of Forochel | Rimewatch |
| **The Palewall** | Misty Mountains | Cruachan, The Skyrie, The Under Gate, **Durndelve**, Kraghold |
| **Grimhold** | Angmar | **Kern Duhl**, Gunnabar, Mount Grunn, The Giantmoors, Zarrakh |

### The Middle Lands

| Region | Was | Holdings |
|---|---|---|
| **Longwater** | Anduin | Bearhall, Frennsburg, The Irisfields, Methelburg, Blossomford, The Crag |
| **Wyldmoor** | Dunland | Angarn, Aralt, Ennodir, Larach Dunn, Trefarn |
| **Windgate** | Gap of Rohan | Glimmerhold, Wealdwatch Camp, The Deepgard, **Thornspire**, Westerfold |
| **Mearhold** | Rohan | Aldenburg, The Dern Ring, Dunbarrow, Easterfold, **Mearsted**, Stowbury |
| **The Whitespine** | White Mountains | The Oathstone, Frewold, The Dead Roads |
| **Upper Ardanth** | North Gondor | Drusan Forest, Imdorath, **Sunspire**, Mornspire, Kingsquay |
| **Lower Ardanth** | South Gondor | Swanhold, Whitestrand, Linhar, Blackroot Post |
| **Longstrand** | Anfalas | Aelhaven, Rendhal |
| **Greenmarch** | Ithilien | Durthrang, The Sunset Window, Skarra Camp, Starholt |
| **Fenmire** | Nindalf | The Seeing Hill, Foamisle, The Drowned Fen |
| **Scorchlands** | Brown Lands | The Hearing Hill, Buthavais, Onghazar |

### The North-East

| Region | Was | Holdings |
|---|---|---|
| **Wildermark** | Rhovanion | Burwidd, Dalemoot, **Erenkarr (Lost)**, Merehaven |
| **North Nightwood** | N. Mirkwood | Amarath, Silverbourne, The Woodking's Halls |
| **South Nightwood** | S. Mirkwood | Cevrun, **Dol Vorrath**, Mossgable, Woodfolk Steading |
| **Nightwood Peaks** | Mtns of Mirkwood | Galbrand, Gorwing Stair |
| **Lumeneth** | Lothlórien | Amberhold, The Golden Mound, Nandruin |
| **Ironreach** | Iron Hills | Azanhold, Barrakshor, Larhuzz |
| **The Blightheath** | Withered Heath | Silverfast, Maglon Keep, Norrhold |
| **Dorvain** | Dorwinion | Rivadd, Skarrenkain |
| **Amber Sea** | Sea of Rhûn | Elgarr, Reaver Hold, Rhubarr, Isle of Burath |
| **Vashkar** | Rhûn | Karvarad, Ilannin, Khelnala, Mistrande |

### Morgahd — the Nightborn heartland

| Region | Was | Holdings |
|---|---|---|
| **Cinderplain** | Gorgoroth | **Nharoth**, Wraithtower, Vraskar, **The Emberwound** |
| **The Emberpit** | Udûn | The Iron Jaws, **The Ironmaw** |
| **Nurhal** | Nurn | Kal Narghil, Lugrakh, Nurmurl, Ordugavais, Rulth, Urlutsun |
| **Ashflats** | Lithlad | Lagleth, Durlith Spire, Ostgurth, Sarragost |
| **The Weftmarch** | Ungol | Weftspire, The Weft Stair, **The Pale City**, Olbamar, The Weaver's Lair |
| **The Battlewaste** | Dagorlad | Ashkirr, Thurangost, Urshanna |

### The South

| Region | Was | Holdings |
|---|---|---|
| **Near Sarrath** | Near Harad | Tolfaras, Kashafra, Methira, Wathdun |
| **Far Sarrath** | Far Harad | Barrahn, Jurajesh, Kashadoul, Lugarr, Vamagh |
| **Corsarn** | Umbar | Ardhumir, Kaldhur, Miralond, The Corsarn Havens, Isighir, Pellandur |
| **Kharda** | Khand | Karagmir, Khardamu, Lagorath, Laorkhi, Neburkha, Ovathrak, Sturlurtsa Kharda |

---

## 6. Naming conventions

For anyone adding cards later. Keep new names inside their culture's sound.

| Culture | Sound | Examples |
|---|---|---|
| **Vaelen** | Liquid; `ae`, `-iel`, `-thir`, `-oth` | Elduriel, Caelthorn, Aerthir, Faenlas, Vaeloth |
| **Dwarves** | Hard, clipped, Norse-ward | Grimni, Thorgrim, Balgrin, Dvalin, Nagrin |
| **Hollowkin** | Homely English; plant and burrow words | Dunn Marrow, Hobb Wattle, Bunce Roundbelly, Paldo Grubb |
| **Men of the North** | Anglo-Saxon | Eadmar, Eadwyn, Thedric, Ercanbald, Ingemar |
| **Ardanth** | Latinate, formal | Borrivan, Farrimar, Dorathen, Imrathil, Castarin |
| **Orcs** | Guttural; `-kh`, `-gh` | Azghor, Gorbakh, Ughlukh, Bugrukh, Gothmakh |
| **Sarrath** *(south)* | Open vowels, desert-ward | Adhumir, Herumar, Araudhul, Kashadoul |
| **Vashkar / Kharda** *(east)* | Steppe; hard stops | Uvathan, Kav Makhow, Din Okhtar, Ovathrak |
| **Wraithlords** | Truncated, hollowed | Vorhal, Zhamor, Aduneth, Akhorrin, Hoarveth |
| **Morgahd** | Ash, iron, wounds | Nharoth, Vraskar, Ostgurth, Lugrakh |

---

## Implementation notes

Applied across `Assets/Resources/Cards/**` — 436 card names, plus every
`startingPC`, `region`, `tags`, `characterGroup`, `nation`, `avatarCharacter`
and `deckId`, plus the proper nouns embedded in flavour and rules text.

**Art was not touched.** `Card.ResolveCardArtwork` tries
`spriteName → portraitName → name`, so any card that had been resolving art
through its `name` had `spriteName` backfilled with its **old** name before the
rename. 36 cards needed this. Every card resolves to the same sprite it did
before, and no image file was renamed. `cardId` values are unchanged, so all
1,728 modular `cardRefs` still resolve.

`RacesEnum` ordinals are unchanged; three members were renamed for display
(`Nazgul→Wraithlord`, `Balrog→Emberfiend`, `Ent→Woldwarden`), along with
`BonusVersusNazguls→BonusVersusWraithlords`.

The full old→new table is in [`CaldrathRenameManifest.json`](CaldrathRenameManifest.json).

### Quotations

A proper-noun rename cannot catch a quotation. "All we have to decide is what to
do with the time that is given to us" contains no proper noun at all, and it was
sitting on the Orren Greycloak card. Twenty-two quotes were verbatim or
near-verbatim book and film lines — the Ring-verse, *I am no man*, *one does not
simply*, *my precious*, *there's some good in this world, Mr. Frodo* — and have
been replaced with original lines written for this setting.

A further four flavour lines named things the map did not cover (*Eorl's folk*,
*where the Rings were forged*, *the Dark Lord* ×2) and were rewritten.

Nine cards named themselves by their old name inside their own quote. Those
survived the bulk pass because short common words (`Merry`, `Sam`, `Bard`,
`Dale`, `Stock`, `Rul`…) were blacklisted from prose substitution to avoid
false positives; they were repaired card by card afterwards.

### Known residue

Deliberately out of scope, and still carrying inherited names:

- **Event cards** (43 names) — not discussed when scope was set; the largest
  remaining gap, and all of it player-visible.
- **Object cards** (16 names) — *Palantír of Orthanc*, etc.
- **Encounter/Ally cards** (11 names) — *Warden of Lothlórien*, *Smaug at Home*, …
- **Environmental cards** (2 names).
- **`mithril`** — a Tolkien coinage, but here it is a *resource keyword*: a
  `<sprite name="mithril">` tag, a sprite asset, and the `mithrilRequired` /
  `mithrilGranted` fields on every card. Renaming it means touching art, TMP
  sprite assets and C# field names together, so it was left for a decision of
  its own rather than folded into this pass.
- `spriteName` / `actionClassName` / `action` fields still hold the old strings.
  These are asset keys and C# type references, never shown to the player.
  Changing them would mean renaming image files and C# classes for no visible gain.
