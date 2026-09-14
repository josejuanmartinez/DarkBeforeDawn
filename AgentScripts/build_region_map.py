"""Builds the Caldrath region map: which regions border which, and where each sits on the page.

Writes Assets/Resources/RegionMap.json (read by RegionMap.cs) and Docs/CaldrathMap.md (the reference
for drawing the map image). Regions are the land cards; alternate land cards for one region
("Greenmarch Ruins", "Fenmire Marshes") are listed as aliases. Coordinates are a 0-10 grid, x west
to east and y north to south, meant as a layout guide rather than survey data.
Run: python AgentScripts/build_region_map.py
"""
import json
import sys

# name: (group, x, y, [neighbours]). Adjacency is symmetrised below, so a border is written once.
REGIONS = {
    # The Marches - the north-west
    'The Rimewater':   ('The Marches', 2.0, 0.5, ['Grimhold', 'North Kingdom', 'Bluecrags', 'The Barrowfells']),
    'Grimhold':        ('The Marches', 4.2, 1.0, ['North Kingdom', 'The Barrowfells', 'Troll Hills', 'Hollowvale', 'The Palewall', 'Longwater', 'The Blightheath']),
    'West Coast':      ('The Marches', 0.6, 2.2, ['Bluecrags', 'North Kingdom', 'Thornhollow']),
    'Bluecrags':       ('The Marches', 1.6, 2.0, ['North Kingdom', 'Thornhollow']),
    'The Barrowfells': ('The Marches', 3.1, 1.8, ['North Kingdom', 'Troll Hills']),
    'North Kingdom':   ('The Marches', 2.6, 2.9, ['Thornhollow', 'West Downs', 'Troll Hills']),
    'Troll Hills':     ('The Marches', 4.3, 2.4, ['West Downs', 'Hollowvale', 'The Palewall']),
    'Hollowvale':      ('The Marches', 5.1, 2.9, ['The Palewall', 'Elderforge']),
    'Thornhollow':     ('The Marches', 2.1, 3.4, ['West Downs']),
    'West Downs':      ('The Marches', 3.1, 3.9, ['Elderforge', 'Wyldmoor']),
    'Elderforge':      ('The Marches', 4.5, 4.1, ['The Palewall', 'Wyldmoor']),
    'The Palewall':    ('The Marches', 5.4, 3.7, ['Longwater', 'Goldenwood', 'Wyldmoor', 'Windgate']),
    # The Middle Lands
    'Longwater':       ('The Middle Lands', 6.1, 3.1, ['The Blightheath', 'North Nightwood', 'South Nightwood', 'Goldenwood', 'Scorchlands', 'Fenmire', 'Horse Plains']),
    'Wyldmoor':        ('The Middle Lands', 4.3, 5.1, ['Windgate', 'Longstrand']),
    'Windgate':        ('The Middle Lands', 5.3, 5.7, ['Horse Plains', 'The Whitespine']),
    'Goldenwood':      ('The Middle Lands', 6.3, 4.3, ['South Nightwood', 'Horse Plains']),
    'Horse Plains':    ('The Middle Lands', 6.1, 6.1, ['The Whitespine', 'Upper Sunlands', 'Fenmire']),
    'The Whitespine':  ('The Middle Lands', 5.5, 6.9, ['Upper Sunlands', 'Lower Sunlands', 'Longstrand']),
    'Longstrand':      ('The Middle Lands', 4.3, 7.4, ['Lower Sunlands']),
    'Upper Sunlands':  ('The Middle Lands', 6.7, 7.1, ['Lower Sunlands', 'Greenmarch', 'Fenmire']),
    'Lower Sunlands':  ('The Middle Lands', 5.9, 7.9, ['Greenmarch', 'Near Southlands', 'Corsair Coast']),
    'Fenmire':         ('The Middle Lands', 7.1, 5.8, ['Scorchlands', 'The Battlewaste', 'Greenmarch']),
    'Scorchlands':     ('The Middle Lands', 7.4, 4.9, ['South Nightwood', 'Wildermark', 'The Battlewaste']),
    'Greenmarch':      ('The Middle Lands', 7.6, 7.1, ['The Battlewaste', 'The Emberpit', 'The Weftmarch', 'Near Southlands']),
    # The North-East
    'The Blightheath': ('The North-East', 7.1, 1.2, ['North Nightwood', 'Wildermark', 'Ironreach']),
    'North Nightwood': ('The North-East', 7.3, 2.3, ['Nightwood Peaks', 'South Nightwood', 'Wildermark']),
    'Nightwood Peaks': ('The North-East', 7.7, 3.2, ['South Nightwood', 'Wildermark']),
    'South Nightwood': ('The North-East', 7.1, 3.9, ['Wildermark']),
    'Wildermark':      ('The North-East', 8.4, 2.6, ['Ironreach', 'Wine Country', 'Amber Sea', 'Eastlands']),
    'Ironreach':       ('The North-East', 9.2, 1.6, ['Wine Country', 'Eastlands']),
    'Wine Country':    ('The North-East', 9.3, 3.3, ['Amber Sea', 'Eastlands']),
    'Amber Sea':       ('The North-East', 9.6, 4.3, ['Eastlands']),
    'Eastlands':       ('The North-East', 9.2, 5.5, ['The Battlewaste', 'Eastern Steppe']),
    # Ashlands - the Dark Servants' heartland
    'The Battlewaste': ('Ashlands', 8.1, 5.7, ['The Emberpit']),
    'The Emberpit':    ('Ashlands', 8.4, 6.4, ['Cinderplain', 'The Weftmarch', 'Ashflats']),
    'Cinderplain':     ('Ashlands', 8.7, 7.2, ['The Weftmarch', 'Ashflats', 'Slave Fields']),
    'The Weftmarch':   ('Ashlands', 8.0, 7.4, ['Slave Fields']),
    'Ashflats':        ('Ashlands', 9.4, 7.0, ['Slave Fields', 'Eastern Steppe']),
    'Slave Fields':    ('Ashlands', 8.9, 8.1, ['Eastern Steppe', 'Near Southlands']),
    # The South
    'Near Southlands': ('The South', 7.2, 8.6, ['Far Southlands', 'Corsair Coast', 'Eastern Steppe']),
    'Corsair Coast':   ('The South', 6.0, 9.0, ['Far Southlands']),
    'Far Southlands':  ('The South', 7.0, 9.7, []),
    'Eastern Steppe':  ('The South', 9.6, 8.7, []),
}
LAND_ALIASES = {'Greenmarch Ruins': 'Greenmarch', 'Fenmire Marshes': 'Fenmire'}

# Where each region's marker sits on Assets/Resources/Maps/Caldrath.png, in pixels of the 1254x1254 image
# (x east, y south). Read off the image's terrain symbols; stored in the JSON as mapX/mapY in 0-1.
MAP_SIZE = 1254
MAP_MARKERS = {
    'The Rimewater':     (233, 149),
    'Grimhold':          (436, 201),
    'West Coast':        (100, 325),
    'Bluecrags':         (196, 302),
    'The Barrowfells':   (337, 283),
    'North Kingdom':     (277, 387),
    'Troll Hills':       (441, 338),
    'Hollowvale':        (500, 406),
    'Thornhollow':       (225, 452),
    'West Downs':        (334, 497),
    'Elderforge':        (449, 530),
    'The Palewall':      (550, 496),
    'Longwater':         (614, 432),
    'Wyldmoor':          (446, 633),
    'Windgate':          (538, 690),
    'Goldenwood':        (633, 541),
    'Horse Plains':      (616, 730),
    'The Whitespine':    (559, 813),
    'Longstrand':        (437, 867),
    'Upper Sunlands':    (669, 831),
    'Lower Sunlands':    (605, 920),
    'Fenmire':           (717, 699),
    'Scorchlands':       (748, 598),
    'Greenmarch':        (773, 843),
    'The Blightheath':   (720, 224),
    'North Nightwood':   (734, 335),
    'Nightwood Peaks':   (775, 436),
    'South Nightwood':   (718, 512),
    'Wildermark':        (855, 370),
    'Ironreach':         (920, 269),
    'Wine Country':      (941, 441),
    'Amber Sea':         (973, 551),
    'Eastlands':         (943, 672),
    'The Battlewaste':   (820, 689),
    'The Emberpit':      (856, 758),
    'Cinderplain':       (889, 843),
    'The Weftmarch':     (823, 878),
    'Ashflats':          (967, 826),
    'Slave Fields':      (911, 934),
    'Near Southlands':   (733, 984),
    'Corsair Coast':     (619, 1023),
    'Far Southlands':    (706, 1104),
    'Eastern Steppe':    (993, 1001),
}


def main():
    land_cards = json.load(open('Assets/Resources/Cards/Meta/LandCards.json', encoding='utf-8'))['cards']
    lands = {c['name'] for c in land_cards}
    terrains = {c['name']: c.get('terrain', '') for c in land_cards}
    pcs = json.load(open('Assets/Resources/Cards/Meta/PCCards.json', encoding='utf-8'))['cards']
    ok = True
    for name in REGIONS:
        if name not in lands:
            print('no land card named', name); ok = False
        if name not in MAP_MARKERS:
            print('no map marker for', name); ok = False
    for land in lands:
        if land not in REGIONS and land not in LAND_ALIASES:
            print('land card without a region entry:', land); ok = False
    for pc in pcs:
        if pc['region'] not in REGIONS:
            print('settlement in unknown region:', pc['name'], pc['region']); ok = False

    adjacency = {name: set() for name in REGIONS}
    for name, (_, _, _, neighbours) in REGIONS.items():
        for other in neighbours:
            if other not in REGIONS:
                print(name, 'borders unknown region', other); ok = False; continue
            adjacency[name].add(other); adjacency[other].add(name)
    if not ok:
        return 1

    data = {
        'regions': [
            {'name': name, 'group': group, 'terrain': terrains.get(name, ''), 'x': x, 'y': y,
             'mapX': round(MAP_MARKERS[name][0] / MAP_SIZE, 4), 'mapY': round(MAP_MARKERS[name][1] / MAP_SIZE, 4),
             'adjacent': sorted(adjacency[name])}
            for name, (group, x, y, _) in REGIONS.items()
        ],
        'landAliases': [{'land': land, 'region': region} for land, region in LAND_ALIASES.items()],
    }
    with open('Assets/Resources/RegionMap.json', 'w', encoding='utf-8') as out:
        json.dump(data, out, indent=4, ensure_ascii=False)
        out.write('\n')

    settlements = {}
    for pc in pcs:
        settlements.setdefault(pc['region'], []).append(pc['name'])
    lines = [
        '# Caldrath - the region map',
        '',
        'Every land card is a region; a settlement belongs to exactly one region (`region` on the PC card).',
        'Two land cards are alternates for one region: ' + ', '.join(f'**{l}** = {r}' for l, r in LAND_ALIASES.items()) + '.',
        'Generated by `python AgentScripts/build_region_map.py`; the runtime reads `Assets/Resources/RegionMap.json`.',
        '',
        '## How the map is used in a match',
        '',
        '- **Distance** between two settlements is the number of borders crossed on the shortest route between their regions,',
        '  clamped to 1-5. Travelling draws that many cards. Same region still counts as 1.',
        '- The route is drawn on the map in the Select Destination popup and walked on it during Travel, one stop per region crossed.',
        '- A settlement can be chosen once its land card is on the board; the route itself needs no lands.',
        '- Each region has a **terrain** (from its land card). At every stop the other company may attack with characters and',
        '  with armies of that terrain; the traveller defends under the same rule.',
        '',
        '## Layout guide',
        '',
        'Coordinates are a 0-10 grid: x runs west to east, y runs north to south. They place the region label; borders are the',
        'adjacency list. Groups are the five areas from `Caldrath.md`. **Map** is where the region marker sits on the drawn',
        'map, `Assets/Resources/Maps/Caldrath.png` (pixels of the 1254x1254 image, x east and y south); the match draws routes on it.',
        '',
        '| Region | Group | Terrain | x | y | Map | Borders | Settlements |',
        '|---|---|---|---|---|---|---|---|',
    ]
    for name, (group, x, y, _) in REGIONS.items():
        mx, my = MAP_MARKERS[name]
        lines.append(f"| **{name}** | {group} | {terrains.get(name, '')} | {x} | {y} | {mx}, {my} | {', '.join(sorted(adjacency[name]))} | {', '.join(settlements.get(name, []))} |")
    lines += ['', '## Border list', '', 'Each border once, alphabetically:', '']
    seen = set()
    for name in sorted(adjacency):
        for other in sorted(adjacency[name]):
            if (other, name) in seen: continue
            seen.add((name, other)); lines.append(f'- {name} — {other}')
    with open('Docs/CaldrathMap.md', 'w', encoding='utf-8') as out:
        out.write('\n'.join(lines) + '\n')
    print(len(REGIONS), 'regions,', len(seen), 'borders')
    return 0


if __name__ == '__main__':
    sys.exit(main())
