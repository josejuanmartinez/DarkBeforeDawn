"""Gives every land (region) and every army a terrain.

A company travelling through a region can only be attacked by, and defended with, armies of that
region's terrain; characters fight anywhere. Land terrains are the region's geography; army terrains
are hand-picked by name, with a fallback on troop type and race. Values are TerrainEnum names.
Run: python AgentScripts/assign_terrains.py (then python AgentScripts/build_region_map.py)
"""
import json
import sys

ROOT = 'Assets/Resources/Cards/'
TERRAINS = ['Plains', 'Forest', 'Hills', 'Mountains', 'Marsh', 'Desert', 'Coast', 'Wasteland']

LANDS = {
    'The Rimewater': 'Coast', 'Grimhold': 'Mountains', 'West Coast': 'Coast', 'Bluecrags': 'Mountains',
    'The Barrowfells': 'Hills', 'North Kingdom': 'Plains', 'Troll Hills': 'Hills', 'Hollowvale': 'Hills',
    'Thornhollow': 'Plains', 'West Downs': 'Hills', 'Elderforge': 'Forest', 'The Palewall': 'Mountains',
    'Longwater': 'Plains', 'Wyldmoor': 'Hills', 'Windgate': 'Plains', 'Goldenwood': 'Forest',
    'Horse Plains': 'Plains', 'The Whitespine': 'Mountains', 'Longstrand': 'Coast', 'Upper Sunlands': 'Plains',
    'Lower Sunlands': 'Coast', 'Fenmire': 'Marsh', 'Fenmire Marshes': 'Marsh', 'Scorchlands': 'Wasteland',
    'Greenmarch': 'Forest', 'Greenmarch Ruins': 'Forest', 'The Blightheath': 'Wasteland',
    'North Nightwood': 'Forest', 'Nightwood Peaks': 'Mountains', 'South Nightwood': 'Forest',
    'Wildermark': 'Plains', 'Ironreach': 'Hills', 'Wine Country': 'Plains', 'Amber Sea': 'Coast',
    'Eastlands': 'Plains', 'The Battlewaste': 'Wasteland', 'The Emberpit': 'Wasteland', 'Cinderplain': 'Wasteland',
    'The Weftmarch': 'Mountains', 'Ashflats': 'Wasteland', 'Slave Fields': 'Plains', 'Near Southlands': 'Desert',
    'Far Southlands': 'Desert', 'Corsair Coast': 'Coast', 'Eastern Steppe': 'Plains',
}

ARMIES = {
    'Grimhold Levies': 'Mountains', 'Archers': 'Plains', 'Eastern Raiders': 'Plains', 'Bannerlords': 'Forest',
    'Bears': 'Forest', 'Beast of the Woods': 'Forest', 'Beasts': 'Hills', 'Blackshaft Men': 'Forest',
    'Fallen Men': 'Wasteland', 'Fallen Men Guard': 'Wasteland', 'Bog Swarm': 'Marsh', 'Hedge Wardens': 'Plains',
    'Camel Riders': 'Desert', 'Catapults': 'Plains', 'Cave Goblins': 'Mountains', 'Cave Trolls': 'Mountains',
    'Chariot Riders': 'Plains', 'Conquerors of Deepdelve': 'Mountains', 'Coastal Corsairs': 'Coast',
    'Creatures of the Deeps': 'Coast', 'Black Crows': 'Mountains', 'Crossbow Orcs': 'Hills',
    'Wine Country Bowmen': 'Plains', 'Wine Country Infantry': 'Plains', 'Wine Country Mariners': 'Coast',
    'Wine Country Riders': 'Plains', 'Dragon Hunters': 'Wasteland', 'Firstblood Rangers': 'Hills',
    'Wyldmoor Archers': 'Hills', 'Wyldmoor Warriors': 'Hills', 'Blackfort Orcs': 'Forest', 'Blackfort Sorcerers': 'Forest',
    'Dwarven Archers': 'Mountains', 'Dwarven Axemen': 'Mountains', 'Dwarven Explorers': 'Hills', 'Dwarven Miners': 'Mountains',
    'Dwarven Pikeshields': 'Mountains', 'Dwarven Siege': 'Mountains', 'Eastlands Cataphracts': 'Plains',
    'Elven Archers': 'Forest', 'Elven Elk Riders': 'Forest', 'Elven Fleet': 'Coast', 'Elven Scouts': 'Forest',
    'Elven Warriors': 'Forest', "Woodking's Guards": 'Forest', 'Tree Guardians': 'Forest', 'Forest Orcs': 'Forest',
    'Forest Spiders': 'Forest', 'Forest War Wolves': 'Forest', 'Goldenwood Archers': 'Forest', 'Goat Riders': 'Mountains',
    'Goblin Sappers': 'Mountains', 'Goblins of Deepdelve': 'Mountains', 'Sunlands Archers': 'Plains',
    'Sunlands Infantry': 'Plains', 'Cinderplain Militia': 'Wasteland', 'The Maw': 'Wasteland', 'Half Elves': 'Hills',
    'Half Orcs': 'Hills', 'Half Trolls': 'Wasteland', 'Greenmantle Hall Guards': 'Forest', 'Southlands Cavalry': 'Desert',
    'Southlands Infantry': 'Desert', 'Head Catapults': 'Wasteland', 'Hillmen': 'Hills', 'Halfling Archers': 'Plains',
    'Halfling Slingers': 'Plains', 'Hobgoblins': 'Hills', 'Hounds': 'Plains', 'Steppe Warriors': 'Plains',
    'Knights of Swanhold': 'Coast', 'Ladders': 'Plains', 'Light Cavalry': 'Plains', 'Lore Seekers': 'Hills',
    'Rimefolk': 'Coast', 'Mangonels': 'Plains', 'Kingshall Guards': 'Plains', 'Men at Arms': 'Plains',
    'Men of Crossway': 'Plains', 'Miners': 'Mountains', 'Mines': 'Mountains', 'Ironmaw Garrison': 'Wasteland',
    'Ashlands Wolf-riders': 'Wasteland', 'Pale City Orcs': 'Mountains', 'Weft Stair Guards': 'Mountains',
    'Mounted Firstblood Rangers': 'Hills', 'Mounted Half-Orcs': 'Hills', 'Mounted Hillmen': 'Hills',
    'Mounted Rangers of the Marches': 'Plains', 'Elephant Guard Spearmen': 'Desert', 'War Elephants': 'Desert',
    'Firstborn Cavalry': 'Forest', 'Slave Fields Cavalry': 'Plains', 'Slave Fields Reavers': 'Plains',
    'Warbred Trolls': 'Wasteland', 'Orcs': 'Hills', 'Kingsquay Fleet': 'Coast', 'Pirates': 'Coast',
    'Rampart Catapults': 'Plains', 'Rangers': 'Forest', 'Rangers of the Greenmarch': 'Forest',
    'Reclaimers of Lonely Mountain': 'Mountains', 'Eastlands Corsairs': 'Coast', 'Eastern Warbands': 'Plains',
    'Riders of Horse Plains': 'Plains', 'Rivermen': 'Marsh', 'Horse-lord Guards': 'Plains', 'Ruffians': 'Plains',
    'Ruin Spiders': 'Wasteland', 'Ships': 'Coast', 'Snow Trolls': 'Mountains', 'Southlands Assassins': 'Desert',
    'Southerners': 'Desert', 'Spawn of the Weaver': 'Mountains', 'Spearmen': 'Plains', 'Stone Trolls': 'Hills',
    'Swanhold Fleet': 'Coast', 'Swordsmen': 'Plains', 'The Golden Host': 'Forest', 'Torch Bearers': 'Wasteland',
    'Tower Guards': 'Plains', 'Tower Wardens': 'Hills', 'Trebuchets': 'Plains', 'Corsair Coast Cavalry': 'Coast',
    'Corsair Coast Riders': 'Desert', 'Undead': 'Marsh', 'Weftmarch Orcs': 'Mountains', 'War Orcs': 'Hills',
    'Wainfolk Riders': 'Plains', 'War Trolls': 'Wasteland', 'Wardens of Old Kingshold': 'Hills',
    'War Wolf Riders': 'Wasteland', 'War Wolves': 'Forest', 'Sunspire Wardens': 'Plains', 'Wolves': 'Forest',
    'Nightwood Elves': 'Forest', 'Wood Trolls': 'Forest', 'Nightwood Archers': 'Forest', 'Nightwood Warriors': 'Forest',
    'Woodmen': 'Forest', 'Woodmen Hunters': 'Forest', 'Oldgrowth Woodfolk': 'Forest',
}


def fallback(army):
    if army['troopType'] == 7: return 'Coast'
    if army['race'] == 2: return 'Mountains'
    if army['race'] == 1: return 'Forest'
    if army['race'] == 17: return 'Desert'
    if army['race'] in (5, 6, 15): return 'Hills'
    return 'Plains'


def stamp(path, table, kind):
    deck = json.load(open(path, encoding='utf-8'))
    missing = []
    for card in deck['cards']:
        terrain = table.get(card['name'])
        if terrain is None:
            terrain = fallback(card) if kind == 'army' else 'Plains'
            missing.append(f"{card['name']} -> {terrain} (fallback)")
        if terrain not in TERRAINS:
            raise SystemExit(f'unknown terrain {terrain} for {card["name"]}')
        card['terrain'] = terrain
    with open(path, 'w', encoding='utf-8') as out:
        json.dump(deck, out, indent=4, ensure_ascii=False)
        out.write('\n')
    return missing


def main():
    problems = stamp(ROOT + 'Meta/LandCards.json', LANDS, 'land') + stamp(ROOT + 'Meta/ArmyCards.json', ARMIES, 'army')
    for line in problems:
        print('WARNING', line)
    print('terrains stamped')
    return 1 if problems else 0


if __name__ == '__main__':
    sys.exit(main())
