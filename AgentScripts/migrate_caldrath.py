"""Apply naming, roster, and explicitly recorded deck composition corrections."""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RESOURCES = ROOT / 'Assets/Resources'
PLAN = ROOT / 'Docs/CaldrathMigration.json'
TEXT_FIELDS = {'name', 'quote', 'actionEffect', 'region', 'description',
               'requirementsText', 'historyText', 'startingPC', 'tags'}


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def write(path, value):
    text = json.dumps(value, ensure_ascii=False, indent=4) + '\n'
    if path.read_text(encoding='utf-8') != text:
        path.write_text(text, encoding='utf-8')


def main():
    plan = read(PLAN)
    replacements = plan['replacements']
    pattern = re.compile(r'(?<!\w)(' + '|'.join(
        re.escape(k) for k in sorted(replacements, key=len, reverse=True)) + r')(?!\w)')

    def replace(text):
        # Rich text and sprite tags are executable presentation references.
        return ''.join(part if part.startswith('<') else pattern.sub(
            lambda m: replacements[m.group()], part)
            for part in re.split(r'(<[^>]*>)', text))

    pools = {}
    cards = {}
    for path in sorted((RESOURCES / 'Cards/Meta').glob('*.json')):
        pool = read(path)
        for card in pool['cards']:
            old_name = card['name']
            for field in TEXT_FIELDS:
                value = card.get(field)
                if isinstance(value, str):
                    card[field] = replace(value)
                elif isinstance(value, list):
                    card[field] = [replace(v) if isinstance(v, str) else v for v in value]
            override = plan['cardOverrides'].get(str(card['cardId']), {})
            card.update(override)
            # Overrides can predate a subsequent vocabulary migration.
            for field in TEXT_FIELDS:
                if field in override and isinstance(card.get(field), str):
                    card[field] = replace(card[field])
            if 'cardNames' in plan:
                card['name'] = plan['cardNames'][str(card['cardId'])]
            card['tags'] = [plan.get('tagAliases', {}).get(tag, tag) for tag in card.get('tags', [])]
            for field, fallback in plan.get('artFallbacks', {}).get(str(card['cardId']), {}).items():
                assert not card.get(field) or card[field] == fallback, (card['cardId'], field)
                card[field] = fallback
            if card['name'] != old_name and not card.get('spriteName') and not card.get('portraitName'):
                card['spriteName'] = old_name
            if card.get('characterGroup') in plan['characterGroups']:
                card['characterGroup'] = plan['characterGroups'][card['characterGroup']]
            assert card['cardId'] not in cards, 'Duplicate card ID'
            cards[card['cardId']] = card
        pools[path] = pool
    if 'cardNames' in plan:
        assert {int(i) for i in plan['cardNames']} == set(cards), 'Incomplete name review'

    manifest = read(RESOURCES / 'Cards.json')
    decks = {}
    roster = {entry['deckId']: entry for entry in plan['avatars']}
    for entry in manifest['decks']:
        if entry['isMetaDeck']:
            continue
        path = RESOURCES / (entry['resourcePath'] + '.json')
        deck = read(path)
        definition = roster[entry['deckId']]
        edit = plan.get('deckEdits', {}).get(entry['deckId'], {})
        removed = set(edit.get('remove', []))
        deck['cardRefs'] = [i for i in deck['cardRefs'] if i not in removed]
        for card_id in edit.get('add', []):
            if card_id not in deck['cardRefs']:
                deck['cardRefs'].append(card_id)
        avatar = cards[definition['cardId']]
        assert avatar['type'] == 'Character'
        assert avatar['race'] == definition['race']
        assert avatar['name'] == definition['avatar']
        assert avatar['cardId'] in deck['cardRefs']
        assert definition['alignment'] == plan['nationAlignments'][definition['nation']]
        entry['alignment'] = deck['alignment'] = definition['alignment']
        assert all(card_id in cards for card_id in deck['cardRefs'])
        entry['nation'] = deck['nation'] = definition['nation']
        entry['thematic'] = definition['theme']
        deck['avatarCharacter'] = avatar['name']
        entry['cardCount'] = len(deck['cardRefs'])
        decks[path] = deck

    assert len(roster) == len(decks)
    if plan.get('coverageRequired'):
        used = {i for deck in decks.values() for i in deck['cardRefs']}
        assert used == set(cards), ('Cards without a playable deck', sorted(set(cards) - used))
    for path, value in {**pools, **decks}.items():
        write(path, value)
    write(RESOURCES / 'Cards.json', manifest)
    print(f'Migrated {len(cards)} cards; assigned {len(decks)} deck avatars.')


if __name__ == '__main__':
    main()
