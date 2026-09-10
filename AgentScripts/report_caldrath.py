"""Generate the complete, reviewable card-to-playable-deck catalog."""
import json
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / 'Assets/Resources'


def read(path):
    return json.loads(path.read_text(encoding='utf-8'))


def main():
    plan = read(ROOT / 'Docs/CaldrathMigration.json')
    manifest = read(RES / 'Cards.json')
    titles = {a['deckId']: a['title'] for a in plan['avatars']}
    cards = {}
    usage = defaultdict(list)
    deck_counts = []
    for entry in manifest['decks']:
        deck = read(RES / (entry['resourcePath'] + '.json'))
        if entry['isMetaDeck']:
            cards.update((c['cardId'], c) for c in deck['cards'])
        elif not entry['excluded']:
            deck_counts.append((entry['nation'], titles[entry['deckId']], deck['avatarCharacter'], len(deck['cardRefs'])))
            for card_id in deck['cardRefs']:
                usage[card_id].append(titles[entry['deckId']])
    assert set(usage) == set(cards), 'Every card must have a playable-deck assignment'
    lines = ['# Caldrath card catalog', '',
             f'{len(cards):,} cards. Every card appears in at least one of the {len(deck_counts)} playable decks.',
             f'{sum(row[3] for row in deck_counts):,} total deck references. Meta ownership pools do not count as usage.', '',
             'Generated with `python AgentScripts/report_caldrath.py`. IDs and artwork keys remain stable; names below are the current player-facing names.', '',
             '## Decks', '', '| Nation | Deck | Avatar | Cards |', '|---|---|---|---:|']
    for nation, title, avatar, count in deck_counts:
        lines.append(f'| {nation} | {title} | {avatar} | {count} |')
    for kind in sorted({c['type'] for c in cards.values()}):
        group = sorted((c for c in cards.values() if c['type'] == kind), key=lambda c: c['cardId'])
        lines += ['', f'## {kind} cards ({len(group)})', '', '| Card ID | Current name | Playable decks |', '|---|---|---|']
        for card in group:
            lines.append(f"| `{card['cardId']}` | {card['name']} | {', '.join(usage[card['cardId']])} |")
    path = ROOT / 'Docs/CardCatalog.md'
    path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    print(f'Wrote {path.relative_to(ROOT)}: complete coverage of {len(cards)} cards.')


if __name__ == '__main__':
    main()
