"""Validate catalog references and the documented avatar roster.

Optional --baseline accepts the pre-migration JSON snapshot for a stricter
comparison of gameplay fields, text mechanics, references, and artwork lookup.
"""
import argparse
from collections import defaultdict
import json
from pathlib import Path
import re
import unicodedata

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def normalize(value):
    return ''.join(c for c in value.lower() if c.isalnum())


def art_key(value):
    value = unicodedata.normalize('NFD', value)
    return re.sub('[^a-zA-Z0-9]', '', value).lower()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', type=Path)
    args = parser.parse_args()
    resources = ROOT / 'Assets/Resources'
    plan = read(ROOT / 'Docs/CaldrathMigration.json')
    assert plan['nationAlignments'] == {'Free Realms': 0, 'Tower Council': 2, 'Shadow Dominion': 1}
    manifest = read(resources / 'Cards.json')
    assert manifest['deckCount'] == len(manifest['decks'])
    assert len({e['deckId'] for e in manifest['decks']}) == len(manifest['decks'])
    cards, decks = {}, {}
    for entry in manifest['decks']:
        path = resources / (entry['resourcePath'] + '.json')
        deck = read(path)
        decks[entry['deckId']] = deck
        for field in ['deckId', 'nation', 'alignment']:
            assert deck[field] == entry[field], (path, field)
        contents = deck['cards'] if entry['isMetaDeck'] else deck['cardRefs']
        assert len(contents) == entry['cardCount'], path
        if entry['isMetaDeck']:
            for card in contents:
                assert card['cardId'] not in cards, card['cardId']
                cards[card['cardId']] = card
        else:
            assert not deck['cards'], path

    for deck in decks.values():
        assert all(i in cards for i in deck.get('cardRefs', [])), deck['deckId']
        assert len(deck.get('cardRefs', [])) == len(set(deck.get('cardRefs', []))), ('Duplicate deck reference', deck['deckId'])
        if deck['nation'] != 'Meta':
            assert deck['alignment'] == plan['nationAlignments'][deck['nation']], ('Nation alignment mismatch', deck['deckId'])
    for deck_id, rule in plan.get('deckRules', {}).items():
        refs = decks[deck_id]['cardRefs']
        assert set(rule.get('requiredCardIds', [])) <= set(refs), ('Missing deck theme cards', deck_id)
        assert not set(rule.get('forbiddenCardIds', [])) & set(refs), ('Forbidden deck content', deck_id)
        for card_id in refs:
            card = cards[card_id]
            if 'unitRaces' in rule and card['type'] in {'Character', 'Army', 'Ally'}:
                assert card['race'] in rule['unitRaces'], ('Nonhuman unit', deck_id, card_id)
            if card['type'] == 'Ally' and 'allowedAllyIds' in rule:
                assert card_id in rule['allowedAllyIds'], ('Nonhuman encounter', deck_id, card_id)
            if 'forbiddenText' in rule:
                text = ' '.join(card.get(k, '') for k in ['name', 'quote', 'actionEffect', 'description', 'requirementsText', 'historyText']) + ' ' + ' '.join(card.get('tags', []))
                assert rule['forbiddenText'].lower() not in text.lower(), ('Off-theme text', deck_id, card_id)
    playable = {e['deckId'] for e in manifest['decks'] if not e['isMetaDeck'] and not e['excluded']}
    assert playable == {a['deckId'] for a in plan['avatars']}
    used = {i for deck_id in playable for i in decks[deck_id]['cardRefs']}
    assert used == set(cards), ('Cards without a playable deck', sorted(set(cards) - used))
    assert {int(i) for i in plan['cardNames']} == set(cards), 'Incomplete name ledger'
    for card_id, card in cards.items():
        assert card['name'] == plan['cardNames'][str(card_id)], ('Unmigrated name', card_id)
        assert not re.search(r'[a-z][A-Z]|\bthe the\b|[\u00c2\u00c3\ufffd]', card['name']), ('Unreadable name', card_id)
    for assignment in plan.get('coverageAssignments', []):
        for deck_id in assignment['decks']:
            assert assignment['cardId'] in decks[deck_id]['cardRefs'], ('Missing thematic assignment', assignment)
    for avatar in plan['avatars']:
        deck = decks[avatar['deckId']]
        card = cards[avatar['cardId']]
        assert avatar['cardId'] in deck['cardRefs'], avatar
        assert card['type'] == 'Character', avatar
        assert card['name'] == avatar['avatar'] == deck['avatarCharacter'], avatar
        assert card['race'] == avatar['race'], avatar
        assert deck['alignment'] == avatar['alignment'], avatar
        assert deck['nation'] == avatar['nation'], avatar
        matches = [c['cardId'] for c in cards.values()
                   if normalize(c['name']) == normalize(avatar['avatar'])]
        assert matches == [avatar['cardId']], ('Ambiguous avatar lookup', matches)

    visible = ['name', 'quote', 'actionEffect', 'description', 'requirementsText', 'historyText', 'startingPC', 'region']
    for card in cards.values():
        for field in visible + ['tags']:
            value = ' '.join(card.get('tags', [])) if field == 'tags' else card.get(field, '')
            assert not re.search(r'\b(Vaelen|Hollowkin|Emberwrought|Dawnward|Nightborn)\b', value, re.I), (card['cardId'], field)
            plain = re.sub(r'<[^>]*>', '', value)
            for legacy in plan.get('retiredVisibleTerms', []):
                assert not re.search(r'(?<!\w)' + re.escape(legacy) + r'(?!\w)', plain, re.I), ('Inherited visible name', card['cardId'], field, legacy)
            assert not re.search(r'[\u00c2\u00c3\ufffd]', plain), ('Damaged encoding', card['cardId'], field)

    if args.baseline:
        baseline = read(args.baseline)
        before_cards = {c['cardId']: c for d in baseline.values() for c in d.get('cards', [])}
        assert cards.keys() == before_cards.keys()
        allowed = set(visible) | {'tags', 'characterGroup', 'spriteName', 'portraitName'}
        textures = {art_key(p.stem) for p in (ROOT / 'Assets/Art/Cards').rglob('*')
                    if p.suffix.lower() in {'.png', '.jpg', '.jpeg', '.tga', '.psd'}}

        def artwork(card):
            return next((art_key(card.get(k, '')) for k in
                         ['spriteName', 'portraitName', 'name', 'actionClassName', 'action']
                         if art_key(card.get(k, '')) in textures), None)

        for card_id, card in cards.items():
            before = before_cards[card_id]
            for key in before.keys() | card.keys():
                if key not in allowed:
                    assert before.get(key) == card.get(key), (card_id, key)
            if before.get('spriteName'):
                assert before['spriteName'] == card['spriteName'], card_id
            if before.get('portraitName'):
                assert before['portraitName'] == card['portraitName'], card_id
            assert artwork(before) == artwork(card), ('Artwork fallback changed', card_id)
            for field in ['actionEffect', 'requirementsText']:
                for expression in [r'<[^>]*>', r'\d+(?:\.\d+)?']:
                    assert re.findall(expression, before.get(field, '')) == re.findall(expression, card.get(field, '')), (card_id, field)

        before_decks = {d['deckId']: d for d in baseline.values() if 'deckId' in d}
        for deck_id, deck in decks.items():
            before = before_decks[deck_id]
            edit = plan.get('deckEdits', {}).get(deck_id, {})
            expected = [i for i in before.get('cardRefs', []) if i not in edit.get('remove', [])]
            for card_id in edit.get('add', []):
                if card_id not in expected:
                    expected.append(card_id)
            assert expected == deck.get('cardRefs', []), deck_id
            if deck['nation'] == 'Meta':
                assert before['alignment'] == deck['alignment'], deck_id
        old_manifest = next(d for d in baseline.values() if 'decks' in d)
        for old, new in zip(old_manifest['decks'], manifest['decks']):
            for key in old:
                if key not in {'nation', 'thematic', 'alignment', 'cardCount'}:
                    assert old[key] == new[key], (old['deckId'], key)

        def unresolved_starts(pool):
            settlements = {normalize(c['name']) for c in pool.values() if c['type'] == 'PC'}
            return {i for i, c in pool.items() if c.get('startingPC') and normalize(c['startingPC']) not in settlements}

        assert unresolved_starts(cards) <= unresolved_starts(before_cards), 'New unresolved starting settlement'

        def collisions(pool):
            groups = defaultdict(set)
            for i, c in pool.items():
                groups[(c['type'], normalize(c['name']))].add(i)
            return {frozenset(ids) for ids in groups.values() if len(ids) > 1}

        assert collisions(cards) <= collisions(before_cards), 'New duplicate card names'
        renamed = sum(c['name'] != before_cards[i]['name'] for i, c in cards.items())
        print(f'Baseline preserved: card gameplay fields, numeric rules, rich-text tags, artwork lookup; deck changes match the explicit plan; {renamed} cards renamed.')
    print(f'PASS: {len(cards)} cards, {len(playable)} avatars, {sum(len(d.get("cardRefs", [])) for d in decks.values())} card references; zero unused cards.')


if __name__ == '__main__':
    main()
