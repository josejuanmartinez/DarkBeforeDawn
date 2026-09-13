"""Derives each settlement's own alignment and its dwellers from the playable decks.

Alignment: the one alignment shared by every deck that lists the settlement; settlements listed by
decks of different alignments (or by none) are Neutral (2). Dwellers: an army card from one of those
decks, chosen by name affinity with the settlement or its region, with hand overrides for the places
the heuristic gets wrong. Run: python AgentScripts/assign_settlements.py
"""
import collections
import glob
import json
import re
import sys

ROOT = 'Assets/Resources/Cards/'
NEUTRAL = 2

OVERRIDES = {
    'Glimmerhold': 'Dwarven Pikeshields', 'The Seeing Hill': 'Lore Seekers', 'The Hearing Hill': 'Hillmen',
    'Songhill': 'Elven Scouts', 'Windtop': 'Firstblood Rangers', 'Ashwatch': 'Eastern Warbands',
    'Ironhall': 'Dwarven Axemen', 'Deepwatch': 'Dwarven Pikeshields', 'The Cairnfields': 'Undead',
    'Broadforge': 'Dwarven Miners', 'Bearhall': 'Bears', 'Bramblemarch': 'Halfling Archers',
    'Woodbridge': 'Woodmen', 'Burnt Crossing': 'Wainfolk Riders', 'Millpond': 'Halfling Slingers',
    'High Bluff': 'Hillmen', 'The Iron Jaws': 'Ironmaw Garrison', 'Silverfast': 'Dragon Hunters',
    'Crook Hollow': 'Hedge Wardens', 'The Drowned Fen': 'Bog Swarm', 'Blackwood Keep': 'Undead',
    'The Skyrie': 'Beasts', 'Starlight Tower': 'Elven Warriors',
    'Lonely Mountain (Lost)': 'Reclaimers of Lonely Mountain',
    'The Oathstone': 'Sunlands Infantry', 'Blackwold': 'Woodmen', 'Wealdwatch Camp': 'War Orcs',
    'Fenngate': 'Hillmen', 'Kingsfast': 'Wardens of Old Kingshold', 'Rimewatch': 'Rimefolk',
    'Stonefield': 'Wyldmoor Warriors', 'The Irisfields': 'Rivermen', 'Frostwatch Keep': 'Cave Trolls',
    'The Deepgard': 'Riders of Horse Plains', 'Hollyburrow': 'Hedge Wardens', 'Hidden Refuge': 'Half Elves',
    'Copper Gorge': 'Dwarven Miners', 'Ash Camp': 'Ashlands Wolf-riders', 'Kraghold': 'Goblin Sappers',
    'Deep Quarry': 'Hobgoblins', 'Rivermeet': 'Rivermen', 'Marlbourne High': 'Wardens of Old Kingshold',
    'Great Delving': 'Halfling Archers', 'The Dead Roads': 'Black Crows', 'Old Elven City': 'Lore Seekers',
    'Shorewatch': 'Sunlands Infantry', 'Mossgable': 'Beast of the Woods', 'Amberwatch': 'Dragon Hunters',
    'Stoneford': 'Firstblood Rangers', 'Blossomford': 'Rivermen', 'Raven Stair': 'Forest Spiders',
    'Stockleigh': 'Halfling Slingers', 'Lakewatch': 'Rivermen', 'Copperhall': 'Dwarven Miners',
    'Stonehall': 'Dwarven Axemen', 'Deepforge': 'Dwarven Pikeshields', 'Blackdelve': 'Dwarven Explorers',
    'Northdelve': 'Dwarven Explorers', 'Old Bridge': 'Ruffians', 'The Wedge': 'Mounted Firstblood Rangers',
    'The Crag': 'Bears', 'The Giantmoors': 'Stone Trolls', 'The Troll Thickets': 'Stone Trolls',
    'Warwatch': 'War Trolls', 'Westerfold': 'Horse-lord Guards', 'Woodfolk Steading': 'Woodmen',
    'Scorched Hold': 'Forest Orcs', 'The Swanfens': 'Bog Swarm', 'Thornspire': 'Tower Wardens',
    'Greymouth Haven': 'Ships', 'Woodhill': 'Nightwood Warriors', 'Silverbourne': 'Nightwood Archers',
    'Highwood': 'Nightwood Warriors', "The Woodking's Halls": "Woodking's Guards",
    'Amberhold': 'The Golden Host',
    'Kingshall': 'Kingshall Guards', 'Sunspire': 'Sunspire Wardens', 'Mornspire': 'Tower Guards',
    'Swanhold': 'Knights of Swanhold', 'Oldgrowth Forest': 'Oldgrowth Woodfolk', 'Sunwatch': 'Sunlands Infantry',
    'Riverhaven': 'Sunlands Infantry', 'Wraithtower': 'Fallen Men Guard', 'The Weft Stair': 'Weft Stair Guards',
    'The Pale City': 'Pale City Orcs', "The Weaver's Lair": 'Spawn of the Weaver',
    'Slave Barracks': 'Slave Fields Reavers',
    'Goblin Mountain': 'War Wolf Riders', 'Mount Frostfang': 'Snow Trolls', 'Eastern Harbour': 'Eastlands Corsairs',
    'Redrock Fort': 'Steppe Warriors', 'Southwatch Tower': 'Southlands Infantry', 'Sandfort': 'Southlands Infantry',
    'Dustfort': 'Southlands Infantry', 'Dune Market': 'Camel Riders', 'Far Southport': 'Coastal Corsairs',
    'Oasis Keep': 'Southlands Cavalry', 'Palmford': 'Southlands Infantry', 'Southford': 'Southlands Infantry',
    'Southhaven Isle': 'Coastal Corsairs', 'Tidefort': 'Corsair Coast Cavalry', 'Pearl Harbour': 'Pirates',
    'The Corsair Coast Havens': 'Coastal Corsairs', 'South Quay': 'Pirates', 'Corsair Keep': 'Corsair Coast Cavalry',
    'Ironford': 'Wyldmoor Warriors', 'Moorhall': 'Wyldmoor Warriors', 'Moorfarm': 'Wyldmoor Warriors',
    'The Forest Circle': 'Tree Guardians', 'The Deepholes': 'Ruffians', 'Deepdelve': 'Goblins of Deepdelve',
    'Cinderfort': 'Torch Bearers', 'Redhorn Pass': 'Black Crows',
}
GENERIC = {'Archers', 'Spearmen', 'Swordsmen', 'Men at Arms', 'Light Cavalry', 'Ships', 'Ladders', 'Catapults',
           'Trebuchets', 'The Maw', 'Head Catapults', 'Mangonels', 'Mines', 'Orcs', 'Crossbow Orcs', 'Half Orcs',
           'Mounted Half-Orcs', 'Hobgoblins', 'Half Trolls', 'Beasts', 'Bears', 'Wolves', 'Rangers',
           'Hedge Wardens', 'Lore Seekers', 'Torch Bearers'}
STOP = {'the', 'of', 'camp', 'fields', 'field', 'watch', 'keep', 'fort', 'hold', 'tower', 'hall', 'halls', 'city',
        'post', 'isle', 'market', 'haven', 'harbour', 'quay', 'port', 'landing', 'stair', 'mound', 'hill', 'circle',
        'pass', 'gate', 'window', 'lair', 'roads', 'delving', 'deep', 'old', 'great', 'high', 'black', 'white',
        'silver', 'golden', 'iron', 'red', 'blue', 'pale', 'dead', 'under', 'north', 'south', 'east', 'west', 'far',
        'near', 'lonely', 'lost', 'sea', 'coast', 'country', 'plains', 'lands', 'land', 'marches', 'mountain',
        'mountains', 'hills', 'wood', 'forest', 'fen', 'marsh', 'marshes', 'downs', 'valley'}
PORT = re.compile(r"Haven|Harbour|Quay|port|Isle|Landing|Quays|Corsair|Tide|Salt|Pearl|Foam|Amber|Mere|Lake|"
                  r"Reaver|Pirate", re.I)


def words(text):
    return {w for w in re.findall(r"[A-Za-z']+", text.lower()) if w not in STOP and len(w) > 2}


def score(army, pc, deck_count):
    total = 0
    name = army['name']
    if words(name) & words(pc['region']):
        total += 100
    if words(name) & words(pc['name']):
        total += 80
    if pc['region'].lower().replace('the ', '') in name.lower():
        total += 60
    port = bool(PORT.search(pc['name']))
    if army['troopType'] == 7:
        total += 40 if port else -80
    if army['troopType'] == 6:
        total -= 90
    if name in GENERIC:
        total -= 40
    return total - 6 * (deck_count - 1)


def main():
    pc_path = ROOT + 'Meta/PCCards.json'
    pc_deck = json.load(open(pc_path, encoding='utf-8'))
    armies = {c['cardId']: c for c in json.load(open(ROOT + 'Meta/ArmyCards.json', encoding='utf-8'))['cards']}
    by_name = {a['name']: a for a in armies.values()}
    decks = {}
    for path in sorted(glob.glob(ROOT + 'Modular/*.json')):
        deck = json.load(open(path, encoding='utf-8'))
        decks[deck['deckId']] = deck
    pc_decks = collections.defaultdict(list)
    army_decks = collections.defaultdict(list)
    for deck_id, deck in decks.items():
        for ref in deck['cardRefs']:
            if ref in armies:
                army_decks[ref].append(deck_id)
            pc_decks[ref].append(deck_id)

    counts = collections.Counter()
    problems = []
    for pc in pc_deck['cards']:
        owners = pc_decks.get(pc['cardId'], [])
        alignments = {decks[d]['alignment'] for d in owners}
        pc['settlementAlignment'] = alignments.pop() if len(alignments) == 1 else NEUTRAL
        counts[pc['settlementAlignment']] += 1
        candidates = {ref for d in owners for ref in decks[d]['cardRefs'] if ref in armies}
        chosen = OVERRIDES.get(pc['name'])
        if chosen:
            if chosen not in by_name:
                problems.append(f"{pc['name']}: no army named {chosen}")
                chosen = None
            elif by_name[chosen]['cardId'] not in candidates:
                problems.append(f"{pc['name']}: {chosen} is not in a deck holding this settlement")
        if not chosen and candidates:
            best = max(candidates, key=lambda ref: (score(armies[ref], pc, len(army_decks[ref])), -ref))
            chosen = armies[best]['name']
        pc['dwellers'] = chosen or ''
    with open(pc_path, 'w', encoding='utf-8') as out:
        json.dump(pc_deck, out, indent=4, ensure_ascii=False)
        out.write('\n')
    print('alignments (0 Free, 1 Dark, 2 Neutral):', dict(counts))
    for problem in problems:
        print('WARNING', problem)
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
